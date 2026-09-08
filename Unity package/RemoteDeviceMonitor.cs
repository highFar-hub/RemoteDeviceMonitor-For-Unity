using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// 可复用的远程状态监控组件。将它挂到预制体上即可。
/// 编辑器运行时从项目根目录读取 device-api-key.txt；
/// 打包后从 exe 所在目录读取该文件。
/// </summary>
public sealed class RemoteDeviceMonitor : MonoBehaviour
{
    [Header("作品信息")]
    [SerializeField] private string projectId = "change-me";
    [SerializeField] private string projectName = "未命名 Unity 作品";
    [SerializeField] private string deviceDisplayName = "";

    [Header("服务器")]
    [SerializeField] private string serverBaseUrl = "https://unity-device-monitor-vercel.vercel.app";
    [SerializeField, Min(10f)] private float reportIntervalSeconds = 30f;
    [SerializeField, Min(2f)] private float commandPollIntervalSeconds = 5f;
    [SerializeField] private bool receiveCommands = true;
    [SerializeField] private bool keepBetweenScenes = true;

    [Header("远程命令")]
    [Tooltip("网页下发播放编号时触发，可绑定项目自己的 public 方法。")]
    [SerializeField] private UnityEvent<int> onVideoCommand = new UnityEvent<int>();

    private string deviceApiKey;
    private string deviceId;
    private string lastCommandTime;
    private string reportedError = "";
    private int currentVideo;
    private bool isPlaying;
    private float fps;
    private float fpsTimer;
    private int fpsFrames;
    private float nextReport;
    private float nextCommandPoll;
    private bool reporting;
    private bool polling;

    public string DeviceId => deviceId;
    public string LastMonitorError => reportedError;
    public UnityEvent<int> OnVideoCommand => onVideoCommand;

    [Serializable]
    private sealed class StatusPayload
    {
        public string device_id;
        public string device_name;
        public int current_video;
        public bool is_playing;
        public float fps;
        public string error_message;
        public string project_id;
        public string project_name;
        public string app_version;
        public string scene_name;
    }

    [Serializable]
    private sealed class CommandPayload
    {
        public int desired_video;
        public string command_updated_at;
    }

    private void Awake()
    {
        if (keepBetweenScenes)
        {
            RemoteDeviceMonitor[] monitors = FindObjectsOfType<RemoteDeviceMonitor>();
            for (int i = 0; i < monitors.Length; i++)
            {
                if (monitors[i] != this && monitors[i].projectId == projectId)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            DontDestroyOnLoad(gameObject);
        }

        Application.runInBackground = true;
        string safeProjectId = string.IsNullOrWhiteSpace(projectId) ? "unknown-project" : projectId.Trim();
        deviceId = safeProjectId + ":" + SystemInfo.deviceUniqueIdentifier;
        deviceApiKey = LoadDeviceApiKey();
        nextReport = 0f;
        nextCommandPoll = 0f;
    }

    private void Update()
    {
        fpsFrames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 1f)
        {
            fps = fpsFrames / fpsTimer;
            fpsFrames = 0;
            fpsTimer = 0f;
        }

        if (!reporting && Time.unscaledTime >= nextReport)
        {
            nextReport = Time.unscaledTime + reportIntervalSeconds;
            StartCoroutine(ReportStatus());
        }

        if (receiveCommands && !polling && Time.unscaledTime >= nextCommandPoll)
        {
            nextCommandPoll = Time.unscaledTime + commandPollIntervalSeconds;
            StartCoroutine(PollCommand());
        }
    }

    /// <summary>由作品自己的逻辑调用，更新将要上报的播放状态。</summary>
    public void SetPlaybackState(int videoNumber, bool playing)
    {
        currentVideo = Mathf.Max(0, videoNumber);
        isPlaying = playing;
    }

    /// <summary>由作品自己的逻辑调用，更新将要上报的错误信息。</summary>
    public void SetError(string message)
    {
        reportedError = message ?? "";
    }

    /// <summary>清空作品错误。</summary>
    public void ClearError()
    {
        reportedError = "";
    }

    /// <summary>不等待下一周期，尽快上报一次。</summary>
    public void ReportNow()
    {
        nextReport = 0f;
    }

    private IEnumerator ReportStatus()
    {
        reporting = true;
        if (!EnsureKey())
        {
            reportedError = "未找到 device-api-key.txt";
            reporting = false;
            yield break;
        }

        StatusPayload payload = new StatusPayload
        {
            device_id = deviceId,
            device_name = BuildDeviceName(),
            current_video = currentVideo,
            is_playing = isPlaying,
            fps = fps,
            error_message = reportedError,
            project_id = projectId,
            project_name = projectName,
            app_version = Application.version,
            scene_name = SceneManager.GetActiveScene().name
        };

        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        using (UnityWebRequest request = new UnityWebRequest(StatusUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-device-key", deviceApiKey);
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                reportedError = "状态上报失败: " + request.error;
        }
        reporting = false;
    }

    private IEnumerator PollCommand()
    {
        polling = true;
        if (!EnsureKey())
        {
            polling = false;
            yield break;
        }

        string url = CommandUrl + "?device_id=" + UnityWebRequest.EscapeURL(deviceId);
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("x-device-key", deviceApiKey);
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                CommandPayload command = JsonUtility.FromJson<CommandPayload>(request.downloadHandler.text);
                if (command != null &&
                    command.desired_video >= 1 &&
                    !string.IsNullOrEmpty(command.command_updated_at) &&
                    command.command_updated_at != lastCommandTime)
                {
                    lastCommandTime = command.command_updated_at;
                    onVideoCommand.Invoke(command.desired_video);
                }
            }
        }
        polling = false;
    }

    private bool EnsureKey()
    {
        if (!string.IsNullOrEmpty(deviceApiKey)) return true;
        deviceApiKey = LoadDeviceApiKey();
        return !string.IsNullOrEmpty(deviceApiKey);
    }

    private string BuildDeviceName()
    {
        string machine = string.IsNullOrWhiteSpace(deviceDisplayName)
            ? Environment.MachineName
            : deviceDisplayName.Trim();
        return string.IsNullOrWhiteSpace(projectName) ? machine : "[" + projectName.Trim() + "] " + machine;
    }

    private string StatusUrl => serverBaseUrl.TrimEnd('/') + "/api/status";
    private string CommandUrl => serverBaseUrl.TrimEnd('/') + "/api/command";

    private static string LoadDeviceApiKey()
    {
        string environmentKey = Environment.GetEnvironmentVariable("UNITY_DEVICE_API_KEY");
        if (!string.IsNullOrWhiteSpace(environmentKey)) return environmentKey.Trim();
#if UNITY_EDITOR
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "device-api-key.txt"));
#else
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "device-api-key.txt");
#endif
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }
        catch
        {
            return "";
        }
    }
}

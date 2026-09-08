# Unity 远程监控 Package 使用说明

监控网页：https://your-project.vercel.app/

## 新项目接入

1. 选择 Assets > Import Package > Custom Package，导入 unitypackage。
2. 把 Remote Device Monitor 预制体拖入作品的启动场景。
3. 等待编译完成，确认 Console 没有红色错误。

预制体推荐设置：

    Project Id：每个作品唯一，例如 mushroom-three-screen
    Project Name：网站显示的作品名
    Device Display Name：通常留空，自动使用电脑名
    Server Base Url：https://your-project.vercel.app/
    Report Interval Seconds：30
    Command Poll Interval Seconds：5
    Receive Commands：勾选
    Keep Between Scenes：勾选

同一个作品的所有电脑使用相同 Project Id；不同作品必须使用不同 Project Id。

## device-api-key.txt

编辑器测试时放在 Unity 项目根目录，与 Assets 同级。打包后放在 exe 旁边：

    作品文件夹/
    ├─ 作品.exe
    ├─ 作品_Data/
    ├─ UnityPlayer.dll
    └─ device-api-key.txt

文件中只放一行 Vercel 环境变量 DEVICE_API_KEY 的值。不要写变量名，不要加引号、空格或说明。当前所有 Demo 设备共用同一文件。

## 添加新设备

1. 把完整打包文件夹复制到新电脑。
2. 把 device-api-key.txt 放到 exe 旁边。
3. 启动程序并保持联网。
4. 等待约 30 秒后刷新网页。

设备会自动出现，无需在网站或 Supabase 中手动创建。

## 从作品代码上报状态

    private RemoteDeviceMonitor monitor;

    private void Start()
    {
        monitor = FindObjectOfType<RemoteDeviceMonitor>();
    }

    monitor.SetPlaybackState(2, true);   // 正在播放内容 2
    monitor.SetPlaybackState(2, false);  // 已停止
    monitor.SetError("第二块显示器没有检测到");
    monitor.ClearError();
    monitor.ReportNow();

正式代码中请先判断 monitor 不为 null。

## 接收网页命令

作品控制脚本添加公开方法：

    public void HandleRemoteContentCommand(int contentNumber)
    {
        SwitchContent(contentNumber);
    }

在预制体 Inspector 的 On Video Command 中点击加号，拖入负责控制内容的 GameObject，然后选择 HandleRemoteContentCommand (Dynamic int)。必须选择 Dynamic int。

监控组件只负责收发命令。如何切换视频、场景、三屏内容或投影校准，由作品自己的代码实现。

## 多场景和离线排查

Keep Between Scenes 勾选后，组件会跨场景保留。建议只在启动场景放置一个预制体。

网页超过约 90 秒未收到上报时显示离线。依次检查程序是否运行、预制体是否存在、密钥文件位置和内容、网络连接以及 Unity Console。

## 密钥区别

    Vercel Token（vcp_ 开头）：部署网站
    Supabase Secret Key（sb_secret_ 开头）：Vercel 访问数据库
    DEVICE_API_KEY：Unity 设备访问服务器
    CONTROL_API_KEY：网页下发控制命令

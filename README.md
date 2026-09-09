# Remote Device Monitor for Unity

一套面向 Unity 展览、互动装置和长期运行作品的轻量远程运维方案。

Unity 客户端定期向 Vercel 上报设备状态，Vercel 将状态保存到 Supabase，
管理人员通过浏览器集中查看设备是否在线、当前运行内容和错误信息，也可以
向设备下发作品已经实现的控制命令。

```text
Unity 设备
   │  状态上报 / 命令轮询
   ▼
Vercel Serverless API
   │  读写
   ▼
Supabase
   ▲
   │  浏览器查看与控制
运维管理网页
```

## 仓库内容

```text
RemoteDeviceMonitor-For-Unity/
├─ Unity package/                 Unity 通用客户端
├─ unity-device-monitor-vercel/   网站、API 和数据库脚本
├─ 网站配置教程/                   部署教程与半自动配置脚本
└─ README.md                      仓库总说明
```

### `Unity package`

包含可导入其他 Unity 项目的通用组件：

- `Remote Device Monitor.unitypackage`
- `RemoteDeviceMonitor.cs`

预制体包含在 `.unitypackage` 内，导入后会出现在 Unity 项目中。

组件负责设备身份、状态上报、在线心跳和命令轮询。每个作品自身的播放、
投影、串口或互动逻辑仍由作品代码实现，再通过公开方法或 UnityEvent 与监控
组件连接。

### `unity-device-monitor-vercel`

包含可部署到 Vercel 的管理网站和 Serverless API：

- 设备在线与离线判断
- 作品分类
- 状态卡片和错误信息
- 视频切换示例
- 蘑菇三屏投影示例
- CONTROL_API_KEY 控制鉴权
- Supabase 建表与升级 SQL

`assets/projects/` 中的每个 JavaScript 文件对应一种作品界面。新增作品时，
可以复制示例模块并在 `assets/app.js` 的 `projects` 对象中登记。

### `网站配置教程`

包含：

- 从零配置 Supabase 和 Vercel 的中文教程
- `supabase.sql`
- `Setup-UnityMonitor.ps1` 半自动部署脚本

建议第一次使用时先阅读 `网站配置教程/网站部署配置教程.md`。

## 从零开始

### 1. 部署数据库和网站

1. 创建 Supabase 项目。
2. 在 Supabase SQL Editor 中执行 `网站配置教程/supabase.sql`。
3. 创建 Vercel 项目并部署 `unity-device-monitor-vercel`。
4. 在 Vercel Production 环境中配置以下变量：

```env
SUPABASE_URL=https://your-project.supabase.co
SUPABASE_SERVICE_ROLE_KEY=your-supabase-secret-key
DEVICE_API_KEY=your-random-device-key
CONTROL_API_KEY=your-different-random-control-key
```

也可以使用 `网站配置教程/Setup-UnityMonitor.ps1` 完成 Vercel 环境变量配置
和生产部署。

### 2. 接入 Unity 作品

1. 在 Unity 中选择 `Assets > Import Package > Custom Package`。
2. 导入 `Unity package/Remote Device Monitor.unitypackage`。
3. 把 `Remote Device Monitor` 预制体拖入作品的启动场景。
4. 在 Inspector 中填写唯一的 `Project Id`、作品名称和自己的 Vercel 地址。
5. 如果需要网页控制，把 `On Video Command` 绑定到作品自己的公开方法。

同一作品的不同电脑使用相同 `Project Id`，设备 ID 会结合电脑自身标识自动
生成；不同作品必须使用不同的 `Project Id`。

### 3. 放置设备密钥

编辑器测试时，将 `device-api-key.txt` 放在 Unity 项目根目录，与 `Assets`
同级。打包后将它放在 exe 旁边：

```text
作品文件夹/
├─ 作品.exe
├─ 作品_Data/
├─ UnityPlayer.dll
└─ device-api-key.txt
```

文件中只写 Vercel 环境变量 `DEVICE_API_KEY` 的值，不写变量名、不加引号。

### 4. 查看设备

启动 Unity 程序并保持联网。设备完成首次上报后，会自动出现在管理网站中，
无需提前在网站或 Supabase 中手动添加。

## Unity 端常用调用

```csharp
private RemoteDeviceMonitor monitor;

private void Start()
{
    monitor = FindObjectOfType<RemoteDeviceMonitor>();
}

private void UpdateMonitor()
{
    if (monitor == null) return;

    monitor.SetPlaybackState(2, true);
    monitor.SetError("第二块显示器未检测到");
    monitor.ReportNow();
}
```

恢复正常后可以调用：

```csharp
monitor.ClearError();
```

## 新增作品模块

如果新作品只增加自定义状态，推荐把数据放进通用的 `status` JSON，不必修改
数据库字段。网站维护通常包含：

1. 在 `assets/projects/` 新增作品模块。
2. 在 `assets/app.js` 中导入并登记模块。
3. 让 Unity 上报一致的 `project_id` 和状态结构。
4. 如有新命令，在 `api/command.js` 中加入允许项，并在 Unity 中实现处理逻辑。
5. 重新执行 `vercel --prod`。

只有新增数据库列或数据表时才需要再次执行 SQL；普通 UI 和 JSON 状态变化
不需要修改数据库。

## 密钥说明

| 名称 | 用途 | 可以放在哪里 |
| --- | --- | --- |
| Vercel Token | 命令行部署网站 | 本机临时环境变量或安全凭据管理器 |
| Supabase Service Role / Secret Key | Vercel 访问数据库 | 仅 Vercel 环境变量 |
| `DEVICE_API_KEY` | Unity 设备访问 API | Vercel 与设备的 `device-api-key.txt` |
| `CONTROL_API_KEY` | 浏览器下发控制命令 | Vercel 与可信管理人员 |

这些密钥不是同一个值，不应互相复用。

仓库中的服务器地址、设备 ID 和密钥都应使用示例值。

## 当前定位

本项目适合个人作品、演示和少量展览设备。正式商业部署前，请根据设备数量、
上报频率、安全要求以及 Vercel、Supabase 的最新服务条款和额度进行评估。

网页关闭不会影响 Unity 设备继续上报状态；网页打开时会自动刷新设备列表。

# Unity 作品运维中心

## 目录结构

- `index.html`：运维中心页面壳
- `assets/app.js`：设备列表、作品筛选、密钥和命令发送等公共逻辑
- `assets/projects/video-demo.js`：视频切换 Demo 模块
- `assets/projects/mushroom.js`：蘑菇三屏投影模块
- `api/status.js`：设备心跳上报和状态查询
- `api/command.js`：命令下发和设备轮询
- `supabase.sql`：数据库初始化及旧表增量升级

以后新增作品时，在 `assets/projects/` 下增加一个独立模块，并在
`assets/app.js` 的 `projects` 表中登记。不要把所有作品逻辑重新堆进
`index.html`。

## 升级和部署

1. 在 Supabase SQL Editor 重新执行一次 `supabase.sql`。升级语句均使用
   `if not exists`，不会删除已有设备数据。
2. 确认 Vercel 已设置 `SUPABASE_URL`、`SUPABASE_SERVICE_ROLE_KEY`、
   `DEVICE_API_KEY`、`CONTROL_API_KEY` 四个环境变量。
3. 在本目录执行 `pnpm dlx vercel --prod`。
4. 蘑菇投影打包目录与 exe 同级放置 `device-api-key.txt`；文件中只写
   `DEVICE_API_KEY` 的值，不写变量名，不加引号。

## 蘑菇投影支持的操作

- 查看三路投影、三块 Arduino、帧率、内存、运行时长和版本
- 启用或停止指定 Unity 投影输出
- 恢复或暂停 Arduino 自动串口连接
- 重新应用三屏输出路由

“停止投影输出”只关闭 Unity 对应 Canvas/Camera，不会从 Windows 层面
物理断开 HDMI。

# Airwar Together

Godot 4.7.2 .NET 科幻空战游戏，包含 2.5D 单人生存和 3D 星际联机空战，面向 PC 与 Android。2.5D 保留激光预警、追踪攻击、弹幕、补给与四档难度；3D 支持服务器权威战斗、最多 32 人房间与四大战区跃迁。

## 打开工程

使用 Godot **4.7.2 .NET** 导入 `airwar-together/project.godot`，构建后按 F6/F5 运行。桌面目标使用 .NET 8，Android 目标使用 .NET 9；同时安装两个 SDK 即可。

```powershell
dotnet build .\airwar-together\airwar-together.csproj
```

主场景已由可复用资产组成。`scenes/Main.tscn` 直接引用战场、UI 和音频场景；无需运行即可查看静态战机、灯光、相机、战场网格和菜单。切换到 3D 视图编辑战场，切换到 2D 视图编辑界面。模型、材质、UI 排版和音频不再由启动代码逐个生成。

资产目录、编辑方式和复用约定见 [资产工作流](docs/assets.md)。

## 操作与进度

PC：WASD、方向键或鼠标移动，Shift 冲刺，Space 清弹脉冲，Esc 暂停。Android：单指相对拖动飞机，第二个手指点击右下技能按钮。游戏失焦自动暂停。

生存计分和金币、四档难度、12 款机体/涂装、机库解锁、音乐/音效/触控偏好均已实现。档案保存到 `user://pilot_profile.json`，使用临时写入与可恢复备份。

连续点击主菜单大标题五次，触发原作鎏金“躲避射线”招牌彩蛋。

完整操作见 [游玩说明](docs/play.md)，设计与联机扩展边界见 [升级设计](docs/game-upgrade.md)。

## 构建发行包

安装与编辑器版本完全匹配的 .NET 导出模板。在 Godot 编辑器设置中配置 Android SDK、JDK 和调试签名。安装 Android SDK Platform 36、Build Tools 36.0.0 和 platform-tools，并使用 JDK 17 或更新版本。

```bash
python3 tools/build.py --godot /absolute/path/to/Godot --checks
```

默认导出 Linux、Windows 和 Android 本地测试包；可用 `--platform Linux` 限定平台。桌面 ZIP 包含 .NET 运行时与授权文件，Android 使用 ARM64 调试签名。产物在 `builds/`，诊断在 `artifacts/logs/`，这些目录不提交版本控制。

本机同时提供隔离构建方式，自动排除本地 MCP 与编辑器设置：`python tools/windows_isolated_export.py --refresh --checks`、`python tools/android_isolated_export.py --refresh`。所需工作区工具链与模板准备见各平台验证文档。

Godot 的 Android C# 支持仍标记为实验性；目标设备性能与触控需要真机验证。官方说明：[C# 平台支持](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/index.html)、[Android 导出](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_android.html)。

## 验证

```bash
dotnet build airwar-together/airwar-together.csproj
"$GODOT_PATH" --headless --path airwar-together -- --self-test
"$GODOT_PATH" --path airwar-together -- --workflow-test
```

战斗检查覆盖三维扫掠碰撞、无敌时间、技能、道具、难度和隔离存档恢复；流程检查覆盖机库、键鼠/双指触控、暂停、重试与结算。检查使用临时档案，保持真实玩家进度。可用 `--scenario=menu|hangar|settings|combat|pause|results --capture=/absolute/path.png` 保存实际渲染截图；`--mobile-preview` 使用触控 HUD。

## 3D 星际空战

主菜单点击「3D 星际空战」。创建房间后，其他电脑或安卓设备输入房主 IP 与 UDP 端口连接；默认端口为 24567。支持自由俯仰/滚转飞行、机炮、推进、护盾、重生与星球战区跃迁。地表有沙漠峡谷、冰海群岛、火山工业区，深空有空间站与小行星。每战区宽 20 公里，四区共享 32 人上限。

房主模式为 1+31，专服为 32 客户端；两类真实本机 UDP 容量测试已通过。公网需要可达 UDP 端口，当前没有中心匹配与 NAT 穿透。完整操作、网络和验证边界见 [星际空战说明](docs/orbital-frontier.md)。

战机、场景和音频已保存为场景、网格、材质和 WAV 资产，随源码管理。战机采用 Quaternius 的 10 种独立舰体，基地采用 KayKit 免费模型，均为 CC0；原许可和改造记录见 [第三方资产](docs/third-party-assets.md)。Noto 中文字体许可证见 `airwar-together/assets/fonts/LICENSE.txt`。彩蛋保留参考项目的原始鎏金标题图片，Java 运行时不纳入新游戏。

仓库：[6NewUser6/Airwar-Together](https://github.com/6NewUser6/Airwar-Together)。

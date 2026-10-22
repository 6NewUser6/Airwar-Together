# 首版验证记录

日期：2026-10-14。Godot 4.7.2 .NET，桌面目标 net8.0，Android 目标 net9.0。

| 验证 | 结果 |
| --- | --- |
| C# 构建 | 0 错误、0 警告 |
| 战斗与存档检查 | 19 项通过：扫掠碰撞、高度差、边界、无敌时间、补给、技能、难度、死亡与档案恢复 |
| 实际 Godot 界面流程 | 14 项通过：菜单、鎏金标题彩蛋、机库购买、键盘飞行、相对触控、双指技能、触点释放、暂停、恢复、重试、死亡结算与返回 |
| 独立 Linux 发行包 | 包内运行时运行上述检查通过，不依赖编辑器启动；AMD Radeon 780M 的原生 OpenGL ES 战场截图通过 |
| Linux 与 Windows 导出 | 成功生成包含 .NET 运行时的 ZIP |
| Android 导出 | 成功生成 ARM64、API 24+ 调试签名 APK |
| APK 结构与签名 | v2/v3 签名通过；启动器 activity-alias 导出并含 MAIN/LAUNCHER；游戏程序集与 Mono 运行库存在 |
| 视觉核验 | 菜单、机库、战场、结算与 1920×864 宽屏触控 HUD 截图已检查 |

流程检查在 Linux X11/Xvfb、Mesa llvmpipe 环境执行，并在本机 AMD Radeon 780M 原生 GPU 验证三维战场渲染。Linux 使用 OpenGL ES 兼容路径，关闭 V-Sync 并限制到 60 FPS，采用本机可运行的兼容路径。Windows 包完成导出，尚未在 Windows 系统实际游玩；Android 未连接真机，触控与性能需要设备测试。双指检查使用 Godot 输入事件，验证的是输入路由与玩法行为。

截图和详细诊断在本机 `artifacts/previews/`、`artifacts/logs/`，不纳入源码仓库。界面测试使用隔离临时档案，存档恢复检查模拟损坏的主档并确认有效备份仍可恢复。

Godot 4.7.2 的命令行导出在成功结束时会打印 `EditorSettings not instantiated yet ... export/android/shutdown_adb_on_exit`。构建工具只忽略这条已识别的编辑器退出诊断；其余错误或非零退出码会中止构建，发行包仍单独验证。

可重复执行：`python3 tools/build.py --checks`，随后运行包内 `AirwarTogether.x86_64 -- --workflow-test`。完整操作见 [游玩说明](play.md)。

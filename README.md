# Airwar Together

Godot 4.7.2 .NET 科幻 2.5D 飞机生存游戏，支持 PC 与 Android。保留参考版本的激光预警、追踪攻击、弹幕、生命/金币道具与四档难度，增加三维战机、冲刺、清弹脉冲、暂停、结算和本地进度。

## 打开工程

使用 Godot **4.7.2 .NET** 导入 `airwar-together/project.godot`，构建后按 F6/F5 运行。桌面目标使用 .NET 8，Android 目标使用 .NET 9；同时安装两个 SDK 即可。

```powershell
dotnet build .\airwar-together\airwar-together.csproj
```

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

Godot 的 Android C# 支持仍标记为实验性；目标设备性能与触控需要真机验证。官方说明：[C# 平台支持](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/index.html)、[Android 导出](https://docs.godotengine.org/en/4.7/tutorials/export/exporting_for_android.html)。

## 验证

```bash
dotnet build airwar-together/airwar-together.csproj
"$GODOT_PATH" --headless --path airwar-together -- --self-test
"$GODOT_PATH" --path airwar-together -- --workflow-test
```

战斗检查覆盖三维扫掠碰撞、无敌时间、技能、道具、难度和隔离存档恢复；流程检查覆盖机库、键鼠/双指触控、暂停、重试与结算。检查使用临时档案，保持真实玩家进度。可用 `--scenario=menu|hangar|settings|combat|pause|results --capture=/absolute/path.png` 保存实际渲染截图；`--mobile-preview` 使用触控 HUD。

## 后续扩展

当前以真正三维场景运行 2.5D 单人生存模式。战斗状态与显示层分离，三维坐标、固定 tick 和实体 ID 为完整 3D、立体弹道与服务端权威模式留出接入点。自由空战、射击武器和联网传输在后续阶段实现。

程序化战机、场景和合成音频随源码生成；Noto 中文字体许可证见 `airwar-together/assets/fonts/LICENSE.txt`。彩蛋保留参考项目的原始鎏金标题图片，Java 运行时不纳入新游戏。

仓库：[6NewUser6/Airwar-Together](https://github.com/6NewUser6/Airwar-Together)。

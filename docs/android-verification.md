# Android 构建与验证

## 构建环境

验证使用 Godot 4.7.2 .NET、.NET SDK 9.0.203、Android workload 35.0.7、JDK 21，以及 Android SDK Platform 36 / Build Tools 36.1.0。为避免改动系统 .NET 安装和用户 Godot 设置，Android workload 放在忽略目录 `.tools/dotnet-android`，每个构建副本使用独立的 `.tools/android-godot-<副本名>/editor_data`，避免同时导出时临时 APK 名称冲突。

官方模板由 [Godot 4.7.2 发布页](https://github.com/godotengine/godot-builds/releases/tag/4.7.2-stable)获取。`tools/android_prepare_templates.py` 用 HTTP Range 仅提取 Android 模板，ZIP CRC 由 Python `zipfile` 验证；不下载其他平台模板。

`tools/android_isolated_export.py` 复制当前游戏源文件和资源到 `.tools` 内的公开构建副本，排除本地 MCP 插件、环境文件、Godot 缓存和本地工程属性，不修改正在编辑的工程。它在副本中依次执行 .NET 构建、Godot 导入和 APK 导出。

```powershell
python tools/android_prepare_templates.py
python tools/android_isolated_export.py --refresh
python tools/android_isolated_export.py --refresh --emulator --name android-emulator-check
```

脚本需要 Python `requests`，以及上述工作区内的 .NET SDK/Android workload。模拟器版本只在副本中切换为 x86_64；正式 Android 预设仍为 ARM64。

## 已完成的构建证据

2026-10-22 首轮导出：

- ARM64 APK 导出、调试签名和 APK 校验成功，产物 `builds/android/AirwarTogether-arm64.apk`。
- x86_64 模拟器 APK 导出成功，产物 `builds/android/AirwarTogether-emulator.apk`。
- 独立公开副本 .NET 构建为零警告、零错误。
- `aapt dump badging` 确认最低 Android API 24、目标 API 36、`android.permission.INTERNET`，以及对应架构的原生库。
- ZIP 检查确认 APK 包含 `assets/assets/arena/collision_world.json` 和对应架构的 `airwar-together.dll`。
- Godot 在完成导出后出现已知的 `EditorSettings not instantiated ... shutdown_adb_on_exit` 退出诊断；进程退出码为 0，APK 已通过签名与校验。这一诊断与游戏运行检查分开记录。

构建与导出原始日志位于忽略目录 `artifacts/android/`。APK 与本地工具依赖不提交版本控制。

## 运行验证状态

当前没有连接的 Android 真机。使用专用无窗口 API 35 Google APIs x86_64 AVD、独立 Android Emulator 37.2.12，通过 `-gpu host -feature -Vulkan` 实际启动了导出的 .NET 游戏。Godot 日志确认 OpenGL ES 3.1 Compatibility，宿主显卡为 NVIDIA RTX 4060。

Android 实际运行得到 `ARENA_WORKFLOW_OK 24`：四战区加载、创建房间、两个真实 ENet peer、显示两架机体、触屏多指所有权、射击/推进同时按住、菜单、镜头、权威跃迁及离房均通过。触屏检查由游戏的输入 API 注入事件，不能等同于 Android OS 实际多指操作。

引擎诊断在稳定飞行阶段记录 57–58 FPS；初次切区/初始化曾记录 8 FPS。上述数字来自宿主 GPU 映射的模拟器，不能用于推断 Android 手机性能。

API 35 与原有 API 36 镜像在本机 WHPX 环境中有 Android `SystemServer`/`zygote` 自身崩溃，API 35 曾在完成游戏 workflow 后丢失系统 package/activity 服务。日志以进程区分，这些系统异常不归因于游戏。后续 API 30 镜像也未能提供稳定的系统服务。

原始证据为 `artifacts/android/workflow-runtime.log`、`workflow-metrics.json`。

## 跨 PC/Android 满房

2026-10-22 实际通过 `ANDROID_MIXED_ROOM_OK`：31 个独立 PC headless 客户端与一个运行在 Android API 35 AVD 的 APK，通过真实 ENet UDP 连接同一个专用服务器。Android 经模拟器宿主别名 `10.0.2.2` 访问 PC，服务器与 Android 同时记录 32 人；Android 收到 525410 字节状态数据，服务端记录 3493 发弹和 23 次跃迁，四战区均有玩家。

验证工具为 `tools/android_mixed_room_check.py`，原始结果 `artifacts/android/mixed-20261022-173013/result.json`，服务端/31 个桌面进程/Android 日志同目录保存。它使用真实客户端进入房间，不直接注入服务端玩家状态。验证结束后工具清理自己启动的进程。

同机同时运行 31 个 PC .NET 进程和 Android 模拟器时，Android 诊断在加载后记录约 21–30 FPS；这属于重负载下的宿主 GPU 模拟器表现，不等同于手机性能。不同物理设备的 Wi-Fi/互联网链路尚未验证。

实际 ADB 点击/拖动和屏幕像素截图尚未通过：后续 API 30 镜像同样发生宿主 WHPX 下的 Android 系统服务反复消失，无法稳定保留可操作的 Activity。曾拍到 Android 桌面、初始化黑屏或系统崩溃对话框，均未计为游戏画面证据。API 注入触控、日志中的 drawCalls 与 FPS 不代替实际 Android UI 画面验收。需要可靠模拟器环境或连接 Android 真机后继续这部分。

用户已确认暂时没有可连接的安卓手机，同意保留上述真机项目为待验收。现有模拟器流程与跨端联机证据继续保留。

API 30 初次声明 GLES 2.0，触发 `EGL_BAD_CONFIG`；开启专用模拟器的 `GLESDynamicVersion` 后报告 GLES 3.1，但系统服务仍反复崩溃。此环境排查与 APK 游戏错误分开记录。没有更改宿主 Windows 防火墙、虚拟化或隐私安全设置，也没有替换用户 SDK 中的原模拟器。

最终 0.2.0 APK 的架构、权限、碰撞 JSON、作者许可证和本地 MCP 排除由 `tools/android_verify_apk.py` 检查，报告保存在 `artifacts/android/{arm64,x64}-package-check.json`。`tools/android_validation_args.py` 只修改验证 APK 的 Godot 参数文件并重新调试签名，游戏程序集与资源保持一致。

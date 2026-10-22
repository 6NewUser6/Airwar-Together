# Windows 0.2.0 独立发行验证

日期：2026-10-22。Godot 4.7.2 .NET、Windows x86_64、桌面目标 .NET 8。

## 构建与隔离

使用当前游戏源码和资产建立独立副本，排除本地 MCP 插件、`.env`、`local.props`、参考工程及编辑器缓存。Windows 模板来自 Godot 官方 4.7.2 mono 发布包，以 HTTP Range 仅取 x86_64 模板并校验 ZIP 条目 CRC。工具与下载位于本机 `.tools/`，不包含在源码或发行包中，也不改用户 Godot 配置。

```powershell
python tools/windows_prepare_templates.py
python tools/windows_isolated_export.py --refresh --checks
```

导出为便携式调试发行包：解压完整 ZIP 后运行 `AirwarTogether.exe`，保留相邻的 `data_airwar-together_windows_x86_64` 文件夹。该文件夹包含 `coreclr.dll`、`hostfxr.dll`、`System.Private.CoreLib.dll` 和游戏程序集，不需要另装 Godot 编辑器或 .NET SDK。

## 实际独立运行检查

使用导出包自带的 `AirwarTogether.console.exe --headless -- --检查参数`，实际得到：

| 检查参数 | 结果 |
| --- | --- |
| `--self-test` | 20 项通过，战斗与存档 |
| `--asset-test` | 39 项通过，2.5D 场景与基础资产 |
| `--arena-self-test` | 82 项通过，权威模拟与协议 |
| `--arena-asset-test` | 31 项通过，四战区、20 km 地形、10 种独立舰体、音频和碰撞数据 |
| `--arena-workflow-test` | 24 项通过，创建房间、双端 ENet、镜头、多指触控路由、菜单、跃迁和退出 |

导出日志确认 `assets/arena/collision_world.json` 及两套社区模型的 `LICENSE.txt` 均进入资源包。导出目录没有 MCP 或 Reflector 依赖。字体许可证与玩法、联机、第三方来源说明作为旁附文档随 ZIP 分发。

发行包的无窗口专用服务器入口也已实际启动，打印 `ARENA_SERVER_READY` 并读取到 1990 个静态碰撞代理：

```powershell
.\AirwarTogether.console.exe --headless -- --arena-dedicated --arena-port=24567
```

官方发行模板禁止从命令行覆盖场景路径，发行包请使用上述模式参数；源码编辑器可直接运行 `Server.tscn`。

该包中的流程检查使用 UDP 24691，复测时需保持测试端口空闲。真实 32 人容量另见主验证记录。

## 产物与边界

在上述检查之外，后续真实 GPU 的 32 架同屏/切区压力复现了一次交互运行中的 Godot 原生崩溃，尚在定位。因此当前包是候选版本；五类自动检查通过不等于多人画面持续运行已稳定。独立包单机自动飞行 180 秒可正常退出，但它不能替代 32 架同屏压力验证。

- `builds/windows/AirwarTogether.exe` 与相邻运行时目录。
- `builds/AirwarTogether-windows-0.2.0.zip`。
- 本地日志 `artifacts/windows/`，`release-result.json` 包含产物字节数、SHA256 与检查结果。

以上证明 Windows 独立包能运行所列检查并加载实际打包资产。真实 GPU 画面、多人容量及 Android 运行由各自的验证记录覆盖。Godot 命令行成功导出后可能打印已知的 `EditorSettings ... export/android/shutdown_adb_on_exit` 退出诊断；脚本仅排除这一条，其余错误及非零退出都会中止。

# 原生渲染稳定性排查（未解决）

环境为 Windows、Godot 4.7.2 .NET、NVIDIA RTX 4060 Laptop GPU。游戏可编译，逻辑与真实 ENet 容量检查通过，但这些结果不能替代真实 GPU 稳定性。

复现入口为默认主场景启动，加 `-- --arena-render-test --arena-duration=240`。该检查使用 32 个本地模拟机体、12 个机型/涂装、四区轮换、射击、死亡重生和镜头切换；它不测试网络容量。原始日志在忽略目录 `artifacts/logs/`。

已核实的主工程结果：

| 对照 | 实际结果 |
| --- | --- |
| 原共享资产，原生 OpenGL | `render-check-main-opengl120.log`，最后指标 55.09 秒，随后 signal 11，未完成 120 秒 |
| 仅实例材质浅复制，仍共享网格与贴图 | `render-check-main-material240.log`，最后指标 215.30 秒，随后同一原生故障；修改已撤回 |
| 原资产，Mobile / Vulkan | `render-check-main-mobile240.log`，约 95 秒开始出现 `particles_get_instance_buffer_motion_vectors_offsets: particles is null`；实际游戏场景没有 GPU 粒子，判为失败，约 200 秒主动结束 |

隔离副本的其他对照：

- 默认 Main 过渡后使用 ANGLE / D3D11，约 170 秒仍在相同 GLES3 路径崩溃，不能作为驱动替代修复。
- 暂停 .NET 垃圾回收、持有网格与材质的强引用、只复制尾焰/护盾材质、材质预热、禁用阴影/LOD 等均有失败样本。
- 直接进入 3D 场景的若干 240 秒对照正常；从 Main 过渡的原版本也偶有 240/300 秒正常样本。但后续直启 Arena 同样首帧崩溃，不能将问题归因于 Main 过渡；单次通过不能证明修复。
- 经过空白场景等待三帧、一次 post-draw 并同步渲染后再进入，仍在约 185 秒崩溃；保留旧 Main 树而不释放，也在约 40 秒崩溃。
- 完全移除音频节点、固定保留四个战区场景、分离 World3D、限制 worker 线程和关闭并行剔除均有失败样本，没有采用这些实验修改。
- 深复制网格/材质曾短时通过，但有内存与帧率代价，未采用。原始外部资产仍保留复用。
- 匹配各自 SDK 的 Godot 4.6.3 稳定版与 4.8 beta1 隔离对照也崩溃，未更改主工程引擎版本。把原 GLTF 几何另存 ArrayMesh 与静态节点也有失败样本，不能仅归因于导入场景的层级。
- 把 93 个网格用 `ArrayMesh.AddSurfaceFromArrays` 重新编码、保留原顶点位置和贴图并移除旧 LOD/shadow buffer 后，240 秒曾通过，但下一轮 600 秒测试在约 75 秒崩溃，未采用这些实验资产。
- 完全移除静态战区实例、只留下机体/标签/弹道的对照仍在约 65 秒崩溃，静态世界不是必要触发条件。

崩溃栈多次落在引擎 GLES3 的 `_render_list_template`。反汇编可将偏移 `main+0x14454ab` / `0x1445501` 对应到渲染表面所有者或着色器数据的读取；这只是定位线索，尚不能据此确认根因。

后续诊断统一强制 C# `Rebuild`，并记录实际 DLL 哈希；恢复源文件的旧时间戳可能让增量构建保留上一次实验的二进制，不能仅凭源码状态判断运行条件。主工程上述原生 OpenGL、材质复制、Mobile/Vulkan 三项对照均由新写入的源文件重新构建。

当前 Windows 与 Android 安装包仍为候选版本。没有以忽略错误、隐藏场景、降低人数或短时测试来标记此项已修复。Android 真机触控与画面验收由用户同意暂缓，详情见 [Android 验证](android-verification.md)。

## 2026-10-22 续查：修正 GPU 验证与运行时对照

发现窗口被遮挡时，模拟、FPS 与上一帧 drawCalls 仍可能继续打印，但实际绘制已经暂停。因而旧的“运行数分钟正常”记录不能单独证明持续 GPU 稳定；已经发生的原生崩溃仍是有效失败证据。

`tools/arena_render_check.py` 现禁用自动渲染循环，由检查入口每帧主动绘制并交换缓冲；每五秒回读真实视口像素，记录 SHA256、完成绘制帧数与实际 .NET 运行时，并保存四区 PNG。通过条件要求像素持续变化、四个战区都有绘制与截图、足够的实际绘制帧数，以及正常退出且没有原生异常或引擎错误。Windows 的访问异常等 NTSTATUS 即使未生成引擎崩溃栈也判为原生失败。普通游戏运行不使用这些检查选项。

新证据：

| 对照 | 结果与原始报告 |
| --- | --- |
| 默认 .NET 9.0.4，显式绘制但不交换缓冲，完整 Main → Arena | 120 秒正常退出；24 个不同像素哈希，四区都有绘制。`artifacts/render/20261022-205904-318063/result.json`。这是单次通过，不能据此认定修复。 |
| .NET 8.0.8，显式绘制并交换缓冲，完整 Main → Arena | 约 80 秒后以 `0xc0000005` 退出；17 个像素哈希，四区截图均已保存。`artifacts/render/20261022-210606-266032/result.json`。改变 .NET 主版本不能解决问题。 |
| 独立渲染线程 | 初始化即发生 `cowdata.h` 下标越界并退出，未开始 32 机体检查。`artifacts/render/20261022-210535-972933/result.json`。没有采用这一实验设置。 |
| 纯 GDScript 资产/生命周期对照 | 同一引擎与原始网格、32 机体、动态标签、256 弹道和四区释放/加载，主动绘制 300 秒后正常退出。`artifacts/logs/native-gdscript-control-full.log`。它没有运行权威模拟和完整 Main 入口，不代表 C# 为根因，也不替代正式游戏验收。 |

运行时对照仅给子进程设置 `DOTNET_ROLL_FORWARD`；不修改用户 .NET 或 Godot 配置。可重复命令：

```powershell
python tools/arena_render_check.py --runs 3 --seconds 300
python tools/arena_render_check.py --runs 1 --seconds 300 --dotnet-roll-forward LatestPatch
```

额外的 Windows 调试器在本机 `.tools/` 隔离监视 GLES3 渲染列表，记录损坏指针的写入现场；调试器、转储与临时对照工程不进入游戏或发行包。旧转储中一个表面指针的低位损坏，尚未确认原始写入来源。

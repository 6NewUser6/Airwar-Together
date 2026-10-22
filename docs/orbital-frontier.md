# Orbital Frontier 星际空战

现有 2.5D 生存模式保留。主菜单的「3D 星际空战」进入独立模式，电脑与 Android 使用同一房间协议和服务端判定。

## 世界与飞行

四个战区分别为 20 × 20 km，使用米作为坐标单位；跃迁后切换战区资产。它们不是以真实天文尺度无缝连接的球面行星。

| 战区 | 场景与飞行特点 |
| --- | --- |
| 阿斯特里昂 | 沙漠、砂岩柱、峡谷航道、矿业基地和高架着陆设施，可绕地标躲避追击 |
| 涅瑞伊德 | 冰海、群岛、冰柱与海上平台，水面形成最低飞行高度 |
| 赫利俄斯 | 火山地貌、地热工业基地，低空高温区持续伤害机体 |
| 深空 | 三维小行星航道、空间站、行星远景和跃迁环，无地表高度约束 |

地表地形、基地、航道、灯光、相机与环境均已固化为可编辑场景和网格资产。地形按 125 m 网格离线生成；服务端使用相同三角插值，建筑、小行星和航道结构使用配套静态碰撞数据。装饰环是导航地标；目前可在安全航行时直接选择目的地跃迁，不要求穿过环。

战机有完整俯仰、偏航和滚转。常规速度由油门决定，范围 80–220 m/s，默认 150 m/s；推进最高 330 m/s 并消耗能量。机炮弹道由服务端扫掠碰撞判定，护盾优先吸收伤害；击落后五秒重生。跃迁充能三秒，消耗能量，冷却期间不能连续跃迁，交战伤害可能打断充能。

## 操作

电脑：按住鼠标右键转向，松开恢复指针；方向键俯仰与偏航，A/D 滚转，Q/E 偏航，W/S 调整油门。鼠标左键或空格开火，Shift 推进，J 跃迁到界面选中的目的地，V 切换追尾/驾驶舱视角，Tab 查看房间战绩，Esc 打开菜单。

Android：在屏幕左侧下部拖动转向，右侧可同时按住开火、推进及滚转按钮，下方滑杆控制油门；选择目的地后点跃迁。每根手指独立归属，移到按钮外释放也会清除原操作。菜单与失焦会清除持续输入；联机房间继续运行。

## 创建和加入房间

在界面输入呼号、机体、端口；「创建房间 / 本机试飞」同时作为服务端和一名玩家。其他设备输入房主 IP 与相同端口连接。默认 UDP 24567；局域网设备需要能互相访问，互联网直连需要房主网络将该 UDP 端口转发至主机。当前没有中心匹配、NAT 穿透或自动公网服务器租用。

房主模式最多 1 名房主加 31 名远程玩家，专用服务器最多 32 名远程玩家。所有战区共用一个房间人数上限，第 33 人会收到房间已满提示。房主退出会关闭该房间；可用专用服务器维持房间。

编辑器/源码运行示例：

```powershell
# 创建可玩房间
& $Godot --path airwar-together -- --arena-server --arena-port=24567

# 专用服务器（不渲染世界）
& $Godot --headless --path airwar-together res://scenes/arena/Server.tscn -- --arena-port=24567

# 普通客户端
& $Godot --path airwar-together -- --arena-client=192.168.1.20 --arena-port=24567
```

专用服务器默认持续运行，直到进程被关闭；`NetworkCheck.tscn` 是另一套有时限的诊断场景。

Windows 独立发行包使用下列入口，不需要场景文件路径：

```powershell
.\AirwarTogether.console.exe --headless -- --arena-dedicated --arena-port=24567
```

## 网络设计与验证

服务端以 60 Hz 判定飞行和战斗，客户端以 30 Hz 发送输入，快照目标 20 Hz，分别使用控制、输入、状态三个 ENet 通道。客户端没有提交位置、血量或击杀的接口。输入包含递增序号，校验有限数值、长度、频道和成员身份；超时输入转为巡航，断线释放席位。细节快照与弹道按战区发送，每个应用层数据包不超过 1200 字节。

复测：

```powershell
& $Godot --headless --path airwar-together -- --arena-self-test
& $Godot --headless --path airwar-together -- --arena-workflow-test
python tools/arena_network_check.py --godot $Godot --mode processes
python tools/arena_network_check.py --godot $Godot --mode processes --listen
```

2026-10-22 已实际通过专服 32 独立客户端、房主 +31 独立客户端两类本机 UDP 测试，覆盖四区并发、开火命中、跃迁、断线重连及溢出拒绝。报告保存在本地 `artifacts/network/`。这些证据不等同于真实公网弱网表现或 Android 真机性能；安卓验收见单独记录。

## 资产编辑

- `scenes/arena/Arena.tscn`：模式入口与组件组合。
- `scenes/arena/ArenaHud.tscn`：界面静态布局。
- `scenes/arena/sectors/`：四区可编辑场景。
- `assets/arena/`：地形、材质、天空与静态碰撞数据。
- `scenes/ships/`：12 款战机预制件，共使用 10 种独立社区舰体。
- `tools/bake_arena_world.py`、`tools/bake_arena_ui.py`：离线重新生成工具，运行游戏不依赖它们。

修改地形或障碍后应同步服务端碰撞数据并重启房间。第三方模型原许可与来源见 [资产来源](third-party-assets.md)。

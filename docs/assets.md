# 资产编辑与复用

主场景 `airwar-together/scenes/Main.tscn` 现在直接组合已保存的场景。模型、界面和音频无需运行脚本生成；编辑器中能看到它们，运行时只绑定节点、响应输入、切换页面、实例化危险物和更新战斗状态。

## 从哪里开始

| 资产 | 路径（相对 Godot 工程） | 可编辑内容 |
| --- | --- | --- |
| 主场景 | `scenes/Main.tscn` | 战场、界面、音频及碰撞判定标记的组合 |
| 战场 | `scenes/world/CombatWorld.tscn` | 地面、网格、机库平台、塔楼、星场、相机、灯光与环境 |
| 战机控制器 | `scenes/ships/PlayerPlane.tscn` | 默认外观实例、展示位置、缩放与朝向 |
| 十二款战机 | `scenes/ships/spectre.tscn` 等 | 机身、机翼、驾驶舱、发动机尾焰、保护环 |
| 机体数据 | `assets/catalog/ShipLibrary.tres` 和各款 `.tres` | 名称、描述、价格、提示色、外观场景引用 |
| 共享网格 | `assets/meshes/` | 机身几何、圆环、基础方块、球体和星场 MultiMesh |
| 共享材质 | `assets/materials/` | 金属、玻璃、机体涂装、光束、补给、粒子及环境 |
| UI 组合 | `scenes/ui/FlightHud.tscn` | 七个页面实例，默认显示主菜单 |
| 独立 UI 页面 | `scenes/ui/MainMenu.tscn`、`Hangar.tscn`、`Settings.tscn`、`GameHud.tscn`、`PauseMenu.tscn`、`ResultsMenu.tscn`、`LegacyMenu.tscn` | 布局、文字、按钮、锚点、容器与初始状态 |
| UI 主题 | `assets/ui/FlightTheme.tres` | 字体、按钮类型、样式与颜色；渐变背景另存为 shader 和材质 |
| 危险物与补给 | `scenes/effects/` | 光束、移动弹幕、维修/金币补给、火花的预制外观 |
| 音频 | `scenes/audio/FlightAudio.tscn`、`assets/audio/` | 16 秒循环音乐、六种音效、八路播放节点及资源引用 |

这些是源资产。修改后保存即可，不存在每次启动覆盖手工调整的生成步骤，也不需要启用 `[Tool]` 脚本来显示它们。

## 编辑器预览

打开 `scenes/Main.tscn`。在 3D 视图中查看战场，也可打开 `scenes/ships/spectre.tscn` 单独编辑战机；选中节点按 F 聚焦。选择战场的 Camera 节点并启用相机预览可以查看取景。在 2D 视图打开任意 UI 子场景查看排版；`FlightHud.tscn` 的页面可通过节点的可见性切换预览，保存前让 Menu 可见、其他页面隐藏。

F5 运行完整游戏。运行时的敌弹、拾取物、相机震动、飞机姿态、尾焰和护盾显示仍由玩法状态驱动，这是对已有资产的实例化和动画。

## 替换与复用

修改一个共享材质会影响所有引用它的资产。如果只想更改一款机体，先将该材质设为独立资源，再修改颜色。战机数据的 `VisualScene` 可直接拖入替代场景，同一机体 ID 也支持替换外观。编辑解锁价格和说明时修改机体 `.tres`，无需修改 C# 常量表。

`PlayerPlane` 控制器通过模型中的 `LeftExhaust`、`RightExhaust` 和 `Shield` 节点控制尾焰与保护环；替换外观时保留这些命名。`CombatWorld` 通过 `Camera`、`PlayerPlane`、`Boundary`、`HangarPad`、`Scenery`、`Entities`、`Sparks` 绑定战场。光束预制的 `Core` 使用单位方块网格，由威胁的端点和宽度缩放。火花材质启用了 `resource_local_to_scene`，每个实例的颜色变化互不污染。

UI 页面常驻，切换菜单不会销毁和重建控件。控制器通过节点路径绑定按钮和数据标签；修改布局可以移动控件位置，但改变节点名称或父子层级时，也需要更新 `FlightHud.cs` 的绑定路径。目前机库的十二个按钮是预制节点：新增机体时，在 `ShipLibrary.tres` 增加定义，并在 `Hangar.tscn` 中复制一个按钮，将节点名称设为该机体 ID。

## 验证

构建 C# 后，使用 Godot .NET 执行：

```text
Godot --headless --path airwar-together -- --asset-test
Godot --headless --path airwar-together -- --self-test
Godot --headless --path airwar-together -- --workflow-test
```

`--asset-test` 在不把测试场景加入场景树、不执行它的 `_Ready` 的情况下，检查战机、战场和界面已存在于预制中，检查共享资源、机体目录和循环音频。流程检查还验证页面节点在导航后保持同一实例，并继续覆盖输入、触控、技能、暂停与结算。所有测试使用临时档案，不修改真实玩家进度。

Godot 场景所有权与序列化约定参考 [PackedScene 官方文档](https://docs.godotengine.org/en/stable/classes/class_packedscene.html)。

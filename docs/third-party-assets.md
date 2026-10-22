# 第三方游戏资产

引入日期：2026-10-22。这里记录实际随游戏分发的社区资产；候选清单见 [community-assets.md](community-assets.md)。这些许可只覆盖对应第三方文件。

## Quaternius Ultimate Spaceships

- 作者：[Quaternius](https://quaternius.com/packs/ultimatespaceships.html)。下载来自作者页面指向的 [Google Drive 原始模型文件夹](https://drive.google.com/drive/folders/1NpfT3wqe2k3Jwue2xryi7tzxP4bWzETu)。
- 许可：**CC0 1.0 Universal**。作者随包许可在 `airwar-together/assets/community/quaternius_spaceships/AUTHOR_LICENSE.txt`，完整 CC0 文本在同目录 `LICENSE.txt`。
- 配色纹理来自 [Malcolm Nixon 的 CC0 Godot 社区移植](https://github.com/Malcolmnixon/Quaternius-Ultimate-Spaceships-Pack)，源版本 `f2782cc8c7964fc38223bfb5d13fb33d85e7dd09`。只选入模型纹理；没有带入它的旧版演示工程、脚本、着色器或插件。
- 处理：将作者的 glTF 2.0 几何规范为面向 **-Z**、包围盒中心在原点，宽不超过 3 米、长不超过 4.5 米；按原比例缩放，不改变轮廓。纹理从 2048 像素降为 512 像素，打包为独立 GLB；材质设置为有限金属度与较高粗糙度。导入场景与原始模型均无游戏逻辑。
- `manifest.json` 记录机型、配色、顶点数、尺寸和 GLB 大小。`airwar_exhaust.obj` 是本项目新制的锥形尾焰几何，采用 CC0，非作者原模型。

| 游戏 ID | 作者机型 | 配色 |
| --- | --- | --- |
| spectre | Striker | Blue |
| ember | Spitfire | Orange |
| wraith | Omen | Purple |
| stratus | Zenith | Blue |
| manta | Pancake | Green |
| raptor | Challenger | Red |
| ion | Insurgent | Blue |
| flare | Executioner | Orange |
| nebula | Dispatcher | Purple |
| arctic | Imperial | Blue |
| jade | Spitfire | Green |
| gold | Challenger | Orange |

10 种独立舰体用于 12 个现有解锁条目，其中 jade 与 gold 是独立配色，不能计为额外两种舰体。所有包装场景仍提供 `LeftExhaust`、`RightExhaust`、`Shield` 挂点。现有 2.5D 战斗展示再按 0.42 倍缩放，并以屏幕上的碰撞标记表示有效碰撞点。

## KayKit Space Base Bits

- 作者：[Kay Lousberg](https://kaylousberg.itch.io/space-base-bits)。来源为 [作者官方 GitHub 仓库](https://github.com/KayKit-Game-Assets/KayKit-Space-Base-Bits-1.0)，版本 `6dfbcac9927d06283752c4defd4882cfe0d29666`。
- 许可：**CC0**，原包许可在 `airwar-together/assets/community/kaykit_space_base/LICENSE.txt`。
- 选入 23 种免费版模型的 glTF、相应 BIN，以及共用的 `spacebits_texture.png`。免费版之外的 Extra 模型、替代纹理和付费 Blend 源文件均未使用。
- 模型：五种基础模块、车库、太阳能屋顶、太阳能板、钻探结构、着陆器、大小着陆平台、货运站、两种集装箱、堆叠货物、照明、两种单岩石、岩石组、高结构、风机与直通道。
- 当前 2.5D 战场把反复出现的方塔替换为 12 组可编辑的殖民基地片段，包含不同模块、屋顶、货物、信标和外围钻探塔；它们沿两侧移动，中央保留弹幕判读空间。机库增加着陆平台。3D 战区复用同一组资产。

## 验证边界

资产来源与 glTF/GLB 的缓冲区、纹理依赖已检查。独立临时工程在 Godot 4.7.2 中完成实际导入与实例化，12 个战机包装的挂点类型正确，Hull 的实际顶点数为 1,169–7,648，并验证一项 KayKit 基础模型。整工程的 39 项资产检查与 20 项核心检查已通过。运行截图及移动设备性能继续由整工程验证统一进行；源包自称移动端优化不等于本游戏已通过真机性能测试。

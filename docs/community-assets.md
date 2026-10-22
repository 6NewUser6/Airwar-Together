# 免费社区资产候选

核验日期：2026-10-22。已选入 Quaternius 的 10 种独立舰体，封装为现有 12 款战机；另外选入 KayKit 的 23 种基地与道具模型。其他资源仍是候选。实际来源、变更和授权见 [第三方资产记录](third-party-assets.md)。模型数量与文件数量分开记录，避免把配色和格式副本计为独立造型。

## 优先组合

战机优先选择 Quaternius Ultimate Spaceships，地表基地选择 KayKit Space Base Bits，宇宙远景选择 Kenney Skyboxes Space。先保持低多边形风格一致，再通过灯光、材质和场景编排形成项目自己的视觉风格。

| 资源及作者来源 | 已核验免费内容 | 用途 |
| --- | --- | --- |
| [Quaternius Ultimate Spaceships](https://quaternius.com/packs/ultimatespaceships.html) | CC0；10 种独立飞船，每种 5 套配色；glTF、FBX、OBJ、Blend | 主力战机、敌机和大型舰船轮廓 |
| [Majadroid Spaceships and Components](https://majadroid.itch.io/3d-lowpoly-spaceships-and-components-cc0) | CC0；飞船及可组合零件，附导弹和补给箱；Blend、FBX、独立 OBJ、PNG | 可组合机体、装备与敌机变化 |
| [KayKit Space Base Bits](https://kaylousberg.itch.io/space-base-bits) | CC0；免费版 48+ 模型；glTF、FBX、OBJ；作者明确面向移动设备优化 | 月面基地、矿业殖民地、发射设施 |
| [Quaternius Ultimate Space Kit](https://quaternius.com/packs/ultimatespacekit.html) | CC0；92 模型；glTF、FBX、OBJ、Blend；包含星球、植被、角色和敌人等 | 异星地表与场景装饰 |
| [Kenney Space Kit](https://kenney.nl/assets/space-kit) | CC0；150 文件，非 150 种战机 | 飞船、星球及太空道具补充 |
| [Kenney Modular Space Kit](https://kenney.nl/assets/modular-space-kit) | CC0；40 文件，含动画与变体 | 模块化太空设施 |
| [Kenney Space Station Kit](https://kenney.nl/assets/space-station-kit) | CC0；90 文件，偏空间站室内 | 母舰内部与机库 |
| [Kenney Skyboxes Space](https://kenney.nl/assets/skyboxes-space) | CC0；5 文件 | 星云与宇宙背景 |

## Godot 社区适配

- [Ultimate Spaceships 的社区版本](https://godotengine.org/asset-library/asset/1674)标注 Godot 3.5、CC0，附动画尾焰。当前工程应取作者原始模型并重新封装自己的 Godot 4 场景，旧版工程不能视为已验证兼容。
- [KayKit 的社区版本](https://godotengine.org/asset-library/asset/2124)由 KayKit Game Assets 发布，标注 Godot 4.1、CC0；[官方仓库](https://github.com/KayKit-Game-Assets/KayKit-Space-Base-Bits-1.0)。在本工程的实际导入与渲染仍待验证。

## 免费范围与后续处理

- KayKit 免费版为 48+ 模型；额外 12 个模型和替代纹理属于 Extra，Blend 源文件属于 Source。
- [Quaternius Modular Sci-Fi MegaKit](https://store.godotengine.org/asset/quaternius/modular-sci-fi-megakit/)也是候选，但免费 Standard 只提供部分模型，且不含 Godot 工程。不能把完整宣传图中的全部内容计为免费资源。
- 配套资源可选择 [Kenney Particle Pack](https://kenney.nl/assets/particle-pack)（80 文件）、[UI Pack Sci-Fi](https://kenney.nl/assets/ui-pack-sci-fi)（130 文件）、[Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds)（70 文件），均标注 CC0。
- 已导入部分保留实际下载包的许可证与作者来源记录；只收入选中的资产及必要纹理，不把参考工程或 MCP 脚本放入仓库。
- 飞船应保存为独立场景，统一尺寸、朝向、挂点及尾焰接口；场景建筑应形成可复用预制件。移动端性能与 Compatibility 渲染效果需在实际导入后验证。

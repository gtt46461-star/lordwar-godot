# 依赖及内容来源（2026-09-27）

| 内容 | 本仓库用途 | 来源版本 | 许可 / 分发 |
|---|---|---|---|
| `LordWarMod.cs`、R30 `Core/`、`Data/` | 用户现有玩法源码；当前安装包只包含原生入口 | 用户上传 R30，SHA-256 `1146cad1ff06ed2130cf674030962710f3a29281c9f35cacc99564dcba5b0dec` | 用户提供；上传材料未附许可证，不向第三方重新授权 |
| NeoModLoader Android 接口 | 手机模组入口和运行时源码编译 | `WorldBoxOpenMods/AndroidModLoader` commit `165fe841f832b7e64dd54cef815ab2b9d5ffed7b`；发布版 2.0 DLL SHA-256 `273aa9ce93e9de9dec56339871fe7d55c10010ccb12a386db0f676a6eeaf3132` | MIT；只在构建时获取，不打进本模组 ZIP |
| NeoModLoader 主仓库 | 0.50.6 兼容声明与示例接口参考 | `WorldBoxOpenMods/ModLoader` 1.1.3；当前主分支 `46f32fe322ec03f2630ef8179d8b9b78a60191d1` | MIT；没有复制源码进入模组 |
| 官方示例模组 | 清单和入口参考 | `WorldBoxOpenMods/ModExample` commit `000340d07cdb5b6954fb6a5583f2900c70a47985` | MIT；没有复制源码进入模组 |
| WorldBox 游戏 APK 与运行时素材 | 独立输入；由游戏本身提供地图、UI、建筑、人物和动画 | 用户提供的 0.50.6 外层 APK SHA-256 `77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5` | 游戏版权和第三方宿主权利未获本仓库再分发许可；不在模组包或源码包内 |
| `Supower.rar`、0.51 模组合集等样本 | 静态研究 API 和目录结构 | 用户上传 | 样本内未见许可证；不复制进模组或源码包 |

CI 仅下载公开参考 DLL 用于编译检查。真正安卓运行依赖用户有权使用的游戏安装、可加载它的 LemonLoader 和手机端 NeoModLoader；这些依赖不会随模组 ZIP 自动安装。参考程序集并非从目标 APK 提取，编译成功不等于目标 APK 已经加载。

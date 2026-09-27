# 领主战争 × WorldBox Android 模组源码接入件

## 目标版本裁决（2026-09-27）

已核验文件库中的两个完整 Android 包。**仅选择 WorldBox 0.50.6 作为模组目标**；用户截图标记为 0.50.6+688，NeoModLoader 1.1.3 发布说明列出游戏 0.50.6 兼容。Android 组合在这个第三方宿主上的实际加载仍未通过。

|版本|APK SHA-256|实测结构|决定|
|---|---|---|---|
|0.22.21|`e4b37e1ffdc27fba37e1fcd9390787a248dcdc5764d123da5a47df0681ab6bd5`|152,904,263 字节；arm64-v8a/armeabi-v7a；IL2CPP|保留资源对照，画面与目标不符|
|0.50.6|`77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5`|322,027,494 字节；arm64-v8a；IL2CPP；内嵌 `assets/hook.apk` 和第三方宿主|当前唯一施工基线|

旧的内层候选包在用户视频中黑屏退回桌面。修正后的外层宿主候选包仅通过 ZIP、对齐和签名静态检查，**设备安装、WorldBox 启动、LemonLoader/NML 加载、模组界面及原生玩法均未验收**。当前入口源码已删除创建独立 `GameWorld`、另绘地图和独立推进时间的执行链；现有原生命令读取选中城市和城内真实人物、校验城市及归属，然后分别调用 `Kingdom.setCapital(city)`、`City.setLeader(actor, true)`、`Army.setCaptain(actor, false)`，结果留在 WorldBox 原版对象里。源码编译和手机操作仍须分别验收；不能把当前 APK 称为完成版。

## 本包是什么

`LordWarMod/` 保留 44 个 R30 核心文件和 23 个数据文件，作为后续逐项迁移的源码依据；当前 `LordWarMod.cs` 不实例化或推进其中的 `GameWorld`。`Build/package_mod.py` 生成的手机安装包只放入当前真实入口 `LordWarMod.cs` 和 `mod.json`，避免把尚无原生消费者的模拟代码和 CSV 加载进游戏。完整源码总包另行包含 Core、Data、构建脚本和工作流。

运行入口通过 NeoModLoader 创建原版底部“领主战争”标签；可使用模组地图选城工具点击原版城市，或先用 WorldBox 原有界面选城再按模组按钮打开原生城市窗口。地图事件委托经 NML 的 IL2CPP 转换器绑定。窗口显示实际城市、国家、金币、人口、原版城主、原版军队长及最多 16 名真实城内人物。王都、城主和军队长命令在执行前核对原版对象仍存在、城市未易主、人物仍属本城；军队长还须是同一支原版军队的士兵。执行后直接读取原版对象确认结果，重复任命不再执行。旧 IMGUI 浮动入口、R30 并行地图和自动城市归属回写已经从运行入口移除。此条原生闭环需要在实际 Android 目标上核对按钮、读档和异常状态；建设、征募、行军、战争及其余清单仍未移植。

## 已完成的检查

- 使用 .NET 8 的 C# 编译器编译纯游戏核心，并实际运行生成世界、推进一天、存档校验和恢复：`CORE_SMOKE_PASS skills=360 units=156 map=160x120 kingdoms=4 cities=4 people=220 day=1`。
- 当前含原生城主与军队长任命的入口在 GitHub Actions [run 36326079559](https://github.com/gtt46461-star/lordwar-godot/actions/runs/36326079559) 编译通过：`dotnet build WorldBoxMod/Build/ModCompileCheck.csproj -c Release`，`0 Warning(s), 0 Error(s)`。参考件来自 AndroidModLoader 的公开程序集和 `NeoModLoader_mobile.dll` 2.0；并非从用户目标 APK 提取的同一版本运行时程序集，不能据此推定目标 APK 已兼容。`Build/ApiProbe/` 可提取公开参考程序集的类型签名供接口核对。
- 旧 run `36293519434` 的候选 APK 属于已弃用的并行地图实现。现在 `Build/build_candidate_apk.py` 可从用户提供的原始外层包、既有 LemonLoader/NML 内层种子和本仓库当前入口重建签名候选；当前 0.2.2 静态候选的外层 SHA-256 是 `6addd309e94ae6a1e25ead96e1592453cee04ffc58bcd382a5e0e27819e4057b`，模组 ZIP SHA-256 是 `e0666797b50ec7d038403cb7c55980b7ab212fcb610d3a20e9d53c56e6672cf3`，内外层 versionCode 为 `689`。两层 APK 的签名、对齐、内容校验通过。尚无安卓安装和启动验证，因此不是已完成的游戏 APK。
- `Smoke/` 和 `Build/` 是独立校验用文件，不要放入手机的 `LordWarMod/` 目录。`_deps/` 是本地下载的公开依赖，不包含在交付 ZIP 中。GitHub Actions 工作流位于仓库分支的 `.github/workflows/`。

## 为什么候选 APK 不是成品

用户 2026-09-27 的启动视频显示旧内层候选 APK 黑屏后数秒退回桌面，游戏主界面未出现。复查发现旧候选包是把原始 322 MB `base.apk` 内嵌的 `assets/hook.apk` 作为独立 APK 重打包，丢失了外层宿主的额外 DEX、`libmod.so`、`libEncryptorC.so` 等启动结构。`Build/repack_outer_hook.py` 现在只替换原始外层包的 `assets/hook.apk`，保留其他所有非签名条目，包括运行时的 `META-INF/services`，并重新签名两层 APK。逐个外层条目的 SHA-256 已与原始包核对，内嵌 APK 与新签内层逐字节一致。包层级错误已修正，但没有设备启动、加载器或模组运行的成功证据；外壳可能校验内嵌包，仍须用设备日志定位下一步。

上传的 `base.apk` 为 WorldBox 0.50.6 的 IL2CPP 构建，外加第三方运行层。已重新使用原始外层宿主封入修改后的内层包，产出 `LordWar-WorldBox-0.50.6-host-candidate.apk`，但 NeoModLoader/LemonLoader 是否能在这个母体上启动仍无实机证据。安卓包 `global-metadata.dat` 开头不含标准 IL2CPP 元数据魔数，Cpp2IL 2022.0.7 直接解析失败。用户提供的 Windows `worldbox.exe` 包含可反编译的 Mono `Assembly-CSharp.dll`（2441 个 C# 文件）及 firstpass（86 个文件），可用于接口研究，但不能直接替换安卓 IL2CPP 逻辑，也不是官方 Unity 原工程。单位、建筑、士兵、国策、技能、特性的 WorldBox 原生实体映射和规则替换均未完成。候选 APK 不可宣称为完整游戏。

## 运行链和待验收项

1. 先用对应 WorldBox 0.50.6 的 Android LemonLoader/NeoModLoader 环境验证加载器启动和日志。社区 Android 仓库已归档，不能仅凭桌面版的 0.50.6 适配公告推定手机兼容。
2. 在有权使用的 WorldBox 安装中按 AndroidModLoader 的说明安装 LemonLoader，并将其 2.0 发布的 `NeoModLoader_mobile.dll` 放入 `MelonLoader/com.mkarpenko.worldbox/Mods`；解压本批 `LordWarMod-0.2.2.zip` 到同一游戏根目录下的 `NMLMods/`，形成 `NMLMods/LordWarMod/mod.json` 和 `NMLMods/LordWarMod/LordWarMod.cs`。加载器的 `ModCompileLoadService` 会搜索模组目录中的 C# 源码并编译。实际游戏根目录由 LemonLoader 的 `MelonEnvironment.GameRootDirectory` 决定，不应猜测绝对路径。
3. 确认模组出现在模组列表，日志出现 `LordWar native city entry registered`，在原版地图选城或按“领主战争”按钮读取当前城市，再依次执行“设为王都”、“任命城主”、“任命军队长”，观察原版对象改变、重复点击不重复执行和重启读档恢复。
4. 若失败，保留设备日志和编译报错，对照实际 IL2CPP 包装程序集修正。这份源码**没有运行过手机编译与实机验收**。

## 来源

### GitHub 公开库复核（2026-09-27）

- [WorldBoxOpenMods/ModLoader](https://github.com/WorldBoxOpenMods/ModLoader) 和 [AndroidModLoader](https://github.com/WorldBoxOpenMods/AndroidModLoader) 公开的是 NeoModLoader 的入口、运行时编译、资源加载和 UI 等接口实现；后者已归档。它们没有包含可重建完整 WorldBox 游戏的 Unity 工程。
- [WorldBoxOpenMods/ModExample](https://github.com/WorldBoxOpenMods/ModExample) 实际调用 `AssetManager.traits.add` 注册特性、创建按钮和标签；[Tuxxego/ModernBox](https://github.com/Tuxxego/ModernBox) 的 `M5` 分支示范了 `AssetManager.actor_library` 与 `AssetManager.buildings` 注册单位和建筑。这些是可参考的模组代码，但 ModernBox 的公开说明也区分旧分支与现代版本，并不证明在本 Android 宿主上运行。
- [PowerBox](https://github.com/WorldBoxOpenMods/PowerBox) README 明确要求另备 `Assembly-CSharp.dll` 等依赖，仓库本身不是完整游戏源码。
- [ModLoader issue #74](https://github.com/WorldBoxOpenMods/ModLoader/issues/74) 报告 Android IL2CPP 启动层初始化后未进入模组；[PR #75](https://github.com/WorldBoxOpenMods/ModLoader/pull/75) 提议的 Android Mono 路径已关闭且未合并。这些讨论说明“有接口”与“特定手机 APK 可加载”是两件需要分别验证的事。

- 游戏核心与数据：用户上传的 `领主战争_R30_陆路可达战争移植_完整Unity工程_未构建APK.zip`，SHA-256 `1146cad1ff06ed2130cf674030962710f3a29281c9f35cacc99564dcba5b0dec`。
- 模组加载接口：[WorldBoxOpenMods/AndroidModLoader](https://github.com/WorldBoxOpenMods/AndroidModLoader) 的 `BasicMod<T>`、`ModCompileLoadService` 与 `Paths.NMLMods` 相关实现。
- 目标 APK：用户上传 `base.apk`，SHA-256 `77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5`。

`CHECKS.json` 记录原始哈希、修改文件与检查结果。没有 APK 签名或真机通过标记。

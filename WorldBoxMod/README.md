# 领主战争 × WorldBox Android 模组源码接入件

## 当前施工状态（2026-09-28）

当前工作分支为 `construction/worldbox-android-mod`，目标母体为用户的 WorldBox Android 0.50.6（外层 APK SHA-256 `77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5`）。源码版本从 0.2.5 提升到 0.3.0，实际运行入口 `LordWarMod.OnModLoad()` 会创建12个主入口，读取原版炸弹分类图标，并尝试把原版 `destruction` 分类的按钮、回调和注册键重绑到该12项面板。反射契约缺失时会隐藏临时分类并保留原版炸弹工具；被任命居民必须是原版 `human` asset 且仍是该原版城市成年居民，领主起始城市必须属于人类国家。任命后仍直接检查 WorldBox `City`、`Kingdom`、`Army` 字段。

这是正在施工的源码，不是已验证的 APK。此前版本的 12 主入口与炸弹分类重绑定代码已在 GitHub Actions run `36372968185` 编译和打包通过。征募代码的第一次 CI 发现 `City` 没有 `addResources`；已改用目标 API 探针确认存在的 `addResourcesToRandomStockpile(String,Int32)` 做失败返还和金币回读，修正后的编译待重跑。U001 乡兵路径是：原版人类居民申请、审批、扣 27 原版城市金币，再调用公开的 `City.makeWarrior(Actor)` 并回读原版 Actor/Army。签名清单确认 `tryToMakeWarrior(Actor)` 是 private，代码不直接调用。旧方案的 3 天训练被折叠为批准时即时执行。这项代码尚未实机验证，兵种映射表其他 3,919 项也未接入。当前候选 693 仍装载旧版 0.2.5。

最新设备日志可见 MelonLoader 0.6.5 的 .NET 8 启动阶段，然后记录结束；没有异常栈、游戏信息或模组进入标记，且没有精确的已安装 APK build ID，所以 .NET 8 之后的第一个失败调用仍未定位。旧日志的 `il2cpp_init` 缺失不能代替当前诊断。当前环境没有 `adb`，本地也没有 .NET CLI。不得用未验证的同一加载器重发 APK。见 [`Build/ANDROID_CANDIDATE.md`](Build/ANDROID_CANDIDATE.md)。

现有用户签名 JKS 可读取，但 `LORDWAR_KEYSTORE_PASS` 不在当前执行环境；候选测试证书与原包官方签名不同，覆盖安装兼容性没有证据。版本号 694 暂留给修复并通过启动关卡后的 APK。最新设备安装、原版存档、点击炸弹栏、任命、重启读档、申请批准、真实征募、战斗和战果状态一律为 `NOT_RUN`。

适配盘点由 [`build_adaptation_map.py`](Build/build_adaptation_map.py) 逐项生成，列出旧 CSV/JSON 的 3,920 条输入和面向原版实体/方法的适配路线。本次将 U001 记为 `SOURCE_ADAPTED_RUNTIME_UNVERIFIED`，并在映射行写明 27 金币、原版资格、公开 `City.makeWarrior(Actor)` 调用，以及 3 天训练被折叠的行为差异；目标 API 清单中的私有 `tryToMakeWarrior(Actor)` 不会被模组直接调用。其余 3,919 项仍为 `NOT_STARTED`。映射表由 CI 作为 `WorldBox-LordWar-adaptation-map` 产物发布，不参与运行包。城市建设、完整官职与家族体系、军队编制、其他兵种与装备、补给、军令、战斗、围城外交、AI同规则、扩展状态存档尚未完成。

## 目标版本裁决（2026-09-27）

已核验文件库中的两个完整 Android 包。**仅选择 WorldBox 0.50.6 作为模组目标**；用户截图标记为 0.50.6+688，NeoModLoader 1.1.3 发布说明列出游戏 0.50.6 兼容。Android 组合在这个第三方宿主上的实际加载仍未通过。

|版本|APK SHA-256|实测结构|决定|
|---|---|---|---|
|0.22.21|`e4b37e1ffdc27fba37e1fcd9390787a248dcdc5764d123da5a47df0681ab6bd5`|152,904,263 字节；arm64-v8a/armeabi-v7a；IL2CPP|保留资源对照，画面与目标不符|
|0.50.6|`77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5`|322,027,494 字节；arm64-v8a；IL2CPP；内嵌 `assets/hook.apk` 和第三方宿主|当前唯一施工基线|

历史上，旧内层候选包在用户视频中黑屏退回桌面；之后的外层候选录像没有“领主战争”入口，具体版本不能确认。0.2.4 的启动日志曾报 `Failed to get function pointer: il2cpp_init`，并确认当时的目标库缺少该导出。该故障属于 0.2.4 尝试。**当前最新设备日志与这次旧日志不同，不能把旧结论当成当前首个失败点。**

## 本包是什么

`LordWarMod/` 保留 44 个 R30 核心文件和 23 个数据文件，作为后续逐项迁移的源码依据；当前 `LordWarMod.cs` 不实例化或推进其中的 `GameWorld`。`Build/package_mod.py` 生成的手机安装包只放入当前真实入口 `LordWarMod.cs` 和 `mod.json`，避免把尚无原生消费者的模拟代码和 CSV 加载进游戏。完整源码总包另行包含 Core、Data、构建脚本和工作流。

运行入口通过 NeoModLoader 创建原版底部“领主战争”标签；可使用模组地图选城工具点击原版城市，或先用 WorldBox 原有界面选城再按模组按钮打开原生城市窗口。地图事件委托经 NML 的 IL2CPP 转换器绑定。窗口显示实际城市、国家、金币、人口、原版城主、原版军队长及最多 16 名真实城内人物。王都、城主和军队长命令在执行前核对原版对象仍存在、城市未易主、人物仍属本城；军队长还须是同一支原版军队的士兵。执行后直接读取原版对象确认结果，重复任命不再执行。旧 IMGUI 浮动入口、R30 并行地图和自动城市归属回写已经从运行入口移除。此条原生闭环需要在实际 Android 目标上核对按钮、读档和异常状态；建设、征募、行军、战争及其余清单仍未移植。

## 已完成的检查

- 使用 .NET 8 的 C# 编译器编译纯游戏核心，并实际运行生成世界、推进一天、存档校验和恢复：`CORE_SMOKE_PASS skills=360 units=156 map=160x120 kingdoms=4 cities=4 people=220 day=1`。
- 当前含入口图标回退的源码在 GitHub Actions [run 36327816423](https://github.com/gtt46461-star/lordwar-godot/actions/runs/36327816423) 编译通过：`dotnet build WorldBoxMod/Build/ModCompileCheck.csproj -c Release`；参考件来自 AndroidModLoader 的公开程序集和 `NeoModLoader_mobile.dll` 2.0，并非用户目标 APK 运行时。`Build/ApiProbe/` 可提取公开参考程序集类型签名。编译通过不代表入口已在手机出现。
- 旧 run `36293519434` 的候选 APK 属于已弃用的并行地图实现。0.2.4 的外层 SHA-256 是 `453ed637d6c65d2f7eddc91e6461891f06274aa5cc6da1e4c6a9e661e5d75290`，模组 ZIP SHA-256 是 `5d843ecee98b4813c0cd7a8bd73edb1286ccb7048a27fd61c0add498b249d7d9`，内外层 versionCode 为 `691`。两层 APK 签名、对齐和内容核对通过，但设备日志证明加载失败。`Build/build_candidate_apk.py` 在 0.2.5 增加导出别名，并调用 `Build/inspect_il2cpp.py` 检查新包的动态导出；设备验收仍未进行。
- `Smoke/` 和 `Build/` 是独立校验用文件，不要放入手机的 `LordWarMod/` 目录。`_deps/` 是本地下载的公开依赖，不包含在交付 ZIP 中。GitHub Actions 工作流位于仓库分支的 `.github/workflows/`。

## 历史版本 0.2.5 符号别名诊断候选

用户手机的 `Latest-Bootstrap.log` 显示 LemonLoader 在查找 `il2cpp_init` 时失败。用户提供的 0.22.21 未改名游戏库与当前 0.50.6 游戏库中 239 个 IL2CPP API 的排列和函数长度完全一致，当前版在有界数组 API 前多两个小函数。`Build/alias_il2cpp_exports.py` 在固定输入哈希下推导符号别名，同时保留游戏原有随机名字；静态校验确认 `.text` 字节不变、两个名字指向同一地址。构建器将同一别名库写入宿主和内层包，模组管理入口在实际运行后尝试向手机存储写 `startup-diagnostic.txt`。新增的管理诊断代码在 GitHub Actions run 36333047066 的公开参考程序集编译通过。这项推导不等于 Android 加载器、游戏和模组已经启动；以前的黑屏只有新设备日志能验证是否解决。完整原版 C# 源码无法从 APK 自动写出。

## 为什么候选 APK 不是成品

当前最新日志可见 MelonLoader 0.6.5、Android 14、`Runtime Type: net8`，之后记录终止。日志没有异常栈或 `MANAGED_MOD_ENTERED`，也无法确定当次安装包 build ID；因此真实失败调用仍待同一次启动的完整 logcat 和版本标识定位。安装一份 APK 或把 DLL 复制到手机都不能作为运行证据。版本 693 没有设备验收记录，本分支不会把相同加载器另封成 versionCode 694。

上传的 `base.apk` 为 WorldBox 0.50.6 的 IL2CPP 构建，外加第三方运行层。已重新使用原始外层宿主封入修改后的内层包，产出 `LordWar-WorldBox-0.50.6-host-candidate.apk`，但 NeoModLoader/LemonLoader 是否能在这个母体上启动仍无实机证据。安卓包 `global-metadata.dat` 开头不含标准 IL2CPP 元数据魔数，Cpp2IL 2022.0.7 直接解析失败。用户提供的 Windows `worldbox.exe` 包含可反编译的 Mono `Assembly-CSharp.dll`（2441 个 C# 文件）及 firstpass（86 个文件），可用于接口研究，但不能直接替换安卓 IL2CPP 逻辑，也不是官方 Unity 原工程。单位、建筑、士兵、国策、技能、特性的 WorldBox 原生实体映射和规则替换均未完成。候选 APK 不可宣称为完整游戏。

## 运行链和待验收项

1. 先用对应 WorldBox 0.50.6 的 Android LemonLoader/NeoModLoader 环境验证加载器启动和日志。社区 Android 仓库已归档，不能仅凭桌面版的 0.50.6 适配公告推定手机兼容。
2. 历史 0.2.5 的手动模组部署方法是：将当时生成的 `NeoModLoader_mobile.dll` 放入 `MelonLoader/com.mkarpenko.worldbox/Mods`，解压当时的 `LordWarMod-0.2.5.zip` 到 `NMLMods/`。当前 0.3.0 代码尚待 CI 编译，候选 APK 中也没有该版本；请勿把旧 ZIP 当作本次施工结果。
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

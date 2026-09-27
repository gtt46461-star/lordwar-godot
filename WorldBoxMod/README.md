# 领主战争 × WorldBox Android 模组源码接入件

## 目标版本裁决（2026-09-27）

已核验文件库中的两个完整 Android 包。**仅选择 WorldBox 0.50.6 作为模组目标**；用户截图标记为 0.50.6+688，NeoModLoader 1.1.3 发布说明列出游戏 0.50.6 兼容。Android 组合在这个第三方宿主上的实际加载仍未通过。

|版本|APK SHA-256|实测结构|决定|
|---|---|---|---|
|0.22.21|`e4b37e1ffdc27fba37e1fcd9390787a248dcdc5764d123da5a47df0681ab6bd5`|152,904,263 字节；arm64-v8a/armeabi-v7a；IL2CPP|保留资源对照，画面与目标不符|
|0.50.6|`77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5`|322,027,494 字节；arm64-v8a；IL2CPP；内嵌 `assets/hook.apk` 和第三方宿主|当前唯一施工基线|

旧的内层候选包在用户视频中黑屏退回桌面。修正后的外层宿主候选包仅通过 ZIP、对齐和签名静态检查，**设备安装、WorldBox 启动、LemonLoader/NML 加载、模组界面及原生玩法均未验收**。当前入口源码已删除创建独立 `GameWorld`、另绘地图和独立推进时间的执行链；第一条原生命令是读取选中城市和城内真实人物、校验城市及归属，然后调用 `Kingdom.setCapital(city)`，结果直接留在 WorldBox 对象里。源码编译和手机操作仍须分别验收；不能把当前 APK 称为完成版。

## 本包是什么

`LordWarMod/` 是针对 NeoModLoader Android 的**源码模组目录**。现有 44 个 R30 纯 C# 核心文件和 23 个数据文件保留为后续逐项迁移的来源；当前 `LordWarMod.cs` 不实例化或推进其中的 `GameWorld`。编译历史中修复了 3 处 R30 原始源码错误，并将 7 个文件中的 `WorldTile` 引用明确限定为领主战争类型。

运行入口通过 NeoModLoader 创建原版底部“领主战争”标签，提供选城地图工具和原生城市窗口。窗口显示实际城市、国家、金币、人口及最多三名真实城内人物；“设为王都”先检查城市存在、国家未改变、人口大于零和重复点击，再修改原版 `Kingdom` 的王都。旧 IMGUI 浮动入口、R30 并行地图和自动城市归属回写已经从运行入口移除。此条原生闭环需要在实际 Android 目标上核对按钮、读档和异常状态；建设、征募、军队、战争及其余清单仍未移植。

## 已完成的检查

- 使用 .NET 8 的 C# 编译器编译纯游戏核心，并实际运行生成世界、推进一天、存档校验和恢复：`CORE_SMOKE_PASS skills=360 units=156 map=160x120 kingdoms=4 cities=4 people=220 day=1`。
- 旧入口曾使用公开的 `NeoModLoader_mobile.dll` 2.0 与 AndroidModLoader 仓库提供的 WorldBox/Unity 程序集完成编译；程序集目标版本不同，编译器发出 `CS1701` 警告。新原生入口必须重新编译，旧结果不代表本次通过。
- 2026-09-27，GitHub Actions run `36293519434`：纯核心编译、含导入地图、原生城市分组和四国的 smoke test、Android NML 参考程序集编译均通过。上一候选 APK 的 69 个模组文件已与当时源码逐字节比对；ZIP、16 KiB 对齐和 v1/v2/v3 签名静态检查通过。新增原生城市创建、现存城市关联和城市归属同步代码尚需重封 APK 和设备验收。游戏运行时的按钮显示、触摸响应和模组实际加载仍待设备验证。
- `Smoke/` 和 `Build/` 是独立校验用文件，不要放入手机的 `LordWarMod/` 目录。`_deps/` 是本地下载的公开依赖，不包含在交付 ZIP 中。GitHub Actions 工作流位于仓库分支的 `.github/workflows/`。

## 为什么候选 APK 不是成品

用户 2026-09-27 的启动视频显示旧候选 APK 黑屏后数秒退回桌面，游戏主界面未出现。复查发现旧候选包是把原始 322 MB `base.apk` 内嵌的 `assets/hook.apk` 作为独立 APK 重打包，丢失了外层宿主的额外 DEX、`libmod.so`、`libEncryptorC.so` 等启动结构。`Build/repack_outer_hook.py` 现在只替换原始外层包的 `assets/hook.apk`，保留其他 12073 个非签名条目并重签外层包。修正了已确认的包层级错误，但还没有设备启动、加载器或模组运行的成功证据；外壳可能校验内嵌包，必须用设备日志定位下一步。

上传的 `base.apk` 为 WorldBox 0.50.6 的 IL2CPP 构建，外加第三方运行层。已重新使用原始外层宿主封入修改后的内层包，产出 `LordWar-WorldBox-0.50.6-host-candidate.apk`，但 NeoModLoader/LemonLoader 是否能在这个母体上启动仍无实机证据。安卓包 `global-metadata.dat` 开头不含标准 IL2CPP 元数据魔数，Cpp2IL 2022.0.7 直接解析失败。用户提供的 Windows `worldbox.exe` 包含可反编译的 Mono `Assembly-CSharp.dll`（2441 个 C# 文件）及 firstpass（86 个文件），可用于接口研究，但不能直接替换安卓 IL2CPP 逻辑，也不是官方 Unity 原工程。单位、建筑、士兵、国策、技能、特性的 WorldBox 原生实体映射和规则替换均未完成。候选 APK 不可宣称为完整游戏。

## 运行链和待验收项

1. 先用对应 WorldBox 0.50.6 的 Android LemonLoader/NeoModLoader 环境验证加载器启动和日志。社区 Android 仓库已归档，不能仅凭桌面版的 0.50.6 适配公告推定手机兼容。
2. 将完整 `LordWarMod/` 目录放在 NeoModLoader Android 的 `NMLMods/` 中，保持 `mod.json`、`LordWarMod.cs`、`Core/` 和 `Data/` 的相对路径。加载器的 `ModCompileLoadService` 会搜索模组目录中的 C# 源码并编译。
3. 确认模组出现在模组列表，日志出现 `LordWar native city entry registered`，在原版地图选城或按“领主战争”按钮读取当前城市，再执行“设为王都”，观察国家王都改变、重复点击不重复执行和重启读档恢复。
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

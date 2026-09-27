# 领主战争 × WorldBox Android 模组源码接入件

## 目标版本裁决（2026-09-27）

已核验文件库中的两个完整 Android 包。**仅选择 WorldBox 0.50.6 作为模组目标**；用户截图标记为 0.50.6+688，NeoModLoader 1.1.3 发布说明列出游戏 0.50.6 兼容。Android 组合在这个第三方宿主上的实际加载仍未通过。

|版本|APK SHA-256|实测结构|决定|
|---|---|---|---|
|0.22.21|`e4b37e1ffdc27fba37e1fcd9390787a248dcdc5764d123da5a47df0681ab6bd5`|152,904,263 字节；arm64-v8a/armeabi-v7a；IL2CPP|保留资源对照，画面与目标不符|
|0.50.6|`77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5`|322,027,494 字节；arm64-v8a；IL2CPP；内嵌 `assets/hook.apk` 和第三方宿主|当前唯一施工基线|

旧的内层候选包在用户视频中黑屏退回桌面。随后手机录像显示外层候选可进入原版 WorldBox，底部标签和菜单均无“领主战争”；录像不能证明具体安装的是 0.2.2 还是 0.2.3，但入口验收明确失败。0.2.4 补齐了外层加载器启动文件；用户 2026-09-27 再次录屏显示白屏后退出，并提供 `Latest-Bootstrap.log`：`Failed to get function pointer: il2cpp_init`。已核实原始外层、内层和 0.2.4 候选包中的 arm64 `libil2cpp.so` SHA-256 均为 `88a9d6e7af066e77de1a60426e8e77f331ec3b4f26c3e2044201f45dbb4a08d3`，其动态导出表无 `il2cpp_init` 等加载器所需的六个 API。**0.2.4 手机启动验收为 FAIL，模组代码尚未执行。**当前入口没有实例化并行 `GameWorld`；已写入的王都、城主和军队长命令仍未在手机上证明执行成功。

## 本包是什么

`LordWarMod/` 保留 44 个 R30 核心文件和 23 个数据文件，作为后续逐项迁移的源码依据；当前 `LordWarMod.cs` 不实例化或推进其中的 `GameWorld`。`Build/package_mod.py` 生成的手机安装包只放入当前真实入口 `LordWarMod.cs` 和 `mod.json`，避免把尚无原生消费者的模拟代码和 CSV 加载进游戏。完整源码总包另行包含 Core、Data、构建脚本和工作流。

运行入口通过 NeoModLoader 创建原版底部“领主战争”标签；可使用模组地图选城工具点击原版城市，或先用 WorldBox 原有界面选城再按模组按钮打开原生城市窗口。地图事件委托经 NML 的 IL2CPP 转换器绑定。窗口显示实际城市、国家、金币、人口、原版城主、原版军队长及最多 16 名真实城内人物。王都、城主和军队长命令在执行前核对原版对象仍存在、城市未易主、人物仍属本城；军队长还须是同一支原版军队的士兵。执行后直接读取原版对象确认结果，重复任命不再执行。旧 IMGUI 浮动入口、R30 并行地图和自动城市归属回写已经从运行入口移除。此条原生闭环需要在实际 Android 目标上核对按钮、读档和异常状态；建设、征募、行军、战争及其余清单仍未移植。

## 已完成的检查

- 使用 .NET 8 的 C# 编译器编译纯游戏核心，并实际运行生成世界、推进一天、存档校验和恢复：`CORE_SMOKE_PASS skills=360 units=156 map=160x120 kingdoms=4 cities=4 people=220 day=1`。
- 当前含入口图标回退的源码在 GitHub Actions [run 36327816423](https://github.com/gtt46461-star/lordwar-godot/actions/runs/36327816423) 编译通过：`dotnet build WorldBoxMod/Build/ModCompileCheck.csproj -c Release`；参考件来自 AndroidModLoader 的公开程序集和 `NeoModLoader_mobile.dll` 2.0，并非用户目标 APK 运行时。`Build/ApiProbe/` 可提取公开参考程序集类型签名。编译通过不代表入口已在手机出现。
- 旧 run `36293519434` 的候选 APK 属于已弃用的并行地图实现。0.2.4 的外层 SHA-256 是 `453ed637d6c65d2f7eddc91e6461891f06274aa5cc6da1e4c6a9e661e5d75290`，模组 ZIP SHA-256 是 `5d843ecee98b4813c0cd7a8bd73edb1286ccb7048a27fd61c0add498b249d7d9`，内外层 versionCode 为 `691`。两层 APK 签名、对齐和内容核对通过，但设备日志证明加载失败。`Build/build_candidate_apk.py` 现在调用 `Build/inspect_il2cpp.py` 检查这条加载链所需的动态导出，缺失时在打包前拒绝生成下一份必失败的候选。
- `Smoke/` 和 `Build/` 是独立校验用文件，不要放入手机的 `LordWarMod/` 目录。`_deps/` 是本地下载的公开依赖，不包含在交付 ZIP 中。GitHub Actions 工作流位于仓库分支的 `.github/workflows/`。

## 为什么候选 APK 不是成品

用户 2026-09-27 的启动视频显示旧内层候选 APK 黑屏后数秒退回桌面。0.2.4 保留第三方外层宿主并映射了加载器启动库，但最新 16.23 秒用户录像与设备日志证明仍在 IL2CPP 初始化之前失败。这个游戏二进制没有导出加载器需要查找的 `il2cpp_init`。重签、改 `mod.json`、复制更多模组文件不能解决这个首个失败点；必须取得与所选游戏版本一致且实际可加载的 IL2CPP/加载器组合，或者完成有运行证据的专门符号适配。

上传的 `base.apk` 为 WorldBox 0.50.6 的 IL2CPP 构建，外加第三方运行层。已重新使用原始外层宿主封入修改后的内层包，产出 `LordWar-WorldBox-0.50.6-host-candidate.apk`，但 NeoModLoader/LemonLoader 是否能在这个母体上启动仍无实机证据。安卓包 `global-metadata.dat` 开头不含标准 IL2CPP 元数据魔数，Cpp2IL 2022.0.7 直接解析失败。用户提供的 Windows `worldbox.exe` 包含可反编译的 Mono `Assembly-CSharp.dll`（2441 个 C# 文件）及 firstpass（86 个文件），可用于接口研究，但不能直接替换安卓 IL2CPP 逻辑，也不是官方 Unity 原工程。单位、建筑、士兵、国策、技能、特性的 WorldBox 原生实体映射和规则替换均未完成。候选 APK 不可宣称为完整游戏。

## 运行链和待验收项

1. 先用对应 WorldBox 0.50.6 的 Android LemonLoader/NeoModLoader 环境验证加载器启动和日志。社区 Android 仓库已归档，不能仅凭桌面版的 0.50.6 适配公告推定手机兼容。
2. 在有权使用的 WorldBox 安装中按 AndroidModLoader 的说明安装 LemonLoader，并将其 2.0 发布的 `NeoModLoader_mobile.dll` 放入 `MelonLoader/com.mkarpenko.worldbox/Mods`；解压本批 `LordWarMod-0.2.4.zip` 到同一游戏根目录下的 `NMLMods/`，形成 `NMLMods/LordWarMod/mod.json` 和 `NMLMods/LordWarMod/LordWarMod.cs`。候选 APK 的内外两层都包含加载器资产和 `assets/copyToData/MelonLoader/...` 部署镜像；实际根目录及复制结果须以设备日志核实。
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

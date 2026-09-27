# 领主战争 × WorldBox Android 模组源码接入件

## 本包是什么

`LordWarMod/` 是针对 NeoModLoader Android 的**源码模组目录**。它移入用户提供的 R30 Unity 工程中 44 个纯 C# 游戏核心文件和 23 个原始数据文件，并新增 `LordWarMod.cs` 作为移动端入口。`Core/` 保留 R30 游戏逻辑；为通过独立编译，修复了 3 处原始源码错误，并将 7 个文件中的 `WorldTile` 引用明确限定为领主战争类型，避免与 WorldBox 的同名类型冲突。

接入的可见操作是在 NeoModLoader 的 WorldBox 底部功能栏创建“领主战争”标签、面板按钮和提交箱按钮（同时保留浮动入口）。它能异步创建 160×120/4 国世界，或从当前 WorldBox 地图采样地形，建立《领主战争》的城市、道路与四国模拟；支持查看地图与国家、暂停、推进一天、宣战出征。提交箱接入 R30 `GameWorld.ApproveProposal` / `RejectProposal`，使用 WorldBox 原有的 `ScrollWindow` 空窗口预制件承载列表，并保留 IMGUI 回退入口。原生标签接法参考用户提供的 `Supower.rar` 的公开接口使用方式，入口代码独立编写。世界的政策、兵种、人物、经济和战争模拟走 R30 `GameWorld` 及其 Owner 链。当前面板尚未暴露所有原版操作；WorldBox 本体单位、建筑、国家、战斗和存档与《领主战争》世界的双向同步尚未实现。

## 已完成的检查

- 使用 .NET 8 的 C# 编译器编译纯游戏核心，并实际运行生成世界、推进一天、存档校验和恢复：`CORE_SMOKE_PASS skills=360 units=156 map=160x120 kingdoms=4 cities=4 people=220 day=1`。
- 使用公开的 `NeoModLoader_mobile.dll` 2.0 与 AndroidModLoader 仓库提供的 WorldBox/Unity 程序集，对全部模组 C# 源码完成编译。程序集目标版本不同，编译器发出 `CS1701` 版本匹配警告；设备端加载尚待验证。
- 2026-09-27，GitHub Actions run `36291333733`：纯核心编译、含导入地图和四国的 smoke test、Android NML 参考程序集编译均通过。APK 构建候选包内 69 个模组文件已与源码逐字节比对；ZIP、16 KiB 对齐和 v1/v2/v3 签名静态检查通过。游戏运行时的按钮显示、触摸响应和模组实际加载仍待设备验证。
- `Smoke/` 和 `Build/` 是独立校验用文件，不要放入手机的 `LordWarMod/` 目录。`_deps/` 是本地下载的公开依赖，不包含在交付 ZIP 中。GitHub Actions 工作流位于仓库分支的 `.github/workflows/`。

## 为什么候选 APK 不是成品

上传的 `base.apk` 为 WorldBox 0.50.6 的 IL2CPP 构建，外加第三方运行层。已重打包并签名 `LordWar-WorldBox-0.50.6-native-inbox-candidate.apk`，但 NeoModLoader/LemonLoader 是否能在这个母体上启动仍无实机证据。安卓包 `global-metadata.dat` 开头不含标准 IL2CPP 元数据魔数，Cpp2IL 2022.0.7 直接解析失败。用户提供的 Windows `worldbox.exe` 包含可反编译的 Mono `Assembly-CSharp.dll`（2441 个 C# 文件）及 firstpass（86 个文件），可用于接口研究，但不能直接替换安卓 IL2CPP 逻辑，也不是官方 Unity 原工程。单位、建筑、士兵、国策、技能、特性的 WorldBox 原生实体映射和规则替换均未完成。候选 APK 不可宣称为完整游戏。

## 运行链和待验收项

1. 先用对应 WorldBox 0.50.6 的 Android LemonLoader/NeoModLoader 环境验证加载器启动和日志。社区 Android 仓库已归档，不能仅凭桌面版的 0.50.6 适配公告推定手机兼容。
2. 将完整 `LordWarMod/` 目录放在 NeoModLoader Android 的 `NMLMods/` 中，保持 `mod.json`、`LordWarMod.cs`、`Core/` 和 `Data/` 的相对路径。加载器的 `ModCompileLoadService` 会搜索模组目录中的 C# 源码并编译。
3. 确认模组出现在模组列表，日志出现 `LordWar R30 core source loaded into NML`，打开面板后按“创建新世界”，核对数据加载、地图、日期变化与战争事件。
4. 若失败，保留设备日志和编译报错，对照实际 IL2CPP 包装程序集修正。这份源码**没有运行过手机编译与实机验收**。

## 来源

- 游戏核心与数据：用户上传的 `领主战争_R30_陆路可达战争移植_完整Unity工程_未构建APK.zip`，SHA-256 `1146cad1ff06ed2130cf674030962710f3a29281c9f35cacc99564dcba5b0dec`。
- 模组加载接口：[WorldBoxOpenMods/AndroidModLoader](https://github.com/WorldBoxOpenMods/AndroidModLoader) 的 `BasicMod<T>`、`ModCompileLoadService` 与 `Paths.NMLMods` 相关实现。
- 目标 APK：用户上传 `base.apk`，SHA-256 `77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5`。

`CHECKS.json` 记录原始哈希、修改文件与检查结果。没有 APK 签名或真机通过标记。

# 领主战争：WorldBox 模组路线

## 版本裁决（2026-09-27）

**目标版本：截图中的 Android WorldBox 0.50.6+688。**参考画面与此版本直接对应，NeoModLoader 1.1.3 的发布说明明确列出游戏 0.50.6 兼容。手机上的 LemonLoader + NML 移动版仍需在这个构建上实际启动测试；PC 加载器的版本声明不能代替手机测试。

|现有版本/资料|用途|作为本批可玩模组基线|
|---|---|---|
|0.9.0 反编译 C#/IL 与旧“现代科技 MOD”差分|研究旧系统和概念|否。API 年代不符，恢复文件也不是原始完整工程|
|0.22.21 Android APK 与素材提取|资源对照；APK SHA-256 `e4b37e1ffdc27fba37e1fcd9390787a248dcdc5764d123da5a47df0681ab6bd5`，IL2CPP，arm64-v8a/armeabi-v7a|否。与目标画面不符|
|0.50.6 Android APK，截图标记 +688|目标 APK SHA-256 `77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5`，arm64-v8a、IL2CPP、外层宿主含 `assets/hook.apk`|**选它**。NML 1.1.3 列 0.50.6 兼容；手机加载仍待验证|
|N01 Godot 90122|已构建的独立原型与玩法数据|否。它不是 WorldBox 模组，不能直接导入原版世界|

文件库已找到并静态核验两份安卓原版包。0.50.6 包存在第三方外层宿主及内嵌游戏 APK，不是标准无包装 Unity 安装集。此前错误地将内层 `hook.apk` 单独重打包的候选包，在用户视频中黑屏退回桌面；修正外层宿主的候选包未做设备运行验收。静态清点不能证明 Android 加载器或模组可玩。

已有模组源码位于本仓库 `construction/worldbox-android-mod` 分支的 `WorldBoxMod/`，后续应沿用它。该分支目前仍启动独立 `GameWorld` 并另绘地图，城市仅有部分回写；这是待迁移的原型，不能宣称玩法已在 WorldBox 原生世界完整运行。下一步把规则逐项接到原版人物、城市、资源、军队与存档，只有一个权威世界状态。

## 施工方式

模组只接入玩法：原版 WorldBox 掌管地图、渲染、动画、人物、城市、时间、库存和保存；领主战争增加城市规划、官员资格、审批、军政与外交规则以及对应按钮。每个命令从当前原版选中的城市/人物取状态，校验后更新原版实体；模组单独保存新增字段，并与原版存档 ID 对应。原 Godot `GameWorld` 不进入模组运行时。

第一条闭环是“选原版城市 → 点模组的建造申请 → 校验原版人物与材料 → 原版城市变化 → 保存/读档一致”。这个闭环在目标 Android 版本运行通过后，才逐项移植其他玩法。

先用只读工具检查目标包：

```bash
python3 mod/tools/inspect_target.py /path/to/worldbox-0.50.6.apk --output target_report.json
```

该工具支持 `.apk`、`.apks` 和 `.xapk`，输出 SHA256、可读到的版本、ABI、Mono/IL2CPP 和关键文件。没有 `aapt` 时版本项会明确写 `NOT_RUN`，`worldBoxPackageVerified` 为 `null` 表示尚未核验包名；拆分包后端取整个安装集的综合结果。它只作静态清点，不能证明加载器兼容或模组可玩。

下一门槛：备份存档 → 在 Android arm64 实测保留原始外层宿主的候选包 → 安卓加载器启动游戏 → 模组在原版内显示一个按钮并写日志 → 首个原生城市规则闭环。每一步保存录屏、日志、版本与哈希；目前启动、加载和玩法均未验收。

模组交付是自己的代码/配置/新增资源包。个人测试用的安卓补丁应从用户自己的游戏安装集生成；不将原版游戏本体或提取的大量原版资源打进公开的模组 ZIP。手机模组尚无游戏官方支持，游戏更新也可能让第三方加载器失效。

参考：

- WorldBox 官方移动端说明：https://www.superworldbox.com/faq
- NML 0.50.6 兼容发布记录：https://github.com/WorldBoxOpenMods/ModLoader/releases
- NML 安卓安装入口：https://github.com/WorldBoxOpenMods/ModLoader
- Unity 脚本后端：https://docs.unity3d.com/6000.4/Documentation/Manual/scripting-backends-intro.html

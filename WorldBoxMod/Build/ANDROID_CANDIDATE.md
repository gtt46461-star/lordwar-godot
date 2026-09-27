# WorldBox 0.50.6 安卓候选包的重建输入和状态

`build_candidate_apk.py` 只接受 SHA-256 为
`77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5`
的用户提供原始外层 APK。另需已有的内层 LemonLoader/NML 种子 APK（此次输入 SHA-256
`b869f6dcba05879b67bf9b493d5f557c1608ddb7721cfacdc71ad984fef0ee6a`），
以及用户单独保管的 LordWar 候选签名 JKS。种子来自用户先前的
`LordWar-WorldBox-0.50.6-host-candidate.apk` 的 `assets/hook.apk`；
它是**未经设备加载验收的旧候选**，只复用加载器文件和游戏数据，旧模组目录被完整替换。
原始 WorldBox、第三方宿主、加载器二进制和 JKS 不入 GitHub，也不入普通源码包。

重建时在有 `python3`、`aapt`、`zipalign`、`apksigner` 的机器上，先从此前候选外层
提取 `assets/hook.apk` 为 `loader-seed-inner.apk`，确认它的 SHA-256，再将密钥口令
以本地环境变量 `LORDWAR_KEYSTORE_PASS` 传给构建脚本：

```bash
python3 WorldBoxMod/Build/build_candidate_apk.py \
  --original-outer /private/original-base.apk \
  --loader-seed-inner /private/loader-seed-inner.apk \
  --keystore /private/lordwar-worldbox-candidate-signing.jks \
  --output-dir /private/build-output
```

脚本校验原始 APK 身份、Unity 资源和 IL2CPP 游戏锚点、加载器种子、模组实际字节，
重签内外两层并验证签名和对齐，还比较所有保留的外层条目内容。独立 ZIP
`LordWarMod-0.2.3.zip` 只含 `LordWarMod/mod.json` 与
`LordWarMod/LordWarMod.cs`。未运行的旧 Core/Data 仅作源码迁移依据。

本次内外两层 APK 都是 `com.mkarpenko.worldbox`，versionName `0.50.6`、versionCode `690`；
构建脚本直接定位二进制清单中的 `android:versionCode`，从原始 `688` 改为 `690`，
其余清单字节保留，并通过 `aapt dump badging` 逐层核对。
当前签名证书 SHA-256 为
`a4452032f871b9297418549807fb2040b70718448d53b3040aa6665ecca6eb13`，
不同于原外层证书 SHA-256
`37803c47397861e81ba0447b486a2bdf3e61202c01f178509f88c59b9b98237b`。
不能保证覆盖用户已有原版安装；**不要卸载、清除数据或覆盖现有存档**。

此次通过的是构建、静态 ZIP、签名、对齐和内容一致性。`adb devices` 为空，
用户反馈上一候选 `0.2.2` 能打开原版，但没看到“领主战争”玩法；因此上一候选的
模组入口验收为 `FAIL（用户手机反馈）`，不能再写成单纯 `NOT_RUN`。这不是本次新包的
启动证据。本次修正了入口图标路径 `ui/Icons` 与公开示例 `ui/icons` 的大小写不一致，
并加入可用图标回退；同时把实际 NML DLL 与模组源码镜像至 LemonLoader 所说明的
`assets/copyToData/MelonLoader/...`，供首次启动复制到游戏数据目录。该复制功能的
运行时路径和宿主行为仍须在用户手机日志里核对，不因 APK 内有对应条目就写 `PASS`。
新包安装、启动、加载器日志、原版选城按钮、王都、城主和军队长命令以及存档重启是 `NOT_RUN`。
此前内层 APK 黑屏视频无法替代本次外层 APK 的实际运行证据。即便它之后能启动，
目前入口只有读取原生城市和人物、王都、原版城主及军队长任命命令；
建设、征募、行军、战争、外交和其余玩法尚未迁入，
所以它不是完整《领主战争》游戏。

在有可访问的 arm64 Android 设备后，先备份用户世界存档与购买状态，再收集
`adb install` 的结果、启动完整 `adb logcat`、原版世界截图、模组按钮操作和重启读档。
若首个失败点在外层宿主、Bootstrap、MelonLoader、NML 或 C# 编译，应以当次
日志逐层修复，不把静态通过推断为启动成功。

# Android 加载器旧候选生成路径

这里保留此前实际用于制作 0.50.6 内层加载器种子的两段 C# 入口代码。
它们来自用户先前的 `LordWar-WorldBox-0.50.6-apk-build-source.zip`，
不是 WorldBox 原版源码，也不证明该加载器已经在用户手机上启动。

`Program.cs` 调用 [LemonLoader/MelonLoaderInstaller](https://github.com/LemonLoader/MelonLoaderInstaller)
commit `ac443fce9f2ef890caddf8c64bba9194a43361bd` 的 Core `Patcher`，
固定 Unity `2022.3.60f1`、包名 `com.mkarpenko.worldbox`。输入依次是用户原始内层
`hook.apk`、LemonLoader 0.6.5.1 的 `melon_data.zip`、匹配 Unity 版本的依赖 ZIP、
输出目录、临时目录。`LoaderPathPatch.cs` 用 Mono.Cecil 对已构建的 MelonLoader
及 NML 移动版程序集调整模组目录；旧流程先改程序集，随后重装 ZIP 并调用 `Patcher`。

从官方源码重编译这两个入口：

```bash
git clone https://github.com/LemonLoader/MelonLoaderInstaller.git WorldBoxMod/_deps/MelonLoaderInstaller
git -C WorldBoxMod/_deps/MelonLoaderInstaller checkout ac443fce9f2ef890caddf8c64bba9194a43361bd
dotnet build WorldBoxMod/Build/LoaderRecipe/InstallerCli.csproj -c Release
dotnet build WorldBoxMod/Build/LoaderRecipe/LoaderPathPatch.csproj -c Release
```

后续实际的 ZIP、Android 清单、签名与对齐操作仍由 upstream Core 及
`../build_candidate_apk.py` 执行。当前完整候选直接以**用户已经拥有**的旧内层
SHA-256 `b869f6dcba05879b67bf9b493d5f557c1608ddb7721cfacdc71ad984fef0ee6a`
作为加载器种子，避免把私人游戏包或签名密钥放入 GitHub。重新生成种子时需要
核对 loader 发布件、Unity 依赖与此前 `REPRODUCE.md` 的 SHA 和结构；此配方的
两个入口可单独编译，并不等于对黑屏的修复或设备启动验收。

官方 installer Core 按 GPL-3.0 发布；MelonLoader 运行时按 Apache-2.0 发布；
Mono.Cecil 0.11.5 按 MIT 发布。对应来源见 `../../DEPENDENCIES_AND_RIGHTS.md`。

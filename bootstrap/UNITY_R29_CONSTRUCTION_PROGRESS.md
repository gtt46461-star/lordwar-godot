# 领主战争 R29 Unity 施工状态（2026-09-27）

本分支是独立 Unity 路线。完整源码与数据（不含提取美术）见 `bootstrap/unity_r29_construction_source_data.tar.gz`；展开后有 Assets/Scripts、Assets/Tests、LordWarData、Packages、ProjectSettings 和 Tools。SHA256：`05608b026223d2b84683d2a9483e55a67b4ba4deac678f4fd5f816956f0d0b2f`。右侧 `bootstrap/UnityProject/` 下的九个文件是本次实际修改的可浏览副本，已同步收入归档。

本次接通新建世界的陆地比例、森林、山地、沙漠、河流、资源六个参数，从中文 HUD 经过 LordWarBootstrap、GameWorld、WorldGenerator 到 WorldMap。海平面按地形高度分位计算；地图记录参数和海平面。版本 27 存档迁移到 28 时保留原地形，标记旧生成器并记录旧海平面。无桥无渡口河流与不可通行山地被普通行军寻路拒绝。添加了确定性、比例、阻断和旧档迁移测试源码。

`Tools/verify_project.py` 的静态扫描无 FAIL；它不是 Unity 编译、NUnit 执行或安卓实机验收。当前容器没有 Unity Editor、C# 编译器、Android SDK/adb。测试源码尚未执行，本次没有新 APK。旧的 Godot 预览 APK 与这条 Unity 路线无关，不得标成成品。

仍缺可提交的场景/预制体/meta/动画控制器/图集绑定；现有 WorldRenderer 的色块底图及 SpriteAssetLibrary 的模糊素材查找距离视频画面很远。提取美术的再分发权未核实，未上传到公开仓库。视频视觉对齐、空间战斗、统一命令事务、长稳、数据逐行行为验证和三机安卓性能均未通过。不要用这份静态记录推断功能已完成。

# N01 code-only sync (not an APK release)

This public branch carries the N01 C# changes, scene source, script metadata, tests, build preflight, and package/version declarations on top of `f911eef2a2237daf0d5d832eb17096caec442f66`.

The user's supplied PNG assets, their hashes, and the detailed evidence package are withheld from this public commit. The complete N01 source candidate and work checkpoint were saved separately to the user's private file collection. The public branch alone cannot produce the N01 visual slice because `Assets/Resources/LordWarArt/N01/` is absent.

Actual build attempt in the current environment exited 42 before Unity compilation: Unity Editor, Android SDK/adb, and the original signing key are unavailable. N01-T01–T06 and G02–G08 are not passed. Do not treat this commit, the existing Godot APK, or a static scan as an N01 delivery. Resume N01 from the complete private candidate, import in Unity 2022.3.62f1, verify artwork/scene/font and run the required APK/device tests before advancing to N02.

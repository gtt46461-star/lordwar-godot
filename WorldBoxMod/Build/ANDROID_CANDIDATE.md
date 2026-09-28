# Android host construction status

Last verified: 2026-09-28. This file distinguishes the current runtime investigation from older loader failures.

## Current input and versions

| Input | Version / hash | What it proves |
|---|---|---|
| User supplied outer WorldBox APK | 0.50.6, versionCode 688; SHA-256 `77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5` | Static baseline only |
| Earlier checked loader candidate | 0.50.6, versionCode 692; outer SHA-256 `7393781d0534737156081b87270105ecab5743abe0b0c3c37dc851273b1b8fbe` | Rebuild seed source; not an installable upgrade to the official app |
| Reconstructed unsigned loader seed | SHA-256 `0d7649e334bfb61952a37d81a20347a2c6e3bb49897d13bb4b844de94e0e1d14` | Reproducible loader/game files only; device compatibility remains unproved |
| Existing version 693 candidate | Outer SHA-256 `97f75b226f46f1a07eebcbc360af81eebcb965103874b54533d8e28e4f830563`; inner SHA-256 `99382589cfa38585e77da064557512f9c0c29affcbca8bbc951b4fdf74ecaafe` | Static archive and payload checks only; still contains source mod 0.2.5 |
| Source now under construction | LordWarMod 0.3.0 | Changes bomb-tab routing and human-only city/person selection; CI/device results pending |

## Current first unverified runtime transition

The latest supplied `Latest.log` identifies MelonLoader 0.6.5, Android 14, an Il2Cpp x64 loader configuration, and `Runtime Type: net8`. It ends at that point. It contains no exception stack, WorldBox game-information line, `MANAGED_MOD_ENTERED` marker, or mod diagnostic file. The paired `Latest-Bootstrap.log` shows JNI initialization and the embedded APK asset copy. The exact failure after the visible .NET 8 startup stage is therefore **undetermined**. There is no evidence that the current version 693 candidate was installed for that log, so the log cannot be attributed to it.

An older 0.2.4 log reported that `il2cpp_init` could not be found. That remains a historical failure from a different startup attempt; it is not the first failure established by the latest log. Do not rebuild the same loader/runtime pair until a launch trace identifies the next failing call.

## Build and install gates

- A connected Android device and `adb` are unavailable in this workspace. Install, start, WorldBox entry, toolbar click, appointment, save/reload, recruit and battle are `NOT_RUN`.
- The local candidate keystore exists, but `LORDWAR_KEYSTORE_PASS` is unavailable. Do not guess it. The candidate signer fingerprint `a4452032f871b9297418549807fb2040b70718448d53b3040aa6665ecca6eb13` differs from the official input APK signer `37803c47397861e81ba0447b486a2bdf3e61202c01f178509f88c59b9b98237b`; a compatible cover install has not been established.
- VersionCode 694 is reserved for a later APK build. **No new APK is produced from version 693's unverified loader**. A mod-only compile or ZIP must not be represented as an Android game build.
- The existing package recipe can be run after resolving the loader and signing gates:

```bash
python3 WorldBoxMod/Build/build_candidate_apk.py \
  --original-outer /private/base.apk \
  --loader-seed-inner /private/loader-seed-inner.apk \
  --reference-apk /private/worldbox-0.22.21.apk \
  --keystore /private/lordwar-worldbox-candidate-signing.jks \
  --output-dir /private/build-output
```

Set `LORDWAR_KEYSTORE_PASS` through the local secret environment. Before running the recipe, obtain one trace tied to the exact installed APK: version/build ID, full `Latest-Bootstrap.log`, full `Latest.log`, and Android logcat from process start through exit. The first unresolved transition is net8 startup into the game/IL2CPP initialization path. Correct that specific failure and prove the original WorldBox reaches a saved world before packaging another candidate.

## Payload scope

`package_mod.py` emits only `mod.json` and `LordWarMod.cs`. It deliberately omits the legacy parallel simulation and CSV/JSON inputs because the live adapter does not consume them. CI generates `WorldBox_Adapter_Map.csv`, which inventories 3,920 data rows/leaves and marks them `NOT_STARTED`; the `WorldBox-LordWar-adaptation-map` workflow artifact is an audit ledger, not working game data. Never describe any row as adapted until the Android runtime code consumes it and its resulting WorldBox behavior is verified.

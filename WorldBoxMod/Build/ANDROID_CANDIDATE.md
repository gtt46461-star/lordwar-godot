# Android host construction status

Last verified: 2026-09-28. This file distinguishes the current runtime investigation from older loader failures.

## Current input and versions

| Input | Version / hash | What it proves |
|---|---|---|
| User supplied outer WorldBox APK | 0.50.6, versionCode 688; SHA-256 `77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5` | Static baseline only |
| Earlier checked loader candidate | 0.50.6, versionCode 692; outer SHA-256 `7393781d0534737156081b87270105ecab5743abe0b0c3c37dc851273b1b8fbe` | Rebuild seed source; not an installable upgrade to the official app |
| Reconstructed unsigned loader seed | SHA-256 `0d7649e334bfb61952a37d81a20347a2c6e3bb49897d13bb4b844de94e0e1d14` | Reproducible loader/game files only; device compatibility remains unproved |
| Existing version 693 candidate | Outer SHA-256 `97f75b226f46f1a07eebcbc360af81eebcb965103874b54533d8e28e4f830563`; inner SHA-256 `99382589cfa38585e77da064557512f9c0c29affcbca8bbc951b4fdf74ecaafe` | Static archive and payload checks only; still contains source mod 0.2.5 |
| Source now under construction | LordWarMod 0.3.0 | Bomb-category routing, U001 recruitment, and native-save extension hooks compile/package in CI run `36379955527`; Android runtime remains `NOT_RUN` |

The first 0.3.0 Android compile failed because the pinned NML wrapper exposes `_power_buttons` as `Il2CppSystem.Collections.Generic.List<PowerButton>`, not a .NET `List<PowerButton>`; the source copies entries explicitly. API probing confirmed public `City.checkCanMakeWarrior(Actor)`, `City.makeWarrior(Actor)`, and `City.addResourcesToRandomStockpile(String,Int32)`, while `City.tryToMakeWarrior(Actor)` is private. U001 recruitment now spends native city gold, calls the native warrior conversion, and checks back the resulting profession. In run `36379955527`, the game-core smoke test, Android NML compile, reference API probe, 3,920-row inventory check, loader-tool compile, and source-mod ZIP packaging all passed. The current source ZIP is an NML mod package only; it is not an APK, and device gameplay remains `NOT_RUN`.

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


## Static native API and loader lifecycle findings (CI run 36379955527)

The probe compiled against the public AndroidModLoader reference assemblies pinned in `.github/workflows/worldbox-android-mod.yml` (AndroidModLoader commit `165fe841f832b7e64dd54cef815ab2b9d5ffed7b`). This is reference metadata, not proof that every signature has been matched to the user-supplied APK's IL2CPP implementation.

- Identity chain exposed by the reference: `Actor.getID()` is inherited from `BaseSimObject`; `ActorData -> BaseObjectData -> BaseSystemData`, where `BaseSystemData.id` has public get/set and `save()` signatures. `CityData -> MetaObjectData -> BaseSystemData`; `City -> MetaObject<CityData> -> CoreSystemObject<T> -> NanoObject`, with `CoreSystemObject<T>.getID()`. This supports storing extension references by native IDs; a save/reload round trip has not been run.
- Save API signatures exposed by the pinned reference include `SaveManager.saveMapData(string,bool)`, `saveWorldToDirectory(string,bool,bool)`, `saveToCurrentPath()`, `getSavePath(string)`, and instance `loadData(SavedMap,string)`. Source `LordWarMod.cs` now installs Harmony postfixes from `OnModLoad`: successful native saves write checksum-validated sidecars under `Application.persistentDataPath/LordWar/saves`; a completed native load restores the extension state by normalized native save path. Stored fields are selected native city/kingdom/actor IDs, the pending recruitment IDs, section, and messages. Missing or corrupt extension state clears only LordWar references and explicitly leaves the native world untouched. The source compiles against the pinned reference; save routing, Harmony runtime patching, Android file writes, and an actual save/reload round trip are **not device-verified**.
- The loader reference declares `0Harmony`, `Il2CppInterop.Runtime`, and `MonoMod.Utils`. `BasicMod<T>` exposes `OnLoad`, `Init`, `PostInit`, and protected `OnModLoad`; save-hook installation is called directly from `OnModLoad`, and each postfix catches its own error so a mod-side sidecar failure does not alter the original save/load call. Runtime callback binding and Harmony patching are not device-verified.

Next device experiment: launch the exact candidate with full bootstrap log, logcat, and `startup-diagnostic.txt`; confirm `NATIVE_SAVE_HOOKS_PATCHED`, save an approved pending request, force-stop, reload that same native slot, and verify the same actor/city IDs resolve. CI compilation does not clear the startup blocker or prove those reference signatures match the supplied 0.50.6 target APK.

## Source modification evidence

- `WorldBoxMod/LordWarMod/LordWarMod.cs`: SHA-256 before this save-hook change `f9c6bb9bbe729f24e3d94b3bb558196dc9c08eb9770f9414fad5454af48804b4`; after `1081dc3a9a36c9c5e6a256174afe6afd8c207b5475cecb596dbe22e8d4141898`. GitHub content blob before `041910e5c2dfefd546d3ee67dc43c7bae5dffa8f`; after `5c5b8b8d3d90fd3f9eb976e75d9dc0f49ef78467`.
- Call path: `LordWarMod.OnModLoad()` → `InstallNativeSaveHooks()` → Harmony postfixes on native `SaveManager` methods. `AfterNativeSave` invokes `WriteLordWarSaveState`; `AfterNativeLoad` invokes `RestoreLordWarSaveState` after the native load completes.
- CI commit `cc5ba0f5262278420b97ada6672548a82e96f348`, workflow run `36379955527`, passed every listed build/package step. This is a reference-assembly compile result, not an APK or device-runtime result.

## Payload scope

`package_mod.py` emits only `mod.json` and `LordWarMod.cs`. It deliberately omits the legacy parallel simulation and CSV/JSON inputs because the live adapter does not consume them. CI generates `WorldBox_Adapter_Map.csv`, which inventories 3,920 data rows/leaves. The 0.3.0 source marks only U001 `SOURCE_ADAPTED_RUNTIME_UNVERIFIED`; all other rows remain `NOT_STARTED`. The `WorldBox-LordWar-adaptation-map` workflow artifact is an audit ledger, not working game data. No source row is verified as functioning in WorldBox until device behavior is tested.

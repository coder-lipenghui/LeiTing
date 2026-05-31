# LeiTing Codex Handoff

This Unity project is a 2D vertical shooter targeting Douyin WebGL mini-game
builds. The current implementation is defined by C# under `Assets/Scripts`,
Unity scene/prefab data, and Luban source tables under `Luban/Datas`.

## Documentation Sync Rule

- Treat runtime code, active Unity assets, and Luban source workbooks as the
  source of truth when a document disagrees with the implementation.
- Every change to core runtime logic must update this file and any affected
  topic document in the same work item. Core logic includes game flow, player
  controls, combat, config/data loading, pickups, UI/navigation, audio,
  platform integration, and build input paths.
- Replace obsolete behavior descriptions instead of leaving legacy assumptions
  as if they were still active.

## Quick State

- Unity version: `2021.3.6f1c1`
- Lobby/menu scene: `Assets/Scenes/SampleScene.unity`
- Battle scene: `Assets/Scenes/BattleScene.unity`
- Both scenes are enabled in `ProjectSettings/EditorBuildSettings.asset`.
- Runtime configuration is generated Luban data in `Assets/Resources/Luban`.
  `ConfigManager.LoadDefaultConfig()` loads Luban only; on failure it leaves
  config unavailable and logs an error. There is no legacy JSON runtime
  fallback.
- WebGL builds use `STARK_UNITY_INPUT_OVERRIDE` in the current project
  settings. `UIManager` ensures an `EventSystem` exists and attaches
  `TTSDK.TTInputOverrideBypass` for WebGL.
- Design notes live in `雷霆战机.md`. Douyin touch debugging and fallback
  switches are recorded in `docs/webgl-douyin-player-input-troubleshooting.md`.

## Validation Commands

Run lightweight checks from the repository root:

```bash
dotnet build Assembly-CSharp.csproj --no-restore -v:minimal
dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal
dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -p:DefineConstants=UNITY_WEBGL%3BUNITY_2021_3_OR_NEWER%3BSTARK_UNITY_INPUT_OVERRIDE%3BENABLE_LEGACY_INPUT_MANAGER%3BCSHARP_7_OR_LATER%3BCSHARP_7_3_OR_NEWER
```

For config changes, regenerate tables before compiling:

```bash
bash Luban/gen.sh
```

Input behavior and rendering changes still require a rebuilt Douyin package
and a phone test.

## Config And Runtime Assets

- Edit gameplay table sources only in `Luban/Datas/*.xlsx`.
- Do not hand-edit generated files under
  `Assets/Scripts/Config/LubanGenerated/**` or
  `Assets/Resources/Luban/**`; regenerate them with `bash Luban/gen.sh`.
- `LubanConfigLoader` constructs `GameConfig` from generated Resources data.
  `ConfigManager` exposes players, enemies, waves, bullets, missiles, pickups,
  levels, stage events, boss phases, and related patterns from that object.
- `RuntimeAssetCatalog` is the runtime lookup path for configured prefabs,
  sprites, and audio assets outside editor-only `AssetDatabase` loading.
  Rebuild its asset data when referenced runtime assets change.

## Scene And Game Flow

- `GameSceneManager` persists across scenes. It enters `BattleScene` after
  saving the requested level, or falls back to starting a battle in the active
  scene if the battle scene cannot be loaded.
- The lobby flow is implemented through `LobbyPage`, `HangarPage`,
  `SettingPage`, and `StagePage`. Stage selection reads Luban level data and
  launches the selected unlocked level.
- `PlaneManager` supplies the current lobby plane list and stores selection,
  ownership, and ad-progress values with `PlayerPrefs`.
- In battle, `GameBootstrap` loads config, sets the design camera, ensures a
  pickup manager and player exist, applies player config, prepares the
  scrolling background, verifies the configured level BGM, then starts
  `GameManager`. Starting or changing a level initiates its BGM during the
  initiating button click and carries playback into the battle scene, after
  stopping any menu BGM; directly launching `BattleScene` starts the same BGM
  from `GameBootstrap`. Entering the lobby starts the looping menu BGM at
  `Assets/Art/Sound/BGM/BGM_Menu_Main_Loop_01.wav`; all lobby pages, including
  hall, stage selection, hangar, and settings, share it.
- `GameManager` owns state, score, selected level, unlock progression, restart
  and next-level transitions. On victory it first attracts remaining pickups
  to the player and waits for collection before completing settlement.

## Player Input And Douyin WebGL

`PlayerController` currently implements multiple selectable input paths:

- Default `Touch Input Strategy`: `DouyinEventsPreferred`. A real WebGL build
  first subscribes to `TT.OnTouchStart`, `TT.OnTouchMove`, `TT.OnTouchEnd`,
  and `TT.OnTouchCancel`.
- If the event path does not provide an active touch, it tries SDK overridden
  polling through `global::Input` when `STARK_UNITY_INPUT_OVERRIDE` is
  compiled, then falls back to explicit `UnityEngine.Input` touch/mouse reads.
- `SdkPollingOnly` and `UnityLegacyOnly` remain Inspector alternatives for
  isolating platform input failures.
- Default `Pointer Tracking Mode`: `PreserveInitialOffset`. On pointer down,
  the controller records the pointer world position and the aircraft position;
  while dragging, the aircraft target is its start position plus the pointer
  delta from that start. This prevents an aircraft jump when a drag begins
  away from it. `FollowFinger` remains available only as a comparison mode.
- On the tested Douyin WebGL handset, the `TTTouch` callback Y value already
  moves in Unity screen-space direction. The default path therefore uses
  `touch.screenY` directly. `Invert Douyin Touch Y` is off by default and
  exists only for a host/SDK variant proven to report the opposite direction.
- The target is clamped to the camera viewport while respecting the player
  hitbox. Movement is blocked only when the starting touch raycasts to
  interactive UI such as a `Selectable`, `ScrollRect`, or `BasePopup`.
- Temporarily enable `Log Pointer Input Diagnostics` to see the selected
  source, strategy, tracking mode, Y inversion flag, pointer coordinates, and
  resulting target coordinates on a test build.

## Combat And Stage Systems

- `EnemyManager` spawns enemies from level wave data.
- `BulletManager`, `BulletPatternManager`, and `MissileManager` handle
  projectile behavior and configured firing patterns.
- `BossController` handles boss entry, HP, configured phase changes, firing,
  defeat, and the victory trigger.
- `StageManager` advances level timeline events. A clear-bullets event clears
  both enemy bullets and enemy missiles, and stage messages can substitute
  `{LEVEL}`, `{MAX_LEVEL}`, `{BOSS_ID}`, and `{BOSS}`.
- `GameManager` exposes the `无敌模式` Inspector toggle on `Managers`. The
  value selected in `SampleScene` is carried into `BattleScene`, where
  `PlayerController` ignores incoming damage without continuously flashing.
- Hitboxes are child-driven through `ActorHitbox`; it forwards damage to the
  owning enemy or boss while preventing duplicate same-frame hits from
  overlapping child hitboxes.

## Pickups, UI, And Audio

- `PickupManager` spawns configured enemy drops and can force active pickups
  toward the player during settlement.
- `PickupItemController` supports star, coin, magnet, bomb, heal, and shield
  behavior. Magnet attracts stars, bomb removes non-boss enemies, and star
  and coin pickups play configured sound paths currently defined in code.
- `UIManager` provides battle HUD/settlement behavior and runtime-created UI
  where an authored view is unavailable. The page classes provide lobby,
  hangar, settings, and stage-selection views.
- `LobbyPage` opens the `Cebianlan` panel from `BtnCebianlan`/`BtnCebian`.
  The panel binds `BtnGod`/`BtnGo` to Douyin `TT.NavigateToScene` with
  `scene=sidebar`, and a sidebar revisit switches the panel action from
  `BtnGo` to `BtnClame` based on Douyin launch options.
- `GameSettingManager` stores music, sound, and vibration preferences in
  `PlayerPrefs`. Player vibration reads that setting; menu and level BGM
  respect `MusicEnabled`, while sound effects and `AircraftEngineAudio`
  respect `SoundEnabled`.
- `AudioManager` plays the menu/level BGM and one-shot SFX through
  catalog/Resources loading on separate 2D audio sources. Only the BGM source
  persists across scene changes, so gameplay transitions do not stop music
  already started from a WebGL user gesture. It retries an assigned BGM that
  is not yet playing for delayed audio activation and resume cases.
  `AircraftEngineAudio` is an optional per-aircraft looping sound component;
  the small helicopter uses
  `Assets/Art/Sound/SFX/Enemy/SFX_Enemy_Engine_Loop_Small_01.wav`.
  Boss entry plays
  `Assets/Art/Sound/SFX/Enemy/SFX_Boss_Attack_Warning_01.wav` once as the
  approach warning is shown.

## Prefabs And Asset Editing

- Player prefabs live under `Assets/Prefabs/Player/`.
- Enemy and boss prefabs live under `Assets/Prefabs/Enemies/`.
- Prefer prefab transform edits for visual mounts and hitbox placement rather
  than embedding asset-specific coordinates in runtime logic.
- Runtime boot deliberately has fallbacks for some missing scene objects and
  prefab references, but data loading itself requires valid generated Luban
  resources.

## Known Verification Points

- The local TT SDK documentation's Y-origin wording did not match the observed
  callback movement direction on the target phone; use real-device behavior
  as the basis for the default and retest after SDK/host changes.
- UI and WebGL input changes need phone validation after packaging, not only
  editor mouse checks.
- Runtime-created pages and mechanically prepared combat prefabs should be
  inspected in Play Mode after visual, hitbox, or navigation edits.

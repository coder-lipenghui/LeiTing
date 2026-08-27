# LeiTing Codex Handoff

This Unity project is a 2D vertical shooter targeting Douyin WebGL mini-game
builds. The current implementation is defined by C# under `Assets/Scripts`,
Unity scene/prefab data, and Luban source tables under `Luban/Datas`.

## Documentation Sync Rule

- Treat runtime code, active Unity assets, and Luban source workbooks as the
  source of truth when a document disagrees with the implementation.
- User-visible text must be expressed in Chinese. This includes in-game prompts,
  UI labels, buttons, HUD text, config display names, loading/error messages,
  and other player-facing copy. Keep internal identifiers, enum values, resource
  paths, generated code keys, protocol fields, and platform/API parameters in
  their required technical form.
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
- Editor quick testing is available from `LeiTing/Test/Level Selector`. It
  accepts commands such as `2#1:35` or `2#95`, shows wave/event/Boss markers,
  and can reload a running battle from a selected timeline time. See
  `docs/editor-quick-testing.md`.
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
  Remote bundle loading falls back to typed sub-assets after the main asset
  lookup, so sprite PNGs imported as Sprite sub-assets can still resolve in
  WebGL. Rebuild catalog asset data when referenced runtime assets change.
- When startup remote resources are required, `UIManager` shows
  `RuntimeResourceDownloadView` on top of the scene. A successful download now
  leaves the loading view visible at 100% and shows the `进入游戏` button. That
  button and the retry button stay hidden during all download/progress states;
  the lobby is opened only after `进入游戏` is clicked.

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
  hall, stage selection, hangar, and settings, share it. If remote resources
  finished loading in `SampleScene`, the `进入游戏` click initializes the lobby UI
  directly and starts menu BGM from that click; outside the lobby scene it
  falls back to `GameSceneManager.EnterLobby()`.
- `GameManager` owns state, score, selected level, unlock progression, restart
  and next-level transitions. It also owns the shared battle timeline consumed
  by `StageManager`, `EnemyManager`, and the battle timer HUD. Editor test runs
  can initialize that timeline at a requested start time; entries strictly
  before that time are treated as skipped, while entries exactly at that time
  remain eligible to trigger. On victory it first attracts remaining pickups
  to the player and waits for collection before completing settlement.
- `StaminaService` gates battle entry in player builds, consuming one stamina
  per start and restoring stamina over time. In the Unity Editor it always
  reports full stamina and does not consume it, so Play Mode testing is not
  blocked by stamina limits.

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

- `EnemyManager` spawns enemies from level wave data. The seven opening
  resource-plane waves in level 2 spawn their configured counts one aircraft
  at a time. Three fixed `WaveSpawn` rows configure `weaponup` plus a one-based
  carrier index; the same configured aircraft carry the guaranteed
  attack-power pickups on every battle, for three pickups in total.
- At 38 seconds, level 2 spawns eight `enemy_small_04` aircraft from the right
  edge. They drift down-left and fire scheduled `enemy_single_down` shots after
  entering. At 42 seconds, a matching eight-aircraft wave enters from the left,
  drifts down-right, and uses the same firing schedule.
- `BulletManager`, `BulletPatternManager`, and `MissileManager` handle
  projectile behavior and configured firing patterns.
- Enemy bullet `firePattern` values can append options after `:`;
  `Single:GlowTrail` keeps straight movement and enables a light trail when
  the bullet also has a positive `glowRange`.
- Enemy `WaveSpawn.attackPatternId` supports scheduled pattern strings in the
  form `patternId@interval+offset`; append `~duration` to stop that scheduled
  loop after a finite number of seconds.
- Enemy `WaveSpawn.movementPath` supports inline movement options, including
  `Orbit:...`, `Bezier:points=x/y|...`, and `Spline:points=x/y|...`. Use
  `Spline` when the path needs to pass through configured turn points.
- `BossController` handles boss entry, HP, configured phase changes, firing,
  defeat, and the victory trigger.
- Level 2 spawns `boss_level_02_mid_01` at 105 seconds from
  `enemy_09.prefab`. It uses the Boss phase controller for two HP-based attack
  stages without showing the Boss HP HUD or phase/entry notices. Defeating it
  does not settle the level because the final `boss_02` wave remains scheduled
  at 180 seconds. Its side missile patterns resolve `left`, `left1`, `right`,
  and `right1` as four separate mounts and emit one missile per mount, so each
  side launches two missiles without overlapping at spawn.
- The level-2 final boss `boss_02` uses
  `Assets/Prefabs/Enemies/BOSS_2.prefab`, has 500 HP, and uses dedicated
  100%-70%, 70%-40%, and 40%-0% phases. Phase 1 emits three center rings every
  two seconds plus one homing missile from each side every two seconds. Phase 2
  emits a slower 2.3-speed center windmill with the ordinary red enemy bullet
  (no rice-grain spin/sway motion) and fast homing missiles from only the
  left/right mounts. The windmill emits 24 six-bullet volleys at 0.08-second
  spacing with an 11-degree rotation step, and Boss movement is locked for the
  pattern's generated duration so the spiral center stays stable. Phase-2 fast
  homing missiles that become due during the windmill are deferred until 0.25
  seconds after movement unlocks. Phase 3 removes ring fire: the outer
  `left1`/`right1`
  mounts track the player with thin red lines for two seconds, freeze their
  directions, then charge for 0.5 seconds and fire 0.8-second lasers while
  Boss movement is locked.
  Laser beams dynamically extend past the viewport edge along their firing
  direction so their hard end caps remain off-screen. The full laser sequence
  repeats every five seconds. The center mount starts
  after the first laser and emits three homing missiles at 0.5-second spacing
  every three seconds.
- Laser bullets use a white or warm-white core, their configured color for the
  beam and outer glow, and animated edge-energy fluctuations. A bullet with no
  configured glow color retains the default cyan player-laser treatment.
- Every non-laser enemy bullet has a glow even when its config omits a range.
  Enemy glow colors are calibrated from each source sprite's dominant hue:
  ordinary bullets use red-pink, rice-grain bullets use magenta, and the level-2
  helicopter bullet uses violet, with per-bullet alpha and radius tuning.
- `StageManager` advances level timeline events. A clear-bullets event clears
  both enemy bullets and enemy missiles, and stage messages can substitute
  `{LEVEL}`, `{MAX_LEVEL}`, `{BOSS_ID}`, and `{BOSS}`.
- Trophy events use IDs beginning with `spawn_trophy`; append inline position
  options such as `spawn_trophy_level_02_left_003:x=-3.2,y=5.7` to override the
  default spawn position. Trophy pickups use the `trophy` pickup item, require
  the player to stay within pickup range for 2 seconds, and currently use a
  configured pickup radius of 1.5. They render with a yellow inner glow plus a
  larger orange-yellow pulsing outer glow.
- `GameManager` exposes the `无敌模式` Inspector toggle on `Managers`. The
  value selected in `SampleScene` is carried into `BattleScene`, where
  `PlayerController` ignores incoming damage without continuously flashing.
- Hitboxes are child-driven through `ActorHitbox`; it forwards damage to the
  owning enemy or boss while preventing duplicate same-frame hits from
  overlapping child hitboxes.

## Pickups, UI, And Audio

- `PickupManager` spawns configured enemy drops and can force active pickups
  toward the player during settlement.
- Enemies that carry special pickups (including bomb, heal, shield, magnet,
  and attack-power pickups) render with the same steady cyan soft glow used by
  special pickups in the scene; the selected level-2 carriers use the same
  treatment.
- `PickupItemController` supports star, coin, magnet, bomb, heal, shield,
  weapon-up, and trophy behavior. Magnet attracts stars, bomb removes non-boss
  enemies, weapon-up permanently adds 1 to the current player's bullet damage
  for the rest of the battle, trophy requires a 2-second in-range hold with a
  yellow glow treatment, and star and coin pickups play configured sound paths
  currently defined in code.
- `UIManager` provides battle HUD/settlement behavior and runtime-created UI
  where an authored view is unavailable. In battle, score, timer, and the
  10-segment Boss HP bar are stacked from a fixed 65-pixel top offset instead
  of using safe-area inset positioning; the bullet-time overlay no longer
  shows LEVEL/HP/STAR/COIN text. After pointer release enters bullet time,
  the top-left pause button toggles `GameState.Paused` plus `Time.timeScale`,
  and the top-right exit button returns to the lobby after clearing
  pause/bullet-time scaling; both corner buttons move down using Douyin
  `TT.GetMenuButtonLayout()` in WebGL builds.
  The Boss HP fill uses
  `#862800` and updates immediately when damage is applied. The defeat overlay
  places `GAME/OVER` near the upper third and its back button near the lower
  third. The page classes provide lobby, hangar, settings, and stage-selection
  views.
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

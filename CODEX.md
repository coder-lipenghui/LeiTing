# LeiTing Codex Handoff

This project is a Unity 2021.3.6f1c1 2D vertical shooter demo inspired by LeiDian/Raiden-style gameplay. The main design notes live in `雷霆战机.md`; the playable implementation is driven mostly by scripts under `Assets/Scripts` and Luban source tables under `Luban/Datas`.

## Quick State

- Unity version: `2021.3.6f1c1`
- Main scene: `Assets/Scenes/SampleScene.unity`
- Runtime config: Luban tables generated to `Assets/Resources/Luban`; `Assets/Resources/Configs/GameConfig.json` is only the legacy fallback.
- Demo scene setup menu: `LeiTing/Setup/Create Demo Scene Skeleton`
- Editor level test menu: `LeiTing/Test/Level Selector`
- Current content count:
  - 5 normal enemy config ids: `enemy_a` to `enemy_e`
  - 12 boss config ids: `boss_01` to `boss_12`
  - 12 level config ids: `level_01` to `level_12`
  - 7 bullet configs
  - 14 bullet pattern configs
  - 14 stage waves
  - 7 stage timeline events
  - Current level final Boss enters at `180s`

## Important Commands

Run these from the repo root:

```bash
dotnet build Assembly-CSharp.csproj --no-restore
dotnet build Assembly-CSharp-Editor.csproj --no-restore
node -e "JSON.parse(require('fs').readFileSync('Assets/Resources/Configs/GameConfig.json','utf8')); console.log('json ok')"
```

These are the current lightweight validation commands. Full gameplay still needs Unity Play Mode checks.

## Luban Config Rule

Do not directly edit Luban generated code or generated JSON:

- `Assets/Scripts/Config/LubanGenerated/**`
- `Assets/Resources/Luban/**`

For config changes, edit the corresponding source workbook under `Luban/Datas/*.xlsx`, then run:

```bash
bash Luban/gen.sh
```

Generated code and generated JSON should only change as output from Luban generation.

## Architecture

Core flow:

- `GameBootstrap` loads config, prepares camera/player/background, then starts the game.
- `GameManager` owns game state and score.
- `ConfigManager` loads generated Luban resources first, then falls back to `Resources/Configs/GameConfig.json`.
- `EnemyManager` reads wave config and spawns enemies.
- `StageManager` reads stage timeline events, expands level/Boss placeholders, and shows notices/clears enemy bullets.
- `BulletManager` pools and fires projectiles.
- `BulletPatternManager` expands pattern configs into bullet volleys.
- `UIManager` builds runtime test UI, HUD, Boss HP bar, phase notices, score popups, and settlement text.

The project intentionally has no complex prefab authoring dependency for basic boot. If a prefab is missing, many systems still fallback to generated/dynamic objects.

## Prefabs And Mounts

Enemy prefabs live in:

```text
Assets/Prefabs/Enemies/
```

Generated prefabs:

- `enemy_01.prefab`
- `boss_01.prefab` through `boss_12.prefab`

Boss prefab structure:

```text
boss_XX
├── Visual
├── FirePoints
│   └── main_3
│       ├── left
│       ├── center
│       └── right
└── HitBoxes
    ├── body
    ├── left
    └── right
```

Relevant scripts:

- `ActorMounts`: finds fire point groups by name.
- `ActorHitbox`: child hitbox component; forwards damage to parent `EnemyController` or `BossController`.
- `EnemyController` / `BossController`: disable root hitbox when child hitboxes exist.

When adjusting enemy/Boss resources, prefer moving `FirePoints` and `HitBoxes` in prefab view instead of hardcoding coordinates.

## Config Rules

Main config file:

```text
Assets/Resources/Configs/GameConfig.json
```

Enemy config supports:

- `id`
- `displayName`
- `prefabPath`
- `hp`
- `moveSpeed`
- `attackInterval`
- `bulletId`
- `bulletPatternId`
- `hitScaleFeedback`
- `score`

Wave spawn config supports:

- `enemyId`
- `count`
- `interval`
- `startPosition`
- `attackPatternId`
- `movementPath`
- `pathAmplitude`
- `pathSpeed`
- `holdDuration`

Supported normal enemy movement paths:

- `Straight`
- `Sine`
- `DriftLeft`
- `DriftRight`
- `Hold`
- `StopAndLeave`

Bullet pattern config supports `firePointGroup`. Current groups are:

- `center`
- `left`
- `right`
- `main_3`

For `main_3`, the pattern fires once from each child point in that group.

## Boss System

`BossController` handles:

- entry movement
- HP tracking
- phase switching
- phase notices
- Boss HP UI
- pattern bursts
- hit flash
- optional hit scale feedback via `hitScaleFeedback`
- defeat explosion sequence
- game victory

Only `boss_01` currently has explicit `bossPhases`. Other bosses fallback to `boss_01` phase config until their own phases are added.

Current `boss_01` HP is `160`. With the default player weapon:

- bullet: `player_laser_01`
- damage: `1`
- fire interval: `0.16s`
- theoretical DPS: `6.25`
- full-hit kill time: `25.6s`

`player_laser_01` is procedural: it uses `Assets/Shaders/ProceduralLaser.shader` and does not need a laser image asset. During gameplay it follows the player's `FirePoint` every frame and dynamically stretches to the camera's top edge.

## Hit Feedback Notes

Recent bugfixes:

- `HitFlash` / `BossHitFlash` now sync with `Visual` transform so prefab-scaled sprites do not flash at the wrong size.
- `ActorHitbox` deduplicates hits by `(same bullet, same actor, same frame)` to avoid overlapping child hitboxes applying multiple damage ticks.
- `BossController` ignores damage after death starts.
- `EnemyController` ignores damage after death starts.

If hit feedback looks wrong again, inspect:

- whether `Visual` has a custom scale
- whether `HitBoxes` overlap too much
- whether root collider is accidentally enabled along with child hitboxes
- whether the bullet is piercing or laser-like

## Level And Stage Timeline

Levels are declared in `levels`; each level owns its background sprite, background scroll speed, and BGM path. Boss spawning is declared by level-specific `Wave` / `WaveSpawn` rows, with each Boss spawn using an explicit `enemyId`.

Current demo structure:

- `0:00 - 0:30`: tutorial
- `0:30 - 1:30`: first combat wave
- `1:30 - 2:30`: escalation
- `2:30 - 3:00`: transition and Boss warning
- `3:00+`: current level final Boss battle

`StageManager` events live in `stageEvents` and can:

- show a message through `UIManager.ShowBossPhaseNotice`
- clear enemy bullets through `BulletManager.ClearEnemyBullets`

## Asset Locations

Enemy/Boss source sprites:

```text
Assets/Art/Animations/Enemies/enemy-01.png
Assets/Art/Animations/Enemies/BOSS-1.png
...
Assets/Art/Animations/Enemies/BOSS-12.png
```

Bullet sprites:

```text
Assets/Art/Sprites/Bullets/player_bullet_01.png
Assets/Art/Sprites/Bullets/enemy_bullet_01.png
```

Player prefab:

```text
Assets/Prefabs/Player/warplane-01.prefab
```

## Known Caveats

- Many UI elements are currently generated in code by `UIManager`, not authored as UI prefabs.
- Enemy/Boss prefabs were generated mechanically; fire points and hitboxes are usable defaults, but should be tuned visually in Unity.
- `boss_02` to `boss_12` exist as config and prefabs, but do not yet have unique phase patterns.
- Full Play Mode visual verification is still recommended after prefab/hitbox changes.
- `.idea/` may appear as an untracked local editor folder; ignore unless the user asks otherwise.

## Coding Preferences For Future Work

- Prefer config-driven behavior in `GameConfig.json`.
- Prefer prefab transform edits for spatial mount/hitbox tuning.
- Keep fallback behavior for missing prefabs/assets where practical.
- Run the two `dotnet build` commands and JSON parse check before handing off.

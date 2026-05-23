# GameConfig 配置说明

本文档记录运行时 `GameConfig` 内存模型的字段含义。当前项目只从 Luban 配置流加载数据，源表在 `Luban/Datas`，生成后的运行时数据在 `Assets/Resources/Luban`。新人改表流程请先看 `docs/luban-config-newcomer-guide.md`。

## 配置总览

| 节点 | 类型 | 作用 |
| --- | --- | --- |
| `player` | object | 玩家飞机基础属性、武器、碰撞和掉落物吸附参数。 |
| `enemies` | array | 敌机和 Boss 的生命、移动、攻击、分数、死亡掉落等配置。 |
| `bullets` | array | 玩家弹、敌机弹、激光等弹体基础配置。 |
| `pickupItems` | array | 掉落物类型配置，目前包含星星。 |
| `levels` | array | 关卡显示名、背景资源、背景滚速和背景音乐。 |
| `bulletPatterns` | array | 敌机和 Boss 使用的弹幕模式。 |
| `waves` | array | 关卡刷怪波次。 |
| `stageEvents` | array | 关卡时间轴事件，例如提示、清弹。 |
| `bossPhases` | array | Boss 阶段、移动和阶段弹幕配置。 |
| `bossSkills` | array | Boss 技能配置，目前保留为数据表，可被后续逻辑使用。 |

## 通用约定

| 项 | 说明 |
| --- | --- |
| ID 引用 | `enemyId`、`bulletId`、`bulletPatternId`、`itemId` 等字段必须引用对应数组中存在的 `id`。 |
| 坐标单位 | `Vector2` 字段使用 Unity 世界坐标单位，例如 `{ "x": 0, "y": 5.8 }`。 |
| 时间单位 | `startTime`、`interval`、`lifetime`、`cooldown` 等均为秒。 |
| 角度单位 | `baseAngle`、`spreadAngle`、`angleStep` 使用角度制；`-90` 表示向下。 |
| 资源路径 | `prefabPath`、`spritePath` 可以是 `Assets/...` 编辑器路径；运行时加载 Resources 资源时会去掉 `Resources/` 前缀和扩展名。 |
| 缺省值 | 多数数值字段为 `0` 时会由代码使用默认值或回退逻辑；配置时建议显式填写。 |

## `player` 玩家配置

作用：控制玩家飞机的初始属性、移动速度、受击无敌时间、碰撞半径、默认武器和掉落物吸附能力。

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `id` | string | 玩家配置唯一 ID。 | `"warplane_01"` |
| `displayName` | string | 显示名称。 | `"Warplane 01"` |
| `prefabPath` | string | 玩家预制体路径。 | `"Assets/Prefabs/Player/warplane-01.prefab"` |
| `hp` | int | 初始生命值。 | `3` |
| `shield` | int | 初始护盾值，优先抵扣伤害。 | `1` |
| `stars` | int | 初始星星数量。 | `0` |
| `coins` | int | 初始金币数量。 | `0` |
| `moveSpeed` | float | 玩家移动速度。 | `18` |
| `invincibleTime` | float | 受击后的无敌时间。 | `1.5` |
| `pickupAttractRange` | float | 掉落物进入该范围后开始吸附。 | `2.2` |
| `pickupAttractSpeed` | float | 掉落物吸附飞向玩家的速度。 | `8` |
| `visualScale` | float | 玩家视觉节点缩放。 | `0.55` |
| `hitboxRadius` | float | 玩家受击判定圆半径。 | `0.18` |
| `hitboxOffset` | Vector2 | 玩家受击判定相对飞机根节点的偏移。 | `{ "x": 0, "y": -0.08 }` |
| `defaultBulletId` | string | 默认使用的玩家弹体 ID。 | `"player_laser_01"` |
| `fireInterval` | float | 自动开火间隔。 | `0.16` |

示例：

```json
{
  "id": "warplane_01",
  "displayName": "Warplane 01",
  "prefabPath": "Assets/Prefabs/Player/warplane-01.prefab",
  "hp": 3,
  "shield": 1,
  "stars": 0,
  "coins": 0,
  "moveSpeed": 18,
  "invincibleTime": 1.5,
  "pickupAttractRange": 2.2,
  "pickupAttractSpeed": 8,
  "visualScale": 0.55,
  "hitboxRadius": 0.18,
  "hitboxOffset": { "x": 0, "y": -0.08 },
  "defaultBulletId": "player_laser_01",
  "fireInterval": 0.16
}
```

## `levels` 关卡配置

作用：控制关卡显示名、滚动背景图片、滚动速度和背景音乐。`bgmPath` 会在进入战斗关卡时开始播放，不会作为大厅/首次进入游戏的背景音乐播放。Boss 不再挂在关卡表上，改由 `waves` / `spawns` 显式配置。

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `id` | string | 关卡唯一 ID。 | `"level_01"` |
| `displayName` | string | 关卡显示名。 | `"第 1 关"` |
| `backgroundSpritePath` | string | 背景图片路径。 | `"Assets/Art/Sprites/Backgrounds/background-01.png"` |
| `backgroundScrollSpeed` | float | 背景向下滚动速度。 | `2.1` |
| `bgmPath` | string | 背景音乐资源路径，可为空。 | `""` |

示例：

```json
{
  "id": "level_01",
  "displayName": "第 1 关",
  "backgroundSpritePath": "Assets/Art/Sprites/Backgrounds/background-01.png",
  "backgroundScrollSpeed": 2.1,
  "bgmPath": ""
}
```

## `enemies` 敌机配置

作用：定义普通敌机和 Boss 的基础战斗参数。`id` 以 `boss` 开头的敌人会走 Boss 控制逻辑。

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `id` | string | 敌人唯一 ID。 | `"enemy_a"` |
| `displayName` | string | 显示名称。 | `"杂兵直线型"` |
| `prefabPath` | string | 敌人预制体路径。 | `"Assets/Prefabs/Enemies/enemy_01.prefab"` |
| `hp` | int | 生命值。 | `3` |
| `moveSpeed` | float | 移动速度。 | `2.4` |
| `attackInterval` | float | 攻击间隔。 | `1.8` |
| `bulletId` | string | 默认发射弹体 ID。 | `"enemy_bullet_01"` |
| `bulletPatternId` | string | 默认弹幕 ID。 | `"enemy_aim_single"` |
| `hitScaleFeedback` | bool | 受击时是否启用缩放反馈，主要用于 Boss。 | `false` |
| `score` | int | 死亡后增加分数。 | `100` |
| `drops` | DropConfig[] | 死亡后掉落物列表。 | 见下方 |

### `drops` 掉落配置

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `itemId` | string | 掉落物 ID，引用 `pickupItems.id`。 | `"star"` |
| `count` | int | 掉落数量。 | `1` |
| `dropOnce` | bool | 是否在本次关卡中只掉落一次，适合 demo 测试道具。 | `true` |

示例：

```json
{
  "id": "enemy_a",
  "displayName": "杂兵直线型",
  "hp": 3,
  "moveSpeed": 2.4,
  "attackInterval": 1.8,
  "bulletId": "enemy_bullet_01",
  "bulletPatternId": "enemy_aim_single",
  "score": 100,
  "drops": [
    { "itemId": "star", "count": 1 },
    { "itemId": "coin", "count": 1 },
    { "itemId": "magnet", "count": 1, "dropOnce": true }
  ],
  "prefabPath": "Assets/Prefabs/Enemies/enemy_01.prefab"
}
```

## `bullets` 弹体配置

作用：定义弹体归属、伤害、速度、生命周期、尺寸和特殊弹体行为。

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `id` | string | 弹体唯一 ID。 | `"player_bullet_01"` |
| `owner` | string | 弹体归属，决定层级和碰撞逻辑。常用 `Player`、`Enemy`。 | `"Player"` |
| `firePattern` | string | 玩家武器发射方式或弹体类型。常用 `Single`、`Double`、`Spread`、`Laser`。 | `"Single"` |
| `spritePath` | string | 弹体图片路径。 | `"Assets/Art/Sprites/Bullets/player_bullet_01.png"` |
| `damage` | int | 命中伤害。 | `1` |
| `speed` | float | 飞行速度；激光通常为 `0`。 | `12` |
| `lifetime` | float | 存活时间。 | `2` |
| `size` | Vector2 | 弹体碰撞和视觉尺寸。 | `{ "x": 0.12, "y": 0.32 }` |
| `glowColor` | Color | 敌方子弹光晕颜色；`a` 为 `0` 时使用代码默认色。 | `{ "r": 1, "g": 0.48, "b": 0.12, "a": 0.58 }` |
| `glowRange` | float | 敌方子弹光晕向外扩展的范围；`0` 表示不发光。 | `0.18` |
| `projectileCount` | int | 一次开火生成的弹体数量。 | `1` |
| `spreadAngle` | float | 散射总角度。 | `32` |
| `muzzleSpacing` | float | 多发弹之间的水平间距。 | `0.28` |
| `pierceCount` | int | 可穿透次数；`-1` 表示无限穿透。 | `2` |
| `laserLength` | float | 激光长度，仅激光弹使用。 | `4.8` |

示例：

```json
{
  "id": "player_bullet_spread_01",
  "owner": "Player",
  "firePattern": "Spread",
  "spritePath": "Assets/Art/Sprites/Bullets/player_bullet_01.png",
  "damage": 1,
  "speed": 11,
  "lifetime": 2,
  "size": { "x": 0.12, "y": 0.32 },
  "glowColor": { "r": 0, "g": 0, "b": 0, "a": 0 },
  "glowRange": 0,
  "projectileCount": 5,
  "spreadAngle": 32,
  "muzzleSpacing": 0.08,
  "pierceCount": 0,
  "laserLength": 0
}
```

## `pickupItems` 掉落物配置

作用：定义可拾取物的表现和收益。星星和金币增加玩家资源；磁体、炸弹、红心、盾牌通过 `itemType` 触发对应效果。

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `id` | string | 掉落物唯一 ID。 | `"star"` |
| `displayName` | string | 显示名称。 | `"星星"` |
| `itemType` | string | 掉落物类型，支持 `Star`、`Coin`、`Magnet`、`Bomb`、`Heal`、`Shield`。 | `"Star"` |
| `spritePath` | string | 掉落物图片路径；星星默认使用 `Assets/Art/Sprites/Item/item_star.png`。 | `"Assets/Art/Sprites/Item/item_star.png"` |
| `starValue` | int | 拾取后增加的星星数量。 | `1` |
| `coinValue` | int | 拾取金币后增加的金币数量。 | `1` |
| `healValue` | int | 拾取红心后恢复的生命值。 | `1` |
| `shieldDuration` | float | 拾取盾牌后的无敌持续时间，单位秒。 | `5` |
| `lifetime` | float | 掉落物未拾取时的存活时间。 | `12` |
| `driftSpeed` | float | 未吸附时向下漂移速度。 | `1.1` |
| `pickupRadius` | float | 拾取判定半径。 | `0.22` |
| `visualScale` | float | 掉落物视觉缩放。 | `0.62` |

示例：

```json
{
  "id": "star",
  "displayName": "星星",
  "itemType": "Star",
  "spritePath": "Assets/Art/Sprites/Item/item_star.png",
  "starValue": 1,
  "coinValue": 0,
  "healValue": 0,
  "shieldDuration": 0,
  "lifetime": 12,
  "driftSpeed": 1.1,
  "pickupRadius": 0.22,
  "visualScale": 0.35
}
```

## `bulletPatterns` 弹幕配置

作用：给敌机和 Boss 定义弹幕形态。运行时会引用 `bulletId` 的弹体配置，并用 `bulletSpeed`、`bulletLifetime` 覆盖速度和生命周期。

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `id` | string | 弹幕唯一 ID。 | `"enemy_aim_single"` |
| `patternType` | string | 弹幕类型。支持 `Single`、`Fan`、`Aim`、`Ring`、`Rotating`、`Spiral`。 | `"Aim"` |
| `bulletId` | string | 使用的弹体 ID。 | `"enemy_bullet_01"` |
| `firePointGroup` | string | 发射点组名，配合 `ActorMounts` 使用。 | `"center"` |
| `firePointOffset` | Vector2 | 发射点偏移。 | `{ "x": 0, "y": -0.38 }` |
| `baseAngle` | float | 基础发射角度。 | `-90` |
| `bulletCount` | int | 每轮弹幕弹体数量。 | `7` |
| `bulletCountPerBurst` | int | `Spiral` 每组子弹数量；未填时使用 `bulletCount`。 | `6` |
| `angleStep` | float | 环形弹幕步进角；或作为散射角备用计算。 | `22.5` |
| `spreadAngle` | float | 扇形、瞄准、旋转弹幕总散布角度。 | `72` |
| `bulletSpeed` | float | 覆盖弹体速度；`0` 时使用弹体配置速度。 | `4.4` |
| `bulletLifetime` | float | 覆盖弹体生命周期；`0` 时使用弹体配置生命周期。 | `5` |
| `rotate` | bool | 是否每次发射递增旋转偏移。 | `true` |
| `rotationSpeed` | float | 每次发射增加的角度偏移。 | `18` |
| `rotateStepDegrees` | float | `Spiral` 每组发射后的旋转角；未填时使用 `rotationSpeed` 的绝对值。 | `10` |
| `clockwise` | bool | `Spiral` 是否顺时针旋转；Luban 表可用负 `rotationSpeed` 表示顺时针。 | `true` |
| `aimAtPlayer` | bool | 是否以玩家方向作为基础角度。 | `true` |
| `burstCount` | int | 连续发射轮数。 | `1` |
| `fireInterval` | float | 连发轮之间的间隔。 | `0.08` |
| `duration` | float | `Spiral` 持续时间；未填时使用 `burstCount * fireInterval`。 | `2` |

示例：

```json
{
  "id": "enemy_fan_07",
  "patternType": "Fan",
  "bulletId": "enemy_bullet_01",
  "firePointOffset": { "x": 0, "y": -0.35 },
  "baseAngle": -90,
  "bulletCount": 7,
  "angleStep": 0,
  "spreadAngle": 72,
  "bulletSpeed": 3.8,
  "bulletLifetime": 5.2,
  "rotate": false,
  "rotationSpeed": 0,
  "aimAtPlayer": false,
  "burstCount": 1,
  "fireInterval": 0,
  "firePointGroup": "center"
}
```

螺旋扩散示例：

```json
{
  "id": "enemy_spiral_windmill",
  "patternType": "Spiral",
  "bulletId": "enemy_bullet_02",
  "firePointGroup": "center",
  "firePointOffset": { "x": 0, "y": 0 },
  "baseAngle": 0,
  "bulletCountPerBurst": 6,
  "bulletSpeed": 4.6,
  "rotateStepDegrees": 10,
  "clockwise": true,
  "fireInterval": 0.05,
  "duration": 2
}
```

## `waves` 波次配置

作用：按关卡时间生成敌机组。每个波次包含一个或多个 `spawns`，按数组顺序执行。

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `id` | string | 波次唯一 ID。 | `"wave_tutorial_001"` |
| `startTime` | float | 关卡开始后第几秒触发。 | `3` |
| `spawns` | WaveSpawnConfig[] | 该波次包含的刷怪组。 | 见下方 |

### `spawns` 刷怪组配置

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `enemyId` | string | 生成的敌人 ID。 | `"enemy_a"` |
| `count` | int | 生成数量。 | `3` |
| `interval` | float | 同组敌人生成间隔。 | `1.2` |
| `startPosition` | Vector2 | 第一架敌人的出生位置；多架会自动横向展开。 | `{ "x": -1.4, "y": 5.8 }` |
| `attackPatternId` | string | 本次刷怪覆盖敌人默认弹幕。 | `"enemy_aim_single"` |
| `movementPath` | string | 移动路径。支持 `Straight`、`Hold`、`StopAndLeave`、`Sine`、`DriftLeft`、`DriftRight`。 | `"Straight"` |
| `pathAmplitude` | float | 路径振幅，主要给 `Sine` 使用。 | `1.0` |
| `pathSpeed` | float | 路径速度或横向漂移速度。 | `0.35` |
| `holdDuration` | float | 停留时间，主要给 `StopAndLeave` 使用。 | `2.4` |

示例：

```json
{
  "id": "wave_tutorial_001",
  "startTime": 3,
  "spawns": [
    {
      "enemyId": "enemy_a",
      "count": 3,
      "interval": 1.2,
      "startPosition": { "x": -1.4, "y": 5.8 },
      "attackPatternId": "enemy_aim_single",
      "movementPath": "Straight",
      "pathAmplitude": 0,
      "pathSpeed": 0,
      "holdDuration": 0
    }
  ]
}
```

## `stageEvents` 关卡事件配置

作用：按关卡时间触发一次性事件，目前支持显示提示文本和清除敌方子弹。

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `id` | string | 事件唯一 ID。 | `"stage_notice_tutorial"` |
| `startTime` | float | 关卡开始后第几秒触发。 | `1` |
| `message` | string | 屏幕提示文本；为空则不显示。 | `"TRAINING START"` |
| `clearEnemyBullets` | bool | 是否清除场上敌方子弹。 | `false` |

示例：

```json
{
  "id": "stage_notice_tutorial",
  "startTime": 1,
  "message": "TRAINING START",
  "clearEnemyBullets": false
}
```

## `bossPhases` Boss 阶段配置

作用：按 Boss 血量百分比切换阶段，控制 Boss 移动范围、攻击节奏和阶段弹幕列表。

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `id` | string | 阶段唯一 ID。 | `"boss_phase_01"` |
| `bossId` | string | 所属 Boss ID。 | `"boss_01"` |
| `displayName` | string | 阶段显示名称。 | `"PHASE 1  弹幕教学"` |
| `triggerHpPercent` | float | 触发血量百分比，`1` 表示满血阶段，`0.65` 表示 65% 及以下。 | `0.65` |
| `attackInterval` | float | 阶段攻击间隔。 | `1.55` |
| `burstCount` | int | 每次攻击的连发轮数。 | `2` |
| `burstInterval` | float | 连发轮间隔。 | `0.22` |
| `movementRange` | Vector2 | 以锚点为中心的横向和纵向摆动范围。 | `{ "x": 0.9, "y": 0.1 }` |
| `movementSpeed` | float | Boss 阶段移动速度。 | `0.85` |
| `bulletPatternIds` | string[] | 阶段循环使用的弹幕 ID 列表。 | `["boss_p1_fan_09"]` |

示例：

```json
{
  "id": "boss_phase_01",
  "bossId": "boss_01",
  "displayName": "PHASE 1  弹幕教学",
  "triggerHpPercent": 1,
  "attackInterval": 1.55,
  "burstCount": 2,
  "burstInterval": 0.22,
  "movementRange": { "x": 0.9, "y": 0.1 },
  "movementSpeed": 0.85,
  "bulletPatternIds": [
    "boss_p1_fan_09",
    "boss_p1_sweep_single"
  ]
}
```

## `bossSkills` Boss 技能配置

作用：保存 Boss 技能数据。当前代码提供 `ConfigManager.GetBossSkill(id)` 查询入口，后续可用于独立技能系统或特殊攻击逻辑。

| 字段 | 类型 | 作用 | 示例 |
| --- | --- | --- | --- |
| `id` | string | 技能唯一 ID。 | `"boss_skill_fan_01"` |
| `bossId` | string | 所属 Boss ID。 | `"boss_01"` |
| `bulletId` | string | 技能使用的弹体 ID。 | `"enemy_bullet_01"` |
| `triggerHpPercent` | float | 预期触发血量百分比。 | `1` |
| `cooldown` | float | 技能冷却时间。 | `2.4` |
| `bulletCount` | int | 技能发射弹体数量。 | `7` |
| `spreadAngle` | float | 技能散布角度。 | `80` |

示例：

```json
{
  "id": "boss_skill_fan_01",
  "bossId": "boss_01",
  "bulletId": "enemy_bullet_01",
  "triggerHpPercent": 1,
  "cooldown": 2.4,
  "bulletCount": 7,
  "spreadAngle": 80
}
```

## 新增配置时的检查清单

| 检查项 | 说明 |
| --- | --- |
| ID 唯一 | 同一数组内的 `id` 不要重复。 |
| 引用存在 | `bulletId`、`bulletPatternId`、`enemyId`、`itemId` 等引用必须能在对应表中找到。 |
| 时间递增 | `waves.startTime` 和 `stageEvents.startTime` 建议按时间顺序排列，方便维护。 |
| 资源路径有效 | `prefabPath`、`spritePath` 指向的文件应存在；掉落物 `spritePath` 可为空。 |
| 数值为正 | 生命、速度、数量、半径、间隔等关键数值建议大于 `0`。 |
| Boss 阶段覆盖 | Boss 的 `bossPhases` 至少配置满血阶段；多个阶段按 `triggerHpPercent` 从高到低更易读。 |

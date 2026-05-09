# 子弹与导弹配置教程

本文档说明 Luban 配置中常用子弹、导弹与发射模式的配置方式。运行时主要读取四组配置：

- `bullets`: 单个子弹的速度、伤害、贴图、碰撞尺寸。
- `bulletPatterns`: 子弹发射模式，决定从哪个挂点、以什么角度和数量发射。
- `missiles`: 单个导弹的行为类型、速度、追踪、锁定、爆炸等参数。
- `missilePatterns`: 导弹发射模式，结构与 `bulletPatterns` 类似。

## 挂点规则

敌机和 Boss prefab 下需要有 `FirePoints` 节点。`firePointGroup` 会查找 `FirePoints/<groupName>`：

- 如果该 group 没有子节点，则 group 自身就是发射点。
- 如果该 group 有多个子节点，则每个子节点都会各发射一次该 pattern。
- 例如 `scatter_2` 下有 `p1`、`p2`，配置一个 3 发扇形 pattern，最终会形成两个炮口各 3 发。

## 普通单发子弹

```json
{
  "id": "heli_bullet_single",
  "patternType": "Aim",
  "bulletId": "enemy_bullet_02",
  "firePointGroup": "center",
  "firePointOffset": { "x": 0, "y": 0 },
  "baseAngle": -90,
  "bulletCount": 1,
  "spreadAngle": 0,
  "bulletSpeed": 5.1,
  "bulletLifetime": 5,
  "aimAtPlayer": true,
  "burstCount": 1,
  "fireInterval": 0
}
```

要点：

- `patternType: "Aim"` 或 `aimAtPlayer: true` 会朝玩家方向修正角度。
- `baseAngle: -90` 表示默认向下。
- `bulletId` 指向 `bullets` 中的实际子弹外观和伤害。

## 双炮口散射子弹

```json
{
  "id": "heli_bullet_spread_2",
  "patternType": "Fan",
  "bulletId": "enemy_bullet_01",
  "firePointGroup": "scatter_2",
  "firePointOffset": { "x": 0, "y": 0 },
  "baseAngle": -90,
  "bulletCount": 3,
  "spreadAngle": 34,
  "bulletSpeed": 4.2,
  "bulletLifetime": 5.2,
  "aimAtPlayer": false
}
```

要点：

- `Fan` 会在 `spreadAngle` 范围内平均展开。
- `scatter_2` 有两个挂点，所以总发射数是 `2 * bulletCount`。
- 想让散射更密，可以提高 `bulletCount` 或减少 `fireInterval`。

## 环形与旋转弹幕

```json
{
  "id": "boss_p2_ring_20",
  "patternType": "Ring",
  "bulletId": "enemy_bullet_02",
  "firePointGroup": "center",
  "firePointOffset": { "x": 0, "y": -0.05 },
  "baseAngle": 0,
  "bulletCount": 20,
  "angleStep": 18,
  "spreadAngle": 360,
  "bulletSpeed": 3,
  "bulletLifetime": 4.8,
  "rotate": true,
  "rotationSpeed": 7
}
```

要点：

- `Ring` 通常用 `spreadAngle: 360`。
- `angleStep` 不填或小于等于 0 时，可按数量自动均分。
- `rotate: true` 会让下一轮发射角度累积偏移，适合做旋转弹幕。

## 激光子弹

```json
{
  "id": "player_laser_01",
  "owner": "Player",
  "firePattern": "Laser",
  "spritePath": "",
  "damage": 1,
  "speed": 0,
  "lifetime": 0.18,
  "size": { "x": 0.28, "y": 13 },
  "pierceCount": -1,
  "laserLength": 13
}
```

要点：

- `firePattern: "Laser"` 会走激光视觉和碰撞逻辑。
- `pierceCount: -1` 表示不因命中回收。
- `lifetime` 控制激光持续时间。

## 直线导弹

```json
{
  "id": "missile_straight_1001",
  "behaviorType": 1,
  "prefabPath": "Assets/Prefabs/Missiles/missile_01_straight.prefab",
  "bodyRes": "Assets/Art/Sprites/Bullets/missile_01.png",
  "speed": 3.2,
  "maxSpeed": 3.2,
  "lifeTime": 5,
  "damage": 1,
  "radius": 0.16
}
```

要点：

- `behaviorType: 1` 是直线飞行。
- `prefabPath` 控制使用哪个导弹 prefab。
- `bodyRes` 是运行时贴图，通常与 prefab 的 Visual 贴图保持一致。

## 弱追踪导弹

```json
{
  "id": "missile_weak_homing_1002",
  "behaviorType": 3,
  "prefabPath": "Assets/Prefabs/Missiles/missile_03_weak_homing.prefab",
  "bodyRes": "Assets/Art/Sprites/Bullets/missile_03.png",
  "speed": 2.2,
  "maxSpeed": 2.8,
  "acceleration": 0.25,
  "trackTime": 2.6,
  "turnSpeed": 74,
  "lifeTime": 6
}
```

要点：

- `behaviorType: 3` 是弱追踪。
- `turnSpeed` 越大，转向越灵敏。
- `trackTime` 到期后停止继续修正方向；`isLoopTrack: true` 可持续追踪。

## 锁定后冲刺导弹

```json
{
  "id": "missile_lock_dash_1003",
  "behaviorType": 5,
  "prefabPath": "Assets/Prefabs/Missiles/missile_09_lock_dash.prefab",
  "bodyRes": "Assets/Art/Sprites/Bullets/missile_09.png",
  "speed": 0.8,
  "maxSpeed": 7.2,
  "lockDelay": 0.8,
  "warningTime": 0.8,
  "lifeTime": 4.5
}
```

要点：

- `behaviorType: 5` 会先进入锁定状态，再沿锁定方向高速冲刺。
- `lockDelay` 控制等待多久后冲刺。
- `warningTime` 会显示预警线，建议与 `lockDelay` 接近。

## 爆炸导弹

```json
{
  "id": "missile_explode_1005",
  "behaviorType": 9,
  "prefabPath": "Assets/Prefabs/Missiles/missile_11_explode.prefab",
  "bodyRes": "Assets/Art/Sprites/Bullets/missile_11.png",
  "speed": 1.7,
  "lifeTime": 4.2,
  "explodeTime": 2.6,
  "explodeRadius": 1.15,
  "warningTime": 0.9
}
```

要点：

- `behaviorType: 9` 会按 `explodeTime` 定时爆炸。
- `explodeRadius` 是爆炸判定半径。
- `warningTime` 会在爆炸前显示范围提示。

## Boss 阶段挂载示例

```json
{
  "id": "boss_helicopter_04_phase_01",
  "bossId": "boss_helicopter_04",
  "displayName": "LOADOUT 4  直升机 BOSS 04",
  "triggerHpPercent": 1,
  "attackInterval": 1.07,
  "burstCount": 2,
  "burstInterval": 0.22,
  "movementRange": { "x": 1.22, "y": 0.14 },
  "movementSpeed": 1.14,
  "bulletPatternIds": [
    "heli_bullet_single",
    "heli_bullet_spread_2",
    "heli_missile_straight_2",
    "heli_missile_explode_2"
  ]
}
```

要点：

- `bulletPatternIds` 同时支持 `bulletPatterns` 和 `missilePatterns` 的 id。
- Boss 会按列表顺序轮流发射，每次 burst 推进一次。
- 新 Boss 只要配置 `enemy.prefabPath` 和对应 `bossPhases` 即可接入关卡。

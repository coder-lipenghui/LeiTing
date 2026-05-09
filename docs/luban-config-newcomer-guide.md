# Luban 配置新人引导

本文面向第一次接触本项目配置的同学。当前项目使用 Luban 工程生成运行时配置，源表在 `Luban/Datas`，生成后的运行时 JSON 在 `Assets/Resources/Luban`，生成后的 C# 表代码在 `Assets/Scripts/Config/LubanGenerated`。运行时由 `ConfigManager` 通过 Luban 的 `cs-simple-json` 代码读取配置。

## 零、目录结构

日常配表主要看这几个位置：

- `Luban/Datas/*.xlsx`：业务源表，当前已经拆成一表一文件，例如 `Bullet.xlsx`、`Enemy.xlsx`、`WaveSpawn.xlsx`。
- `Luban/Datas/__tables__.xlsx`：Luban 表注册表，记录每张表的表名、数据类型、输入 Excel、输出模式和分组。日常改数据不需要动它；新增全新表时才需要加一行。
- `Luban/Datas/__beans__.xlsx`、`Luban/Datas/__enums__.xlsx`：保留给公共结构和枚举，目前业务表主要通过各自 Excel 表头读取结构。
- `Luban/Defines/__root__.xml`：Luban XML 根定义；当前主要由 `luban.conf` 和 `__tables__.xlsx` 管理表结构。
- `Luban/luban.conf`：Luban 工程配置。
- `Luban/gen.sh`：生成脚本，会同时输出 `cs-simple-json` C# 代码和 JSON 数据。
- `Assets/Scripts/Config/LubanConfigLoader.cs`：运行时加载入口，用生成的 `cfg.Tables` 读取 JSON，再转换成项目现有 `GameConfig`。

## 一、核心关系

从关卡到战斗对象的关系可以按下面理解：

```text
Level
  -> 背景图 / 背景滚速 / 背景音乐

Level
  -> Wave(levelId 为空表示所有关卡通用)
      -> WaveSpawn
          -> Enemy
              -> bulletPatternId
                  -> BulletPattern -> Bullet
                  -> MissilePattern -> Missile
              -> BossPhase
                  -> BossPhasePattern
                      -> BulletPattern -> Bullet
                      -> MissilePattern -> Missile

Enemy
  -> EnemyDrop
      -> PickupItem
```

通俗说：

- `Bullet` / `Missile` 是“弹体本身”，决定外观、伤害、速度、生命周期、碰撞尺寸等。
- `BulletPattern` / `MissilePattern` 是“怎么发射”，决定挂点、角度、数量、散射、瞄准、连发等。
- `Enemy` 是“飞机或 Boss 的基础属性”，决定血量、速度、默认攻击模式、分数、预制体和掉落。
- `Wave` / `WaveSpawn` 是“关卡什么时候刷什么怪”。
- `Level` 决定本关名字和本关 Boss。
- `BossPhase` / `BossPhasePattern` 决定 Boss 在不同血量阶段使用哪些子弹或导弹模式。
- `EnemyDrop` / `PickupItem` 决定怪物死亡后掉什么。

## 二、改表和生成

1. 打开要修改的业务表，例如：
   - 子弹：`Luban/Datas/Bullet.xlsx`
   - 导弹：`Luban/Datas/Missile.xlsx`
   - 敌机 / Boss：`Luban/Datas/Enemy.xlsx`
   - 关卡：`Luban/Datas/Level.xlsx`
   - 波次：`Luban/Datas/Wave.xlsx`
   - 刷怪组：`Luban/Datas/WaveSpawn.xlsx`
   - 掉落：`Luban/Datas/EnemyDrop.xlsx`、`Luban/Datas/PickupItem.xlsx`
2. 在对应 sheet 里新增或修改数据。每张业务表前三行是 Luban 表头：
   - 第 1 行 `##var`：字段名。
   - 第 2 行 `##type`：字段类型。
   - 第 3 行 `##`：字段说明。
   - 第 4 行开始才是正式数据。
3. 运行 Luban 生成运行时代码和 JSON：

```bash
bash Luban/gen.sh
```

4. 生成的 JSON 会落到 `Assets/Resources/Luban`，生成的 C# 表代码会落到 `Assets/Scripts/Config/LubanGenerated`，进入 Unity 后运行时会自动优先读取这些表。

`gen.sh` 可以在项目根目录执行，也可以在 `Luban` 目录里执行。它会自动定位本项目内置的 `Luban/Luban/Luban.dll`，不需要再手动设置 `LUBAN_DLL`。

脚本当前使用 Luban 官方 `Csharp_Unity_json` 示例里的 `-c cs-simple-json -d json` 组合，Unity 运行时依赖 `Packages/manifest.json` 里的 `com.code-philosophy.luban` 包提供 `Luban.SimpleJSON`。不要手改 `Assets/Scripts/Config/LubanGenerated` 下的文件；这些文件每次生成都会被覆盖。

`__tables__.xlsx` 当前已经把每张业务表的 `input` 指向拆分后的 Excel，例如 `Bullet@Bullet.xlsx`。如果新增全新表，需要同步新增业务 Excel，并在 `__tables__.xlsx` 加一行；如果只是新增子弹、敌机、关卡、掉落等普通数据，不需要动 `__tables__.xlsx`。

Luban 会在生成阶段做基础结构和类型校验。如果填错字段类型、漏掉输入表或表头不匹配，应优先修表，不要在代码里兜底。

## 三、新增子弹

新增普通子弹或玩家弹：

1. 在 `Luban/Datas/Bullet.xlsx` 的 `Bullet` sheet 新增一行，填写唯一 `id`。
2. `owner` 填 `Player` 或 `Enemy`，它会影响层级和碰撞逻辑。
3. `firePattern` 对玩家武器很重要，常用 `Single`、`Double`、`Spread`、`Laser`。
4. 填 `spritePath`、`damage`、`speed`、`lifetime`、`sizeX`、`sizeY`。
5. 如果是激光，`firePattern` 填 `Laser`，`laserLength` 填长度，`pierceCount` 可填 `-1` 表示不因命中回收。

让玩家默认装备新子弹：

1. 到 `Luban/Datas/Player.xlsx` 的 `Player` sheet。
2. 把 `defaultBulletId` 改成新 `Bullet.id`。

让敌机使用新子弹：

1. 先在 `Luban/Datas/BulletPattern.xlsx` 的 `BulletPattern` sheet 新增一行。
2. `bulletId` 填新 `Bullet.id`。
3. 配 `patternType`、`firePointGroup`、`bulletCount`、`spreadAngle`、`aimAtPlayer` 等。
4. 再把 `Enemy.bulletPatternId` 或 `WaveSpawn.attackPatternId` 填成这个 `BulletPattern.id`。

## 四、新增导弹

新增导弹分两步：

1. 在 `Luban/Datas/Missile.xlsx` 的 `Missile` sheet 新增导弹本体，填写 `id`、`behaviorType`、`prefabPath`、`bodyRes`、`damage`、`speed`、`lifeTime` 等。
2. 在 `Luban/Datas/MissilePattern.xlsx` 的 `MissilePattern` sheet 新增发射模式，`missileId` 填上一步的 `Missile.id`。

常用 `behaviorType`：

- `1`：直线导弹。
- `3`：弱追踪导弹，关注 `turnSpeed`、`trackTime`、`isLoopTrack`。
- `5`：锁定后冲刺，关注 `lockDelay`、`warningTime`、`maxSpeed`。
- `9`：定时爆炸，关注 `explodeTime`、`explodeRadius`、`warningTime`。

把导弹装备给敌机或 Boss：

- 普通敌机：把 `Enemy.bulletPatternId` 填成 `MissilePattern.id`。
- 某个刷怪组临时覆盖：把 `WaveSpawn.attackPatternId` 填成 `MissilePattern.id`。
- Boss 阶段：在 `Luban/Datas/BossPhasePattern.xlsx` 的 `BossPhasePattern` sheet 新增一行，`patternId` 填 `MissilePattern.id`。

代码会先按 `BulletPattern` 查找，找不到再按 `MissilePattern` 查找，所以这几个字段都可以放子弹模式或导弹模式 ID。

## 五、新增飞机

新增普通敌机：

1. 在 `Luban/Datas/Enemy.xlsx` 的 `Enemy` sheet 新增一行。
2. `id` 使用唯一 ID，例如 `enemy_f`。
3. 填 `prefabPath`、`hp`、`moveSpeed`、`attackInterval`、`score`。
4. `bulletId` 填兜底单发子弹。
5. `bulletPatternId` 填默认攻击模式，可以是 `BulletPattern.id` 或 `MissilePattern.id`。
6. 如果要它出现在关卡里，在 `Luban/Datas/WaveSpawn.xlsx` 的 `WaveSpawn` sheet 新增或修改一行，`enemyId` 填这个新敌机 ID。

新增 Boss：

1. 在 `Luban/Datas/Enemy.xlsx` 的 `Enemy` sheet 新增一行，`id` 建议以 `boss` 开头，例如 `boss_13`。代码用这个前缀识别 Boss。
2. 填 Boss 预制体、血量、分数、兜底子弹等。
3. 在 `Luban/Datas/BossPhase.xlsx` 的 `BossPhase` sheet 给它添加至少一个满血阶段，`triggerHpPercent` 填 `1`。
4. 在 `Luban/Datas/BossPhasePattern.xlsx` 的 `BossPhasePattern` sheet 给阶段挂子弹或导弹模式。
5. 在 `Luban/Datas/Wave.xlsx` 的 `Wave` sheet 给目标关卡新增或修改 Boss 波次。
6. 在 `Luban/Datas/WaveSpawn.xlsx` 的 `WaveSpawn` sheet 把 Boss 波次的 `enemyId` 填成新 Boss ID。

## 六、给飞机装备子弹、导弹和挂点

普通敌机有三层优先级：

1. `WaveSpawn.attackPatternId`：本次刷怪专用攻击模式，优先级最高。
2. `Enemy.bulletPatternId`：敌机默认攻击模式。
3. `Enemy.bulletId`：兜底单发子弹。

Boss 装备在阶段上：

1. `BossPhase` 定义阶段，例如满血、65% 血、35% 血。
2. `BossPhasePattern` 用 `bossPhaseId` 关联阶段，用 `sort` 决定循环顺序。
3. `patternId` 可填子弹模式或导弹模式。

挂点规则：

- 飞机或 Boss prefab 下需要有 `FirePoints` 节点。
- `firePointGroup` 会查找 `FirePoints/<groupName>`。
- 如果 group 下有多个子节点，每个子节点都会发一次同一个 pattern。
- 如果找不到挂点，会从飞机当前位置发射。

## 七、配置关卡、小怪和 Boss

新增关卡：

1. 在 `Luban/Datas/Level.xlsx` 的 `Level` sheet 新增一行，填写 `id`、`displayName`、`backgroundSpritePath`、`backgroundScrollSpeed`、`bgmPath`。
2. 在 `Luban/Datas/Wave.xlsx` 的 `Wave` sheet 新增关卡波次：
   - `levelId` 填关卡 ID，例如 `level_13`。
   - `levelId` 留空表示所有关卡都会使用这波。
   - `startTime` 是关卡开始后第几秒触发。
3. 在 `Luban/Datas/WaveSpawn.xlsx` 的 `WaveSpawn` sheet 给波次添加刷怪组：
   - `waveId` 指向 `Wave.id`。
   - `sort` 决定同一波次内的执行顺序。
   - `enemyId` 填小怪 ID。
   - `count` 和 `interval` 决定刷多少、间隔多久。
   - `startPositionX/Y` 决定第一架飞机出生点，多架会自动横向展开。
   - `movementPath` 常用 `Straight`、`Hold`、`StopAndLeave`、`Sine`、`DriftLeft`、`DriftRight`。
4. Boss 波次通常这样配：
   - `Wave.levelId` 填对应关卡。
   - `WaveSpawn.enemyId` 直接填 Boss 的 `Enemy.id`。
   - Boss 如何出现、何时出现、刷哪一个，都交给 `Wave` 和 `WaveSpawn`。

关卡事件在 `Luban/Datas/StageEvent.xlsx` 的 `StageEvent` sheet：

- `levelId` 填关卡 ID，留空表示所有关卡通用。
- `message` 可以使用 `{LEVEL}`、`{MAX_LEVEL}`、`{BOSS_ID}`、`{BOSS}` 占位符。
- `clearEnemyBullets` 为 `true` 时会清除敌方子弹和导弹。

## 八、配置掉落

先定义掉落物：

1. 在 `Luban/Datas/PickupItem.xlsx` 的 `PickupItem` sheet 新增一行。
2. `id` 是掉落物 ID，例如 `coin`、`heal`、`shield`。
3. `itemType` 决定行为，当前常用 `Star`、`Coin`、`Magnet`、`Bomb`、`Heal`、`Shield`。
4. 填 `spritePath`、`lifetime`、`driftSpeed`、`pickupRadius`、`visualScale`。

再挂到敌机：

1. 在 `Luban/Datas/EnemyDrop.xlsx` 的 `EnemyDrop` sheet 新增一行。
2. `enemyId` 填掉落来源敌机或 Boss ID。
3. `itemId` 填 `PickupItem.id`。
4. `count` 填数量。
5. `dropOnce` 填 `true` 表示本关只掉一次，适合磁铁、炸弹、护盾等强道具。

## 九、提交前检查

- 每张表的 `id` 保持唯一。
- 引用 ID 必须存在：`enemyId`、`bulletId`、`missileId`、`patternId`、`itemId`。
- 不要删除前三行 Luban 表头，也不要改 `##var`、`##type`、`##` 这几个标记。
- 新增全新业务表时，同步维护 `Luban/Datas/__tables__.xlsx` 的 `full_name`、`value_type`、`read_schema_from_file`、`input`、`mode`、`group`。
- `Wave.levelId` 和 `StageEvent.levelId` 留空会影响所有关卡。
- Boss 的 `Enemy.id` 必须以 `boss` 开头。
- Boss 至少有一个 `BossPhase.triggerHpPercent = 1` 的阶段。
- 子弹或导弹 pattern 的 `firePointGroup` 要和 prefab 上的 `FirePoints` 子节点对上。
- 配完表后运行 `bash Luban/gen.sh`，再进 Unity 验证控制台没有配置缺失警告。

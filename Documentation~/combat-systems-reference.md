# Combat 系统参考

本文档记录 `BattleSimulation.Step()` 的权威阶段顺序、Core 系统职责和 `BattleWorld` flush 合同。它描述发布包本身，不包含消费项目的关卡或玩法编排。

## 阅读约定

- `BattleSimulation.Step()` 是有效 Tick 入口。
- internal `BattleSimulationPhasePipeline` 保存静态阶段表。
- `BattleWorld` 是状态和 flush 的权威 facade；内部 resolver 承接具体 spawn/action/effect/death/snapshot 规则。
- 普通攻击使用基础能力模型，没有独立 `AttackSystem`。
- Core 产生规则事实；Runtime/Unity 只消费和表现。

## Tick 顺序

| 顺序 | 阶段 | 主要职责与输出 |
| --- | --- | --- |
| 1 | `FlushSpawnCombatantCommands` | 创建单位，写 `UnitSpawned`，初始 facing 为 `BattleVector2.Right` |
| 2 | `StatusSystem.Run` | 推进状态持续时间、周期效果和过期，写 `StatusExpired` 或排 status effect |
| 3 | `FlushEffectCommands.Status` | 结算状态 effect、reaction、death check |
| 4 | `VictorySystem.TryGetWinningTeam` | 状态阶段后 victory checkpoint |
| 5 | `ProjectileEmitterSystem.Run` | 推进 emitter 并排 `SpawnProjectileCommand` |
| 6 | `FlushSpawnProjectileCommands` | 创建 projectile，建立 `ProjectileId` 索引，写 `ProjectileSpawned` |
| 7 | `ProjectileSystem.Run` | 移动、culling、连续 sweep、命中与生命周期，写 projectile facts 并排 impact effects |
| 8 | `FlushEffectCommands.Projectile` | 结算 projectile impact effects、reaction、death check |
| 9 | `VictorySystem.TryGetWinningTeam` | projectile 阶段后 victory checkpoint |
| 10 | `InputIntentSystem.Run` | 标准化输入；`Hold`、`MoveToPosition`、`Garrison` 可在 release 前结束 active action |
| 11 | `UnitActionExecutionSystem.Run` | 释放到期 ability effect frames，写 `AbilityReleased` / `AbilityEnded` |
| 12 | `FlushEffectCommands.ActionRelease` | 结算 action release effects、reaction、death check |
| 13 | `VictorySystem.ActionRelease` | action release 后 victory checkpoint |
| 14 | `TargetingSystem.Run` | 按 intent 获取、保留、释放或锁定目标 |
| 15 | `MovementSystem.Run` | 构造同 Tick movement frame，提交直线移动或可选局部避让 |
| 16 | `AiDecisionSystem.Run` | 更新轻量 `BrainState` |
| 17 | `AbilitySystem.Run` | 推进 cooldown，自动或显式选择能力并排 action |
| 18 | `FlushActionCommands` | 启动 active ability action，写 `AbilityStarted` |
| 19 | `FlushEffectCommands.Action` | 保留 action start 同阶段的 effect flush；当前 ability effects 不在 start 时释放 |
| 20 | `VictorySystem.TryGetWinningTeam` | Tick 末 victory checkpoint |
| 21 | `ApplyStructuralCommands` | 应用延迟 entity/component 结构变化 |

关键顺序约束：

- `InputIntentSystem` 必须位于 `UnitActionExecutionSystem` 之前，使打断类 intent 可以阻止同 Tick effect frame release。
- status、projectile 和 action release 分别拥有自己的 effect flush 与 victory checkpoint。
- `FinishBattle` 写 `BattleEnded` 后清理仍 active 的 projectile，并写 `ProjectileDestroyed`。
- 即使提前判胜，也必须执行结构命令收尾。

## Core 系统

### `StatusSystem`

读取 `StatusComponent` 和生命状态；推进持续时间与周期倒计时。周期伤害只排 `BattleEffectCommand`，不直接改血。死亡 owner 的状态被移除；正常过期写 `StatusExpired`。详细规则见 [Status 系统](status-system-reference.md)。

### `ProjectileEmitterSystem`

读取 emitter、source/target position 和 `TicksPerSecond`，按 pattern、direction mode 与发射节奏生成 projectile command：

- `FollowSource` 从当前 source 位置发射。
- `FixedPosition` 从记录 origin 发射。
- `Single` 支持 `FixedDirection` / `TargetDirection`。
- `Circle` 使用 fixed-backed 圆周方向。
- payload speed 表示战斗单位/秒，生成 velocity 时除以 TPS。

### `ProjectileSystem`

读取 projectile runtime 和 `BattleUnitQuery` target snapshots；负责：

1. 保存 start，按 velocity 写回 Tick 终点并写 `ProjectileMoved`。
2. 先执行 culling。
3. 用 start/end/radius 执行固定点 Circle-Circle sweep。
4. 按 `(ProjectileId, Fraction, TargetUnitId)` 选择稳定候选。
5. 跳过生命周期内已经命中的 `UnitId`。
6. 写 `ProjectileHit`，按顺序排 impact effects。
7. 根据 `DestroyOnFirstHit` 或 `Pierce(maxHitCount)` 决定销毁。
8. 在最后 lifetime Tick 允许移动和命中，再处理过期。

系统不直接改血；命中效果在后续 projectile effect flush 结算。详细几何合同见 [Spatial 碰撞系统](spatial-collision-system-guide.md)。

### `InputIntentSystem`

把 `BattleInputFrame` 标准化为单位 `IntentComponent`。当前 intent：

- `Auto`
- `Hold`
- `MoveToPosition`
- `Garrison`
- `FocusTarget`
- transient `UseAbility`

空输入会为没有显式 intent 的存活单位补 `Auto`。`Hold`、`MoveToPosition`、`Garrison` 会结束 active action并清理自动目标/追踪；`Garrison` 让单位退出战场候选，恢复 `Auto` 后重新部署。

### `TargetingSystem`

统一通过 `BattleUnitQuery` 选择候选：

- `Auto` 首次在警戒范围内选择最近敌人，距离平局按 `UnitId`。
- 合法目标离开警戒范围后仍保持粘性。
- 攻击范围外且允许移动时累计接敌进展；超时后短期排除旧目标并同 Tick 重选。
- 无合法目标时可以消费有效攻击者刺激。
- `FocusTarget` 只尝试指定敌人，无效时不回退自动目标。

目标、朝向与 action lock 分离：允许选中目标不等于允许转向。

### `MovementSystem`

读取 intent、position/radius、team、effective stats、目标、action locks 和 `BattleConfig.LocalAvoidanceEnabled`：

- `Auto` / `FocusTarget` 向接战范围移动。
- `MoveToPosition` 向确定性目标点移动。
- 没有战斗目标的 `Auto` 可用全局最近敌人作为直线推进引导，但不写 `TargetComponent`。
- 默认关闭 LocalAvoidance，直接按 `UnitId` 提交 `PreferredStep`。
- 显式开启后才执行 overlap recovery、grid 邻居和 RVO-like 候选。

移动和距离计算使用 `BattleScalar` / `BattleVector2`。

### `AiDecisionSystem`

只更新 `Idle / Chase / Attack / Dead` brain state，不排 action、不结算 effect、不访问 Unity。

### `AbilitySystem`

每 Tick 先推进 cooldown，再按 intent 决策：

- `Auto` / 有效 `FocusTarget` 按技能列表顺序选择可用技能，再回退基础能力。
- `UseAbility` 只尝试指定 ability index 和 target，不回退自动技能。
- 目标解析与显式校验统一经过 `AbilityTargeting`。
- 允许开始时排 `BattleActionCommand.UseAbility`，不直接结算效果。

### `UnitActionExecutionSystem`

读取 active `UnitActionComponent`：

- action start 保存 ability、target、`StartedTick`、`ReleaseTick`、`EndTick`、`ReleasedFrameCount` 和 `BattleActionLocks`。
- 到期后复查 source ability、target 和 range。
- 按 frame `TickOffset`、`Order` 和原始顺序释放所有已到期 frame。
- 每个 frame 内按 effect 顺序排 command，并写一次 `AbilityReleased`。
- 到 `EndTick` 写 `AbilityEnded` 并清除 action。
- 同 Tick release/end 时先 release，再 end。

### `VictorySystem`

仅在 `BattleConfig.AutomaticVictoryEnabled` 开启时运行自动判胜。当前合同是：

- 至少一个单位存活。
- 所有存活单位只属于同一队伍。

没有存活单位不会产生获胜队伍。复杂目标、平局、多阶段或跨房间胜负应由消费项目编排，或在明确合同后扩展 Core。

## `BattleWorld` Flush

| Flush | 输入 | 结果 |
| --- | --- | --- |
| `FlushSpawnCombatantCommands` | `SpawnCombatantCommand` | `BattleSpawnResolver` 创建单位组件并写 `UnitSpawned` |
| `FlushSpawnProjectileCommands` | `SpawnProjectileCommand` | 创建 projectile、分配稳定 ID、写 `ProjectileSpawned` |
| `FlushActionCommands` | `BattleActionCommand.UseAbility` | 校验目标、消耗 cooldown、启动 action、写 `AbilityStarted` |
| `FlushEffectCommands` | `BattleEffectCommand` | 结算 primary effects、reaction effects、death check 和尾段 kill reaction |
| `DestroyActiveProjectiles` | active projectile | battle end 时统一写销毁事实并排结构销毁 |
| `ApplyStructuralCommands` | destroy/add/remove | destroy 优先；同批对已销毁 entity 的后续 add/remove 被跳过 |

`FlushEffectCommands` 是完整 batch 边界：

1. drain 当前 primary effects。
2. drain reaction effects。
3. drain death checks 并写 `UnitDied`。
4. 处理 death 产生的 `AfterEnemyKilled` 尾段 reaction。
5. 再 drain 一次尾段 reaction 造成的 death checks。

某个队列被 drain 后新排入的同类命令留到下一次对应 flush。reaction effect 默认 suppress nested reaction，避免无限递归。

## Effect 统一链路

当前 effect 类型：

- `Damage`
- `Heal`
- `ApplyStatus`
- `SpawnProjectileEmitter`
- `AreaEffect`

统一链路：

```text
BattleEffectDefinition
  -> BattleEffectRuntimeDataFactory
  -> BattleEffectData
  -> BattleEffectCommandFactory
  -> BattleEffectCommand
  -> BattleEffectResolver
```

Ability effect frame、projectile impact、status reaction 和 area child effect 共用该链路。新增 effect 时必须扩展整条链路，不在单个入口增加私有结算分支。

## 确定性与性能

- 规则数值使用 `BattleScalar` / `BattleVector2`。
- 系统候选使用稳定领域 ID 排序。
- `BattlePerformanceRecorder` 是可选诊断工具，不参与规则。
- 性能样本名称与 pipeline 阶段对应；改变阶段必须更新顺序测试。
- `SpatialQueryWorkspace`、projectile scratch 和系统 workspace 应在每场战斗复用，避免热路径分配。

## 文档维护

以下变化必须更新本文档：

- 新增、删除、重命名或重排 Core 系统。
- 修改 `BattleSimulationPhasePipeline`、victory checkpoint 或性能阶段名称。
- 修改任一 system 的输入、输出或关键语义。
- 修改 `BattleWorld` flush、effect batch、reaction 或 death check 顺序。
- 修改 unit facing、projectile direction/hit/lifecycle 或 Runtime/Unity 职责。
- 修改通用 Authoring 字段时，同时更新 [Authoring 与表现层](authoring-and-presentation.md)。

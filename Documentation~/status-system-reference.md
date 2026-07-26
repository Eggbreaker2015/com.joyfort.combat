# Status 系统参考

Status 是单位上的规则状态，不是 UI 标记。它承载生命周期、周期 effect、叠层、modifier、trigger、condition gate 和 reaction。

## 职责拆分

| 类型 | 职责 |
| --- | --- |
| `StatusSystem` | 推进现有状态的 duration、periodic effect 和过期 |
| `StatusApplicationDataFactory` | `StatusDefinition` 到 `StatusApplicationData` |
| `StatusApplicationResolver` | 创建 runtime definition、应用、刷新和叠层 |
| `BattleModifierResolver` | damage 或 scalar stat modifier 计算 |
| `BattleStatResolver` | base stats + owner statuses 得到 effective stat |
| `StatusTriggerResolver` | 明确 hook 上扫描 trigger、执行 condition、排 reaction |
| `BattleEffectResolver` | 校验 ApplyStatus 参与者、调用 application resolver、写事实 |

不要把应用/叠层放回 `StatusSystem`，也不要让 modifier 或 trigger 扫描散落到显示层。

## 数据模型

| 层级 | 类型 | 内容 |
| --- | --- | --- |
| Definition | `StatusDefinition` | id、polarity、duration、interval、periodic damage、stack、modifier、trigger |
| Runtime data | `StatusApplicationData` | ApplyStatus effect 使用的预转换数据 |
| Runtime definition | `StatusRuntimeDefinition` | instance 共享的不可变规则 payload |
| Runtime instance | `StatusInstance` | source、剩余时间、周期倒计时和 `StackCount` |
| Unit component | `StatusComponent` | 当前单位的 status instances |
| Unity authoring | `StatusConfigAsset` | 秒制生命周期、叠层、modifier、condition 和 reaction |

同一个单位身上同 id 只有一份 runtime instance；不同 id 可以并存。

## 生命周期

`StatusSystem.Run` 位于 Tick 的 status 阶段：

1. 遍历 status owner 快照。
2. owner 已死亡时移除整个 `StatusComponent`，不写 `StatusExpired`。
3. 递减 duration 和 periodic countdown。
4. periodic countdown 到期且 source 存活时，排 status effect。
5. duration 到期时写 `StatusExpired` 并移除。
6. 无剩余状态时移除 component。

周期伤害使用 `BattleEffectContext.Status(statusId, Damage)`，使日志、事件、modifier 和显示都能识别来源。

## 应用与叠层

首次应用：

- 创建不可变 runtime definition。
- `DurationRemainingTicks = DurationTicks`。
- `TicksUntilNextPeriodicEffect = TickIntervalTicks`，不会在应用同 Tick 立即触发周期效果。
- `StackCount = 1`。
- 成功后写 `StatusApplied`。

当前 `RefreshDurationAndAddStack`：

- 重复同 id 时使用新应用的 source、时间、modifier 和 trigger payload。
- 刷新完整持续时间与周期倒计时。
- 新层数为 `min(old.StackCount + 1, MaxStacks)`。

新增 stack policy 时需要同步 definition、runtime data、resolver、Authoring、converter、validator 和测试。

## Modifier

`BattleModifierTarget`：

| Target | 读取位置 | 当前 key |
| --- | --- | --- |
| `Damage` | damage effect resolution | source `DamageDealt`、target `DamageTaken` |
| `Stat` | 属性具体使用点 | `MoveSpeed`、`MaxHealth` |

Operation 顺序按每个 modifier key/pass 独立执行：

1. `base + sum(Flat)`
2. 乘以 `1 + sum(PercentAdd)`
3. `Override`
4. `MinClamp / MaxClamp`

`Flat` 和 `PercentAdd` 乘 `StackCount`；`Override`、`MinClamp`、`MaxClamp` 不按层数放大。

Damage 先执行 source `DamageDealt` pass，再以结果执行 target `DamageTaken` pass。Stat 只读取 owner 自身 statuses，不改写 base component。

`MaxHealth` effective value至少为 1，并按 deterministic half-up 转成整数。提升 MaxHealth 不自动治疗；降低或 status 过期后，当前生命会 clamp 到新的上限。

Heal 不读取 damage modifier、不触发 damage status trigger，也不进入 death check。

## Trigger 与 Reaction

当前 timing：

| Timing | Owner | Source | Target |
| --- | --- | --- | --- |
| `AfterDamageDealt` | 造成伤害者 | 造成伤害者 | 受伤者 |
| `AfterDamageTaken` | 受伤者 | 造成伤害者 | 受伤者 |
| `AfterEnemyKilled` | 击杀者 | 击杀者 | 死亡单位 |

扫描顺序：

1. 检查 `BattleEffectTriggerPolicy`。
2. 读取 `context.Owner` statuses。
3. timing 匹配后执行 `BattleConditionProgramEvaluator`。
4. 根据 `Self / Source / Target` 解析 reaction target。
5. 排入 reaction effect queue。

Reaction effect 使用 `BattleEffectTriggerPolicy.SuppressReactions`，不会递归触发 nested reactions。

Effect batch 先 drain primary，再 reaction，再 death check。Death 产生的 `AfterEnemyKilled` reaction 在同一 flush 的尾段处理；尾段 reaction 造成的 death 再 drain 一次。

## Authoring

`StatusConfigAsset` 主要字段：

- id / polarity
- duration seconds / tick interval seconds
- periodic damage
- `MaxStacks` / `StatusStackPolicy`
- target-aware modifiers
- triggers、condition match mode、conditions 和 reaction effects

Reaction effects 复用通用 `BattleEffectConfig`。Status reaction scope 允许 direct Heal。

Validator 必须拒绝：

- 非法 stack、时间或 periodic damage。
- 不支持的 modifier target/stat/operation。
- 同一 key 的重复 Override。
- `MinClamp > MaxClamp`。
- 场景图中可同时存在 statuses 的跨状态 Override/clamp 冲突。
- condition 编译失败或递归 effect 引用。

## 扩展规则

- 新生命周期/stack：修改 `StatusSystem` 或 `StatusApplicationResolver` 的明确边界。
- 新 stat modifier：先定义 use-site resolver，不隐式改写 component。
- 新 trigger timing：先定义规则 hook 和 `Owner / Source / Target`。
- Before 类 trigger：必须进入对应计算窗口，不能塞进 after reaction queue。
- 新 reaction effect：扩展统一 effect pipeline，不为 status 建私有 effect switch。
- Dispel、immune、aura、tag 等能力要先明确属于 lifecycle、filter、effect 还是 condition。

Condition program 细节见 [Battle Condition](battle-condition-reference.md)。

# Joyfort Combat Framework 文档

本目录是 `com.joyfort.combat` 随 UPM 包发布的跨项目架构文档。内容只描述包内 `Combat.*` 程序集、公开集成边界和通用 Authoring，不依赖任何具体玩法、UI、依赖注入容器或消费项目目录。

## 阅读路径

第一次接入建议按以下顺序阅读：

1. [架构总览](architecture-overview.md)
2. [Combat 系统参考](combat-systems-reference.md)
3. [Authoring 与表现层](authoring-and-presentation.md)
4. 按功能进入 [Spatial 碰撞系统](spatial-collision-system-guide.md)、[Status 系统](status-system-reference.md) 或 [Battle Condition](battle-condition-reference.md)

## 文档职责

| 文档 | 负责内容 |
| --- | --- |
| [架构总览](architecture-overview.md) | 分层、依赖方向、运行入口、事件边界、扩展原则与已知限制 |
| [Combat 系统参考](combat-systems-reference.md) | `BattleSimulation.Step()` 顺序、系统输入输出、victory checkpoint 和 `BattleWorld` flush |
| [Authoring 与表现层](authoring-and-presentation.md) | Unity/Tuanjie asset 转换、校验、Runtime 显示桥、`BattleEvent` 到 `VisualCommand` 映射 |
| [Spatial 碰撞系统](spatial-collision-system-guide.md) | 固定点几何、过滤、稳定排序、uniform grid、projectile 连续碰撞和静态空间地图边界 |
| [Status 系统](status-system-reference.md) | 生命周期、叠层、modifier、trigger、reaction 与 authoring |
| [Battle Condition](battle-condition-reference.md) | compiled condition program、operand/filter、求值上下文与扩展合同 |

## 真相来源与维护

- 当前版本源码和包内测试是最终真相来源。
- 本目录描述发布包本身；消费项目的关卡流程、队伍编排、经济、奖励、UI 和场景生命周期应记录在消费项目文档中。
- 修改 Core 系统、Tick 顺序、flush、显示映射、Authoring schema、Spatial、Status 或 Condition 合同时，必须同步更新对应包内文档。
- `Documentation~` 使用 Unity Package Manager 的隐藏文档目录约定，不需要也不应生成 `.meta` 文件。

## 测试

在消费项目的 `Packages/manifest.json` 中加入：

```json
{
  "testables": [
    "com.joyfort.combat"
  ]
}
```

然后运行 `Combat.Tests` EditMode tests。Tuanjie 命令行运行 EditMode tests 时不要传 `-quit`。

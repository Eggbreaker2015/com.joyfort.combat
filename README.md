# Joyfort Combat Framework

`com.joyfort.combat` 是可跨项目复用的确定性战斗框架。包内只包含通用战斗规则、无显示单场 Runtime、显示命令桥、Unity/Tuanjie Authoring 与通用编辑器工具；不包含 AutoDefense、GameApp、GameUI 或具体游戏内容。

## 程序集

- `Combat.Foundation`：通用事件与诊断基础设施，纯 C#。
- `Combat.Core`：确定性战斗事实、ECS 状态和系统管线，纯 C#。
- `Combat.Runtime`：无显示 `BattleInstance`、战斗输出和显示命令桥，纯 C#。
- `Combat.Unity`：ScriptableObject Authoring、Unity 显示、日志和通用 standalone preview 适配。
- `Combat.Unity.Editor`：Authoring 校验、空间地图、Sprite 动画和 standalone demo 构建工具，仅 Editor。

## 安装

当前 MyCombatSystem 仓库通过 `Packages/com.joyfort.combat` 以 embedded package 使用；独立版本发布在 `https://github.com/Eggbreaker2015/com.joyfort.combat`。其他项目通过 Git 安装时，还必须在项目 `Packages/manifest.json` 中安装 `com.mrdav30.fixedmathsharp.lean` `v5.0.1`；该依赖当前来自 Git，因此消费项目需要提供对应 Git URL。

新项目的 `Packages/manifest.json` 可加入：

```json
{
  "dependencies": {
    "com.mrdav30.fixedmathsharp.lean": "https://github.com/mrdav30/FixedMathSharp-Unity.git?path=/com.mrdav30.fixedmathsharp.lean#v5.0.1",
    "com.joyfort.combat": "https://github.com/Eggbreaker2015/com.joyfort.combat.git#v0.1.0"
  }
}
```

升级时把 `v0.1.0` 改为新的发布 tag；不要让生产项目长期跟随可变分支。

包的生产程序集不依赖 `GameApp.*`、`GameUI`、Joyfort UI Framework 或 VContainer。具体玩法应在消费项目中通过 `BattleInstance` 或自己的 application gateway 组合。

## 迁移到新玩法项目

1. 安装包和 FixedMathSharp 依赖。
2. 在新项目自己的 `Assets` 下创建地牢、五人小队、房间流程、AI 指令和 UI 等上层模块；不要修改包来表达玩法流程。
3. 通过 `Combat.Unity` 的通用 authoring asset 构建单位、技能、状态、projectile 和 scenario；项目内容不放入包目录。
4. 纯 C# application 层可直接组合 `BattleInstance`，或实现类似 `IBattleGateway` 的项目契约。需要先复制同步事件视图再提交诊断时，使用 `*WithDeferredDiagnostics` 与 `CommitCurrentDiagnostics()` 配对调用。

当前仓库的 `Assets/GameApp`、`Assets/GameUI`、`Assets/GameApp/Content/AutoDefense` 和 `Assets/CombatSamples/Standalone` 都不是包的一部分，迁移到新项目时按需重新实现或复制内容，不能作为框架依赖。

## 测试

在消费项目的 `Packages/manifest.json` 的 `testables` 中加入 `com.joyfort.combat`，再运行 `Combat.Tests` EditMode tests。Tuanjie 命令行运行 EditMode tests 时不要传 `-quit`。

using System.Collections.Generic;
using Combat.Core.Battle;
using NUnit.Framework;

namespace Combat.Tests.Core
{
    public sealed class BattleInputFrameTests
    {
        [Test]
        public void Empty_HasNoCommands()
        {
            BattleInputFrame frame = BattleInputFrame.Empty;

            Assert.AreEqual(0, frame.Commands.Count);
        }

        [Test]
        public void Constructor_CopiesCommands()
        {
            var commands = new List<BattleInputCommand>
            {
                BattleInputCommand.Auto(new UnitId(1))
            };

            var frame = new BattleInputFrame(commands);
            commands.Add(BattleInputCommand.Auto(new UnitId(2)));

            Assert.AreEqual(1, frame.Commands.Count);
            Assert.AreEqual(BattleInputCommandType.Auto, frame.Commands[0].Type);
            Assert.AreEqual(new UnitId(1), frame.Commands[0].UnitId);
        }

        [Test]
        public void AutoCommand_StoresUnitId()
        {
            BattleInputCommand command = BattleInputCommand.Auto(new UnitId(7));

            Assert.AreEqual(BattleInputCommandType.Auto, command.Type);
            Assert.AreEqual(new UnitId(7), command.UnitId);
        }

        [Test]
        public void HoldCommand_StoresUnitId()
        {
            BattleInputCommand command = BattleInputCommand.Hold(new UnitId(7));

            Assert.AreEqual(BattleInputCommandType.Hold, command.Type);
            Assert.AreEqual(new UnitId(7), command.UnitId);
        }

        [Test]
        public void FocusTargetCommand_StoresSourceAndTargetUnitIds()
        {
            BattleInputCommand command = BattleInputCommand.FocusTarget(new UnitId(7), new UnitId(9));

            Assert.AreEqual(BattleInputCommandType.FocusTarget, command.Type);
            Assert.AreEqual(new UnitId(7), command.UnitId);
            Assert.AreEqual(new UnitId(9), command.TargetUnitId);
        }

        [Test]
        public void UseAbilityCommand_StoresAbilityIndexAndTargetUnitId()
        {
            BattleInputCommand command = BattleInputCommand.UseAbility(new UnitId(7), abilityIndex: 2, targetUnitId: new UnitId(9));

            Assert.AreEqual(BattleInputCommandType.UseAbility, command.Type);
            Assert.AreEqual(new UnitId(7), command.UnitId);
            Assert.AreEqual(2, command.AbilityIndex);
            Assert.AreEqual(new UnitId(9), command.TargetUnitId);
        }
    }
}

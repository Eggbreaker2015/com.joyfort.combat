using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Combat.Runtime.Display;
using Combat.Runtime.Runner;
using NUnit.Framework;

namespace Combat.Tests.Architecture
{
    public sealed class AssemblyReferenceGuardTests
    {
        [Test]
        public void Foundation_HasNoReferencesAndNoEngineReferences()
        {
            string json = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Foundation/Combat.Foundation.asmdef");

            Assert.That(json, Does.Contain("\"references\": []"));
            Assert.That(json, Does.Contain("\"noEngineReferences\": true"));
        }

        [Test]
        public void Core_ReferencesOnlyFoundationAndHasNoEngineReferences()
        {
            string json = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Combat.Core.asmdef");

            Assert.That(json, Does.Contain("\"Combat.Foundation\""));
            Assert.That(json, Does.Not.Contain("\"Combat.Runtime\""));
            Assert.That(json, Does.Not.Contain("\"Combat.Unity\""));
            Assert.That(json, Does.Contain("\"noEngineReferences\": true"));
        }

        [Test]
        public void PackageAssemblies_DoNotReferenceProjectOwnedAssemblies()
        {
            string[] files = Directory.GetFiles(
                "Packages/com.joyfort.combat",
                "*.asmdef",
                SearchOption.AllDirectories);
            string[] forbidden =
            {
                "GameApp",
                "GameUI",
                "UIFramework",
                "VContainer"
            };

            var offenders = new List<string>();
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void PackageProductionCode_DoesNotFriendOrReferenceGameProjectNamespaces()
        {
            string[] roots =
            {
                "Packages/com.joyfort.combat/Runtime",
                "Packages/com.joyfort.combat/Editor"
            };
            string[] forbidden =
            {
                "GameApp",
                "GameUI",
                "UIFramework",
                "Sirenix"
            };

            var offenders = new List<string>();
            foreach (string root in roots)
            {
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    foreach (string term in forbidden)
                    {
                        if (text.Contains(term))
                        {
                            offenders.Add($"{file} contains {term}");
                        }
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void CoreCode_DoesNotReferenceUnityRuntimeOrUnityFixedMathPackage()
        {
            string[] files = Directory.GetFiles("Packages/com.joyfort.combat/Runtime/Core", "*.cs", SearchOption.AllDirectories);
            string[] forbidden =
            {
                "using UnityEngine",
                "UnityEngine.",
                "FixedMathSharp.Unity",
                "FixedMathSharp-Unity"
            };

            var offenders = new List<string>();
            for (var i = 0; i < files.Length; i++)
            {
                string file = files[i].Replace('\\', '/');
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void LocalAvoidanceCore_HasNoBattleStateUnityOrNonDeterministicMathDependencies()
        {
            string[] files = Directory.GetFiles(
                "Packages/com.joyfort.combat/Runtime/Core/LocalAvoidance",
                "*.cs",
                SearchOption.AllDirectories);
            string[] forbiddenPatterns =
            {
                @"\bBattleWorld\b",
                @"\bEntityId\b",
                @"\b(?:BattleIntent|Intent[A-Za-z0-9_]*)\b",
                @"\bAbility[A-Za-z0-9_]*\b",
                @"\b(?:BattleEvent|Event[A-Za-z0-9_]*)\b",
                @"(?<![A-Za-z0-9_])(?:global::)?Unity(?:Engine|Editor)?\b",
                @"\b(?:float|double)\b",
                @"(?<![A-Za-z0-9_])(?:global::)?(?:System\.)?Math\b"
            };

            Assert.That(files, Is.Not.Empty);
            var offenders = new List<string>();
            for (var i = 0; i < files.Length; i++)
            {
                string file = files[i].Replace('\\', '/');
                string code = StripCommentsAndLiterals(
                    File.ReadAllText(file),
                    out bool containsInterpolatedString);
                if (containsInterpolatedString)
                {
                    offenders.Add($"{file} contains an interpolated string");
                }

                foreach (string pattern in forbiddenPatterns)
                {
                    if (Regex.IsMatch(code, pattern))
                    {
                        offenders.Add($"{file} matches {pattern}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void SpatialCore_HasNoBattleStateUnityOrNonDeterministicMathDependencies()
        {
            string[] files = Directory.GetFiles(
                "Packages/com.joyfort.combat/Runtime/Core/Spatial",
                "*.cs",
                SearchOption.AllDirectories);
            string[] forbiddenPatterns =
            {
                @"\bBattleWorld\b",
                @"\bEntityId\b",
                @"\bUnitId\b",
                @"\bProjectileId\b",
                @"\b(?:BattleEvent|EventBuffer)\b",
                @"(?<![A-Za-z0-9_])(?:global::)?Unity(?:Engine|Editor)?\b",
                @"\b(?:float|double)\b",
                @"(?<![A-Za-z0-9_])(?:global::)?(?:System\.)?Math\b"
            };

            Assert.That(files, Is.Not.Empty);
            var offenders = new List<string>();
            for (var i = 0; i < files.Length; i++)
            {
                string file = files[i].Replace('\\', '/');
                string code = StripCommentsAndLiterals(
                    File.ReadAllText(file),
                    out bool containsInterpolatedString);
                if (containsInterpolatedString)
                {
                    offenders.Add($"{file} contains an interpolated string");
                }

                foreach (string pattern in forbiddenPatterns)
                {
                    if (Regex.IsMatch(code, pattern))
                    {
                        offenders.Add($"{file} matches {pattern}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        private static string StripCommentsAndLiterals(
            string source,
            out bool containsInterpolatedString)
        {
            containsInterpolatedString = false;
            char[] stripped = source.ToCharArray();
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    Blank(stripped, i++);
                    Blank(stripped, i);
                    while (i + 1 < source.Length && source[i + 1] != '\n')
                    {
                        Blank(stripped, ++i);
                    }
                }
                else if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    Blank(stripped, i++);
                    Blank(stripped, i);
                    while (i + 1 < source.Length)
                    {
                        Blank(stripped, ++i);
                        if (source[i - 1] == '*' && source[i] == '/')
                        {
                            break;
                        }
                    }
                }
                else if (source[i] == '"')
                {
                    containsInterpolatedString |= IsInterpolatedStringStart(source, i);
                    bool verbatim = i > 0 && source[i - 1] == '@';
                    Blank(stripped, i);
                    while (i + 1 < source.Length)
                    {
                        i++;
                        Blank(stripped, i);
                        if (source[i] != '"')
                        {
                            if (!verbatim && source[i] == '\\' && i + 1 < source.Length)
                            {
                                Blank(stripped, ++i);
                            }

                            continue;
                        }

                        if (verbatim && i + 1 < source.Length && source[i + 1] == '"')
                        {
                            Blank(stripped, ++i);
                            continue;
                        }

                        break;
                    }
                }
                else if (source[i] == '\'')
                {
                    Blank(stripped, i);
                    while (i + 1 < source.Length)
                    {
                        i++;
                        Blank(stripped, i);
                        if (source[i] == '\\' && i + 1 < source.Length)
                        {
                            Blank(stripped, ++i);
                            continue;
                        }

                        if (source[i] == '\'')
                        {
                            break;
                        }
                    }
                }
            }

            return new string(stripped);
        }

        private static bool IsInterpolatedStringStart(string source, int quoteIndex)
        {
            int prefixIndex = quoteIndex - 1;
            if (prefixIndex >= 0 && source[prefixIndex] == '@')
            {
                prefixIndex--;
            }

            return prefixIndex >= 0 && source[prefixIndex] == '$';
        }

        private static void Blank(char[] source, int index)
        {
            if (source[index] != '\r' && source[index] != '\n')
            {
                source[index] = ' ';
            }
        }

        [Test]
        public void CoreBattleMath_DoesNotUseSystemMathForVectorMagnitudeOrProjectileTrig()
        {
            string[] forbidden =
            {
                "Math.Sqrt",
                "Math.Sin",
                "Math.Cos"
            };
            string[] files =
            {
                "Packages/com.joyfort.combat/Runtime/Core/Battle/BattleVector2.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/ProjectileEmitterSystem.cs"
            };

            var offenders = new List<string>();
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void CoreRuleSystems_UseBattleScalarForDistanceRangeAndRadiusChecks()
        {
            string[] files =
            {
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/AbilityEngagement.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/AbilityTargeting.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/AiDecisionSystem.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/MovementSystem.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/BattleUnitQuery.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/TargetingSystem.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/ProjectileSystem.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/ProjectileCollision.cs"
            };
            string[] forbidden =
            {
                "BattleVector2.Distance(",
                "BattleVector2.SqrDistance(",
                "Math.Abs(",
                "out float range",
                "float distance",
                "float nearestDistance",
                "Run(BattleWorld world, float secondsPerTick"
            };

            var offenders = new List<string>();
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void CoreRuleState_StoresSimulationScalarsAsBattleScalar()
        {
            string[] files =
            {
                "Packages/com.joyfort.combat/Runtime/Core/Battle/AbilityDefinition.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/BattleConfig.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/CombatantDefinition.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/AbilityComponents.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/CombatantSpawnData.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/ProjectileComponents.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/ProjectileData.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/UnitComponents.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/ProjectileCollision.cs"
            };
            string[] forbidden =
            {
                "public float Range { get; }",
                "public float Radius { get; }",
                "public float MoveSpeed { get; }",
                "public float Speed { get; }",
                "public float Padding { get; }"
            };

            var offenders = new List<string>();
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void CoreCode_DoesNotContainLegacyDamageCommandModel()
        {
            string[] files = Directory.GetFiles("Packages/com.joyfort.combat/Runtime/Core", "*.cs", SearchOption.AllDirectories);
            string[] forbidden =
            {
                string.Concat("Apply", "DamageCommand"),
                string.Concat("Apply", "DamageCommands"),
                string.Concat("Flush", "Apply", "DamageCommands"),
                string.Concat("Clear", "Apply", "DamageCommands"),
                string.Concat(".", "Apply", "Damage", "(")
            };

            var offenders = new List<string>();
            for (var i = 0; i < files.Length; i++)
            {
                string file = files[i].Replace('\\', '/');
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void CoreCode_DoesNotContainLegacyMarkDeadCommandPath()
        {
            string[] files = Directory.GetFiles("Packages/com.joyfort.combat/Runtime/Core", "*.cs", SearchOption.AllDirectories);
            string[] forbidden =
            {
                string.Concat("Mark", "Dead", "Command"),
                string.Concat("Flush", "Mark", "Dead", "Commands"),
                string.Concat(".", "Mark", "Dead", "("),
                string.Concat("Clear", "Mark", "Dead", "Commands")
            };

            var offenders = new List<string>();
            for (var i = 0; i < files.Length; i++)
            {
                string file = files[i].Replace('\\', '/');
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void AbilitySystem_DoesNotQueueEffectsDirectly()
        {
            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/AbilitySystem.cs");
            string[] forbidden =
            {
                "QueueEffect",
                "BattleEffectCommand.Damage"
            };

            var offenders = new List<string>();
            foreach (string term in forbidden)
            {
                if (text.Contains(term))
                {
                    offenders.Add($"AbilitySystem.cs contains {term}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void EntityCommandBuffer_DoesNotStoreInputOrIntentCommands()
        {
            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/EntityCommandBuffer.cs");
            string[] forbidden =
            {
                "BattleInputCommand",
                "BattleInputFrame",
                "BattleIntent",
                "IntentCommand",
                "IntentCommands",
                "InputCommand",
                "InputCommands"
            };

            var offenders = new List<string>();
            foreach (string term in forbidden)
            {
                if (text.Contains(term))
                {
                    offenders.Add($"EntityCommandBuffer.cs contains {term}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void ActionSystems_DoNotReadStatusDamageModifiersOrProjectileImplementations()
        {
            string[] files =
            {
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/AbilitySystem.cs"
            };
            string[] forbidden =
            {
                "StatusComponents",
                "StatusComponent",
                "StatusInstance",
                "BattleModifierResolver",
                "BattleModifier",
                "ProjectileSystem",
                "ProjectileEmitterSystem",
                "IProjectileCollisionDetector"
            };
            var offenders = new List<string>();

            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void ActionSystems_DoNotReadReactionTriggers()
        {
            string[] files =
            {
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/AbilitySystem.cs"
            };
            string[] forbidden =
            {
                "BattleTrigger",
                "BattleReaction",
                "DamageReactionResolver",
                string.Concat(".", "Triggers"),
                "ReactionEffectCommands",
                "QueueReactionEffect"
            };
            var offenders = new List<string>();

            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void StatusSystem_DoesNotApplyDamageDirectly()
        {
            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/StatusSystem.cs");
            string[] forbidden =
            {
                "HealthComponents.Add",
                "HealthComponents.Remove",
                "HealthComponents.Set",
                "LifeStateComponents.Add",
                "LifeStateComponents.Remove",
                "LifeStateComponents.Set",
                "SetComponent<HealthComponent",
                "SetComponent<LifeStateComponent",
                "RemoveComponent<HealthComponent",
                "RemoveComponent<LifeStateComponent",
                "BattleEvent.DamageApplied",
                "BattleEvent.UnitDied",
                "MarkDead(",
                "MarkDeadCommand",
                "DestroyEntity(",
                "DestroyEntityCommand",
                "LifeState.Dead"
            };

            var offenders = new List<string>();
            foreach (string term in forbidden)
            {
                if (text.Contains(term))
                {
                    offenders.Add($"StatusSystem.cs contains {term}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void StatusSystem_DoesNotResolveDamageModifiers()
        {
            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/StatusSystem.cs");
            string[] forbidden =
            {
                "BattleModifierResolver",
                "BattleModifier"
            };
            var offenders = new List<string>();

            foreach (string term in forbidden)
            {
                if (text.Contains(term))
                {
                    offenders.Add($"StatusSystem.cs contains {term}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void StatusSystem_DoesNotResolveReactionTriggers()
        {
            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/StatusSystem.cs");
            string[] forbidden =
            {
                "BattleTrigger",
                "BattleReaction",
                "DamageReactionResolver",
                "ReactionEffectCommands",
                "QueueReactionEffect",
                "status.Triggers",
                "SuppressReactions"
            };
            var offenders = new List<string>();

            foreach (string term in forbidden)
            {
                if (text.Contains(term))
                {
                    offenders.Add($"StatusSystem.cs contains {term}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void StatusInstance_SeparatesRuntimeDefinitionFromMutableState()
        {
            Assert.That(File.Exists("Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/StatusRuntimeDefinition.cs"), Is.True);

            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/StatusComponents.cs");
            string[] required =
            {
                "StatusRuntimeDefinition Definition",
                "TicksUntilNextPeriodicEffect"
            };
            string[] forbidden =
            {
                "_modifiers",
                "_readOnlyModifiers",
                "_triggers",
                "_readOnlyTriggers",
                "TicksUntilNextTrigger"
            };
            var offenders = new List<string>();

            foreach (string term in required)
            {
                if (!text.Contains(term))
                {
                    offenders.Add($"StatusComponents.cs does not contain {term}");
                }
            }

            foreach (string term in forbidden)
            {
                if (text.Contains(term))
                {
                    offenders.Add($"StatusComponents.cs contains {term}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void BattleEffectResolver_DelegatesStatusApplication()
        {
            Assert.That(File.Exists("Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/StatusApplicationResolver.cs"), Is.True);

            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/BattleEffectResolver.cs");
            string[] required =
            {
                "StatusApplicationResolver.ApplyOrRefresh"
            };
            string[] forbidden =
            {
                "new StatusInstance(",
                "CreateTriggerInstances"
            };
            var offenders = new List<string>();

            foreach (string term in required)
            {
                if (!text.Contains(term))
                {
                    offenders.Add($"BattleEffectResolver.cs does not contain {term}");
                }
            }

            foreach (string term in forbidden)
            {
                if (text.Contains(term))
                {
                    offenders.Add($"BattleEffectResolver.cs contains {term}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void ProjectileSystem_DoesNotApplyDamageDirectly()
        {
            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/ProjectileSystem.cs");
            string[] forbidden =
            {
                "HealthComponents.Set",
                "BattleEvent.DamageApplied",
                "MarkDead("
            };

            var offenders = new List<string>();
            foreach (string term in forbidden)
            {
                if (text.Contains(term))
                {
                    offenders.Add($"ProjectileSystem.cs contains {term}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void CoreSystems_DoNotDriveViewPortsDirectly()
        {
            string[] files =
            {
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/MovementSystem.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/ProjectileSystem.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/StatusSystem.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/AbilitySystem.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/UnitActionExecutionSystem.cs"
            };
            string[] forbidden =
            {
                "ICombatViewPort",
                "VisualCommandDispatcher",
                "UnityCombatViewPort",
                "Combat.Runtime.Display",
                "Combat.Unity.Display"
            };
            var offenders = new List<string>();

            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void BattleModifierResolver_IsPureCoreCalculation()
        {
            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/Systems/BattleModifierResolver.cs");
            string[] forbidden =
            {
                "BattleWorld",
                "EventBuffer",
                "BattleEvent",
                "HealthComponents",
                "CommandBuffer",
                "UnityEngine"
            };
            var offenders = new List<string>();

            foreach (string term in forbidden)
            {
                if (text.Contains(term))
                {
                    offenders.Add($"BattleModifierResolver.cs contains {term}");
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void Runtime_HasNoEngineReferences()
        {
            string json = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Runtime/Combat.Runtime.asmdef");

            Assert.That(json, Does.Contain("\"Combat.Core\""));
            Assert.That(json, Does.Contain("\"Combat.Foundation\""));
            Assert.That(json, Does.Contain("\"noEngineReferences\": true"));
        }

        [Test]
        public void BattleInstance_HasNoPresentationOrUnityDependencies()
        {
            DependencyScanResult result = CombatArchitectureGuard.ScanTypeDependencies(
                typeof(BattleInstance));

            Assert.That(result.ResolutionFailures, Is.Empty);
            Assert.That(result.ForbiddenTypes, Is.Empty);
        }

        [Test]
        public void BattleInstanceDependencyGuard_DetectsSyntheticPresentationDependencies()
        {
            DependencyScanResult result = CombatArchitectureGuard.ScanTypeDependencies(
                typeof(LeakyBattleInstanceDependency));

            Assert.That(result.ResolutionFailures, Is.Empty);
            Assert.That(result.ForbiddenTypes, Does.Contain(typeof(BattlePresentationBridge).FullName));
            Assert.That(result.ForbiddenTypes, Does.Contain(typeof(VisualCommandDispatcher).FullName));
        }

        [Test]
        public void BattleInstanceDependencyGuard_DetectsCapturedLambdaBodyDependencies()
        {
            DependencyScanResult result = CombatArchitectureGuard.ScanTypeDependencies(
                typeof(LambdaOnlyBattleInstanceDependency));

            Assert.That(result.ResolutionFailures, Is.Empty);
            Assert.That(result.ForbiddenTypes, Does.Contain(typeof(BattlePresentationBridge).FullName));
        }

        private sealed class LeakyBattleInstanceDependency
        {
            public object CreateBridge()
            {
                return new BattlePresentationBridge((IVisualCommandSink)null);
            }

            public object CreateDispatcher()
            {
                return new VisualCommandDispatcher((IVisualCommandSink)null);
            }
        }

        private sealed class LambdaOnlyBattleInstanceDependency
        {
            public Func<object> CreateFactory()
            {
                var captured = 1;
                return () =>
                {
                    GC.KeepAlive(captured);
                    return new BattlePresentationBridge((IVisualCommandSink)null);
                };
            }
        }

        [Test]
        public void VisualCommandDispatcher_MapsActionVisualsFromAbilityStarted()
        {
            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Runtime/Display/VisualCommandDispatcher.cs");

            Assert.That(text, Does.Contain("case BattleEventType.AbilityStarted:"));
            Assert.That(text, Does.Contain("VisualCommand.PlayAction"));
            Assert.That(text, Does.Not.Contain("case BattleEventType.AbilityUsed:"));
        }

        [Test]
        public void ProductionCode_DoesNotExposeLegacyAbilityUsedCompatibilityEvent()
        {
            string[] roots =
            {
                "Packages/com.joyfort.combat/Runtime/Core",
                "Packages/com.joyfort.combat/Runtime/Runtime",
                "Packages/com.joyfort.combat/Runtime/Unity"
            };
            string[] forbidden =
            {
                "AbilityUsed",
                "BattleEventType.AbilityUsed",
                "BattleEvent.AbilityUsed("
            };
            var offenders = new List<string>();

            foreach (string root in roots)
            {
                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (var i = 0; i < files.Length; i++)
                {
                    string file = files[i].Replace('\\', '/');
                    string text = File.ReadAllText(file);
                    foreach (string term in forbidden)
                    {
                        if (text.Contains(term))
                        {
                            offenders.Add($"{file} contains {term}");
                        }
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void AbilityAuthoring_UsesEffectFramesOnly()
        {
            string text = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Unity/Authoring/AbilityConfigAsset.cs");
            int assetClassStart = text.IndexOf("public sealed class AbilityConfigAsset", System.StringComparison.Ordinal);
            Assert.That(assetClassStart, Is.GreaterThanOrEqualTo(0));
            string assetClassText = text.Substring(assetClassStart);

            Assert.That(assetClassText, Does.Contain("AbilityEffectFrameConfig[] _effectFrames"));
            Assert.That(assetClassText, Does.Not.Contain("BattleEffectConfig[] _effects"));
            Assert.That(assetClassText, Does.Not.Contain("IReadOnlyList<BattleEffectConfig> Effects"));
            Assert.That(assetClassText, Does.Not.Contain("HasEffectFrames"));
        }

        [Test]
        public void ProjectileHitPolicy_IsOwnedByProjectileDefinitionAndRuntime()
        {
            string projectileData = File.ReadAllText(
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/ProjectileData.cs");
            string projectileComponent = File.ReadAllText(
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/ProjectileComponents.cs");
            string projectileAsset = File.ReadAllText(
                "Packages/com.joyfort.combat/Runtime/Unity/Authoring/ProjectileConfigAsset.cs");
            string emitterAuthoring = File.ReadAllText(
                "Packages/com.joyfort.combat/Runtime/Unity/Authoring/ProjectileAuthoringConfigs.cs");

            Assert.That(projectileData, Does.Contain("ProjectileHitPolicy"));
            Assert.That(projectileComponent, Does.Contain("HitPolicy"));
            Assert.That(projectileAsset, Does.Contain("_hitPolicyMode"));
            Assert.That(emitterAuthoring, Does.Not.Contain("HitPolicy"));
        }

        [Test]
        public void ScriptableObjectAuthoring_StaysOutOfCoreAndRuntime()
        {
            string[] roots =
            {
                "Packages/com.joyfort.combat/Runtime/Core",
                "Packages/com.joyfort.combat/Runtime/Runtime"
            };
            var offenders = new List<string>();

            foreach (string root in roots)
            {
                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (var i = 0; i < files.Length; i++)
                {
                    string file = files[i].Replace('\\', '/');
                    string text = File.ReadAllText(file);
                    if (text.Contains("ScriptableObject") || text.Contains("UnityEngine"))
                    {
                        offenders.Add(file);
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void RuntimeAndUnity_DoNotUseInternalEntityIds()
        {
            string[] roots =
            {
                "Packages/com.joyfort.combat/Runtime/Runtime",
                "Packages/com.joyfort.combat/Runtime/Unity"
            };
            var offenders = new List<string>();

            foreach (string root in roots)
            {
                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (var i = 0; i < files.Length; i++)
                {
                    string file = files[i].Replace('\\', '/');
                    string text = File.ReadAllText(file);
                    if (text.Contains("EntityId"))
                    {
                        offenders.Add(file);
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void RuntimeAndUnity_DoNotUseModifierRuntimeTypes()
        {
            string[] roots =
            {
                "Packages/com.joyfort.combat/Runtime/Runtime",
                "Packages/com.joyfort.combat/Runtime/Unity"
            };
            string[] forbidden =
            {
                "BattleModifierData",
                "BattleModifierInstance",
                "StatusComponent",
                "StatusInstance"
            };
            var offenders = new List<string>();

            foreach (string root in roots)
            {
                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (var i = 0; i < files.Length; i++)
                {
                    string file = files[i].Replace('\\', '/');
                    string text = File.ReadAllText(file);
                    foreach (string term in forbidden)
                    {
                        if (text.Contains(term))
                        {
                            offenders.Add($"{file} contains {term}");
                        }
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void RuntimeAndUnity_DoNotUseReactionRuntimeTypes()
        {
            string[] roots =
            {
                "Packages/com.joyfort.combat/Runtime/Runtime",
                "Packages/com.joyfort.combat/Runtime/Unity"
            };
            string[] forbidden =
            {
                "BattleTriggerData",
                "BattleTriggerInstance",
                "BattleReactionEffectData",
                "BattleReactionEffectInstance",
                "DamageReactionResolver",
                "BattleDamageContext"
            };
            var offenders = new List<string>();

            foreach (string root in roots)
            {
                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (var i = 0; i < files.Length; i++)
                {
                    string file = files[i].Replace('\\', '/');
                    string text = File.ReadAllText(file);
                    foreach (string term in forbidden)
                    {
                        if (text.Contains(term))
                        {
                            offenders.Add($"{file} contains {term}");
                        }
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void BattleWorldRuleResolvers_AreInternalCoreTypes()
        {
            string[] resolverFiles =
            {
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/BattleSpawnResolver.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/BattleActionResolver.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/BattleEffectResolver.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/BattleDeathResolver.cs",
                "Packages/com.joyfort.combat/Runtime/Core/Battle/Ecs/BattleSnapshotBuilder.cs"
            };

            foreach (string file in resolverFiles)
            {
                string text = File.ReadAllText(file);
                Assert.That(text, Does.Contain("namespace Combat.Core.Battle"), file);
                Assert.That(text, Does.Match(@"internal\s+(static\s+)?(?:sealed\s+)?class\s+Battle"), file);
            }
        }

        [Test]
        public void BattleSimulationPhasePipeline_IsInternalCoreType()
        {
            string pipelineText = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/BattleSimulationPhasePipeline.cs");
            Assert.That(pipelineText, Does.Contain("namespace Combat.Core.Battle"));
            Assert.That(pipelineText, Does.Match(@"internal\s+static\s+class\s+BattleSimulationPhasePipeline"));

            string simulationText = File.ReadAllText("Packages/com.joyfort.combat/Runtime/Core/Battle/BattleSimulation.cs");
            Assert.That(simulationText, Does.Contain("BattleSimulationPhasePipeline.Run(this, inputFrame);"));
            Assert.That(simulationText, Does.Not.Contain("StatusSystem.Run(_world"));
            Assert.That(simulationText, Does.Not.Contain("ProjectileSystem.Run(_world"));
            Assert.That(simulationText, Does.Not.Contain("AbilitySystem.Run(_world"));
        }

        [Test]
        public void ProductionCode_DoesNotContainUnitStateCompatibilityModel()
        {
            string[] files = Directory.GetFiles("Packages/com.joyfort.combat", "*.cs", SearchOption.AllDirectories);
            var offenders = new List<string>();

            for (var i = 0; i < files.Length; i++)
            {
                string file = files[i].Replace('\\', '/');
                if (file.Contains("/Tests/"))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                if (text.Contains("UnitState"))
                {
                    offenders.Add(file);
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }

        [Test]
        public void ProductionCode_DoesNotContainLegacySpawnAuthoringModel()
        {
            string[] files = Directory.GetFiles("Packages/com.joyfort.combat", "*.cs", SearchOption.AllDirectories);
            string[] forbidden =
            {
                string.Concat("Unit", "Definition"),
                string.Concat("Unit", "SpawnConfig"),
                string.Concat("Spawn", "UnitCommand"),
                string.Concat("Spawn", "UnitCommands"),
                string.Concat("Flush", "Spawn", "UnitCommands"),
                string.Concat("Clear", "Spawn", "UnitCommands"),
                string.Concat(".", "Spawn", "Unit", "(")
            };

            var offenders = new List<string>();
            for (var i = 0; i < files.Length; i++)
            {
                string file = files[i].Replace('\\', '/');
                if (file.Contains("/Tests/"))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                foreach (string term in forbidden)
                {
                    if (text.Contains(term))
                    {
                        offenders.Add($"{file} contains {term}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders.OrderBy(path => path)));
        }
    }

    internal sealed class DependencyScanResult
    {
        public DependencyScanResult(IEnumerable<string> forbiddenTypes, IEnumerable<string> resolutionFailures)
        {
            ForbiddenTypes = forbiddenTypes.OrderBy(value => value).ToArray();
            ResolutionFailures = resolutionFailures.OrderBy(value => value).ToArray();
        }

        public IReadOnlyList<string> ForbiddenTypes { get; }
        public IReadOnlyList<string> ResolutionFailures { get; }
    }

    internal static class CombatArchitectureGuard
    {
        private static readonly IReadOnlyDictionary<ushort, OpCode> OpCodesByValue =
            typeof(OpCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(OpCode))
                .Select(field => (OpCode)field.GetValue(null))
                .ToDictionary(opCode => unchecked((ushort)opCode.Value));

        public static DependencyScanResult ScanTypeDependencies(Type targetType)
        {
            var dependencies = new HashSet<Type>();
            var failures = new List<string>();
            var scannedTypes = new HashSet<Type>();
            var scannedMethods = new HashSet<MethodBase>();
            var pendingTypes = new Queue<Type>();
            pendingTypes.Enqueue(targetType);

            while (pendingTypes.Count > 0)
            {
                Type currentType = pendingTypes.Dequeue();
                if (!scannedTypes.Add(currentType))
                {
                    continue;
                }

                ScanDeclaredType(currentType, dependencies, failures, scannedMethods);
                foreach (Type nestedType in currentType.GetNestedTypes(
                             BindingFlags.Public | BindingFlags.NonPublic))
                {
                    pendingTypes.Enqueue(nestedType);
                }
            }

            string[] forbidden = dependencies
                .Where(IsForbiddenPresentationType)
                .Select(type => type.FullName ?? type.Name)
                .Distinct()
                .ToArray();
            return new DependencyScanResult(forbidden, failures);
        }

        private static void ScanDeclaredType(
            Type targetType,
            ISet<Type> dependencies,
            ICollection<string> failures,
            ISet<MethodBase> scannedMethods)
        {
            const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic |
                                          BindingFlags.Instance | BindingFlags.Static |
                                          BindingFlags.DeclaredOnly;

            AddType(targetType.BaseType, dependencies);
            foreach (Type interfaceType in targetType.GetInterfaces())
            {
                AddType(interfaceType, dependencies);
            }

            AddGenericConstraints(targetType.GetGenericArguments(), dependencies);
            foreach (FieldInfo field in targetType.GetFields(declared))
            {
                AddType(field.FieldType, dependencies);
            }

            foreach (PropertyInfo property in targetType.GetProperties(declared))
            {
                AddType(property.PropertyType, dependencies);
                foreach (ParameterInfo parameter in property.GetIndexParameters())
                {
                    AddType(parameter.ParameterType, dependencies);
                }
            }

            var methods = targetType.GetConstructors(declared).Cast<MethodBase>()
                .Concat(targetType.GetMethods(declared))
                .ToArray();
            foreach (MethodBase method in methods)
            {
                if (!scannedMethods.Add(method))
                {
                    continue;
                }

                if (method is MethodInfo methodInfo)
                {
                    AddType(methodInfo.ReturnType, dependencies);
                }

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    AddType(parameter.ParameterType, dependencies);
                }

                AddGenericConstraints(GetMethodGenericArguments(method), dependencies);
                ScanMethodBody(method, dependencies, failures);
            }
        }

        private static void ScanMethodBody(
            MethodBase method,
            ISet<Type> dependencies,
            ICollection<string> failures)
        {
            MethodBody body;
            try
            {
                body = method.GetMethodBody();
            }
            catch (Exception exception)
            {
                failures.Add(method + ": GetMethodBody failed: " + exception.GetType().Name);
                return;
            }

            if (body == null)
            {
                return;
            }

            foreach (LocalVariableInfo local in body.LocalVariables)
            {
                AddType(local.LocalType, dependencies);
            }

            foreach (ExceptionHandlingClause clause in body.ExceptionHandlingClauses)
            {
                AddType(clause.CatchType, dependencies);
            }

            byte[] il = body.GetILAsByteArray();
            if (il == null)
            {
                return;
            }

            Type[] typeArguments = method.DeclaringType?.GetGenericArguments() ?? Type.EmptyTypes;
            Type[] methodArguments = GetMethodGenericArguments(method);
            var offset = 0;
            while (offset < il.Length)
            {
                int instructionOffset = offset;
                try
                {
                    ushort value = il[offset++];
                    if (value == 0xfe)
                    {
                        value = (ushort)(0xfe00 | il[offset++]);
                    }

                    if (!OpCodesByValue.TryGetValue(value, out OpCode opCode))
                    {
                        failures.Add(method + $": unknown opcode 0x{value:x4} at {instructionOffset}");
                        return;
                    }

                    ReadOperand(
                        method,
                        opCode,
                        il,
                        ref offset,
                        typeArguments,
                        methodArguments,
                        dependencies,
                        failures);
                }
                catch (Exception exception)
                {
                    failures.Add(method + $": IL parse failed at {instructionOffset}: " +
                                 exception.GetType().Name + " " + exception.Message);
                    return;
                }
            }
        }

        private static void ReadOperand(
            MethodBase method,
            OpCode opCode,
            byte[] il,
            ref int offset,
            Type[] typeArguments,
            Type[] methodArguments,
            ISet<Type> dependencies,
            ICollection<string> failures)
        {
            switch (opCode.OperandType)
            {
                case OperandType.InlineNone:
                    return;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    RequireBytes(il, offset, 1);
                    offset += 1;
                    return;
                case OperandType.InlineVar:
                    RequireBytes(il, offset, 2);
                    offset += 2;
                    return;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.ShortInlineR:
                    RequireBytes(il, offset, 4);
                    offset += 4;
                    return;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    RequireBytes(il, offset, 8);
                    offset += 8;
                    return;
                case OperandType.InlineSwitch:
                    RequireBytes(il, offset, 4);
                    int count = BitConverter.ToInt32(il, offset);
                    if (count < 0)
                    {
                        throw new InvalidOperationException("Negative switch target count.");
                    }

                    offset += 4;
                    RequireBytes(il, offset, checked(count * 4));
                    offset += count * 4;
                    return;
                case OperandType.InlineType:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                    RequireBytes(il, offset, 4);
                    int token = BitConverter.ToInt32(il, offset);
                    offset += 4;
                    ResolveToken(
                        method,
                        opCode.OperandType,
                        token,
                        typeArguments,
                        methodArguments,
                        dependencies,
                        failures);
                    return;
                default:
                    throw new NotSupportedException("Unsupported operand type " + opCode.OperandType);
            }
        }

        private static void ResolveToken(
            MethodBase method,
            OperandType operandType,
            int token,
            Type[] typeArguments,
            Type[] methodArguments,
            ISet<Type> dependencies,
            ICollection<string> failures)
        {
            try
            {
                Module module = method.Module;
                if (operandType == OperandType.InlineType)
                {
                    AddType(module.ResolveType(token, typeArguments, methodArguments), dependencies);
                    return;
                }

                MemberInfo member = operandType == OperandType.InlineField
                    ? (MemberInfo)module.ResolveField(token, typeArguments, methodArguments)
                    : operandType == OperandType.InlineMethod
                        ? module.ResolveMethod(token, typeArguments, methodArguments)
                        : module.ResolveMember(token, typeArguments, methodArguments);
                AddMemberTypes(member, dependencies);
            }
            catch (Exception exception)
            {
                failures.Add(method + $": token 0x{token:x8} resolution failed: " +
                             exception.GetType().Name + " " + exception.Message);
            }
        }

        private static void AddMemberTypes(MemberInfo member, ISet<Type> dependencies)
        {
            if (member == null)
            {
                return;
            }

            AddType(member.DeclaringType, dependencies);
            if (member is Type type)
            {
                AddType(type, dependencies);
            }
            else if (member is FieldInfo field)
            {
                AddType(field.FieldType, dependencies);
            }
            else if (member is MethodBase method)
            {
                if (method is MethodInfo methodInfo)
                {
                    AddType(methodInfo.ReturnType, dependencies);
                }

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    AddType(parameter.ParameterType, dependencies);
                }

                AddGenericConstraints(GetMethodGenericArguments(method), dependencies);
            }
        }

        private static Type[] GetMethodGenericArguments(MethodBase method)
        {
            return method is MethodInfo methodInfo && methodInfo.IsGenericMethod
                ? methodInfo.GetGenericArguments()
                : Type.EmptyTypes;
        }

        private static void AddGenericConstraints(IEnumerable<Type> arguments, ISet<Type> dependencies)
        {
            foreach (Type argument in arguments)
            {
                AddType(argument, dependencies);
                if (!argument.IsGenericParameter)
                {
                    continue;
                }

                foreach (Type constraint in argument.GetGenericParameterConstraints())
                {
                    AddType(constraint, dependencies);
                }
            }
        }

        private static void AddType(Type type, ISet<Type> dependencies)
        {
            if (type == null || !dependencies.Add(type))
            {
                return;
            }

            if (type.HasElementType)
            {
                AddType(type.GetElementType(), dependencies);
            }

            if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                {
                    AddType(argument, dependencies);
                }
            }

            if (type.IsGenericParameter)
            {
                foreach (Type constraint in type.GetGenericParameterConstraints())
                {
                    AddType(constraint, dependencies);
                }
            }
        }

        private static bool IsForbiddenPresentationType(Type type)
        {
            string typeNamespace = type.Namespace ?? string.Empty;
            return typeNamespace.StartsWith("Combat.Runtime.Display", StringComparison.Ordinal) ||
                   typeNamespace.StartsWith("Combat.Unity", StringComparison.Ordinal) ||
                   typeNamespace.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                   typeNamespace.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                   type.Name == "VisualCommand" ||
                   type.Name == "VisualCommandDispatcher" ||
                   type.Name == "BattlePresentationBridge" ||
                   type.Name == "IVisualCommandSink" ||
                   type.Name == "ICombatViewPort";
        }

        private static void RequireBytes(byte[] il, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > il.Length - count)
            {
                throw new InvalidOperationException("Truncated IL operand.");
            }
        }
    }
}

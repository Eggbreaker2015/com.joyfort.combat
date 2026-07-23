using System;
using System.Collections.Generic;

namespace Combat.Core.Battle
{
    internal readonly struct StatusApplicationResult
    {
        public StatusApplicationResult(string id, StatusPolarity polarity, int stackCount, int maxStacks)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Status id is required.", nameof(id)) : id;
            Polarity = polarity;
            MaxStacks = maxStacks > 0 ? maxStacks : throw new ArgumentOutOfRangeException(nameof(maxStacks));
            StackCount = stackCount > 0 && stackCount <= MaxStacks ? stackCount : throw new ArgumentOutOfRangeException(nameof(stackCount));
        }

        public string Id { get; }
        public StatusPolarity Polarity { get; }
        public int StackCount { get; }
        public int MaxStacks { get; }
    }

    internal static class StatusApplicationResolver
    {
        public static StatusApplicationResult ApplyOrRefresh(BattleWorld world, EntityId source, EntityId target, StatusApplicationData status)
        {
            StatusRuntimeDefinition definition = StatusRuntimeDefinition.FromApplicationData(status);
            var applied = new StatusInstance(
                definition,
                source,
                status.DurationTicks,
                status.TickIntervalTicks);

            StatusComponent component = world.StatusComponents.TryGet(target, out StatusComponent existing)
                ? ApplyOrRefresh(existing, applied)
                : new StatusComponent(new[] { applied });

            world.StatusComponents.Set(target, component);
            StatusInstance current = FindCurrentStatus(component.Statuses, applied.Id);
            return new StatusApplicationResult(current.Id, current.Polarity, current.StackCount, current.MaxStacks);
        }

        private static StatusComponent ApplyOrRefresh(StatusComponent component, StatusInstance applied)
        {
            IReadOnlyList<StatusInstance> statuses = component.Statuses;
            var next = new StatusInstance[statuses.Count + 1];
            var refreshed = false;
            for (var i = 0; i < statuses.Count; i++)
            {
                StatusInstance existing = statuses[i];
                if (StringComparer.Ordinal.Equals(existing.Id, applied.Id))
                {
                    next[i] = Refresh(existing, applied);
                    refreshed = true;
                }
                else
                {
                    next[i] = existing;
                }
            }

            if (!refreshed)
            {
                next[statuses.Count] = applied;
                return new StatusComponent(next);
            }

            var trimmed = new StatusInstance[statuses.Count];
            Array.Copy(next, trimmed, trimmed.Length);
            return new StatusComponent(trimmed);
        }

        private static StatusInstance Refresh(StatusInstance existing, StatusInstance applied)
        {
            switch (applied.StackPolicy)
            {
                case StatusStackPolicy.RefreshDurationAndAddStack:
                    int nextStackCount = existing.StackCount < applied.MaxStacks
                        ? existing.StackCount + 1
                        : applied.MaxStacks;
                    return applied.WithStackCount(nextStackCount);
                default:
                    throw new ArgumentOutOfRangeException(nameof(applied), applied.StackPolicy, "Unsupported status stack policy.");
            }
        }

        private static StatusInstance FindCurrentStatus(IReadOnlyList<StatusInstance> statuses, string id)
        {
            for (var i = 0; i < statuses.Count; i++)
            {
                if (StringComparer.Ordinal.Equals(statuses[i].Id, id))
                {
                    return statuses[i];
                }
            }

            throw new InvalidOperationException($"Applied status '{id}' was not found on the target.");
        }
    }
}

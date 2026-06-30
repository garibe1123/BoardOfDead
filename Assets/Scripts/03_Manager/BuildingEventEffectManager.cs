using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public sealed class BuildingEventEffectManager
    {
        private readonly IBuildingEventRuntimeService runtimeService;
        private readonly BuildingEventSessionManager sessionManager;
        private readonly System.Random random;
        private readonly bool debugLog;

        public BuildingEventEffectManager(
            IBuildingEventRuntimeService service,
            BuildingEventSessionManager state,
            System.Random runtimeRandom,
            bool enableDebugLog)
        {
            runtimeService = service;
            sessionManager = state;
            random = runtimeRandom;
            debugLog = enableDebugLog;
        }

        public void ApplyResult(
            BuildingEventResultData result,
            BuildingEventContext context)
        {
            if (result == null || result.effects == null || context == null)
            {
                return;
            }

            List<IndexedEffect> ordered = new List<IndexedEffect>();

            for (int index = 0; index < result.effects.Count; index++)
            {
                BuildingEventEffectData effect = result.effects[index];

                if (effect != null)
                {
                    ordered.Add(new IndexedEffect(effect, index));
                }
            }

            ordered.Sort(delegate(IndexedEffect left, IndexedEffect right)
            {
                int priorityCompare = left.Effect.priority.CompareTo(right.Effect.priority);
                return priorityCompare != 0
                    ? priorityCompare
                    : left.Index.CompareTo(right.Index);
            });

            for (int index = 0; index < ordered.Count; index++)
            {
                ApplyEffect(ordered[index].Effect, context);
            }
        }

        private void ApplyEffect(
            BuildingEventEffectData effect,
            BuildingEventContext context)
        {
            if (runtimeService == null || effect == null)
            {
                return;
            }

            switch (effect.effectType)
            {
                case BuildingEventEffectType.ChangeHealth:
                    runtimeService.ChangeHealth(context.player, effect.amount);
                    break;
                case BuildingEventEffectType.AddBleeding:
                    runtimeService.AddStatus(context.player, "Bleeding");
                    break;
                case BuildingEventEffectType.RemoveBleeding:
                    runtimeService.RemoveStatus(context.player, "Bleeding");
                    break;
                case BuildingEventEffectType.ChangeInfection:
                    runtimeService.ChangeInfection(context.player, effect.amount);
                    break;
                case BuildingEventEffectType.ChangeStress:
                    runtimeService.ChangeStress(context.player, effect.amount);
                    break;
                case BuildingEventEffectType.ChangeSupplies:
                    runtimeService.ChangeSupplies(effect.amount);
                    break;
                case BuildingEventEffectType.AddItem:
                    runtimeService.AddItem(
                        context.player,
                        effect.id,
                        Mathf.Max(1, effect.amount));
                    break;
                case BuildingEventEffectType.RemoveItem:
                    runtimeService.TryRemoveItem(
                        context.player,
                        effect.id,
                        Mathf.Max(1, effect.amount));
                    break;
                case BuildingEventEffectType.AddRandomItem:
                    ApplyRandomItem(effect, context);
                    break;
                case BuildingEventEffectType.ChangeNoise:
                    runtimeService.ChangeNoise(context.districtId, effect.amount);
                    break;
                case BuildingEventEffectType.SpawnZombies:
                    runtimeService.SpawnZombies(context.districtId, effect.amount);
                    break;
                case BuildingEventEffectType.ChangeDistrictThreat:
                    runtimeService.ChangeThreat(context.districtId, effect.amount);
                    break;
                case BuildingEventEffectType.ChangeBuildingState:
                    runtimeService.ChangeBuildingState(context.nodeId, effect.id);
                    break;
                case BuildingEventEffectType.AddFlag:
                    runtimeService.AddFlag(context, effect.id);
                    break;
                case BuildingEventEffectType.RemoveFlag:
                    runtimeService.RemoveFlag(context, effect.id);
                    break;
                case BuildingEventEffectType.ScheduleFollowUp:
                    sessionManager?.Schedule(effect.followUp, context);
                    break;
            }

            if (debugLog)
            {
                Debug.Log(
                    "[BuildingEventEffect] " + effect.effectType +
                    " / ID " + effect.id +
                    " / Amount " + effect.amount);
            }
        }

        private void ApplyRandomItem(
            BuildingEventEffectData effect,
            BuildingEventContext context)
        {
            if (effect.candidateIds == null || effect.candidateIds.Count == 0)
            {
                return;
            }

            int index = random != null
                ? random.Next(0, effect.candidateIds.Count)
                : Random.Range(0, effect.candidateIds.Count);
            string itemId = effect.candidateIds[index];

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                runtimeService.AddItem(
                    context.player,
                    itemId,
                    Mathf.Max(1, effect.amount));
            }
        }

        private sealed class IndexedEffect
        {
            public readonly BuildingEventEffectData Effect;
            public readonly int Index;

            public IndexedEffect(BuildingEventEffectData effect, int index)
            {
                Effect = effect;
                Index = index;
            }
        }
    }
}

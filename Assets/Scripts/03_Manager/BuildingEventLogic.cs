using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    // =====================================================================
    // 03_Manager/BuildingEventConditionManager.cs
    // =====================================================================

    public sealed class BuildingEventConditionManager
    {
        private readonly IBuildingEventRuntimeService runtimeService;

        public BuildingEventConditionManager(
            IBuildingEventRuntimeService service)
        {
            runtimeService = service;
        }

        public bool EvaluateAll(
            IList<BuildingEventConditionData> conditions,
            BuildingEventContext context,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (conditions == null)
            {
                return true;
            }

            for (int index = 0; index < conditions.Count; index++)
            {
                BuildingEventConditionData condition = conditions[index];
                if (condition == null)
                {
                    continue;
                }

                if (!Evaluate(condition, context, out failureReason))
                {
                    return false;
                }
            }

            return true;
        }

        public bool EvaluateChoice(
            BuildingEventChoiceData choice,
            BuildingEventContext context,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (choice == null || context == null || runtimeService == null)
            {
                failureReason = "선택지 데이터 또는 런타임 서비스가 없습니다.";
                return false;
            }

            if (!EvaluateAll(choice.conditions, context, out failureReason))
            {
                return false;
            }

            if (runtimeService.GetAP(context.player) + 0.0001f < choice.apCost)
            {
                failureReason = "필요 AP " + choice.apCost.ToString("0.##");
                return false;
            }

            if (runtimeService.GetSupplies() < choice.suppliesCost)
            {
                failureReason = "필요 물자 " + choice.suppliesCost;
                return false;
            }

            for (int index = 0; index < choice.requiredItems.Count; index++)
            {
                BuildingEventItemRequirement item = choice.requiredItems[index];
                if (item != null &&
                    !runtimeService.HasItem(
                        context.player,
                        item.itemId,
                        item.amount))
                {
                    failureReason =
                        "필요 아이템: " + item.itemId + " x" +
                        Mathf.Max(1, item.amount);
                    return false;
                }
            }

            for (int index = 0; index < choice.consumedItems.Count; index++)
            {
                BuildingEventItemRequirement item = choice.consumedItems[index];
                if (item != null &&
                    !runtimeService.HasItem(
                        context.player,
                        item.itemId,
                        item.amount))
                {
                    failureReason =
                        "소모 아이템 부족: " + item.itemId + " x" +
                        Mathf.Max(1, item.amount);
                    return false;
                }
            }

            for (int index = 0; index < choice.requiredTraits.Count; index++)
            {
                string traitId = choice.requiredTraits[index];
                if (!runtimeService.HasTrait(context.player, traitId))
                {
                    failureReason = "필요 특성: " + traitId;
                    return false;
                }
            }

            for (int index = 0; index < choice.requiredFlags.Count; index++)
            {
                string flagId = choice.requiredFlags[index];
                if (!runtimeService.HasFlag(context, flagId))
                {
                    failureReason = "필요 플래그: " + flagId;
                    return false;
                }
            }

            for (int index = 0; index < choice.forbiddenFlags.Count; index++)
            {
                string flagId = choice.forbiddenFlags[index];
                if (runtimeService.HasFlag(context, flagId))
                {
                    failureReason = "사용 불가 상태: " + flagId;
                    return false;
                }
            }

            return true;
        }

        private bool Evaluate(
            BuildingEventConditionData condition,
            BuildingEventContext context,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (context == null || runtimeService == null)
            {
                failureReason = "이벤트 Context 또는 런타임 서비스가 없습니다.";
                return false;
            }

            bool passed;
            switch (condition.conditionType)
            {
                case BuildingEventConditionType.BuildingDefinitionId:
                    passed = EqualsId(
                        context.buildingDefinitionId,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.BuildingTypeId:
                    passed = EqualsId(
                        context.buildingTypeId,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.BuildingRoleId:
                    passed = EqualsId(
                        context.buildingRoleId,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.BuildingTag:
                    passed = ContainsId(
                        context.buildingTags,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.BuildingState:
                    passed = EqualsId(
                        context.buildingState,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.DistrictId:
                    passed = EqualsId(
                        context.districtId,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.DistrictType:
                    passed = EqualsId(
                        context.districtType.ToString(),
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.MinimumRound:
                    passed = context.roundNumber >= condition.intValue;
                    break;

                case BuildingEventConditionType.MaximumRound:
                    passed = context.roundNumber <= condition.intValue;
                    break;

                case BuildingEventConditionType.MinimumThreat:
                    passed = context.districtThreat >= condition.intValue;
                    break;

                case BuildingEventConditionType.MaximumThreat:
                    passed = context.districtThreat <= condition.intValue;
                    break;

                case BuildingEventConditionType.HasFlag:
                    passed = runtimeService.HasFlag(
                        context,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.MissingFlag:
                    passed = !runtimeService.HasFlag(
                        context,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.MinimumAbility:
                    passed = runtimeService.GetAbility(
                        context.player,
                        condition.abilityType) >= condition.intValue;
                    break;

                case BuildingEventConditionType.MaximumAbility:
                    passed = runtimeService.GetAbility(
                        context.player,
                        condition.abilityType) <= condition.intValue;
                    break;

                case BuildingEventConditionType.HasTrait:
                    passed = runtimeService.HasTrait(
                        context.player,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.HasItem:
                    passed = runtimeService.HasItem(
                        context.player,
                        condition.stringValue,
                        Mathf.Max(1, condition.intValue));
                    break;

                case BuildingEventConditionType.MissingItem:
                    passed = !runtimeService.HasItem(
                        context.player,
                        condition.stringValue,
                        Mathf.Max(1, condition.intValue));
                    break;

                case BuildingEventConditionType.HasStatus:
                    passed = runtimeService.HasStatus(
                        context.player,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.MissingStatus:
                    passed = !runtimeService.HasStatus(
                        context.player,
                        condition.stringValue);
                    break;

                case BuildingEventConditionType.MinimumAP:
                    passed = runtimeService.GetAP(context.player) + 0.0001f >=
                             condition.floatValue;
                    break;

                case BuildingEventConditionType.MinimumHealth:
                    passed = runtimeService.GetHealth(context.player) >=
                             condition.intValue;
                    break;

                case BuildingEventConditionType.MaximumHealth:
                    passed = runtimeService.GetHealth(context.player) <=
                             condition.intValue;
                    break;

                case BuildingEventConditionType.MinimumInfection:
                    passed = runtimeService.GetInfection(context.player) >=
                             condition.intValue;
                    break;

                case BuildingEventConditionType.MaximumInfection:
                    passed = runtimeService.GetInfection(context.player) <=
                             condition.intValue;
                    break;

                case BuildingEventConditionType.MinimumStress:
                    passed = runtimeService.GetStress(context.player) >=
                             condition.intValue;
                    break;

                case BuildingEventConditionType.MaximumStress:
                    passed = runtimeService.GetStress(context.player) <=
                             condition.intValue;
                    break;

                default:
                    passed = true;
                    break;
            }

            if (!passed)
            {
                failureReason = condition.conditionType + " 조건 불충족";
            }

            return passed;
        }

        private static bool EqualsId(string left, string right)
        {
            return string.Equals(
                left ?? string.Empty,
                right ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsId(IList<string> values, string target)
        {
            if (values == null || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            for (int index = 0; index < values.Count; index++)
            {
                if (EqualsId(values[index], target))
                {
                    return true;
                }
            }

            return false;
        }
    }

    // =====================================================================
    // 03_Manager/BuildingEventEffectManager.cs
    // =====================================================================

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

            ordered.Sort(
                delegate(IndexedEffect left, IndexedEffect right)
                {
                    int priorityCompare =
                        left.Effect.priority.CompareTo(right.Effect.priority);
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
                : UnityEngine.Random.Range(0, effect.candidateIds.Count);

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

    // =====================================================================
    // 03_Manager/D100CheckManager.cs
    // =====================================================================

    public static class D100CheckManager
    {
        public static BuildingEventCheckResult CalculatePreview(
            int baseAbility,
            int situationModifier,
            BuildingEventDifficulty difficulty)
        {
            BuildingEventCheckResult result = BuildBaseResult(
                baseAbility,
                situationModifier,
                difficulty);

            int successCount = 0;
            for (int roll = 1; roll <= 100; roll++)
            {
                BuildingEventSuccessLevel level = EvaluateLevel(
                    roll,
                    result.FinalAbility,
                    result.NormalThreshold,
                    result.HardThreshold,
                    result.ExtremeThreshold);

                if (MeetsDifficulty(level, difficulty))
                {
                    successCount++;
                }
            }

            result.SuccessProbability = successCount;
            return result;
        }

        public static BuildingEventCheckResult Roll(
            int baseAbility,
            int situationModifier,
            BuildingEventDifficulty difficulty,
            System.Random random)
        {
            BuildingEventCheckResult result = CalculatePreview(
                baseAbility,
                situationModifier,
                difficulty);

            result.Roll = random != null
                ? random.Next(1, 101)
                : UnityEngine.Random.Range(1, 101);

            result.SuccessLevel = EvaluateLevel(
                result.Roll,
                result.FinalAbility,
                result.NormalThreshold,
                result.HardThreshold,
                result.ExtremeThreshold);

            result.IsCriticalFailure =
                result.SuccessLevel == BuildingEventSuccessLevel.CriticalFailure;

            result.MeetsDifficulty = MeetsDifficulty(
                result.SuccessLevel,
                difficulty);

            return result;
        }

        public static bool MeetsDifficulty(
            BuildingEventSuccessLevel level,
            BuildingEventDifficulty difficulty)
        {
            if (level == BuildingEventSuccessLevel.CriticalFailure ||
                level == BuildingEventSuccessLevel.Failure ||
                level == BuildingEventSuccessLevel.None)
            {
                return false;
            }

            switch (difficulty)
            {
                case BuildingEventDifficulty.Easy:
                case BuildingEventDifficulty.Normal:
                    return level >= BuildingEventSuccessLevel.NormalSuccess;

                case BuildingEventDifficulty.Hard:
                    return level >= BuildingEventSuccessLevel.HardSuccess;

                case BuildingEventDifficulty.Extreme:
                    return level >= BuildingEventSuccessLevel.ExtremeSuccess;

                default:
                    return false;
            }
        }

        private static BuildingEventCheckResult BuildBaseResult(
            int baseAbility,
            int situationModifier,
            BuildingEventDifficulty difficulty)
        {
            int clampedModifier = Mathf.Clamp(situationModifier, -20, 20);
            int difficultyBonus =
                difficulty == BuildingEventDifficulty.Easy ? 20 : 0;
            int finalAbility = Mathf.Clamp(
                baseAbility + clampedModifier + difficultyBonus,
                10,
                95);

            BuildingEventCheckResult result = new BuildingEventCheckResult();
            result.BaseAbility = baseAbility;
            result.SituationModifier = clampedModifier + difficultyBonus;
            result.FinalAbility = finalAbility;
            result.NormalThreshold = finalAbility;
            result.HardThreshold = Mathf.FloorToInt(finalAbility / 2f);
            result.ExtremeThreshold = Mathf.FloorToInt(finalAbility / 5f);
            result.Difficulty = difficulty;
            return result;
        }

        private static BuildingEventSuccessLevel EvaluateLevel(
            int roll,
            int finalAbility,
            int normalThreshold,
            int hardThreshold,
            int extremeThreshold)
        {
            if (roll == 1)
            {
                return BuildingEventSuccessLevel.ExtremeSuccess;
            }

            bool criticalFailure = finalAbility < 50
                ? roll >= 96
                : roll == 100;

            if (criticalFailure)
            {
                return BuildingEventSuccessLevel.CriticalFailure;
            }

            if (roll <= extremeThreshold)
            {
                return BuildingEventSuccessLevel.ExtremeSuccess;
            }

            if (roll <= hardThreshold)
            {
                return BuildingEventSuccessLevel.HardSuccess;
            }

            if (roll <= normalThreshold)
            {
                return BuildingEventSuccessLevel.NormalSuccess;
            }

            return BuildingEventSuccessLevel.Failure;
        }
    }
}

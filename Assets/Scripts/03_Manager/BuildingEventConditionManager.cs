using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
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
                    failureReason = "필요 아이템: " + item.itemId +
                                    " x" + Mathf.Max(1, item.amount);
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
                    failureReason = "소모 아이템 부족: " + item.itemId +
                                    " x" + Mathf.Max(1, item.amount);
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
                    passed = EqualsId(context.buildingDefinitionId, condition.stringValue);
                    break;
                case BuildingEventConditionType.BuildingTypeId:
                    passed = EqualsId(context.buildingTypeId, condition.stringValue);
                    break;
                case BuildingEventConditionType.BuildingRoleId:
                    passed = EqualsId(context.buildingRoleId, condition.stringValue);
                    break;
                case BuildingEventConditionType.BuildingTag:
                    passed = ContainsId(context.buildingTags, condition.stringValue);
                    break;
                case BuildingEventConditionType.BuildingState:
                    passed = EqualsId(context.buildingState, condition.stringValue);
                    break;
                case BuildingEventConditionType.DistrictId:
                    passed = EqualsId(context.districtId, condition.stringValue);
                    break;
                case BuildingEventConditionType.DistrictType:
                    passed = EqualsId(context.districtType.ToString(), condition.stringValue);
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
                    passed = runtimeService.HasFlag(context, condition.stringValue);
                    break;
                case BuildingEventConditionType.MissingFlag:
                    passed = !runtimeService.HasFlag(context, condition.stringValue);
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
                    passed = runtimeService.HasTrait(context.player, condition.stringValue);
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
                    passed = runtimeService.HasStatus(context.player, condition.stringValue);
                    break;
                case BuildingEventConditionType.MissingStatus:
                    passed = !runtimeService.HasStatus(context.player, condition.stringValue);
                    break;
                case BuildingEventConditionType.MinimumAP:
                    passed = runtimeService.GetAP(context.player) + 0.0001f >= condition.floatValue;
                    break;
                case BuildingEventConditionType.MinimumHealth:
                    passed = runtimeService.GetHealth(context.player) >= condition.intValue;
                    break;
                case BuildingEventConditionType.MaximumHealth:
                    passed = runtimeService.GetHealth(context.player) <= condition.intValue;
                    break;
                case BuildingEventConditionType.MinimumInfection:
                    passed = runtimeService.GetInfection(context.player) >= condition.intValue;
                    break;
                case BuildingEventConditionType.MaximumInfection:
                    passed = runtimeService.GetInfection(context.player) <= condition.intValue;
                    break;
                case BuildingEventConditionType.MinimumStress:
                    passed = runtimeService.GetStress(context.player) >= condition.intValue;
                    break;
                case BuildingEventConditionType.MaximumStress:
                    passed = runtimeService.GetStress(context.player) <= condition.intValue;
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
}

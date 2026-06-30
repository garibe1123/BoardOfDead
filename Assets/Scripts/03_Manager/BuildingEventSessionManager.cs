using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 이벤트 중복 방지, 카드 슬롯별 고정 사건, 후속 사건 예약을 한 게임 세션 동안 보관합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingEventSessionManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private int recentHistoryLimit = 6;

        private readonly List<string> recentEventIds = new List<string>();
        private readonly List<string> recentArchetypeIds = new List<string>();
        private readonly List<string> recentFamilyIds = new List<string>();
        private readonly List<string> recentIllustrationIds = new List<string>();

        private readonly HashSet<string> completedGameEventIds =
            new HashSet<string>();
        private readonly Dictionary<string, HashSet<string>> completedByBuilding =
            new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, HashSet<string>> completedByDistrict =
            new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, HashSet<string>> completedByPlayer =
            new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, int> lastCompletedRoundByEvent =
            new Dictionary<string, int>();
        private readonly Dictionary<string, AssignedBuildingEvent> assignedBySlot =
            new Dictionary<string, AssignedBuildingEvent>();
        private readonly List<ScheduledBuildingEvent> scheduled =
            new List<ScheduledBuildingEvent>();

        public void ClearSession()
        {
            recentEventIds.Clear();
            recentArchetypeIds.Clear();
            recentFamilyIds.Clear();
            recentIllustrationIds.Clear();
            completedGameEventIds.Clear();
            completedByBuilding.Clear();
            completedByDistrict.Clear();
            completedByPlayer.Clear();
            lastCompletedRoundByEvent.Clear();
            assignedBySlot.Clear();
            scheduled.Clear();
        }

        public bool IsRepeatAllowed(
            BuildingEventArchetypeSOBJ archetype,
            BuildingEventContext context)
        {
            if (archetype == null || context == null)
            {
                return false;
            }

            switch (archetype.repeatPolicy)
            {
                case BuildingEventRepeatPolicy.Always:
                    return !IsMostRecentEvent(archetype.eventId);

                case BuildingEventRepeatPolicy.OncePerBuilding:
                    return !ContainsCompleted(
                        completedByBuilding,
                        context.nodeId,
                        archetype.eventId);

                case BuildingEventRepeatPolicy.OncePerDistrict:
                    return !ContainsCompleted(
                        completedByDistrict,
                        context.districtId,
                        archetype.eventId);

                case BuildingEventRepeatPolicy.OncePerGame:
                    return !completedGameEventIds.Contains(archetype.eventId);

                case BuildingEventRepeatPolicy.OncePerPlayer:
                    return !ContainsCompleted(
                        completedByPlayer,
                        context.playerId,
                        archetype.eventId);

                case BuildingEventRepeatPolicy.CooldownRounds:
                    int lastRound;
                    return !lastCompletedRoundByEvent.TryGetValue(
                               archetype.eventId,
                               out lastRound) ||
                           context.roundNumber - lastRound >=
                           Mathf.Max(0, archetype.cooldownRounds);

                default:
                    return true;
            }
        }

        public float GetRecentWeightMultiplier(
            BuildingEventArchetypeSOBJ archetype,
            string illustrationId)
        {
            if (archetype == null)
            {
                return 0f;
            }

            if (IsMostRecentEvent(archetype.eventId))
            {
                return 0f;
            }

            float multiplier = 1f;

            if (ContainsRecent(recentArchetypeIds, archetype.archetypeId, 3))
            {
                multiplier *= 0.35f;
            }

            if (ContainsRecent(recentFamilyIds, archetype.familyId, 3))
            {
                multiplier *= 0.6f;
            }

            if (!string.IsNullOrWhiteSpace(illustrationId) &&
                ContainsRecent(recentIllustrationIds, illustrationId, 3))
            {
                multiplier *= 0.25f;
            }

            return multiplier;
        }

        public void AssignToSlot(
            string slotId,
            string eventId,
            string variantId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return;
            }

            AssignedBuildingEvent data;

            if (!assignedBySlot.TryGetValue(slotId, out data))
            {
                data = new AssignedBuildingEvent();
                data.slotId = slotId;
                assignedBySlot.Add(slotId, data);
            }

            data.eventId = eventId;
            data.variantId = variantId;
            data.resolved = false;
        }

        public bool TryGetAssigned(
            string slotId,
            out AssignedBuildingEvent data)
        {
            return assignedBySlot.TryGetValue(slotId ?? string.Empty, out data);
        }

        public void MarkAssignedResolved(string slotId)
        {
            AssignedBuildingEvent data;

            if (assignedBySlot.TryGetValue(slotId ?? string.Empty, out data))
            {
                data.resolved = true;
            }
        }

        public void RecordCompleted(
            BuildingEventArchetypeSOBJ archetype,
            BuildingEventVariantSOBJ variant,
            BuildingEventContext context,
            string illustrationId)
        {
            if (archetype == null || context == null)
            {
                return;
            }

            completedGameEventIds.Add(archetype.eventId);
            AddCompleted(completedByBuilding, context.nodeId, archetype.eventId);
            AddCompleted(completedByDistrict, context.districtId, archetype.eventId);
            AddCompleted(completedByPlayer, context.playerId, archetype.eventId);
            lastCompletedRoundByEvent[archetype.eventId] = context.roundNumber;

            PushRecent(recentEventIds, archetype.eventId);
            PushRecent(recentArchetypeIds, archetype.archetypeId);
            PushRecent(recentFamilyIds, archetype.familyId);
            PushRecent(recentIllustrationIds, illustrationId);
        }

        public void Schedule(
            BuildingEventFollowUpData followUp,
            BuildingEventContext context)
        {
            if (followUp == null ||
                context == null ||
                string.IsNullOrWhiteSpace(followUp.followUpEventId))
            {
                return;
            }

            ScheduledBuildingEvent item = new ScheduledBuildingEvent();
            item.eventId = followUp.followUpEventId;
            item.trigger = followUp.trigger;
            item.priorityExecution = followUp.priorityExecution;
            item.discardWhenConditionFails = followUp.discardWhenConditionFails;
            item.threatThreshold = followUp.threatThreshold;
            item.flagId = followUp.flagId;
            item.playerId = followUp.targetCurrentPlayer ? context.playerId : string.Empty;
            item.nodeId = followUp.targetCurrentBuilding ? context.nodeId : string.Empty;
            item.districtId = followUp.targetCurrentDistrict ? context.districtId : string.Empty;

            int delay = Mathf.Max(0, followUp.delayRounds);

            if (followUp.trigger == BuildingEventFollowUpTrigger.NextRound)
            {
                delay = 1;
            }

            item.dueRound = context.roundNumber + delay;
            item.expiryRound = followUp.expiryAfterRounds > 0
                ? item.dueRound + followUp.expiryAfterRounds
                : 0;

            scheduled.Add(item);
        }

        public bool TryTakeDueFollowUp(
            BuildingEventContext context,
            IBuildingEventRuntimeService runtimeService,
            out ScheduledBuildingEvent result)
        {
            result = null;

            if (context == null)
            {
                return false;
            }

            CleanupExpired(context.roundNumber);

            int bestIndex = -1;

            for (int index = 0; index < scheduled.Count; index++)
            {
                ScheduledBuildingEvent item = scheduled[index];

                if (!MatchesTarget(item, context) ||
                    !IsTriggerSatisfied(item, context, runtimeService))
                {
                    continue;
                }

                if (bestIndex < 0 ||
                    (item.priorityExecution &&
                     !scheduled[bestIndex].priorityExecution))
                {
                    bestIndex = index;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            result = scheduled[bestIndex];
            scheduled.RemoveAt(bestIndex);
            return true;
        }

        private void CleanupExpired(int round)
        {
            for (int index = scheduled.Count - 1; index >= 0; index--)
            {
                ScheduledBuildingEvent item = scheduled[index];

                if (item.expiryRound > 0 && round > item.expiryRound)
                {
                    scheduled.RemoveAt(index);
                }
            }
        }

        private static bool MatchesTarget(
            ScheduledBuildingEvent item,
            BuildingEventContext context)
        {
            if (item == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(item.playerId) &&
                item.playerId != context.playerId)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(item.nodeId) &&
                item.nodeId != context.nodeId)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(item.districtId) &&
                item.districtId != context.districtId)
            {
                return false;
            }

            return true;
        }

        private static bool IsTriggerSatisfied(
            ScheduledBuildingEvent item,
            BuildingEventContext context,
            IBuildingEventRuntimeService runtimeService)
        {
            switch (item.trigger)
            {
                case BuildingEventFollowUpTrigger.Immediate:
                    return true;
                case BuildingEventFollowUpTrigger.NextRound:
                case BuildingEventFollowUpTrigger.AfterRounds:
                    return context.roundNumber >= item.dueRound;
                case BuildingEventFollowUpTrigger.RevisitBuilding:
                    return context.nodeId == item.nodeId;
                case BuildingEventFollowUpTrigger.ReenterDistrict:
                    return context.districtId == item.districtId;
                case BuildingEventFollowUpTrigger.ThreatAtLeast:
                    return context.districtThreat >= item.threatThreshold;
                case BuildingEventFollowUpTrigger.FlagAdded:
                    return runtimeService != null &&
                           runtimeService.HasFlag(context, item.flagId);
                default:
                    return false;
            }
        }

        private bool IsMostRecentEvent(string eventId)
        {
            return recentEventIds.Count > 0 &&
                   recentEventIds[recentEventIds.Count - 1] == eventId;
        }

        private static bool ContainsRecent(
            List<string> list,
            string value,
            int count)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            int start = Mathf.Max(0, list.Count - Mathf.Max(1, count));

            for (int index = start; index < list.Count; index++)
            {
                if (list[index] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private void PushRecent(List<string> list, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            list.Add(value);

            while (list.Count > Mathf.Max(1, recentHistoryLimit))
            {
                list.RemoveAt(0);
            }
        }

        private static bool ContainsCompleted(
            Dictionary<string, HashSet<string>> map,
            string key,
            string eventId)
        {
            HashSet<string> set;
            return !string.IsNullOrWhiteSpace(key) &&
                   map.TryGetValue(key, out set) &&
                   set.Contains(eventId);
        }

        private static void AddCompleted(
            Dictionary<string, HashSet<string>> map,
            string key,
            string eventId)
        {
            if (string.IsNullOrWhiteSpace(key) ||
                string.IsNullOrWhiteSpace(eventId))
            {
                return;
            }

            HashSet<string> set;

            if (!map.TryGetValue(key, out set))
            {
                set = new HashSet<string>();
                map.Add(key, set);
            }

            set.Add(eventId);
        }
    }
}

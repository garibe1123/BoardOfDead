using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [Serializable]
    public class ScheduledBuildingEvent
    {
        public string eventId;
        public BuildingEventFollowUpTrigger trigger;
        public int dueRound;
        public int expiryRound;
        public bool priorityExecution;
        public bool discardWhenConditionFails;
        public int threatThreshold;
        public string flagId;
        public string playerId;
        public string nodeId;
        public string districtId;
    }

    [Serializable]
    public class AssignedBuildingEvent
    {
        public string slotId;
        public string eventId;
        public string variantId;
        public bool resolved;
    }
}

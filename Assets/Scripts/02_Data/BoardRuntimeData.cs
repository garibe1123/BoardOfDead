using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [Serializable]
    public class DistrictRuntimeData
    {
        public string districtId;
        public string districtName;
        public DistrictType districtType;
        public int threat;
        public int noise;
        public int zombieCount;
        public bool hasHorde;
        public List<string> flags = new List<string>();
        public List<string> completedEventIds = new List<string>();
    }

    [Serializable]
    public class BuildingRuntimeData
    {
        public string nodeId;
        public string districtId;
        public string buildingDefinitionId;
        public string buildingTypeId;
        public string buildingRoleId;
        public string buildingState = "Normal";
        public int searchCount;
        public List<string> tags = new List<string>();
        public List<string> flags = new List<string>();
        public List<string> completedEventIds = new List<string>();
    }
}

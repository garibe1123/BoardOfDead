using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// ScriptableObject의 정적 정의와 분리된 한 게임 세션의 지구·건물 상태를 보관합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardRuntimeStateManager : MonoBehaviour
    {
        [Header("Session State")]
        [SerializeField, Min(0)] private int supplies;

        private readonly Dictionary<string, DistrictRuntimeData> districts =
            new Dictionary<string, DistrictRuntimeData>();

        private readonly Dictionary<string, BuildingRuntimeData> buildings =
            new Dictionary<string, BuildingRuntimeData>();

        private readonly HashSet<string> globalFlags =
            new HashSet<string>();

        public int Supplies => supplies;

        public void ClearRuntimeState()
        {
            districts.Clear();
            buildings.Clear();
            globalFlags.Clear();
            supplies = 0;
        }

        public DistrictRuntimeData RegisterDistrict(
            string districtId,
            string districtName,
            DistrictType districtType)
        {
            if (string.IsNullOrWhiteSpace(districtId))
            {
                return null;
            }

            DistrictRuntimeData state;

            if (!districts.TryGetValue(districtId, out state))
            {
                state = new DistrictRuntimeData();
                state.districtId = districtId;
                districts.Add(districtId, state);
            }

            state.districtName = string.IsNullOrWhiteSpace(districtName)
                ? districtId
                : districtName;
            state.districtType = districtType;
            return state;
        }

        public BuildingRuntimeData RegisterBuilding(
            BuildingBoardPrefab building)
        {
            if (building == null || string.IsNullOrWhiteSpace(building.RuntimeNodeId))
            {
                return null;
            }

            BuildingRuntimeData state;

            if (!buildings.TryGetValue(building.RuntimeNodeId, out state))
            {
                state = new BuildingRuntimeData();
                state.nodeId = building.RuntimeNodeId;
                buildings.Add(state.nodeId, state);
            }

            state.districtId = building.RuntimeDistrictId;
            state.buildingDefinitionId = building.BuildingDefinitionId;
            state.buildingTypeId = building.BuildingTypeId;
            state.buildingRoleId = building.BuildingRoleId;
            state.tags = new List<string>(building.BuildingTags);

            if (string.IsNullOrWhiteSpace(state.buildingState))
            {
                state.buildingState = "Normal";
            }

            return state;
        }

        public bool TryGetDistrict(
            string districtId,
            out DistrictRuntimeData state)
        {
            return districts.TryGetValue(districtId ?? string.Empty, out state);
        }

        public bool TryGetBuilding(
            string nodeId,
            out BuildingRuntimeData state)
        {
            return buildings.TryGetValue(nodeId ?? string.Empty, out state);
        }

        public int GetThreat(string districtId)
        {
            DistrictRuntimeData state;
            return TryGetDistrict(districtId, out state) && state != null
                ? state.threat
                : 0;
        }

        public int ChangeThreat(string districtId, int amount)
        {
            DistrictRuntimeData state;

            if (!TryGetDistrict(districtId, out state) || state == null)
            {
                return 0;
            }

            state.threat = Mathf.Max(0, state.threat + amount);
            return state.threat;
        }

        public int ChangeNoise(string districtId, int amount)
        {
            DistrictRuntimeData state;

            if (!TryGetDistrict(districtId, out state) || state == null)
            {
                return 0;
            }

            state.noise = Mathf.Max(0, state.noise + amount);
            return state.noise;
        }

        public int ChangeZombieCount(string districtId, int amount)
        {
            DistrictRuntimeData state;

            if (!TryGetDistrict(districtId, out state) || state == null)
            {
                return 0;
            }

            state.zombieCount = Mathf.Max(0, state.zombieCount + amount);
            return state.zombieCount;
        }

        public void ChangeSupplies(int amount)
        {
            supplies = Mathf.Max(0, supplies + amount);
        }

        public bool TrySpendSupplies(int amount)
        {
            amount = Mathf.Max(0, amount);

            if (supplies < amount)
            {
                return false;
            }

            supplies -= amount;
            return true;
        }

        public string GetBuildingState(string nodeId)
        {
            BuildingRuntimeData state;
            return TryGetBuilding(nodeId, out state) && state != null
                ? state.buildingState
                : string.Empty;
        }

        public void SetBuildingState(string nodeId, string value)
        {
            BuildingRuntimeData state;

            if (TryGetBuilding(nodeId, out state) && state != null)
            {
                state.buildingState = string.IsNullOrWhiteSpace(value)
                    ? "Normal"
                    : value;
            }
        }

        public void IncrementSearchCount(string nodeId)
        {
            BuildingRuntimeData state;

            if (TryGetBuilding(nodeId, out state) && state != null)
            {
                state.searchCount++;
            }
        }

        public bool HasGlobalFlag(string flagId)
        {
            return !string.IsNullOrWhiteSpace(flagId) &&
                   globalFlags.Contains(flagId);
        }

        public void AddGlobalFlag(string flagId)
        {
            if (!string.IsNullOrWhiteSpace(flagId))
            {
                globalFlags.Add(flagId);
            }
        }

        public void RemoveGlobalFlag(string flagId)
        {
            if (!string.IsNullOrWhiteSpace(flagId))
            {
                globalFlags.Remove(flagId);
            }
        }

        public bool HasBuildingFlag(string nodeId, string flagId)
        {
            BuildingRuntimeData state;

            return TryGetBuilding(nodeId, out state) &&
                   state != null &&
                   state.flags.Contains(flagId);
        }

        public void AddBuildingFlag(string nodeId, string flagId)
        {
            BuildingRuntimeData state;

            if (!string.IsNullOrWhiteSpace(flagId) &&
                TryGetBuilding(nodeId, out state) &&
                state != null &&
                !state.flags.Contains(flagId))
            {
                state.flags.Add(flagId);
            }
        }

        public void RemoveBuildingFlag(string nodeId, string flagId)
        {
            BuildingRuntimeData state;

            if (TryGetBuilding(nodeId, out state) && state != null)
            {
                state.flags.Remove(flagId);
            }
        }
    }
}

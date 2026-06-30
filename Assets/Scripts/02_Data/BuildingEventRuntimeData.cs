using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public interface IBuildingEventRuntimeService
    {
        int GetAbility(PlayerData player, BuildingEventAbilityType abilityType);
        int GetHealth(PlayerData player);
        int GetInfection(PlayerData player);
        int GetStress(PlayerData player);
        float GetAP(PlayerData player);
        int GetSupplies();

        bool HasTrait(PlayerData player, string traitId);
        bool HasItem(PlayerData player, string itemId, int amount);
        bool HasStatus(PlayerData player, string statusId);

        bool TrySpendAP(PlayerData player, float amount);
        bool TrySpendSupplies(int amount);
        bool TryRemoveItem(PlayerData player, string itemId, int amount);

        void ChangeHealth(PlayerData player, int amount);
        void ChangeInfection(PlayerData player, int amount);
        void ChangeStress(PlayerData player, int amount);
        void ChangeSupplies(int amount);
        void AddItem(PlayerData player, string itemId, int amount);
        void AddStatus(PlayerData player, string statusId);
        void RemoveStatus(PlayerData player, string statusId);

        void ChangeNoise(string districtId, int amount);
        void SpawnZombies(string districtId, int amount);
        void ChangeThreat(string districtId, int amount);
        void ChangeBuildingState(string nodeId, string stateId);
        string GetBuildingState(string nodeId);

        bool HasFlag(BuildingEventContext context, string flagId);
        void AddFlag(BuildingEventContext context, string flagId);
        void RemoveFlag(BuildingEventContext context, string flagId);
    }

    [Serializable]
    public class PlayerEventProfileBinding
    {
        [Tooltip("비워두면 Default Profile을 사용합니다.")]
        public string playerId;
        public PlayerEventProfileSOBJ profile;
    }

    [Serializable]
    internal class PlayerEventRuntimeState
    {
        public string playerId;
        public int maximumHealth = 10;
        public int health = 10;
        public int infection;
        public int stress;
        public Dictionary<BuildingEventAbilityType, int> abilities =
            new Dictionary<BuildingEventAbilityType, int>();
        public HashSet<string> traits = new HashSet<string>();
        public HashSet<string> statuses = new HashSet<string>();
        public Dictionary<string, int> items = new Dictionary<string, int>();
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 현재 첨부 코드에 HP·감염·인벤토리 매니저가 없으므로 사용하는 교체 가능한 프로토타입 어댑터입니다.
    /// AP는 기존 TurnManager를 직접 사용하고 나머지 값은 세션 메모리에 보관합니다.
    /// 추후 실제 시스템이 생기면 IBuildingEventRuntimeService 구현체만 교체하십시오.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingEventRuntimeManager : MonoBehaviour,
        IBuildingEventRuntimeService
    {
        [Header("Profiles")]
        [SerializeField] private PlayerEventProfileSOBJ defaultProfile;
        [SerializeField] private List<PlayerEventProfileBinding> playerProfiles =
            new List<PlayerEventProfileBinding>();
        [SerializeField, Range(1, 99)] private int fallbackAbility = 50;

        private readonly Dictionary<string, PlayerEventRuntimeState> states =
            new Dictionary<string, PlayerEventRuntimeState>();

        private TurnManager turnManager;
        private BoardRuntimeStateManager boardState;

        public void Initialize(
            TurnManager runtimeTurnManager,
            BoardRuntimeStateManager runtimeBoardState)
        {
            turnManager = runtimeTurnManager;
            boardState = runtimeBoardState;
            states.Clear();
        }

        public int GetAbility(
            PlayerData player,
            BuildingEventAbilityType abilityType)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);
            int value;

            return state != null && state.abilities.TryGetValue(abilityType, out value)
                ? value
                : fallbackAbility;
        }

        public int GetHealth(PlayerData player)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);
            return state != null ? state.health : 0;
        }

        public int GetInfection(PlayerData player)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);
            return state != null ? state.infection : 0;
        }

        public int GetStress(PlayerData player)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);
            return state != null ? state.stress : 0;
        }

        public float GetAP(PlayerData player)
        {
            return player != null ? player.CurrentAP : 0f;
        }

        public int GetSupplies()
        {
            return boardState != null ? boardState.Supplies : 0;
        }

        public bool HasTrait(PlayerData player, string traitId)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);
            return state != null && !string.IsNullOrWhiteSpace(traitId) &&
                   state.traits.Contains(traitId);
        }

        public bool HasItem(PlayerData player, string itemId, int amount)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);
            int current;

            return state != null &&
                   !string.IsNullOrWhiteSpace(itemId) &&
                   state.items.TryGetValue(itemId, out current) &&
                   current >= Mathf.Max(1, amount);
        }

        public bool HasStatus(PlayerData player, string statusId)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);
            return state != null &&
                   !string.IsNullOrWhiteSpace(statusId) &&
                   state.statuses.Contains(statusId);
        }

        public bool TrySpendAP(PlayerData player, float amount)
        {
            amount = Mathf.Max(0f, amount);

            if (amount <= 0f)
            {
                return true;
            }

            return turnManager != null &&
                   turnManager.CurrentPlayer == player &&
                   turnManager.TrySpendCurrentPlayerAP(amount);
        }

        public bool TrySpendSupplies(int amount)
        {
            return boardState != null && boardState.TrySpendSupplies(amount);
        }

        public bool TryRemoveItem(PlayerData player, string itemId, int amount)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);
            amount = Mathf.Max(1, amount);

            if (state == null || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            int current;

            if (!state.items.TryGetValue(itemId, out current) || current < amount)
            {
                return false;
            }

            current -= amount;

            if (current <= 0)
            {
                state.items.Remove(itemId);
            }
            else
            {
                state.items[itemId] = current;
            }

            return true;
        }

        public void ChangeHealth(PlayerData player, int amount)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);

            if (state != null)
            {
                state.health = Mathf.Clamp(
                    state.health + amount,
                    0,
                    Mathf.Max(1, state.maximumHealth));
            }
        }

        public void ChangeInfection(PlayerData player, int amount)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);

            if (state != null)
            {
                state.infection = Mathf.Max(0, state.infection + amount);
            }
        }

        public void ChangeStress(PlayerData player, int amount)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);

            if (state != null)
            {
                state.stress = Mathf.Max(0, state.stress + amount);
            }
        }

        public void ChangeSupplies(int amount)
        {
            boardState?.ChangeSupplies(amount);
        }

        public void AddItem(PlayerData player, string itemId, int amount)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);

            if (state == null || string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            int current;
            state.items.TryGetValue(itemId, out current);
            state.items[itemId] = current + Mathf.Max(1, amount);
        }

        public void AddStatus(PlayerData player, string statusId)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);

            if (state != null && !string.IsNullOrWhiteSpace(statusId))
            {
                state.statuses.Add(statusId);
            }
        }

        public void RemoveStatus(PlayerData player, string statusId)
        {
            PlayerEventRuntimeState state = GetOrCreateState(player);

            if (state != null && !string.IsNullOrWhiteSpace(statusId))
            {
                state.statuses.Remove(statusId);
            }
        }

        public void ChangeNoise(string districtId, int amount)
        {
            boardState?.ChangeNoise(districtId, amount);
        }

        public void SpawnZombies(string districtId, int amount)
        {
            boardState?.ChangeZombieCount(districtId, amount);
        }

        public void ChangeThreat(string districtId, int amount)
        {
            boardState?.ChangeThreat(districtId, amount);
        }

        public void ChangeBuildingState(string nodeId, string stateId)
        {
            boardState?.SetBuildingState(nodeId, stateId);
        }

        public string GetBuildingState(string nodeId)
        {
            return boardState != null
                ? boardState.GetBuildingState(nodeId)
                : string.Empty;
        }

        public bool HasFlag(BuildingEventContext context, string flagId)
        {
            if (boardState == null || string.IsNullOrWhiteSpace(flagId))
            {
                return false;
            }

            if (flagId.StartsWith("BUILDING:", StringComparison.OrdinalIgnoreCase))
            {
                return boardState.HasBuildingFlag(
                    context.nodeId,
                    flagId.Substring("BUILDING:".Length));
            }

            return boardState.HasGlobalFlag(flagId);
        }

        public void AddFlag(BuildingEventContext context, string flagId)
        {
            if (boardState == null || string.IsNullOrWhiteSpace(flagId))
            {
                return;
            }

            if (flagId.StartsWith("BUILDING:", StringComparison.OrdinalIgnoreCase))
            {
                boardState.AddBuildingFlag(
                    context.nodeId,
                    flagId.Substring("BUILDING:".Length));
                return;
            }

            boardState.AddGlobalFlag(flagId);
        }

        public void RemoveFlag(BuildingEventContext context, string flagId)
        {
            if (boardState == null || string.IsNullOrWhiteSpace(flagId))
            {
                return;
            }

            if (flagId.StartsWith("BUILDING:", StringComparison.OrdinalIgnoreCase))
            {
                boardState.RemoveBuildingFlag(
                    context.nodeId,
                    flagId.Substring("BUILDING:".Length));
                return;
            }

            boardState.RemoveGlobalFlag(flagId);
        }

        private PlayerEventRuntimeState GetOrCreateState(PlayerData player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.PlayerId))
            {
                return null;
            }

            PlayerEventRuntimeState state;

            if (states.TryGetValue(player.PlayerId, out state))
            {
                return state;
            }

            PlayerEventProfileSOBJ profile = FindProfile(player.PlayerId);
            state = new PlayerEventRuntimeState();
            state.playerId = player.PlayerId;
            state.maximumHealth = profile != null
                ? Mathf.Max(1, profile.maximumHealth)
                : 10;
            state.health = state.maximumHealth;
            state.infection = profile != null
                ? Mathf.Max(0, profile.startingInfection)
                : 0;
            state.stress = profile != null
                ? Mathf.Max(0, profile.startingStress)
                : 0;

            Array abilities = Enum.GetValues(typeof(BuildingEventAbilityType));

            for (int index = 0; index < abilities.Length; index++)
            {
                BuildingEventAbilityType type =
                    (BuildingEventAbilityType)abilities.GetValue(index);
                state.abilities[type] = profile != null
                    ? profile.GetAbility(type, fallbackAbility)
                    : fallbackAbility;
            }

            if (profile != null)
            {
                for (int index = 0; index < profile.traits.Count; index++)
                {
                    string trait = profile.traits[index];

                    if (!string.IsNullOrWhiteSpace(trait))
                    {
                        state.traits.Add(trait);
                    }
                }

                for (int index = 0; index < profile.startingItems.Count; index++)
                {
                    PlayerEventProfileSOBJ.StartingItem item =
                        profile.startingItems[index];

                    if (item != null && !string.IsNullOrWhiteSpace(item.itemId))
                    {
                        state.items[item.itemId] = Mathf.Max(1, item.amount);
                    }
                }
            }

            states.Add(player.PlayerId, state);
            return state;
        }

        private PlayerEventProfileSOBJ FindProfile(string playerId)
        {
            for (int index = 0; index < playerProfiles.Count; index++)
            {
                PlayerEventProfileBinding binding = playerProfiles[index];

                if (binding != null &&
                    binding.playerId == playerId &&
                    binding.profile != null)
                {
                    return binding.profile;
                }
            }

            return defaultProfile;
        }
    }
}

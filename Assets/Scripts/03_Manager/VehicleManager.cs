using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public class VehicleManager : MonoBehaviour
    {
        private readonly Dictionary<string, VehicleSOBJ> definitions = new Dictionary<string, VehicleSOBJ>();
        private GameSessionData sessionData;
        private GameLogManager logManager;

        public void Initialize(GameSessionData session, IEnumerable<CardSOBJ> cardPool, GameLogManager log)
        {
            sessionData = session;
            logManager = log;
            definitions.Clear();

            foreach (CardSOBJ card in cardPool)
            {
                if (card == null || card.CardType != CardType.Vehicle || card.VehicleSOBJ == null)
                {
                    continue;
                }

                definitions[card.VehicleSOBJ.VehicleId] = card.VehicleSOBJ;
            }
        }

        public VehicleData CreateVehicleFromCard(VehicleSOBJ definition, string nodeId, string sourceCardInstanceId)
        {
            if (definition == null || sessionData == null)
            {
                return null;
            }

            string instanceId = $"VEH-{Guid.NewGuid():N}";
            VehicleData vehicle = new VehicleData(instanceId, definition.VehicleId, sourceCardInstanceId, nodeId);
            sessionData.Vehicles.Add(vehicle);
            sessionData.FindNode(nodeId)?.AddVehicle(instanceId);
            logManager?.AddLog(LogCategory.Vehicle, $"{definition.DisplayName} 차량이 {nodeId}에 발견되었습니다.");
            return vehicle;
        }

        public VehicleSOBJ GetDefinition(string vehicleSOBJId)
        {
            definitions.TryGetValue(vehicleSOBJId, out VehicleSOBJ definition);
            return definition;
        }

        public bool IsRepairComplete(VehicleData vehicle)
        {
            VehicleSOBJ definition = vehicle != null ? GetDefinition(vehicle.VehicleSOBJId) : null;
            if (definition == null)
            {
                return false;
            }

            foreach (ItemRequirementSOBJEntry requirement in definition.RequiredParts)
            {
                if (requirement?.Item == null)
                {
                    continue;
                }

                if (vehicle.GetInstalledPartAmount(requirement.Item.ItemId) < requirement.RequiredAmount)
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsUsable(VehicleData vehicle)
        {
            VehicleSOBJ definition = vehicle != null ? GetDefinition(vehicle.VehicleSOBJId) : null;
            return vehicle != null && definition != null && !vehicle.Destroyed &&
                   IsRepairComplete(vehicle) && vehicle.CurrentFuel >= definition.MinimumFuelToUse;
        }

        public bool TryInstallPart(string playerId, string vehicleInstanceId, string itemId, int amount, float apCost)
        {
            PlayerData player = sessionData?.FindPlayer(playerId);
            VehicleData vehicle = sessionData?.FindVehicle(vehicleInstanceId);
            VehicleSOBJ definition = vehicle != null ? GetDefinition(vehicle.VehicleSOBJId) : null;
            if (player == null || vehicle == null || definition == null || amount <= 0)
            {
                return false;
            }

            if (player.CurrentNodeId != vehicle.CurrentNodeId)
            {
                return false;
            }

            ItemRequirementSOBJEntry requirement = null;
            foreach (ItemRequirementSOBJEntry entry in definition.RequiredParts)
            {
                if (entry?.Item != null && entry.Item.ItemId == itemId)
                {
                    requirement = entry;
                    break;
                }
            }

            if (requirement == null)
            {
                return false;
            }

            int missing = requirement.RequiredAmount - vehicle.GetInstalledPartAmount(itemId);
            int installAmount = Mathf.Min(amount, Mathf.Max(0, missing));
            if (installAmount <= 0 || player.GetItemAmount(itemId) < installAmount)
            {
                return false;
            }

            if (!player.TrySpendAP(apCost) || !player.RemoveItem(itemId, installAmount))
            {
                return false;
            }

            vehicle.InstallPart(itemId, installAmount);
            logManager?.AddLog(LogCategory.Vehicle, $"{player.PlayerName}이(가) 차량에 {itemId} x{installAmount} 설치.");
            return true;
        }

        public bool TryRefuel(string playerId, string vehicleInstanceId, int fuelAmount, float apCost)
        {
            PlayerData player = sessionData?.FindPlayer(playerId);
            VehicleData vehicle = sessionData?.FindVehicle(vehicleInstanceId);
            VehicleSOBJ definition = vehicle != null ? GetDefinition(vehicle.VehicleSOBJId) : null;
            if (player == null || vehicle == null || definition?.FuelItem == null || fuelAmount <= 0)
            {
                return false;
            }

            if (player.CurrentNodeId != vehicle.CurrentNodeId)
            {
                return false;
            }

            int availableSpace = definition.MaxFuel - vehicle.CurrentFuel;
            int appliedAmount = Mathf.Min(fuelAmount, availableSpace);
            if (appliedAmount <= 0 || player.GetItemAmount(definition.FuelItem.ItemId) < appliedAmount)
            {
                return false;
            }

            if (!player.TrySpendAP(apCost) || !player.RemoveItem(definition.FuelItem.ItemId, appliedAmount))
            {
                return false;
            }

            vehicle.AddFuel(appliedAmount, definition.MaxFuel);
            logManager?.AddLog(LogCategory.Vehicle, $"{player.PlayerName}이(가) {definition.DisplayName}에 연료 {appliedAmount} 주입.");
            return true;
        }

        public bool TryBoard(string playerId, string vehicleInstanceId)
        {
            PlayerData player = sessionData?.FindPlayer(playerId);
            VehicleData vehicle = sessionData?.FindVehicle(vehicleInstanceId);
            VehicleSOBJ definition = vehicle != null ? GetDefinition(vehicle.VehicleSOBJId) : null;
            if (player == null || vehicle == null || definition == null || !IsUsable(vehicle))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(player.CurrentVehicleInstanceId) &&
                player.CurrentVehicleInstanceId != vehicleInstanceId)
            {
                return false;
            }

            if (player.CurrentNodeId != vehicle.CurrentNodeId || !vehicle.AddOccupant(playerId, definition.SeatCount))
            {
                return false;
            }

            player.SetVehicle(vehicleInstanceId);
            logManager?.AddLog(LogCategory.Vehicle, $"{player.PlayerName}이(가) {definition.DisplayName}에 탑승.");
            return true;
        }

        public bool TryLeave(string playerId)
        {
            PlayerData player = sessionData?.FindPlayer(playerId);
            if (player == null || string.IsNullOrEmpty(player.CurrentVehicleInstanceId))
            {
                return false;
            }

            VehicleData vehicle = sessionData.FindVehicle(player.CurrentVehicleInstanceId);
            vehicle?.RemoveOccupant(playerId);
            player.SetVehicle(string.Empty);
            logManager?.AddLog(LogCategory.Vehicle, $"{player.PlayerName}이(가) 차량에서 내렸습니다.");
            return true;
        }

        public float GetMovementAPMultiplier(PlayerData player)
        {
            if (player == null || string.IsNullOrEmpty(player.CurrentVehicleInstanceId))
            {
                return 1f;
            }

            VehicleData vehicle = sessionData.FindVehicle(player.CurrentVehicleInstanceId);
            VehicleSOBJ definition = vehicle != null ? GetDefinition(vehicle.VehicleSOBJId) : null;
            return IsUsable(vehicle) && definition != null ? definition.MovementAPMultiplier : 1f;
        }

        public bool CanDriverMove(PlayerData player)
        {
            if (player == null || string.IsNullOrEmpty(player.CurrentVehicleInstanceId))
            {
                return true;
            }

            VehicleData vehicle = sessionData.FindVehicle(player.CurrentVehicleInstanceId);
            return vehicle != null && vehicle.DriverPlayerId == player.PlayerId && IsUsable(vehicle);
        }

        public bool HasFuelForMove(PlayerData player)
        {
            if (player == null || string.IsNullOrEmpty(player.CurrentVehicleInstanceId))
            {
                return true;
            }

            VehicleData vehicle = sessionData.FindVehicle(player.CurrentVehicleInstanceId);
            VehicleSOBJ definition = vehicle != null ? GetDefinition(vehicle.VehicleSOBJId) : null;
            return vehicle != null && definition != null &&
                   vehicle.CurrentFuel >= definition.FuelConsumptionPerMove;
        }

        public bool ConsumeFuelForMove(PlayerData player)
        {
            if (player == null || string.IsNullOrEmpty(player.CurrentVehicleInstanceId))
            {
                return true;
            }

            VehicleData vehicle = sessionData.FindVehicle(player.CurrentVehicleInstanceId);
            VehicleSOBJ definition = vehicle != null ? GetDefinition(vehicle.VehicleSOBJId) : null;
            return vehicle != null && definition != null && vehicle.ConsumeFuel(definition.FuelConsumptionPerMove);
        }
    }
}

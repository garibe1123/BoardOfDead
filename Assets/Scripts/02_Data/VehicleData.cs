using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [Serializable]
    public class VehicleData
    {
        [SerializeField] private string vehicleInstanceId;
        [SerializeField] private string vehicleSOBJId;
        [SerializeField] private string sourceCardInstanceId;
        [SerializeField] private string currentNodeId;
        [SerializeField] private int currentFuel;
        [SerializeField] private bool destroyed;
        [SerializeField] private string driverPlayerId;
        [SerializeField] private List<string> occupantPlayerIds = new List<string>();
        [SerializeField] private List<ItemAmountData> installedParts = new List<ItemAmountData>();

        public string VehicleInstanceId => vehicleInstanceId;
        public string VehicleSOBJId => vehicleSOBJId;
        public string SourceCardInstanceId => sourceCardInstanceId;
        public string CurrentNodeId => currentNodeId;
        public int CurrentFuel => currentFuel;
        public bool Destroyed => destroyed;
        public string DriverPlayerId => driverPlayerId;
        public IReadOnlyList<string> OccupantPlayerIds => occupantPlayerIds;
        public IReadOnlyList<ItemAmountData> InstalledParts => installedParts;

        public VehicleData(string instanceId, string vehicleSOBJId, string sourceCardInstanceId, string nodeId)
        {
            vehicleInstanceId = instanceId;
            this.vehicleSOBJId = vehicleSOBJId;
            this.sourceCardInstanceId = sourceCardInstanceId;
            currentNodeId = nodeId;
            currentFuel = 0;
            destroyed = false;
            driverPlayerId = string.Empty;
        }

        public int GetInstalledPartAmount(string itemId)
        {
            ItemAmountData entry = installedParts.Find(x => x.ItemId == itemId);
            return entry != null ? entry.Amount : 0;
        }

        public void InstallPart(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                return;
            }

            ItemAmountData entry = installedParts.Find(x => x.ItemId == itemId);
            if (entry == null)
            {
                installedParts.Add(new ItemAmountData(itemId, amount));
                return;
            }

            entry.Add(amount);
        }

        public void AddFuel(int amount, int maxFuel)
        {
            currentFuel = Mathf.Clamp(currentFuel + Mathf.Max(0, amount), 0, Mathf.Max(1, maxFuel));
        }

        public bool ConsumeFuel(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (currentFuel < amount)
            {
                return false;
            }

            currentFuel -= amount;
            return true;
        }

        public bool AddOccupant(string playerId, int seatCount)
        {
            if (occupantPlayerIds.Contains(playerId))
            {
                return true;
            }

            if (occupantPlayerIds.Count >= Mathf.Max(1, seatCount))
            {
                return false;
            }

            occupantPlayerIds.Add(playerId);
            if (string.IsNullOrEmpty(driverPlayerId))
            {
                driverPlayerId = playerId;
            }
            return true;
        }

        public void RemoveOccupant(string playerId)
        {
            occupantPlayerIds.Remove(playerId);
            if (driverPlayerId == playerId)
            {
                driverPlayerId = occupantPlayerIds.Count > 0 ? occupantPlayerIds[0] : string.Empty;
            }
        }

        public void SetCurrentNode(string nodeId)
        {
            currentNodeId = nodeId;
        }
    }
}

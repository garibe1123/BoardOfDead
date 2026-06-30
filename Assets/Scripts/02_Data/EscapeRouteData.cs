using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [Serializable]
    public class EscapeRouteData
    {
        [SerializeField] private string escapeRouteInstanceId;
        [SerializeField] private string escapeRouteSOBJId;
        [SerializeField] private string sourceCardInstanceId;
        [SerializeField] private string nodeId;
        [SerializeField] private List<ItemAmountData> installedMaterials = new List<ItemAmountData>();
        [SerializeField] private int remainingDefenseRounds;
        [SerializeField] private bool activated;
        [SerializeField] private bool completed;

        public string EscapeRouteInstanceId => escapeRouteInstanceId;
        public string EscapeRouteSOBJId => escapeRouteSOBJId;
        public string SourceCardInstanceId => sourceCardInstanceId;
        public string NodeId => nodeId;
        public IReadOnlyList<ItemAmountData> InstalledMaterials => installedMaterials;
        public int RemainingDefenseRounds => remainingDefenseRounds;
        public bool Activated => activated;
        public bool Completed => completed;

        public EscapeRouteData(string instanceId, string escapeRouteSOBJId, string sourceCardInstanceId, string nodeId, int defenseRounds)
        {
            escapeRouteInstanceId = instanceId;
            this.escapeRouteSOBJId = escapeRouteSOBJId;
            this.sourceCardInstanceId = sourceCardInstanceId;
            this.nodeId = nodeId;
            remainingDefenseRounds = Mathf.Max(0, defenseRounds);
            activated = false;
            completed = false;
        }

        public int GetInstalledAmount(string itemId)
        {
            ItemAmountData entry = installedMaterials.Find(x => x.ItemId == itemId);
            return entry != null ? entry.Amount : 0;
        }

        public void InstallMaterial(string itemId, int amount)
        {
            ItemAmountData entry = installedMaterials.Find(x => x.ItemId == itemId);
            if (entry == null)
            {
                installedMaterials.Add(new ItemAmountData(itemId, amount));
                return;
            }

            entry.Add(amount);
        }

        public void Activate()
        {
            activated = true;
        }

        public void TickDefenseRound()
        {
            if (!activated || completed)
            {
                return;
            }

            remainingDefenseRounds = Mathf.Max(0, remainingDefenseRounds - 1);
            if (remainingDefenseRounds == 0)
            {
                completed = true;
            }
        }
    }
}

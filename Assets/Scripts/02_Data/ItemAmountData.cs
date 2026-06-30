using System;
using UnityEngine;

namespace BoardOfDead
{
    [Serializable]
    public class ItemAmountData
    {
        [SerializeField] private string itemId;
        [SerializeField] private int amount;

        public string ItemId => itemId;
        public int Amount => amount;

        public ItemAmountData(string itemId, int amount = 0)
        {
            this.itemId = itemId;
            this.amount = Mathf.Max(0, amount);
        }

        public void SetAmount(int value)
        {
            amount = Mathf.Max(0, value);
        }

        public void Add(int value)
        {
            amount = Mathf.Max(0, amount + value);
        }
    }
}

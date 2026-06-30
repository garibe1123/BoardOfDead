using System;
using UnityEngine;

namespace BoardOfDead
{
    [Serializable]
    public class ItemRequirementSOBJEntry
    {
        [SerializeField] private ItemSOBJ item;
        [SerializeField, Min(1)] private int requiredAmount = 1;

        public ItemSOBJ Item => item;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);
    }
}

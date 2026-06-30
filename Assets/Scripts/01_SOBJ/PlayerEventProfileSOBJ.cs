using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [CreateAssetMenu(
        fileName = "PlayerEventProfile",
        menuName = "Board Of Dead/Event/Player Event Profile")]
    public class PlayerEventProfileSOBJ : ScriptableObject
    {
        [Serializable]
        public class AbilityValue
        {
            public BuildingEventAbilityType abilityType;
            [Range(1, 99)] public int value = 50;
        }

        [Serializable]
        public class StartingItem
        {
            public string itemId;
            [Min(1)] public int amount = 1;
        }

        public string profileId = "DEFAULT";
        [Min(1)] public int maximumHealth = 10;
        [Min(0)] public int startingInfection;
        [Min(0)] public int startingStress;
        public List<AbilityValue> abilities = new List<AbilityValue>();
        public List<string> traits = new List<string>();
        public List<StartingItem> startingItems = new List<StartingItem>();

        public int GetAbility(BuildingEventAbilityType type, int fallback)
        {
            for (int index = 0; index < abilities.Count; index++)
            {
                AbilityValue item = abilities[index];

                if (item != null && item.abilityType == type)
                {
                    return Mathf.Clamp(item.value, 1, 99);
                }
            }

            return Mathf.Clamp(fallback, 1, 99);
        }
    }
}

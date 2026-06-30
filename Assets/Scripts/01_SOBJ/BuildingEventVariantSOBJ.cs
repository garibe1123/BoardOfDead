using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [CreateAssetMenu(
        fileName = "BuildingEventVariant",
        menuName = "Board Of Dead/Event/Building Event Variant")]
    public class BuildingEventVariantSOBJ : ScriptableObject
    {
        [Header("Identity")]
        public string variantId;
        public BuildingEventArchetypeSOBJ archetype;

        [Header("Location Filters")]
        public List<string> allowedDistrictIds = new List<string>();
        public List<DistrictType> allowedDistrictTypes =
            new List<DistrictType>();
        public List<string> allowedBuildingDefinitionIds =
            new List<string>();
        public List<string> allowedBuildingTypeIds =
            new List<string>();
        public List<string> allowedBuildingRoleIds =
            new List<string>();
        public List<string> requiredBuildingTags = new List<string>();
        public List<string> forbiddenBuildingTags = new List<string>();

        [Header("Threat")]
        [Min(0)] public int minimumThreat;
        [Min(0)] public int maximumThreat = 999;

        [Header("Presentation Override")]
        public string title;
        [TextArea(3, 12)] public string body;
        public Sprite illustration;
        public string illustrationId;

        [Header("Choice Override")]
        public List<BuildingEventChoiceOverrideData> choiceOverrides =
            new List<BuildingEventChoiceOverrideData>();

        [Header("Weight")]
        [Min(0f)] public float weightMultiplier = 1f;

        private void OnValidate()
        {
            minimumThreat = Mathf.Max(0, minimumThreat);
            maximumThreat = Mathf.Max(minimumThreat, maximumThreat);
            weightMultiplier = Mathf.Max(0f, weightMultiplier);
        }
    }
}

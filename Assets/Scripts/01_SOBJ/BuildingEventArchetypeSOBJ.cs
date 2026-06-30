using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [CreateAssetMenu(
        fileName = "BuildingEventArchetype",
        menuName = "Board Of Dead/Event/Building Event Archetype")]
    public class BuildingEventArchetypeSOBJ : ScriptableObject
    {
        [Header("Identity")]
        public string eventId;
        public string archetypeId;
        public string familyId;
        public string defaultTitle;

        [Header("Presentation")]
        [TextArea(3, 12)] public string defaultBody;
        public Sprite defaultIllustration;
        public string illustrationId;

        [Header("Selection")]
        [Min(0f)] public float baseWeight = 1f;
        public BuildingEventRepeatPolicy repeatPolicy =
            BuildingEventRepeatPolicy.Always;
        [Min(0)] public int cooldownRounds = 3;
        public bool allowPushByDefault = true;

        [Header("Conditions")]
        public List<BuildingEventConditionData> conditions =
            new List<BuildingEventConditionData>();

        [Header("Choices")]
        public List<BuildingEventChoiceData> choices =
            new List<BuildingEventChoiceData>();

        [Header("Fallback Result")]
        [Tooltip("선택지가 없는 단순 사건에 사용합니다.")]
        public BuildingEventResultData defaultResult =
            new BuildingEventResultData();

        [Header("Follow Up")]
        public List<BuildingEventFollowUpData> followUps =
            new List<BuildingEventFollowUpData>();

        private void OnValidate()
        {
            baseWeight = Mathf.Max(0f, baseWeight);
            cooldownRounds = Mathf.Max(0, cooldownRounds);

            if (string.IsNullOrWhiteSpace(archetypeId))
            {
                archetypeId = eventId;
            }
        }
    }
}

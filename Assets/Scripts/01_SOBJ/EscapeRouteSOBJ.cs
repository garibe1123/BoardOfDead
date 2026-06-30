using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [CreateAssetMenu(fileName = "EscapeRoute_", menuName = "Board Of Dead/SOBJ/Escape Route")]
    public class EscapeRouteSOBJ : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string escapeRouteId;
        [SerializeField] private string displayName;
        [TextArea] [SerializeField] private string description;
        [SerializeField] private EscapeRouteType routeType;

        [Header("Requirement")]
        [SerializeField] private List<ItemRequirementSOBJEntry> requiredMaterials = new List<ItemRequirementSOBJEntry>();
        [SerializeField, Min(1)] private int maximumPassengerCount = 1;
        [SerializeField, Min(0)] private int defenseRounds = 0;
        [SerializeField] private bool infectedOnly;

        public string EscapeRouteId => escapeRouteId;
        public string DisplayName => displayName;
        public string Description => description;
        public EscapeRouteType RouteType => routeType;
        public IReadOnlyList<ItemRequirementSOBJEntry> RequiredMaterials => requiredMaterials;
        public int MaximumPassengerCount => Mathf.Max(1, maximumPassengerCount);
        public int DefenseRounds => Mathf.Max(0, defenseRounds);
        public bool InfectedOnly => infectedOnly;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(escapeRouteId))
            {
                escapeRouteId = name;
            }
        }
#endif
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [CreateAssetMenu(fileName = "Node_", menuName = "Board Of Dead/SOBJ/Node")]
    public class NodeSOBJ : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string nodeId;
        [SerializeField] private string displayName;
        [SerializeField] private DistrictSOBJ districtSOBJ;

        [Header("Board")]
        [SerializeField] private Vector3 boardPosition;
        [SerializeField] private List<string> adjacentNodeIds = new List<string>();
        [SerializeField] private bool enterable = true;

        [Header("Initial Field")]
        [SerializeField, Min(0)] private int initialCardCount = 1;
        [SerializeField] private bool allowVehicleCard = true;
        [SerializeField] private bool allowEscapeRouteCard = true;

        public string NodeId => nodeId;
        public string DisplayName => displayName;
        public DistrictSOBJ DistrictSOBJ => districtSOBJ;
        public Vector3 BoardPosition => boardPosition;
        public IReadOnlyList<string> AdjacentNodeIds => adjacentNodeIds;
        public bool Enterable => enterable;
        public int InitialCardCount => Mathf.Max(0, initialCardCount);
        public bool AllowVehicleCard => allowVehicleCard;
        public bool AllowEscapeRouteCard => allowEscapeRouteCard;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                nodeId = name;
            }
        }
#endif
    }
}

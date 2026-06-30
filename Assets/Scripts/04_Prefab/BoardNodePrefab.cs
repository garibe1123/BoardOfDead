using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 고정 보드에 직접 배치되는 노드입니다.
    /// 위치는 이 오브젝트의 Transform이 원본이며 별도 Data에 저장하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardNodePrefab : MonoBehaviour
    {
        [Header("Node")]
        [Tooltip("비워 두면 GameObject 이름을 자동으로 사용합니다.")]
        [SerializeField] private string nodeId;
        [Tooltip("비워 두면 GameObject 이름을 자동으로 사용합니다.")]
        [SerializeField] private string displayName;
        [SerializeField] private BoardNodeType nodeType = BoardNodeType.Other;

        [Header("District")]
        [SerializeField, Range(1, 5)] private int districtNumber = 1;
        [SerializeField] private DistrictType districtType = DistrictType.Mixed;

        [Header("Rule")]
        [SerializeField] private bool enterable = true;
        [SerializeField, Min(0)] private int initialCardCount = 1;
        [SerializeField] private bool allowVehicleCard = true;
        [SerializeField] private bool allowEscapeRouteCard = true;

        [Header("Player Placement")]
        [Tooltip("비어 있으면 이 노드 Transform을 기준으로 배치합니다.")]
        [SerializeField] private Transform playerPlacementRoot;
        [SerializeField] private Vector2 playerPlacementArea = new Vector2(0.8f, 0.8f);
        [SerializeField, Min(0f)] private float playerPlacementEdgeMargin = 0.08f;
        [SerializeField, Min(0.01f)] private float minimumPlayerSpacing = 0.18f;
        [SerializeField, Min(1)] private int randomPlacementAttemptsPerPlayer = 30;
        [SerializeField] private float playerHeightOffset = 0.05f;
        [SerializeField] private bool randomizePlayerYaw = true;

        [Header("Optional Spawn Root")]
        [SerializeField] private Transform vehicleSpawnPoint;
        [SerializeField] private Transform escapeRouteSpawnPoint;
        [SerializeField] private Transform cardMarkerRoot;

        [Header("Runtime Debug")]
        [SerializeField] private string boundNodeId;

        public string NodeId => string.IsNullOrWhiteSpace(nodeId) ? gameObject.name : nodeId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public BoardNodeType NodeType => nodeType;
        public string DistrictId => $"DISTRICT-{DistrictNumber:00}";
        public int DistrictNumber => Mathf.Clamp(districtNumber, 1, 5);
        public DistrictType DistrictType => districtType;
        public bool Enterable => enterable;
        public int InitialCardCount => Mathf.Max(0, initialCardCount);
        public bool AllowVehicleCard => allowVehicleCard;
        public bool AllowEscapeRouteCard => allowEscapeRouteCard;
        public Transform VehicleSpawnPoint => vehicleSpawnPoint != null ? vehicleSpawnPoint : transform;
        public Transform EscapeRouteSpawnPoint => escapeRouteSpawnPoint != null ? escapeRouteSpawnPoint : transform;
        public Transform CardMarkerRoot => cardMarkerRoot != null ? cardMarkerRoot : transform;

        /// <summary>
        /// Inspector 값을 비워 둔 테스트 노드도 즉시 사용할 수 있게 기본값을 채웁니다.
        /// </summary>
        public void EnsureRuntimeDefaults(string fallbackNodeId = null, string fallbackDisplayName = null)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                nodeId = string.IsNullOrWhiteSpace(fallbackNodeId) ? gameObject.name : fallbackNodeId;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = string.IsNullOrWhiteSpace(fallbackDisplayName) ? gameObject.name : fallbackDisplayName;
            }
        }

        public void Bind(NodeData nodeData)
        {
            boundNodeId = nodeData != null ? nodeData.NodeId : string.Empty;
        }

        public void CreateRandomizedPlayerSpawnPoses(
            int totalPlayerCount,
            int randomSeed,
            List<Vector3> worldPositions,
            List<Quaternion> worldRotations)
        {
            if (worldPositions == null)
            {
                throw new ArgumentNullException(nameof(worldPositions));
            }

            if (worldRotations == null)
            {
                throw new ArgumentNullException(nameof(worldRotations));
            }

            worldPositions.Clear();
            worldRotations.Clear();

            int count = Mathf.Clamp(totalPlayerCount, 0, NodeData.MaxPlayerCount);
            if (count == 0)
            {
                return;
            }

            Transform root = playerPlacementRoot != null ? playerPlacementRoot : transform;
            System.Random random = new System.Random(randomSeed);
            List<Vector2> acceptedPoints = new List<Vector2>(count);

            float halfWidth = Mathf.Max(0.01f, playerPlacementArea.x * 0.5f - playerPlacementEdgeMargin);
            float halfDepth = Mathf.Max(0.01f, playerPlacementArea.y * 0.5f - playerPlacementEdgeMargin);
            float minSpacingSqr = minimumPlayerSpacing * minimumPlayerSpacing;

            for (int playerIndex = 0; playerIndex < count; playerIndex++)
            {
                bool found = false;
                Vector2 candidate = Vector2.zero;

                for (int attempt = 0; attempt < randomPlacementAttemptsPerPlayer; attempt++)
                {
                    candidate = new Vector2(
                        Mathf.Lerp(-halfWidth, halfWidth, (float)random.NextDouble()),
                        Mathf.Lerp(-halfDepth, halfDepth, (float)random.NextDouble()));

                    bool overlaps = false;
                    foreach (Vector2 accepted in acceptedPoints)
                    {
                        if ((accepted - candidate).sqrMagnitude < minSpacingSqr)
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    float angle = (360f / count) * playerIndex * Mathf.Deg2Rad;
                    float fallbackRadius = Mathf.Min(halfWidth, halfDepth) * 0.75f;
                    candidate = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * fallbackRadius;
                }

                acceptedPoints.Add(candidate);

                Vector3 localPosition = new Vector3(candidate.x, playerHeightOffset, candidate.y);
                worldPositions.Add(root.TransformPoint(localPosition));

                float yaw = randomizePlayerYaw ? (float)random.NextDouble() * 360f : 0f;
                worldRotations.Add(root.rotation * Quaternion.Euler(0f, yaw, 0f));
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            districtNumber = Mathf.Clamp(districtNumber, 1, 5);
            EnsureRuntimeDefaults();
        }
#endif
    }
}

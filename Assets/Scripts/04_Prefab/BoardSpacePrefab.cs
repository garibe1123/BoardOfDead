using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 도로와 건물을 공통으로 표현하는 단일 BoardSpacePrefab입니다.
    /// 기존 프로토타입 API와 현재 자동 보드 생성 API를 모두 포함합니다.
    /// 프로젝트에는 이 클래스 정의가 정확히 하나만 존재해야 합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardSpacePrefab : MonoBehaviour
    {
        private const int MaximumPlayerCount = 6;

        [Header("Building")]
        [SerializeField] private BuildingType buildingType = BuildingType.Generic;
        [SerializeField] private string districtName = "District";
        [SerializeField] private DistrictType districtType = DistrictType.Mixed;
        [SerializeField] private string districtShortName;
        [SerializeField] private Sprite districtIcon;

        [Header("Runtime Building Presentation")]
        [SerializeField] private Color runtimeBuildingColor = Color.white;
        [SerializeField] private bool hasRuntimeBuildingColor;

        [Header("Placement Root")]
        [SerializeField] private Transform playerPlacementRoot;
        [SerializeField] private Transform radioCardRoot;
        [SerializeField] private Transform cardSlotRoot;

        [Header("Player Scatter")]
        [SerializeField] private Vector2 playerPlacementArea = new Vector2(0.75f, 0.75f);
        [SerializeField, Min(0f)] private float placementEdgeMargin = 0.08f;
        [SerializeField, Min(0.01f)] private float minimumPlayerSpacing = 0.16f;
        [SerializeField, Min(1)] private int placementAttempts = 40;
        [SerializeField] private float playerHeightOffset = 0.15f;
        [SerializeField] private bool randomizePlayerYaw = true;

        [Header("Runtime")]
        [SerializeField] private string nodeId;
        [SerializeField] private string districtId;
        [SerializeField] private int districtIndex = -1;
        [SerializeField] private Vector2Int boardCoordinate;
        [SerializeField] private BoardSpaceType spaceType = BoardSpaceType.Road;
        [SerializeField] private float movementAPCost = 1f;
        [SerializeField] private bool enterable = true;

        [Header("Building Placement Runtime")]
        [Tooltip("건물이 실제로 점유하는 전역 보드 셀입니다. 절차 건물 비주얼과 카드/플레이어 위치의 공통 기준으로 사용합니다.")]
        [SerializeField] private List<Vector2Int> occupiedBoardCells =
            new List<Vector2Int>();

        [SerializeField] private Vector2Int entranceBoardCell;
        [SerializeField] private bool buildingHorizontal;
        [SerializeField] private bool hasBuildingPlacementData;

        [Header("Radio Runtime")]
        [SerializeField] private int radioRemainingTurns;

        private readonly List<BoardSpacePrefab> connectedSpaces =
            new List<BoardSpacePrefab>();

        private readonly List<PlayerBoardPrefab> players =
            new List<PlayerBoardPrefab>();

        private GameObject radioMarkerInstance;

        public string NodeId { get { return nodeId; } }
        public string DistrictId { get { return districtId; } }

        public string DistrictName
        {
            get
            {
                return string.IsNullOrWhiteSpace(districtName)
                    ? districtId
                    : districtName;
            }
        }

        public DistrictType DistrictType { get { return districtType; } }

        public string DistrictDisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(districtShortName)
                    ? DistrictPrefab.GetDistrictTypeDisplayName(districtType)
                    : districtShortName;
            }
        }

        public Sprite DistrictIcon { get { return districtIcon; } }
        public Color RuntimeBuildingColor { get { return runtimeBuildingColor; } }
        public bool HasRuntimeBuildingColor { get { return hasRuntimeBuildingColor; } }

        public int DistrictIndex { get { return districtIndex; } }
        public Vector2Int BoardCoordinate { get { return boardCoordinate; } }
        public BoardSpaceType SpaceType { get { return spaceType; } }
        public BuildingType BuildingType { get { return buildingType; } }
        public float MovementAPCost { get { return Mathf.Max(0f, movementAPCost); } }
        public bool Enterable { get { return enterable; } }
        public bool IsBuilding { get { return spaceType == BoardSpaceType.Building; } }
        public bool HasBuildingPlacementData { get { return hasBuildingPlacementData; } }
        public Vector2Int EntranceBoardCell { get { return entranceBoardCell; } }
        public bool BuildingHorizontal { get { return buildingHorizontal; } }

        public IList<Vector2Int> OccupiedBoardCells
        {
            get { return occupiedBoardCells.AsReadOnly(); }
        }

        // 기존 ProceduralCityBoardManager와의 호환용 별칭입니다.
        public IList<Vector2Int> OccupiedBoardCoordinates
        {
            get { return occupiedBoardCells.AsReadOnly(); }
        }

        public Transform RadioCardRoot
        {
            get { return radioCardRoot != null ? radioCardRoot : transform; }
        }

        public Transform CardSlotRoot
        {
            get { return cardSlotRoot != null ? cardSlotRoot : transform; }
        }
        public int RadioRemainingTurns { get { return radioRemainingTurns; } }
        public bool HasRadioCard { get { return radioRemainingTurns > 0; } }

        public IList<BoardSpacePrefab> ConnectedSpaces
        {
            get { return connectedSpaces.AsReadOnly(); }
        }

        public IList<PlayerBoardPrefab> Players
        {
            get { return players.AsReadOnly(); }
        }

        /// <summary>
        /// 신규 자동 보드 생성용 초기화.
        /// </summary>
        public void Initialize(
            string runtimeNodeId,
            BoardSpaceType runtimeSpaceType,
            int runtimeDistrictIndex,
            float apCost)
        {
            nodeId = runtimeNodeId;
            spaceType = runtimeSpaceType;
            districtIndex = runtimeDistrictIndex;
            districtId =
                runtimeDistrictIndex >= 0
                    ? "DISTRICT-" + (runtimeDistrictIndex + 1).ToString("00")
                    : "CITY";

            districtName = districtId;
            movementAPCost = Mathf.Max(0f, apCost);
            enterable = true;

            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                gameObject.name = nodeId;
            }
        }

        /// <summary>
        /// 자동 생성 건물의 논리 배치 정보를 한 번에 설정합니다.
        /// 건물 비주얼은 이 데이터를 기준으로 생성해야 하며, 가장 가까운 도로를 다시 추측하지 않습니다.
        /// </summary>
        public void ConfigureBuildingRuntime(
            string runtimeNodeId,
            string runtimeDistrictId,
            IList<Vector2Int> runtimeOccupiedBoardCells,
            Vector2Int runtimeEntranceBoardCell,
            bool runtimeHorizontal,
            float apCost,
            bool canEnter)
        {
            ConfigureBuildingRuntime(
                runtimeNodeId,
                runtimeDistrictId,
                runtimeDistrictId,
                DistrictType.Mixed,
                null,
                string.Empty,
                runtimeOccupiedBoardCells,
                runtimeEntranceBoardCell,
                runtimeHorizontal,
                apCost,
                canEnter);
        }

        public void ConfigureBuildingRuntime(
            string runtimeNodeId,
            string runtimeDistrictId,
            string runtimeDistrictName,
            IList<Vector2Int> runtimeOccupiedBoardCells,
            Vector2Int runtimeEntranceBoardCell,
            bool runtimeHorizontal,
            float apCost,
            bool canEnter)
        {
            ConfigureBuildingRuntime(
                runtimeNodeId,
                runtimeDistrictId,
                runtimeDistrictName,
                DistrictType.Mixed,
                null,
                string.Empty,
                runtimeOccupiedBoardCells,
                runtimeEntranceBoardCell,
                runtimeHorizontal,
                apCost,
                canEnter);
        }

        public void ConfigureBuildingRuntime(
            string runtimeNodeId,
            string runtimeDistrictId,
            string runtimeDistrictName,
            DistrictType runtimeDistrictType,
            Sprite runtimeDistrictIcon,
            string runtimeDistrictShortName,
            IList<Vector2Int> runtimeOccupiedBoardCells,
            Vector2Int runtimeEntranceBoardCell,
            bool runtimeHorizontal,
            float apCost,
            bool canEnter)
        {
            nodeId = runtimeNodeId;
            districtId = runtimeDistrictId;
            districtName =
                string.IsNullOrWhiteSpace(runtimeDistrictName)
                    ? runtimeDistrictId
                    : runtimeDistrictName;
            districtType = runtimeDistrictType;
            districtIcon = runtimeDistrictIcon;
            districtShortName = runtimeDistrictShortName ?? string.Empty;
            hasRuntimeBuildingColor = false;

            spaceType = BoardSpaceType.Building;
            movementAPCost = Mathf.Max(0f, apCost);
            enterable = canEnter;

            occupiedBoardCells.Clear();

            if (runtimeOccupiedBoardCells != null)
            {
                for (int index = 0;
                     index < runtimeOccupiedBoardCells.Count;
                     index++)
                {
                    Vector2Int cell = runtimeOccupiedBoardCells[index];

                    if (!occupiedBoardCells.Contains(cell))
                    {
                        occupiedBoardCells.Add(cell);
                    }
                }
            }

            entranceBoardCell = runtimeEntranceBoardCell;
            buildingHorizontal = runtimeHorizontal;
            hasBuildingPlacementData = occupiedBoardCells.Count > 0;

            if (hasBuildingPlacementData)
            {
                long totalX = 0;
                long totalY = 0;

                for (int index = 0;
                     index < occupiedBoardCells.Count;
                     index++)
                {
                    totalX += occupiedBoardCells[index].x;
                    totalY += occupiedBoardCells[index].y;
                }

                boardCoordinate = new Vector2Int(
                    Mathf.RoundToInt(
                        (float)totalX / occupiedBoardCells.Count),
                    Mathf.RoundToInt(
                        (float)totalY / occupiedBoardCells.Count));
            }

            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                gameObject.name = nodeId;
            }
        }

        public void SetBoardCoordinate(Vector2Int coordinate)
        {
            boardCoordinate = coordinate;
        }

        /// <summary>
        /// 기존 보드 생성 코드 호환 초기화.
        /// </summary>
        public void ConfigureRuntime(
            string runtimeNodeId,
            string runtimeDistrictId,
            Vector2Int coordinate,
            BoardSpaceType runtimeSpaceType,
            float apCost,
            bool canEnter)
        {
            nodeId = runtimeNodeId;
            districtId = runtimeDistrictId;
            districtName = runtimeDistrictId;
            boardCoordinate = coordinate;
            spaceType = runtimeSpaceType;
            movementAPCost = Mathf.Max(0f, apCost);
            enterable = canEnter;

            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                gameObject.name = nodeId;
            }
        }

        /// <summary>
        /// 기존 카드/도시 코드 호환용 설정 메서드입니다.
        /// </summary>
        public void ConfigureBuildingIdentity(
            string runtimeDistrictName,
            IList<Vector2Int> runtimeOccupiedCoordinates)
        {
            districtName =
                string.IsNullOrWhiteSpace(runtimeDistrictName)
                    ? districtId
                    : runtimeDistrictName;

            occupiedBoardCells.Clear();

            if (runtimeOccupiedCoordinates == null)
            {
                hasBuildingPlacementData = false;
                return;
            }

            for (int index = 0;
                 index < runtimeOccupiedCoordinates.Count;
                 index++)
            {
                Vector2Int coordinate = runtimeOccupiedCoordinates[index];

                if (!occupiedBoardCells.Contains(coordinate))
                {
                    occupiedBoardCells.Add(coordinate);
                }
            }

            hasBuildingPlacementData = occupiedBoardCells.Count > 0;
        }

        public void SetRuntimeBuildingColor(Color color)
        {
            runtimeBuildingColor = color;
            runtimeBuildingColor.a = 1f;
            hasRuntimeBuildingColor = true;
        }

        public void ClearRuntimeBuildingColor()
        {
            runtimeBuildingColor = Color.white;
            hasRuntimeBuildingColor = false;
        }

        public void ConnectBidirectional(BoardSpacePrefab other)
        {
            if (other == null || other == this)
            {
                return;
            }

            if (!connectedSpaces.Contains(other))
            {
                connectedSpaces.Add(other);
            }

            if (!other.connectedSpaces.Contains(this))
            {
                other.connectedSpaces.Add(this);
            }
        }

        public bool AddPlayer(PlayerBoardPrefab player)
        {
            if (player == null)
            {
                return false;
            }

            if (players.Contains(player))
            {
                RearrangePlayers();
                return true;
            }

            if (players.Count >= MaximumPlayerCount)
            {
                Debug.LogWarning(
                    "[BoardSpacePrefab] 한 칸에는 최대 6명만 배치할 수 있습니다.",
                    this);

                return false;
            }

            players.Add(player);
            RearrangePlayers();
            return true;
        }

        public void RemovePlayer(PlayerBoardPrefab player)
        {
            if (player == null)
            {
                return;
            }

            if (players.Remove(player))
            {
                RearrangePlayers();
            }
        }

        public void RearrangePlayers()
        {
            if (players.Count == 0)
            {
                return;
            }

            Transform root =
                playerPlacementRoot != null
                    ? playerPlacementRoot
                    : transform;

            List<Vector3> worldPositions = new List<Vector3>();
            List<Quaternion> worldRotations = new List<Quaternion>();

            CreateRandomPlayerPoses(
                players.Count,
                GetStablePlacementSeed(players.Count),
                worldPositions,
                worldRotations);

            for (int index = 0; index < players.Count; index++)
            {
                Quaternion rotation =
                    randomizePlayerYaw
                        ? worldRotations[index]
                        : root.rotation;

                players[index].SetBoardPosition(
                    worldPositions[index],
                    rotation);
            }
        }

        public void CreateRandomPlayerPoses(
            int playerCount,
            int randomSeed,
            List<Vector3> worldPositions,
            List<Quaternion> worldRotations)
        {
            if (worldPositions == null)
            {
                throw new ArgumentNullException("worldPositions");
            }

            if (worldRotations == null)
            {
                throw new ArgumentNullException("worldRotations");
            }

            worldPositions.Clear();
            worldRotations.Clear();

            int count = Mathf.Clamp(playerCount, 0, MaximumPlayerCount);

            if (count == 0)
            {
                return;
            }

            Transform root =
                playerPlacementRoot != null
                    ? playerPlacementRoot
                    : transform;

            System.Random random = new System.Random(randomSeed);
            List<Vector2> accepted = new List<Vector2>(count);

            float halfX =
                Mathf.Max(
                    0.01f,
                    playerPlacementArea.x * 0.5f - placementEdgeMargin);

            float halfZ =
                Mathf.Max(
                    0.01f,
                    playerPlacementArea.y * 0.5f - placementEdgeMargin);

            float spacingSquared =
                minimumPlayerSpacing * minimumPlayerSpacing;

            for (int index = 0; index < count; index++)
            {
                Vector2 selected = Vector2.zero;
                bool found = false;

                for (int attempt = 0; attempt < placementAttempts; attempt++)
                {
                    Vector2 candidate = new Vector2(
                        Mathf.Lerp(
                            -halfX,
                            halfX,
                            (float)random.NextDouble()),
                        Mathf.Lerp(
                            -halfZ,
                            halfZ,
                            (float)random.NextDouble()));

                    bool overlaps = false;

                    for (int previousIndex = 0;
                         previousIndex < accepted.Count;
                         previousIndex++)
                    {
                        if ((accepted[previousIndex] - candidate).sqrMagnitude <
                            spacingSquared)
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        selected = candidate;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    float angle =
                        Mathf.PI * 2f * index / Mathf.Max(1, count);

                    float radius =
                        Mathf.Min(halfX, halfZ) * 0.75f;

                    selected =
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle)) *
                        radius;
                }

                accepted.Add(selected);

                Vector3 localPosition =
                    new Vector3(
                        selected.x,
                        playerHeightOffset,
                        selected.y);

                worldPositions.Add(
                    root.TransformPoint(localPosition));

                worldRotations.Add(
                    root.rotation *
                    Quaternion.Euler(
                        0f,
                        (float)random.NextDouble() * 360f,
                        0f));
            }
        }

        public float GetSearchAPCost(float normalSearchAPCost)
        {
            if (!IsBuilding)
            {
                return float.PositiveInfinity;
            }

            return HasRadioCard
                ? 1f
                : Mathf.Max(3f, normalSearchAPCost);
        }

        public void PlaceRadioCard(
            int durationTurns,
            GameObject markerPrefab)
        {
            if (!IsBuilding)
            {
                return;
            }

            radioRemainingTurns =
                Mathf.Max(
                    radioRemainingTurns,
                    Mathf.Max(1, durationTurns));

            if (radioMarkerInstance != null)
            {
                return;
            }

            Transform markerRoot = RadioCardRoot;

            if (markerPrefab != null)
            {
                radioMarkerInstance =
                    Instantiate(
                        markerPrefab,
                        markerRoot.position,
                        markerRoot.rotation,
                        markerRoot);
            }
            else
            {
                radioMarkerInstance =
                    GameObject.CreatePrimitive(PrimitiveType.Sphere);

                radioMarkerInstance.name =
                    "Runtime_RadioCardMarker";

                radioMarkerInstance.transform.SetParent(
                    markerRoot,
                    false);

                radioMarkerInstance.transform.localPosition =
                    new Vector3(0f, 0.8f, 0f);

                radioMarkerInstance.transform.localScale =
                    Vector3.one * 0.22f;

                Collider markerCollider =
                    radioMarkerInstance.GetComponent<Collider>();

                if (markerCollider != null)
                {
                    Destroy(markerCollider);
                }
            }
        }

        public void TickRadioCard()
        {
            if (radioRemainingTurns <= 0)
            {
                return;
            }

            radioRemainingTurns--;

            if (radioRemainingTurns > 0)
            {
                return;
            }

            radioRemainingTurns = 0;

            if (radioMarkerInstance != null)
            {
                Destroy(radioMarkerInstance);
                radioMarkerInstance = null;
            }
        }

        private int GetStablePlacementSeed(int count)
        {
            unchecked
            {
                int seed = 17;
                seed = seed * 31 + (nodeId == null ? 0 : nodeId.GetHashCode());
                seed = seed * 31 + count;
                return seed;
            }
        }
    }
}

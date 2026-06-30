using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public class GameBoardSettingManager : MonoBehaviour
    {
        [Header("Start")]
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool clearOldBoard = true;

        [Header("Board")]
        [SerializeField, Range(1, 9)] private int districtCount = 5;
        [SerializeField] private Vector2Int districtSlotGridSize =
            new Vector2Int(3, 3);

        [SerializeField] private Vector2Int districtSize =
            new Vector2Int(6, 6);

        [SerializeField, Min(0.1f)]
        private float cellWorldSize = 1f;

        [SerializeField, Min(2)]
        private int minimumDistrictGap = 3;

        [SerializeField, Min(2)]
        private int maximumDistrictGap = 5;

        [Header("Buildings")]
        [SerializeField, Range(1, 6)]
        private int buildingCountPerDistrict = 3;

        [Header("Logical Building Placement Priority")]
        [Tooltip("카드가 붙는 논리 건물 사이에 우선 확보할 빈 셀 수입니다. 공간이 부족하면 0칸 조건으로 재시도해 건물 수를 보존합니다.")]
        [SerializeField, Range(0, 2)]
        private int preferredBuildingClearanceCells = 1;

        [Tooltip("건물 후보를 무작위 한 번으로 끝내지 않고 구역 전체 후보를 검사합니다.")]
        [SerializeField]
        private bool searchWholeDistrictForBuildingPlacement = true;

        [Tooltip("건물의 지정 입구뿐 아니라 실제 점유 셀에 접한 모든 도로와 연결합니다.")]
        [SerializeField]
        private bool connectBuildingToAllAdjacentRoads = true;

        [Header("Roads")]
        [SerializeField, Min(1)]
        private int minimumInternalRoadCount = 10;

        [SerializeField, Min(1)]
        private int maximumInternalRoadCount = 16;

        [SerializeField, Range(0f, 1f)]
        private float secondConnectionLaneChance = 0.45f;

        [Header("AP")]
        [SerializeField, Min(0f)]
        private float roadMovementAPCost = 1f;

        [SerializeField, Min(0f)]
        private float buildingMovementAPCost = 2f;

        [Header("District Prefabs")]
        [SerializeField]
        private DistrictPrefab[] districtPrefabs;

        [Header("Inter District Road Prefabs")]
        [SerializeField] private GameObject interRoadDeadEndPrefab;
        [SerializeField] private GameObject interRoadStraightPrefab;
        [SerializeField] private GameObject interRoadCornerPrefab;
        [SerializeField] private GameObject interRoadTJunctionPrefab;
        [SerializeField] private GameObject interRoadCrossPrefab;

        [Header("Players")]
        [SerializeField, Range(1, 6)]
        private int playerCount = 2;

        [SerializeField]
        private DefaultPlayerSettingData defaultPlayerSetting =
            new DefaultPlayerSettingData();

        [Header("Runtime Roots")]
        [SerializeField] private Transform boardSpawnRoot;
        [SerializeField] private Transform playerSpawnRoot;

        [Header("Managers - Optional")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private CardManager cardManager;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private SearchManager searchManager;
        [SerializeField] private RadioEventManager radioEventManager;
        [SerializeField] private BoardCameraController cameraController;

        [Header("Building Event System")]
        [SerializeField] private BoardRuntimeStateManager boardRuntimeStateManager;
        [SerializeField] private BuildingEventSessionManager buildingEventSessionManager;
        [SerializeField] private BuildingEventRuntimeManager buildingEventRuntimeManager;
        [SerializeField] private BuildingEventManager buildingEventManager;

        [Header("Runtime UI")]
        [Tooltip("보드와 플레이어 생성이 끝난 뒤 Ui_Util을 초기화합니다.")]
        [SerializeField] private bool initializeRuntimeUI = true;
        [SerializeField] private Ui_Util uiUtil;
        [SerializeField] private GameLogManager gameLogManager;

        [Header("Random")]
        [Tooltip("0이면 실행마다 다른 시드를 사용합니다.")]
        [SerializeField] private int fixedRandomSeed;

        private readonly List<GeneratedDistrict> generatedDistricts =
            new List<GeneratedDistrict>();

        private readonly Dictionary<Vector2Int, RoadCellRecord> roadCells =
            new Dictionary<Vector2Int, RoadCellRecord>();

        private readonly Dictionary<Vector2Int, BoardSpacePrefab> roadSpaces =
            new Dictionary<Vector2Int, BoardSpacePrefab>();

        private readonly List<BuildingRecord> buildings =
            new List<BuildingRecord>();

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        private System.Random random;
        private Transform generatedBoardRoot;

        private void Start()
        {
            if (generateOnStart)
            {
                CreateAndStartGame();
            }
        }

        [ContextMenu("Create And Start Game")]
        public void CreateAndStartGame()
        {
            ValidateSettings();
            EnsureManagers();
            PrepareRoots();

            gridManager.ClearAll();
            playerManager.ClearPlayers();
            generatedDistricts.Clear();
            roadCells.Clear();
            roadSpaces.Clear();
            buildings.Clear();
            boardRuntimeStateManager.ClearRuntimeState();
            buildingEventSessionManager.ClearSession();

            int seed =
                fixedRandomSeed == 0
                    ? Environment.TickCount
                    : fixedRandomSeed;

            random = new System.Random(seed);

            GenerateBoard();
            ConnectGeneratedSpaces();

            // 건물 정보 카드와 숨겨진 탐색 카드 슬롯은
            // 모든 건물 BoardSpace가 생성·등록된 뒤 초기화해야 합니다.
            cardManager.InitializeBoardSlots(
                gridManager,
                random);

            Bounds bounds = CalculateBoardBounds();

            if (cameraController != null)
            {
                cameraController.SetBoardBounds(
                    bounds,
                    true);
            }

            BoardSpacePrefab defaultStartNode =
                gridManager.GetNearestRoad(
                    bounds.center);

            if (defaultStartNode == null &&
                gridManager.BuildingSpaces.Count > 0)
            {
                defaultStartNode =
                    gridManager.BuildingSpaces[0];
            }

            playerManager.CreateDefaultPlayers(
                playerCount,
                defaultPlayerSetting,
                defaultStartNode,
                playerSpawnRoot);

            radioEventManager.Initialize(
                gridManager,
                new System.Random(seed + 3001));

            turnManager.Initialize(
                playerManager,
                radioEventManager,
                gameLogManager);

            buildingEventManager.Initialize(
                turnManager,
                cardManager,
                boardRuntimeStateManager,
                buildingEventSessionManager,
                buildingEventRuntimeManager,
                new System.Random(seed + 4001));

            searchManager.Initialize(
                gridManager,
                turnManager,
                cardManager,
                buildingEventManager,
                new System.Random(seed + 5001));

            InitializeRuntimeUI();

            turnManager.StartGameTurns();

            Debug.Log(
                "[GameBoardSettingManager] 생성 완료 / " +
                "지구 " + generatedDistricts.Count +
                " / 건물 " + buildings.Count +
                " / 도로 " + roadSpaces.Count +
                " / 플레이어 " + playerManager.Players.Count +
                " / Seed " + seed);
        }

        private void InitializeRuntimeUI()
        {
            if (!initializeRuntimeUI)
            {
                return;
            }

            if (uiUtil == null)
            {
                Debug.LogWarning(
                    "[GameBoardSettingManager] Ui_Util을 찾지 못해 런타임 UI를 초기화하지 못했습니다.",
                    this);

                return;
            }

            uiUtil.Initialize(
                gridManager,
                playerManager,
                turnManager,
                searchManager,
                cardManager,
                cameraController,
                gameLogManager);
        }

        private void ValidateSettings()
        {
            districtSlotGridSize.x =
                Mathf.Max(1, districtSlotGridSize.x);

            districtSlotGridSize.y =
                Mathf.Max(1, districtSlotGridSize.y);

            districtSize.x =
                Mathf.Max(4, districtSize.x);

            districtSize.y =
                Mathf.Max(4, districtSize.y);

            int maximumDistrictCount =
                districtSlotGridSize.x *
                districtSlotGridSize.y;

            districtCount =
                Mathf.Clamp(
                    districtCount,
                    1,
                    maximumDistrictCount);

            maximumDistrictGap =
                Mathf.Max(
                    minimumDistrictGap,
                    maximumDistrictGap);

            maximumInternalRoadCount =
                Mathf.Max(
                    minimumInternalRoadCount,
                    maximumInternalRoadCount);

            playerCount =
                Mathf.Clamp(
                    playerCount,
                    1,
                    6);

            if (defaultPlayerSetting == null)
            {
                defaultPlayerSetting =
                    new DefaultPlayerSettingData();
            }
        }

        private void EnsureManagers()
        {
            gridManager =
                GetOrAddManager(gridManager);

            playerManager =
                GetOrAddManager(playerManager);

            cardManager =
                GetOrAddManager(cardManager);

            turnManager =
                GetOrAddManager(turnManager);

            searchManager =
                GetOrAddManager(searchManager);

            radioEventManager =
                GetOrAddManager(radioEventManager);

            boardRuntimeStateManager =
                GetOrAddManager(boardRuntimeStateManager);

            buildingEventSessionManager =
                GetOrAddManager(buildingEventSessionManager);

            buildingEventRuntimeManager =
                GetOrAddManager(buildingEventRuntimeManager);

            buildingEventManager =
                GetOrAddManager(buildingEventManager);

            playerManager.Initialize(gridManager);

            if (cameraController == null)
            {
                cameraController =
                    FindObjectOfType<BoardCameraController>();
            }

            if (cameraController == null &&
                Camera.main != null)
            {
                cameraController =
                    Camera.main.GetComponent<BoardCameraController>();

                if (cameraController == null)
                {
                    cameraController =
                        Camera.main.gameObject.AddComponent<BoardCameraController>();
                }
            }

            if (gameLogManager == null)
            {
                gameLogManager =
                    FindObjectOfType<GameLogManager>();
            }

            if (uiUtil == null)
            {
                uiUtil =
                    GetComponent<Ui_Util>();
            }

            if (uiUtil == null)
            {
                uiUtil =
                    FindObjectOfType<Ui_Util>();
            }

            if (initializeRuntimeUI &&
                uiUtil == null)
            {
                uiUtil =
                    gameObject.AddComponent<Ui_Util>();
            }
        }

        private T GetOrAddManager<T>(T current)
            where T : Component
        {
            if (current != null)
            {
                return current;
            }

            T found = FindObjectOfType<T>();

            if (found != null)
            {
                return found;
            }

            return gameObject.AddComponent<T>();
        }

        private void PrepareRoots()
        {
            if (boardSpawnRoot == null)
            {
                GameObject boardRootObject =
                    GameObject.Find("Board_RuntimeRoot");

                if (boardRootObject == null)
                {
                    boardRootObject =
                        new GameObject("Board_RuntimeRoot");
                }

                boardSpawnRoot =
                    boardRootObject.transform;
            }

            if (playerSpawnRoot == null)
            {
                Transform existingPlayerRoot =
                    boardSpawnRoot.Find("Players");

                if (existingPlayerRoot == null)
                {
                    GameObject playerRootObject =
                        new GameObject("Players");

                    playerRootObject.transform.SetParent(
                        boardSpawnRoot,
                        false);

                    existingPlayerRoot =
                        playerRootObject.transform;
                }

                playerSpawnRoot =
                    existingPlayerRoot;
            }

            Transform previous =
                boardSpawnRoot.Find("GeneratedBoard");

            if (previous != null && clearOldBoard)
            {
                if (Application.isPlaying)
                {
                    Destroy(previous.gameObject);
                }
                else
                {
                    DestroyImmediate(previous.gameObject);
                }
            }

            GameObject generatedRootObject =
                new GameObject("GeneratedBoard");

            generatedRootObject.transform.SetParent(
                boardSpawnRoot,
                false);

            generatedBoardRoot =
                generatedRootObject.transform;
        }

        private void GenerateBoard()
        {
            List<Vector2Int> slots =
                GenerateCompactDistrictSlots();

            Dictionary<int, int> xStarts =
                BuildAxisStarts(
                    districtSlotGridSize.x,
                    districtSize.x);

            Dictionary<int, int> yStarts =
                BuildAxisStarts(
                    districtSlotGridSize.y,
                    districtSize.y);

            CreateDistrictRoots(
                slots,
                xStarts,
                yStarts);

            List<DistrictLinkRecord> links =
                CreateDistrictLinks();

            AssignGates(links);

            for (int index = 0;
                 index < generatedDistricts.Count;
                 index++)
            {
                GenerateDistrictContents(
                    generatedDistricts[index]);
            }

            GenerateInterDistrictRoads(links);
            InstantiateRoads();
            InstantiateBuildings();
        }

        private List<Vector2Int> GenerateCompactDistrictSlots()
        {
            List<Vector2Int> allSlots =
                new List<Vector2Int>();

            for (int y = 0;
                 y < districtSlotGridSize.y;
                 y++)
            {
                for (int x = 0;
                     x < districtSlotGridSize.x;
                     x++)
                {
                    allSlots.Add(
                        new Vector2Int(x, y));
                }
            }

            List<List<Vector2Int>> combinations =
                new List<List<Vector2Int>>();

            BuildSlotCombinations(
                allSlots,
                0,
                districtCount,
                new List<Vector2Int>(),
                combinations);

            int bestScore = int.MinValue;
            List<List<Vector2Int>> bestCandidates =
                new List<List<Vector2Int>>();

            for (int index = 0;
                 index < combinations.Count;
                 index++)
            {
                List<Vector2Int> candidate =
                    combinations[index];

                if (!IsConnectedSlotSet(candidate))
                {
                    continue;
                }

                int score =
                    ScoreSlotSet(candidate);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidates.Clear();
                    bestCandidates.Add(candidate);
                }
                else if (score == bestScore)
                {
                    bestCandidates.Add(candidate);
                }
            }

            if (bestCandidates.Count == 0)
            {
                return new List<Vector2Int>
                {
                    new Vector2Int(
                        districtSlotGridSize.x / 2,
                        districtSlotGridSize.y / 2)
                };
            }

            return new List<Vector2Int>(
                bestCandidates[
                    random.Next(
                        0,
                        bestCandidates.Count)]);
        }

        private void BuildSlotCombinations(
            List<Vector2Int> allSlots,
            int startIndex,
            int remaining,
            List<Vector2Int> current,
            List<List<Vector2Int>> output)
        {
            if (remaining == 0)
            {
                output.Add(
                    new List<Vector2Int>(current));

                return;
            }

            for (int index = startIndex;
                 index <= allSlots.Count - remaining;
                 index++)
            {
                current.Add(allSlots[index]);

                BuildSlotCombinations(
                    allSlots,
                    index + 1,
                    remaining - 1,
                    current,
                    output);

                current.RemoveAt(
                    current.Count - 1);
            }
        }

        private bool IsConnectedSlotSet(
            List<Vector2Int> slots)
        {
            if (slots.Count == 0)
            {
                return false;
            }

            HashSet<Vector2Int> set =
                new HashSet<Vector2Int>(slots);

            Queue<Vector2Int> open =
                new Queue<Vector2Int>();

            HashSet<Vector2Int> visited =
                new HashSet<Vector2Int>();

            open.Enqueue(slots[0]);
            visited.Add(slots[0]);

            while (open.Count > 0)
            {
                Vector2Int current =
                    open.Dequeue();

                for (int directionIndex = 0;
                     directionIndex < Directions.Length;
                     directionIndex++)
                {
                    Vector2Int next =
                        current +
                        Directions[directionIndex];

                    if (set.Contains(next) &&
                        visited.Add(next))
                    {
                        open.Enqueue(next);
                    }
                }
            }

            return visited.Count == slots.Count;
        }

        private int ScoreSlotSet(
            List<Vector2Int> slots)
        {
            HashSet<Vector2Int> set =
                new HashSet<Vector2Int>(slots);

            int adjacencyCount = 0;

            for (int index = 0;
                 index < slots.Count;
                 index++)
            {
                Vector2Int slot = slots[index];

                if (set.Contains(
                    slot + Vector2Int.right))
                {
                    adjacencyCount++;
                }

                if (set.Contains(
                    slot + Vector2Int.up))
                {
                    adjacencyCount++;
                }
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            for (int index = 0;
                 index < slots.Count;
                 index++)
            {
                minX =
                    Mathf.Min(minX, slots[index].x);

                maxX =
                    Mathf.Max(maxX, slots[index].x);

                minY =
                    Mathf.Min(minY, slots[index].y);

                maxY =
                    Mathf.Max(maxY, slots[index].y);
            }

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            int squarePenalty =
                Mathf.Abs(width - height);

            int areaPenalty =
                width * height;

            return
                adjacencyCount * 100 -
                squarePenalty * 15 -
                areaPenalty * 2;
        }

        private Dictionary<int, int> BuildAxisStarts(
            int slotCount,
            int districtCellLength)
        {
            Dictionary<int, int> result =
                new Dictionary<int, int>();

            result.Add(0, 0);

            for (int index = 1;
                 index < slotCount;
                 index++)
            {
                int gap =
                    random.Next(
                        minimumDistrictGap,
                        maximumDistrictGap + 1);

                result.Add(
                    index,
                    result[index - 1] +
                    districtCellLength +
                    gap);
            }

            int fullStart = result[0];
            int fullEnd =
                result[slotCount - 1] +
                districtCellLength -
                1;

            int centerOffset =
                Mathf.RoundToInt(
                    (fullStart + fullEnd) * 0.5f);

            List<int> keys =
                new List<int>(result.Keys);

            for (int index = 0;
                 index < keys.Count;
                 index++)
            {
                result[keys[index]] -=
                    centerOffset;
            }

            return result;
        }

        private void CreateDistrictRoots(
            List<Vector2Int> slots,
            Dictionary<int, int> xStarts,
            Dictionary<int, int> yStarts)
        {
            for (int index = 0;
                 index < slots.Count;
                 index++)
            {
                Vector2Int slot =
                    slots[index];

                Vector2Int origin =
                    new Vector2Int(
                        xStarts[slot.x],
                        yStarts[slot.y]);

                DistrictPrefab source =
                    GetDistrictSource(index);

                DistrictPrefab runtimeTheme = null;
                Transform districtRoot;

                if (source != null)
                {
                    runtimeTheme =
                        Instantiate(
                            source,
                            GridToWorld(origin),
                            Quaternion.identity,
                            generatedBoardRoot);

                    districtRoot =
                        runtimeTheme.transform;
                }
                else
                {
                    GameObject fallback =
                        new GameObject(
                            "District_" +
                            (index + 1).ToString("00"));

                    fallback.transform.SetParent(
                        generatedBoardRoot,
                        false);

                    fallback.transform.position =
                        GridToWorld(origin);

                    districtRoot =
                        fallback.transform;
                }

                districtRoot.gameObject.name =
                    "District_" +
                    (index + 1).ToString("00");

                generatedDistricts.Add(
                    new GeneratedDistrict
                    {
                        Index = index,
                        Slot = slot,
                        Origin = origin,
                        Root = districtRoot,
                        Theme = runtimeTheme
                    });
            }
        }

        private DistrictPrefab GetDistrictSource(
            int index)
        {
            if (districtPrefabs == null ||
                districtPrefabs.Length == 0)
            {
                return null;
            }

            int startIndex =
                index %
                districtPrefabs.Length;

            for (int offset = 0;
                 offset < districtPrefabs.Length;
                 offset++)
            {
                int sourceIndex =
                    (startIndex + offset) %
                    districtPrefabs.Length;

                if (districtPrefabs[sourceIndex] != null)
                {
                    return districtPrefabs[sourceIndex];
                }
            }

            return null;
        }

        private List<DistrictLinkRecord>
            CreateDistrictLinks()
        {
            Dictionary<Vector2Int, GeneratedDistrict> bySlot =
                new Dictionary<Vector2Int, GeneratedDistrict>();

            for (int index = 0;
                 index < generatedDistricts.Count;
                 index++)
            {
                bySlot.Add(
                    generatedDistricts[index].Slot,
                    generatedDistricts[index]);
            }

            List<DistrictLinkRecord> links =
                new List<DistrictLinkRecord>();

            for (int index = 0;
                 index < generatedDistricts.Count;
                 index++)
            {
                GeneratedDistrict district =
                    generatedDistricts[index];

                GeneratedDistrict east;

                if (bySlot.TryGetValue(
                    district.Slot + Vector2Int.right,
                    out east))
                {
                    links.Add(
                        CreateLink(
                            district,
                            east,
                            DistrictLinkDirection.East));
                }

                GeneratedDistrict north;

                if (bySlot.TryGetValue(
                    district.Slot + Vector2Int.up,
                    out north))
                {
                    links.Add(
                        CreateLink(
                            district,
                            north,
                            DistrictLinkDirection.North));
                }
            }

            return links;
        }

        private DistrictLinkRecord CreateLink(
            GeneratedDistrict a,
            GeneratedDistrict b,
            DistrictLinkDirection direction)
        {
            DistrictLinkRecord link =
                new DistrictLinkRecord();

            link.A = a;
            link.B = b;
            link.Direction = direction;

            int laneCount =
                random.NextDouble() <
                secondConnectionLaneChance
                    ? 2
                    : 1;

            int laneLimit =
                direction == DistrictLinkDirection.East
                    ? districtSize.y - 2
                    : districtSize.x - 2;

            laneLimit =
                Mathf.Max(1, laneLimit);

            List<int> availableA =
                CreateShuffledRange(
                    1,
                    laneLimit);

            List<int> availableB =
                CreateShuffledRange(
                    1,
                    laneLimit);

            laneCount =
                Mathf.Min(
                    laneCount,
                    availableA.Count,
                    availableB.Count);

            for (int laneIndex = 0;
                 laneIndex < laneCount;
                 laneIndex++)
            {
                link.Lanes.Add(
                    new LanePair
                    {
                        ALane = availableA[laneIndex],
                        BLane = availableB[laneIndex]
                    });
            }

            return link;
        }

        private List<int> CreateShuffledRange(
            int start,
            int count)
        {
            List<int> values =
                new List<int>();

            for (int index = 0;
                 index < count;
                 index++)
            {
                values.Add(start + index);
            }

            for (int index = values.Count - 1;
                 index > 0;
                 index--)
            {
                int swapIndex =
                    random.Next(0, index + 1);

                int temp = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = temp;
            }

            return values;
        }

        private void AssignGates(
            List<DistrictLinkRecord> links)
        {
            for (int linkIndex = 0;
                 linkIndex < links.Count;
                 linkIndex++)
            {
                DistrictLinkRecord link =
                    links[linkIndex];

                for (int laneIndex = 0;
                     laneIndex < link.Lanes.Count;
                     laneIndex++)
                {
                    LanePair lane =
                        link.Lanes[laneIndex];

                    if (link.Direction ==
                        DistrictLinkDirection.East)
                    {
                        lane.AGate =
                            new Vector2Int(
                                districtSize.x - 1,
                                lane.ALane);

                        lane.BGate =
                            new Vector2Int(
                                0,
                                lane.BLane);
                    }
                    else
                    {
                        lane.AGate =
                            new Vector2Int(
                                lane.ALane,
                                districtSize.y - 1);

                        lane.BGate =
                            new Vector2Int(
                                lane.BLane,
                                0);
                    }

                    link.A.Gates.Add(lane.AGate);
                    link.B.Gates.Add(lane.BGate);
                }
            }
        }

        private void GenerateDistrictContents(
            GeneratedDistrict district)
        {
            List<PlacedBuilding> placedBuildings =
                PlaceBuildings(district);

            HashSet<Vector2Int> localRoads =
                CreateLocalRoadNetwork(
                    district,
                    placedBuildings);

            foreach (Vector2Int localRoad in localRoads)
            {
                Vector2Int globalRoad =
                    district.Origin +
                    localRoad;

                if (!roadCells.ContainsKey(globalRoad))
                {
                    roadCells.Add(
                        globalRoad,
                        new RoadCellRecord
                        {
                            Cell = globalRoad,
                            DistrictIndex = district.Index,
                            Theme = district.Theme,
                            IsInterDistrict = false
                        });
                }
            }

            for (int index = 0;
                 index < placedBuildings.Count;
                 index++)
            {
                PlacedBuilding placed =
                    placedBuildings[index];

                buildings.Add(
                    new BuildingRecord
                    {
                        District = district,
                        OccupiedLocalCells =
                            placed.OccupiedLocalCells,
                        EntranceLocalCell =
                            placed.EntranceLocalCell,
                        Horizontal =
                            placed.Horizontal
                    });
            }
        }

        private List<PlacedBuilding> PlaceBuildings(
            GeneratedDistrict district)
        {
            List<PlacedBuilding> result =
                new List<PlacedBuilding>();

            HashSet<Vector2Int> occupiedFootprints =
                new HashSet<Vector2Int>(district.Gates);

            List<BuildingPlacementCandidate> candidates =
                BuildBuildingPlacementCandidates();

            ShuffleBuildingPlacementCandidates(candidates);

            for (int pass = 0;
                 pass < 2 && result.Count < buildingCountPerDistrict;
                 pass++)
            {
                int clearance =
                    pass == 0
                        ? preferredBuildingClearanceCells
                        : 0;

                for (int candidateIndex = 0;
                     candidateIndex < candidates.Count &&
                     result.Count < buildingCountPerDistrict;
                     candidateIndex++)
                {
                    BuildingPlacementCandidate candidate =
                        candidates[candidateIndex];

                    List<Vector2Int> occupiedCells =
                        GetFootprintCells(
                            candidate.Anchor,
                            candidate.Size);

                    if (!CanPlaceBuildingFootprint(
                            occupiedCells,
                            occupiedFootprints,
                            clearance))
                    {
                        continue;
                    }

                    List<Vector2Int> entranceCandidates =
                        GetEntranceCandidates(
                            occupiedCells,
                            occupiedFootprints);

                    if (entranceCandidates.Count == 0)
                    {
                        continue;
                    }

                    Vector2Int center =
                        new Vector2Int(
                            districtSize.x / 2,
                            districtSize.y / 2);

                    entranceCandidates.Sort(
                        delegate (Vector2Int first, Vector2Int second)
                        {
                            int firstDistance = Manhattan(first, center);
                            int secondDistance = Manhattan(second, center);

                            if (firstDistance != secondDistance)
                            {
                                return firstDistance.CompareTo(secondDistance);
                            }

                            int firstHash =
                                first.x * 73856093 ^ first.y * 19349663;

                            int secondHash =
                                second.x * 73856093 ^ second.y * 19349663;

                            return firstHash.CompareTo(secondHash);
                        });

                    Vector2Int entrance = entranceCandidates[0];

                    for (int cellIndex = 0;
                         cellIndex < occupiedCells.Count;
                         cellIndex++)
                    {
                        occupiedFootprints.Add(occupiedCells[cellIndex]);
                    }

                    result.Add(
                        new PlacedBuilding
                        {
                            OccupiedLocalCells = occupiedCells,
                            EntranceLocalCell = entrance,
                            Horizontal = candidate.Horizontal
                        });
                }
            }

            if (result.Count < buildingCountPerDistrict)
            {
                Debug.LogWarning(
                    "[GameBoardSettingManager] District " +
                    district.Index +
                    " 논리 건물 배치 수: " +
                    result.Count +
                    "/" +
                    buildingCountPerDistrict +
                    ". 구역 크기 또는 건물 수를 조정하십시오.");
            }

            return result;
        }

        private List<BuildingPlacementCandidate>
            BuildBuildingPlacementCandidates()
        {
            List<BuildingPlacementCandidate> result =
                new List<BuildingPlacementCandidate>();

            for (int orientationIndex = 0;
                 orientationIndex < 2;
                 orientationIndex++)
            {
                bool horizontal = orientationIndex == 0;

                Vector2Int size =
                    horizontal
                        ? new Vector2Int(2, 1)
                        : new Vector2Int(1, 2);

                int maximumX = districtSize.x - size.x;
                int maximumY = districtSize.y - size.y;

                for (int y = 0; y <= maximumY; y++)
                {
                    for (int x = 0; x <= maximumX; x++)
                    {
                        result.Add(
                            new BuildingPlacementCandidate
                            {
                                Anchor = new Vector2Int(x, y),
                                Size = size,
                                Horizontal = horizontal
                            });
                    }
                }
            }

            return result;
        }

        private void ShuffleBuildingPlacementCandidates(
            List<BuildingPlacementCandidate> candidates)
        {
            if (!searchWholeDistrictForBuildingPlacement)
            {
                return;
            }

            for (int index = candidates.Count - 1;
                 index > 0;
                 index--)
            {
                int swapIndex = random.Next(0, index + 1);

                BuildingPlacementCandidate temporary = candidates[index];
                candidates[index] = candidates[swapIndex];
                candidates[swapIndex] = temporary;
            }
        }

        private bool CanPlaceBuildingFootprint(
            IList<Vector2Int> footprint,
            ISet<Vector2Int> occupiedFootprints,
            int clearanceCells)
        {
            int clearance = Mathf.Max(0, clearanceCells);

            for (int index = 0; index < footprint.Count; index++)
            {
                Vector2Int cell = footprint[index];

                if (!IsInsideDistrict(cell))
                {
                    return false;
                }

                for (int offsetY = -clearance;
                     offsetY <= clearance;
                     offsetY++)
                {
                    for (int offsetX = -clearance;
                         offsetX <= clearance;
                         offsetX++)
                    {
                        Vector2Int checkedCell =
                            cell + new Vector2Int(offsetX, offsetY);

                        if (occupiedFootprints.Contains(checkedCell))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private HashSet<Vector2Int>
            CreateLocalRoadNetwork(
                GeneratedDistrict district,
                List<PlacedBuilding> placedBuildings)
        {
            HashSet<Vector2Int> blocked =
                new HashSet<Vector2Int>();

            for (int buildingIndex = 0;
                 buildingIndex < placedBuildings.Count;
                 buildingIndex++)
            {
                List<Vector2Int> occupied =
                    placedBuildings[buildingIndex]
                        .OccupiedLocalCells;

                for (int cellIndex = 0;
                     cellIndex < occupied.Count;
                     cellIndex++)
                {
                    blocked.Add(
                        occupied[cellIndex]);
                }
            }

            List<Vector2Int> terminals =
                new List<Vector2Int>();

            terminals.AddRange(
                district.Gates);

            for (int index = 0;
                 index < placedBuildings.Count;
                 index++)
            {
                terminals.Add(
                    placedBuildings[index]
                        .EntranceLocalCell);
            }

            Vector2Int hubA =
                FindNearestFreeCell(
                    new Vector2Int(
                        districtSize.x / 2,
                        districtSize.y / 2),
                    blocked);

            Vector2Int hubB =
                FindNearestFreeCell(
                    hubA +
                    new Vector2Int(
                        random.NextDouble() < 0.5
                            ? 1
                            : -1,
                        random.NextDouble() < 0.5
                            ? 1
                            : -1),
                    blocked);

            HashSet<Vector2Int> roads =
                new HashSet<Vector2Int>();

            roads.Add(hubA);
            roads.Add(hubB);

            for (int terminalIndex = 0;
                 terminalIndex < terminals.Count;
                 terminalIndex++)
            {
                Vector2Int terminal =
                    terminals[terminalIndex];

                Vector2Int target =
                    Manhattan(terminal, hubA) <=
                    Manhattan(terminal, hubB)
                        ? hubA
                        : hubB;

                List<Vector2Int> path =
                    FindPathInsideDistrict(
                        terminal,
                        target,
                        blocked);

                for (int pathIndex = 0;
                     pathIndex < path.Count;
                     pathIndex++)
                {
                    roads.Add(path[pathIndex]);
                }
            }

            int targetRoadCount =
                random.Next(
                    minimumInternalRoadCount,
                    maximumInternalRoadCount + 1);

            int expansionAttempts = 0;

            while (roads.Count < targetRoadCount &&
                   expansionAttempts < 500)
            {
                expansionAttempts++;

                List<Vector2Int> roadList =
                    new List<Vector2Int>(roads);

                Vector2Int current =
                    roadList[
                        random.Next(
                            0,
                            roadList.Count)];

                Vector2Int direction =
                    Directions[
                        random.Next(
                            0,
                            Directions.Length)];

                int branchLength =
                    random.Next(1, 4);

                for (int step = 0;
                     step < branchLength;
                     step++)
                {
                    current += direction;

                    if (!IsInsideDistrict(current) ||
                        blocked.Contains(current))
                    {
                        break;
                    }

                    roads.Add(current);

                    if (random.NextDouble() < 0.25)
                    {
                        direction =
                            Directions[
                                random.Next(
                                    0,
                                    Directions.Length)];
                    }
                }
            }

            return roads;
        }

        private List<Vector2Int>
            FindPathInsideDistrict(
                Vector2Int start,
                Vector2Int target,
                HashSet<Vector2Int> blocked)
        {
            Queue<Vector2Int> open =
                new Queue<Vector2Int>();

            Dictionary<Vector2Int, Vector2Int> parent =
                new Dictionary<Vector2Int, Vector2Int>();

            HashSet<Vector2Int> visited =
                new HashSet<Vector2Int>();

            open.Enqueue(start);
            visited.Add(start);

            while (open.Count > 0)
            {
                Vector2Int current =
                    open.Dequeue();

                if (current == target)
                {
                    return BuildPath(
                        parent,
                        start,
                        target);
                }

                List<Vector2Int> neighbors =
                    new List<Vector2Int>();

                for (int directionIndex = 0;
                     directionIndex < Directions.Length;
                     directionIndex++)
                {
                    Vector2Int next =
                        current +
                        Directions[directionIndex];

                    if (IsInsideDistrict(next) &&
                        !blocked.Contains(next) &&
                        !visited.Contains(next))
                    {
                        neighbors.Add(next);
                    }
                }

                neighbors.Sort(
                    delegate (
                        Vector2Int first,
                        Vector2Int second)
                    {
                        int firstDistance =
                            Manhattan(first, target);

                        int secondDistance =
                            Manhattan(second, target);

                        if (firstDistance != secondDistance)
                        {
                            return firstDistance.CompareTo(
                                secondDistance);
                        }

                        return random.Next(-1, 2);
                    });

                for (int neighborIndex = 0;
                     neighborIndex < neighbors.Count;
                     neighborIndex++)
                {
                    Vector2Int neighbor =
                        neighbors[neighborIndex];

                    visited.Add(neighbor);
                    parent.Add(neighbor, current);
                    open.Enqueue(neighbor);
                }
            }

            return new List<Vector2Int>
            {
                start
            };
        }

        private static List<Vector2Int> BuildPath(
            Dictionary<Vector2Int, Vector2Int> parent,
            Vector2Int start,
            Vector2Int target)
        {
            List<Vector2Int> result =
                new List<Vector2Int>();

            Vector2Int current = target;
            result.Add(current);

            while (current != start)
            {
                Vector2Int previous;

                if (!parent.TryGetValue(
                    current,
                    out previous))
                {
                    break;
                }

                current = previous;
                result.Add(current);
            }

            result.Reverse();
            return result;
        }

        private void GenerateInterDistrictRoads(
            List<DistrictLinkRecord> links)
        {
            for (int linkIndex = 0;
                 linkIndex < links.Count;
                 linkIndex++)
            {
                DistrictLinkRecord link =
                    links[linkIndex];

                for (int laneIndex = 0;
                     laneIndex < link.Lanes.Count;
                     laneIndex++)
                {
                    LanePair lane =
                        link.Lanes[laneIndex];

                    Vector2Int start =
                        link.A.Origin +
                        lane.AGate;

                    Vector2Int end =
                        link.B.Origin +
                        lane.BGate;

                    List<Vector2Int> path =
                        CreateBentInterDistrictPath(
                            start,
                            end,
                            link.Direction);

                    for (int pathIndex = 0;
                         pathIndex < path.Count;
                         pathIndex++)
                    {
                        AddRoadCell(
                            path[pathIndex],
                            -1,
                            null,
                            true);
                    }
                }
            }
        }

        private List<Vector2Int>
            CreateBentInterDistrictPath(
                Vector2Int startGate,
                Vector2Int endGate,
                DistrictLinkDirection direction)
        {
            List<Vector2Int> path =
                new List<Vector2Int>();

            if (direction ==
                DistrictLinkDirection.East)
            {
                int startX = startGate.x + 1;
                int endX = endGate.x - 1;

                int bendX =
                    startX <= endX
                        ? random.Next(
                            startX,
                            endX + 1)
                        : startX;

                AddHorizontalLine(
                    path,
                    new Vector2Int(
                        startX,
                        startGate.y),
                    new Vector2Int(
                        bendX,
                        startGate.y));

                AddVerticalLine(
                    path,
                    new Vector2Int(
                        bendX,
                        startGate.y),
                    new Vector2Int(
                        bendX,
                        endGate.y));

                AddHorizontalLine(
                    path,
                    new Vector2Int(
                        bendX,
                        endGate.y),
                    new Vector2Int(
                        endX,
                        endGate.y));
            }
            else
            {
                int startY = startGate.y + 1;
                int endY = endGate.y - 1;

                int bendY =
                    startY <= endY
                        ? random.Next(
                            startY,
                            endY + 1)
                        : startY;

                AddVerticalLine(
                    path,
                    new Vector2Int(
                        startGate.x,
                        startY),
                    new Vector2Int(
                        startGate.x,
                        bendY));

                AddHorizontalLine(
                    path,
                    new Vector2Int(
                        startGate.x,
                        bendY),
                    new Vector2Int(
                        endGate.x,
                        bendY));

                AddVerticalLine(
                    path,
                    new Vector2Int(
                        endGate.x,
                        bendY),
                    new Vector2Int(
                        endGate.x,
                        endY));
            }

            return path;
        }

        private static void AddHorizontalLine(
            List<Vector2Int> output,
            Vector2Int from,
            Vector2Int to)
        {
            int step =
                from.x <= to.x
                    ? 1
                    : -1;

            int x = from.x;

            while (true)
            {
                AddUnique(
                    output,
                    new Vector2Int(x, from.y));

                if (x == to.x)
                {
                    break;
                }

                x += step;
            }
        }

        private static void AddVerticalLine(
            List<Vector2Int> output,
            Vector2Int from,
            Vector2Int to)
        {
            int step =
                from.y <= to.y
                    ? 1
                    : -1;

            int y = from.y;

            while (true)
            {
                AddUnique(
                    output,
                    new Vector2Int(from.x, y));

                if (y == to.y)
                {
                    break;
                }

                y += step;
            }
        }

        private static void AddUnique(
            List<Vector2Int> output,
            Vector2Int cell)
        {
            if (!output.Contains(cell))
            {
                output.Add(cell);
            }
        }

        private void AddRoadCell(
            Vector2Int cell,
            int districtIndex,
            DistrictPrefab theme,
            bool isInterDistrict)
        {
            RoadCellRecord existing;

            if (roadCells.TryGetValue(
                cell,
                out existing))
            {
                if (isInterDistrict)
                {
                    existing.IsInterDistrict = true;
                }

                return;
            }

            roadCells.Add(
                cell,
                new RoadCellRecord
                {
                    Cell = cell,
                    DistrictIndex = districtIndex,
                    Theme = theme,
                    IsInterDistrict = isInterDistrict
                });
        }

        private void InstantiateRoads()
        {
            foreach (KeyValuePair<Vector2Int, RoadCellRecord> pair
                     in roadCells)
            {
                Vector2Int cell = pair.Key;
                RoadCellRecord record = pair.Value;

                RoadConnectionMask mask =
                    GetRoadMask(cell);

                Quaternion rotation;
                GameObject prefab =
                    GetRoadPrefab(
                        record,
                        mask,
                        out rotation);

                Transform parent =
                    record.IsInterDistrict ||
                    record.DistrictIndex < 0
                        ? generatedBoardRoot
                        : generatedDistricts[
                            record.DistrictIndex].Root;

                Vector3 worldPosition =
                    GridToWorld(cell);

                GameObject roadObject;

                if (prefab != null)
                {
                    roadObject =
                        Instantiate(
                            prefab,
                            worldPosition,
                            rotation,
                            parent);
                }
                else
                {
                    roadObject =
                        CreateFallbackRoad(
                            worldPosition,
                            rotation,
                            parent);
                }

                BoardSpacePrefab space =
                    roadObject.GetComponent<BoardSpacePrefab>();

                if (space == null)
                {
                    space =
                        roadObject.AddComponent<BoardSpacePrefab>();
                }

                string nodeId =
                    "ROAD_" +
                    cell.x +
                    "_" +
                    cell.y;

                space.ConfigureRuntime(
                    nodeId,
                    record.DistrictIndex >= 0
                        ? "DISTRICT-" +
                          (record.DistrictIndex + 1).ToString("00")
                        : "CITY",
                    cell,
                    BoardSpaceType.Road,
                    roadMovementAPCost,
                    true);

                roadSpaces.Add(
                    cell,
                    space);

                gridManager.RegisterSpace(space);
            }
        }

        private void InstantiateBuildings()
        {
            int globalBuildingIndex = 0;

            for (int index = 0;
                 index < buildings.Count;
                 index++)
            {
                BuildingRecord record =
                    buildings[index];

                globalBuildingIndex++;

                GameObject prefab =
                    record.District.Theme != null
                        ? record.District.Theme
                            .GetRandomBuildingPrefab(random)
                        : null;

                Vector2 center =
                    GetBuildingCenter(
                        record.District.Origin,
                        record.OccupiedLocalCells);

                Vector3 worldPosition =
                    new Vector3(
                        center.x * cellWorldSize,
                        0f,
                        center.y * cellWorldSize);

                Quaternion rotation =
                    record.Horizontal
                        ? Quaternion.identity
                        : Quaternion.Euler(
                            0f,
                            90f,
                            0f);

                GameObject buildingObject;

                if (prefab != null)
                {
                    buildingObject =
                        Instantiate(
                            prefab,
                            worldPosition,
                            rotation,
                            record.District.Root);
                }
                else
                {
                    buildingObject =
                        CreateFallbackBuilding(
                            worldPosition,
                            record.Horizontal,
                            record.District.Root);
                }

                BoardSpacePrefab space =
                    buildingObject.GetComponent<BoardSpacePrefab>();

                if (space == null)
                {
                    space =
                        buildingObject.AddComponent<BoardSpacePrefab>();
                }

                string nodeId =
                    "BUILDING_D" +
                    (record.District.Index + 1)
                        .ToString("00") +
                    "_" +
                    globalBuildingIndex.ToString("00");

                List<Vector2Int> globalOccupiedCells =
                    new List<Vector2Int>();

                for (int cellIndex = 0;
                     cellIndex < record.OccupiedLocalCells.Count;
                     cellIndex++)
                {
                    globalOccupiedCells.Add(
                        record.District.Origin +
                        record.OccupiedLocalCells[cellIndex]);
                }

                Vector2Int globalEntranceCell =
                    record.District.Origin +
                    record.EntranceLocalCell;

                string runtimeDistrictId =
                    "DISTRICT-" +
                    (record.District.Index + 1).ToString("00");

                string runtimeDistrictName =
                    record.District.Theme != null
                        ? record.District.Theme.DistrictName
                        : runtimeDistrictId;

                DistrictType runtimeDistrictType =
                    record.District.Theme != null
                        ? record.District.Theme.DistrictType
                        : DistrictType.Mixed;

                Sprite runtimeDistrictIcon =
                    record.District.Theme != null
                        ? record.District.Theme.DistrictIcon
                        : null;

                string runtimeDistrictShortName =
                    record.District.Theme != null
                        ? record.District.Theme.DistrictShortName
                        : DistrictPrefab.GetDistrictTypeDisplayName(
                            runtimeDistrictType);

                space.ConfigureBuildingRuntime(
                    nodeId,
                    runtimeDistrictId,
                    runtimeDistrictName,
                    runtimeDistrictType,
                    runtimeDistrictIcon,
                    runtimeDistrictShortName,
                    globalOccupiedCells,
                    globalEntranceCell,
                    record.Horizontal,
                    buildingMovementAPCost,
                    true);

                BuildingBoardPrefab buildingMetadata =
                    buildingObject.GetComponent<BuildingBoardPrefab>();

                if (buildingMetadata == null)
                {
                    buildingMetadata =
                        buildingObject.GetComponentInChildren<BuildingBoardPrefab>(true);
                }

                if (buildingMetadata == null)
                {
                    buildingMetadata =
                        buildingObject.AddComponent<BuildingBoardPrefab>();
                }

                buildingMetadata.BindRuntime(
                    nodeId,
                    runtimeDistrictId,
                    runtimeDistrictName,
                    runtimeDistrictType,
                    prefab != null ? prefab.name : nodeId);

                boardRuntimeStateManager.RegisterDistrict(
                    runtimeDistrictId,
                    runtimeDistrictName,
                    runtimeDistrictType);

                boardRuntimeStateManager.RegisterBuilding(buildingMetadata);

                record.RuntimeSpace = space;
                gridManager.RegisterSpace(space);
            }
        }

        private void ConnectGeneratedSpaces()
        {
            foreach (KeyValuePair<Vector2Int, BoardSpacePrefab> pair
                     in roadSpaces)
            {
                Vector2Int cell = pair.Key;
                BoardSpacePrefab road = pair.Value;

                for (int directionIndex = 0;
                     directionIndex < Directions.Length;
                     directionIndex++)
                {
                    BoardSpacePrefab neighbor;

                    if (roadSpaces.TryGetValue(
                        cell + Directions[directionIndex],
                        out neighbor))
                    {
                        road.ConnectBidirectional(neighbor);
                    }
                }
            }

            for (int index = 0;
                 index < buildings.Count;
                 index++)
            {
                BuildingRecord building =
                    buildings[index];

                if (building.RuntimeSpace == null)
                {
                    continue;
                }

                Vector2Int entranceGlobal =
                    building.District.Origin +
                    building.EntranceLocalCell;

                HashSet<Vector2Int> accessRoadCells =
                    new HashSet<Vector2Int>();

                accessRoadCells.Add(entranceGlobal);

                if (connectBuildingToAllAdjacentRoads)
                {
                    for (int occupiedIndex = 0;
                         occupiedIndex < building.OccupiedLocalCells.Count;
                         occupiedIndex++)
                    {
                        Vector2Int occupiedGlobal =
                            building.District.Origin +
                            building.OccupiedLocalCells[occupiedIndex];

                        for (int directionIndex = 0;
                             directionIndex < Directions.Length;
                             directionIndex++)
                        {
                            Vector2Int neighborCell =
                                occupiedGlobal + Directions[directionIndex];

                            if (roadSpaces.ContainsKey(neighborCell))
                            {
                                accessRoadCells.Add(neighborCell);
                            }
                        }
                    }
                }

                int connectedRoadCount = 0;

                foreach (Vector2Int accessCell in accessRoadCells)
                {
                    BoardSpacePrefab accessRoad;

                    if (roadSpaces.TryGetValue(accessCell, out accessRoad))
                    {
                        building.RuntimeSpace.ConnectBidirectional(accessRoad);
                        connectedRoadCount++;
                    }
                }

                if (connectedRoadCount == 0)
                {
                    Debug.LogWarning(
                        "[GameBoardSettingManager] 건물 인접 도로 누락: " +
                        building.RuntimeSpace.NodeId +
                        " / 입구 " +
                        entranceGlobal);
                }
            }
        }

        private RoadConnectionMask GetRoadMask(
            Vector2Int cell)
        {
            RoadConnectionMask mask =
                RoadConnectionMask.None;

            if (roadCells.ContainsKey(
                cell + Vector2Int.up))
            {
                mask |=
                    RoadConnectionMask.North;
            }

            if (roadCells.ContainsKey(
                cell + Vector2Int.right))
            {
                mask |=
                    RoadConnectionMask.East;
            }

            if (roadCells.ContainsKey(
                cell + Vector2Int.down))
            {
                mask |=
                    RoadConnectionMask.South;
            }

            if (roadCells.ContainsKey(
                cell + Vector2Int.left))
            {
                mask |=
                    RoadConnectionMask.West;
            }

            return mask;
        }

        private GameObject GetRoadPrefab(
            RoadCellRecord record,
            RoadConnectionMask mask,
            out Quaternion rotation)
        {
            if (!record.IsInterDistrict &&
                record.Theme != null)
            {
                return record.Theme.GetRoadPrefab(
                    mask,
                    out rotation);
            }

            rotation = Quaternion.identity;
            int count =
                DistrictPrefab.CountDirections(mask);

            if (count >= 4)
            {
                return interRoadCrossPrefab;
            }

            if (count == 3)
            {
                rotation =
                    DistrictPrefab.RotationForTJunction(mask);

                return interRoadTJunctionPrefab;
            }

            if (count == 2)
            {
                bool northSouth =
                    DistrictPrefab.Has(
                        mask,
                        RoadConnectionMask.North) &&
                    DistrictPrefab.Has(
                        mask,
                        RoadConnectionMask.South);

                bool eastWest =
                    DistrictPrefab.Has(
                        mask,
                        RoadConnectionMask.East) &&
                    DistrictPrefab.Has(
                        mask,
                        RoadConnectionMask.West);

                if (northSouth ||
                    eastWest)
                {
                    rotation =
                        eastWest
                            ? Quaternion.Euler(
                                0f,
                                90f,
                                0f)
                            : Quaternion.identity;

                    return interRoadStraightPrefab;
                }

                rotation =
                    DistrictPrefab.RotationForCorner(mask);

                return interRoadCornerPrefab;
            }

            if (count == 1)
            {
                rotation =
                    DistrictPrefab.RotationForDeadEnd(mask);

                return interRoadDeadEndPrefab;
            }

            return interRoadStraightPrefab;
        }

        private GameObject CreateFallbackRoad(
            Vector3 worldPosition,
            Quaternion rotation,
            Transform parent)
        {
            GameObject road =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            road.name = "FallbackRoad";
            road.transform.SetParent(parent, true);
            road.transform.position = worldPosition;
            road.transform.rotation = rotation;
            road.transform.localScale =
                new Vector3(
                    cellWorldSize * 0.9f,
                    0.1f,
                    cellWorldSize * 0.9f);

            return road;
        }

        private GameObject CreateFallbackBuilding(
            Vector3 worldPosition,
            bool horizontal,
            Transform parent)
        {
            GameObject building =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            building.name =
                "FallbackBuilding";

            building.transform.SetParent(
                parent,
                true);

            building.transform.position =
                worldPosition +
                Vector3.up * 0.4f;

            building.transform.localScale =
                horizontal
                    ? new Vector3(
                        cellWorldSize * 1.8f,
                        0.8f,
                        cellWorldSize * 0.8f)
                    : new Vector3(
                        cellWorldSize * 0.8f,
                        0.8f,
                        cellWorldSize * 1.8f);

            return building;
        }

        private Bounds CalculateBoardBounds()
        {
            bool initialized = false;
            Bounds bounds =
                new Bounds(
                    Vector3.zero,
                    Vector3.zero);

            foreach (BoardSpacePrefab space
                     in gridManager.AllSpaces)
            {
                Renderer[] renderers =
                    space.GetComponentsInChildren<Renderer>();

                if (renderers.Length == 0)
                {
                    if (!initialized)
                    {
                        bounds =
                            new Bounds(
                                space.transform.position,
                                Vector3.one *
                                cellWorldSize);

                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(
                            space.transform.position);
                    }

                    continue;
                }

                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    if (!initialized)
                    {
                        bounds =
                            renderers[rendererIndex].bounds;

                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(
                            renderers[rendererIndex].bounds);
                    }
                }
            }

            if (!initialized)
            {
                bounds =
                    new Bounds(
                        Vector3.zero,
                        Vector3.one * 10f);
            }

            return bounds;
        }

        private List<Vector2Int> GetFootprintCells(
            Vector2Int anchor,
            Vector2Int size)
        {
            List<Vector2Int> cells =
                new List<Vector2Int>();

            for (int y = 0;
                 y < size.y;
                 y++)
            {
                for (int x = 0;
                     x < size.x;
                     x++)
                {
                    cells.Add(
                        anchor +
                        new Vector2Int(x, y));
                }
            }

            return cells;
        }

        private List<Vector2Int> GetEntranceCandidates(
            List<Vector2Int> occupiedCells,
            HashSet<Vector2Int> blocked)
        {
            HashSet<Vector2Int> candidates =
                new HashSet<Vector2Int>();

            for (int cellIndex = 0;
                 cellIndex < occupiedCells.Count;
                 cellIndex++)
            {
                Vector2Int cell =
                    occupiedCells[cellIndex];

                for (int directionIndex = 0;
                     directionIndex < Directions.Length;
                     directionIndex++)
                {
                    Vector2Int candidate =
                        cell +
                        Directions[directionIndex];

                    if (IsInsideDistrict(candidate) &&
                        !blocked.Contains(candidate) &&
                        !occupiedCells.Contains(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            return new List<Vector2Int>(
                candidates);
        }

        private Vector2Int FindNearestFreeCell(
            Vector2Int requested,
            HashSet<Vector2Int> blocked)
        {
            requested.x =
                Mathf.Clamp(
                    requested.x,
                    0,
                    districtSize.x - 1);

            requested.y =
                Mathf.Clamp(
                    requested.y,
                    0,
                    districtSize.y - 1);

            if (!blocked.Contains(requested))
            {
                return requested;
            }

            Vector2Int best =
                Vector2Int.zero;

            int bestDistance =
                int.MaxValue;

            for (int y = 0;
                 y < districtSize.y;
                 y++)
            {
                for (int x = 0;
                     x < districtSize.x;
                     x++)
                {
                    Vector2Int cell =
                        new Vector2Int(x, y);

                    if (blocked.Contains(cell))
                    {
                        continue;
                    }

                    int distance =
                        Manhattan(
                            cell,
                            requested);

                    if (distance < bestDistance)
                    {
                        best = cell;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        private bool IsInsideDistrict(
            Vector2Int cell)
        {
            return
                cell.x >= 0 &&
                cell.y >= 0 &&
                cell.x < districtSize.x &&
                cell.y < districtSize.y;
        }

        private Vector3 GridToWorld(
            Vector2Int cell)
        {
            return new Vector3(
                cell.x * cellWorldSize,
                0f,
                cell.y * cellWorldSize);
        }

        private static int Manhattan(
            Vector2Int first,
            Vector2Int second)
        {
            return
                Mathf.Abs(
                    first.x - second.x) +
                Mathf.Abs(
                    first.y - second.y);
        }

        private static Vector2 GetBuildingCenter(
            Vector2Int districtOrigin,
            List<Vector2Int> occupiedLocalCells)
        {
            float totalX = 0f;
            float totalY = 0f;

            for (int index = 0;
                 index < occupiedLocalCells.Count;
                 index++)
            {
                totalX +=
                    occupiedLocalCells[index].x;

                totalY +=
                    occupiedLocalCells[index].y;
            }

            float count =
                Mathf.Max(
                    1,
                    occupiedLocalCells.Count);

            return new Vector2(
                districtOrigin.x +
                totalX / count,
                districtOrigin.y +
                totalY / count);
        }

        [ContextMenu("Test/Move Current Player To First Connected")]
        private void TestMoveCurrentPlayer()
        {
            PlayerData player =
                turnManager != null
                    ? turnManager.CurrentPlayer
                    : null;

            if (player == null)
            {
                return;
            }

            BoardSpacePrefab current;

            if (!gridManager.TryGetSpace(
                player.CurrentNodeId,
                out current))
            {
                return;
            }

            if (current.ConnectedSpaces.Count == 0)
            {
                return;
            }

            BoardSpacePrefab destination =
                current.ConnectedSpaces[0];

            if (!turnManager.TrySpendCurrentPlayerAP(
                destination.MovementAPCost))
            {
                Debug.LogWarning(
                    "[GameBoardSettingManager] 이동 AP 부족");

                return;
            }

            playerManager.TryMovePlayer(
                player.PlayerId,
                destination.NodeId);
        }

        [ContextMenu("Test/Search Current Player")]
        private void TestSearchCurrentPlayer()
        {
            if (searchManager != null)
            {
                searchManager.TrySearchCurrentPlayer();
            }
        }

        [ContextMenu("Test/End Current Turn")]
        private void TestEndCurrentTurn()
        {
            if (turnManager != null)
            {
                turnManager.EndCurrentTurn();
            }
        }

        private sealed class GeneratedDistrict
        {
            public int Index;
            public Vector2Int Slot;
            public Vector2Int Origin;
            public Transform Root;
            public DistrictPrefab Theme;

            public readonly List<Vector2Int> Gates =
                new List<Vector2Int>();
        }

        private sealed class DistrictLinkRecord
        {
            public GeneratedDistrict A;
            public GeneratedDistrict B;
            public DistrictLinkDirection Direction;

            public readonly List<LanePair> Lanes =
                new List<LanePair>();
        }

        private sealed class LanePair
        {
            public int ALane;
            public int BLane;
            public Vector2Int AGate;
            public Vector2Int BGate;
        }

        private sealed class RoadCellRecord
        {
            public Vector2Int Cell;
            public int DistrictIndex;
            public DistrictPrefab Theme;
            public bool IsInterDistrict;
        }

        private sealed class BuildingPlacementCandidate
        {
            public Vector2Int Anchor;
            public Vector2Int Size;
            public bool Horizontal;
        }

        private sealed class PlacedBuilding
        {
            public List<Vector2Int> OccupiedLocalCells;
            public Vector2Int EntranceLocalCell;
            public bool Horizontal;
        }

        private sealed class BuildingRecord
        {
            public GeneratedDistrict District;
            public List<Vector2Int> OccupiedLocalCells;
            public Vector2Int EntranceLocalCell;
            public bool Horizontal;
            public BoardSpacePrefab RuntimeSpace;
        }
    }
}

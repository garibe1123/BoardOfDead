using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BoardOfDead
{
    [DefaultExecutionOrder(-12000)]
    [DisallowMultipleComponent]
    public sealed class ProceduralCityBoardManager : MonoBehaviour
    {
        [Header("GameBoardSettingManager Source")]
        [Tooltip("새 보드를 독립 생성하지 않습니다. 기존 GameBoardSettingManager의 생성 결과를 원본으로 사용합니다.")]
        [SerializeField] private GameBoardSettingManager sourceBoardManager;

        [SerializeField] private GridManager gridManager;
        [SerializeField] private bool generateOnStart = true;

        [SerializeField, HideInInspector]
        private int appliedVisualPresetVersion;

        private const int CurrentVisualPresetVersion = 11;

        private static readonly Vector2Int[] RuntimeNeighborDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        [Tooltip("기존 GameBoardSettingManager의 Start 자동 실행을 막고 이 매니저가 CreateAndStartGame을 직접 한 번 호출합니다.")]
        [SerializeField] private bool controlSourceGeneration = true;

        [SerializeField] private bool autoRefreshWhenBoardChanges = true;
        [SerializeField, Min(0.1f)] private float refreshCheckInterval = 0.5f;

        [Header("Actual Secondary Road Nodes")]
        [Tooltip("기존 도로에서 파생된 보조 도로를 실제 BoardSpacePrefab 도로 노드로 추가하고 GridManager에 등록합니다. 시각 전용 도로가 아닙니다.")]
        [SerializeField] private bool expandGameRoadNetwork = true;

        [SerializeField] private int secondaryRoadSeed = 46327;

        [SerializeField, Range(0f, 0.65f)]
        private float secondaryRoadBranchChance = 0.42f;

        [SerializeField, Range(1, 8)]
        private int secondaryRoadMinimumLength = 3;

        [SerializeField, Range(1, 12)]
        private int secondaryRoadMaximumLength = 9;

        [SerializeField, Range(0f, 0.8f)]
        private float secondaryRoadTurnChance = 0.22f;

        [Tooltip("보조 도로 중 일부를 X/Y 교대 스텝으로 생성해 시각적으로 사선 도로가 되게 합니다.")]
        [SerializeField, Range(0f, 0.75f)]
        private float secondaryRoadDiagonalChance = 0.62f;

        [SerializeField, Min(0)]
        private int maximumSecondaryRoadCells = 520;

        [SerializeField, Range(0, 5)]
        private int secondaryRoadBoundsExpansionCells = 1;

        [SerializeField, Range(0, 3)]
        private int secondaryRoadBuildingClearanceCells = 1;

        [SerializeField, Min(0)]
        private int secondaryRoadMovementAPCost = 1;

        [Header("Continuous Road Curve")]
        [Tooltip("블록 한 칸 대비 도로 폭입니다. 0.18~0.28 정도가 도시 도로처럼 얇게 보입니다.")]
        [SerializeField, Range(0.10f, 0.60f)]
        private float roadWidthCells = 0.21f;

        [Tooltip("도로 체인 전체에 적용되는 완만한 좌우 흔들림입니다. 각 셀을 따로 흔들지 않습니다.")]
        [SerializeField, Range(0f, 0.42f)]
        private float roadCrookednessCells = 0.16f;

        [Tooltip("값이 클수록 긴 파장으로 천천히 흔들립니다.")]
        [SerializeField, Range(1.5f, 12f)]
        private float roadWobbleWavelengthCells = 5.8f;

        [SerializeField] private int roadCrookednessSeed = 8173;

        [Tooltip("교차로를 논리 좌표에 얼마나 강하게 고정할지 결정합니다.")]
        [SerializeField, Range(0f, 1f)]
        private float intersectionStability = 0.94f;

        [SerializeField, Range(0, 4)]
        private int roadSmoothingIterations = 2;

        [SerializeField, Range(1, 10)]
        private int roadSamplesPerCell = 7;

        [Header("Organic Edge Connections")]
        [Tooltip("인접한 도로 노드 사이를 단순 직선으로 잇지 않고 중간 제어점을 삽입할 확률입니다.")]
        [SerializeField, Range(0f, 1f)]
        private float roadEdgeBendChance = 0.82f;

        [Tooltip("각 도로 구간이 옆으로 휘거나 사선으로 진입하는 최대 폭입니다.")]
        [SerializeField, Range(0f, 0.45f)]
        private float roadEdgeBendCells = 0.20f;

        [Tooltip("중간 제어점이 구간 중심에서 앞뒤로 이동하는 정도입니다.")]
        [SerializeField, Range(0f, 0.35f)]
        private float roadEdgeLongitudinalJitterCells = 0.12f;

        [SerializeField, Range(0.9f, 1.5f)]
        private float roadJunctionRadiusMultiplier = 1.08f;

        [SerializeField, Min(0.005f)]
        private float roadThickness = 0.045f;

        [SerializeField]
        private float roadSurfaceOffset = 0.025f;

        [SerializeField]
        private Color roadColor = new Color(0.095f, 0.105f, 0.115f, 1f);

        [Header("Sidewalk")]
        [SerializeField] private bool createSidewalks = true;

        [SerializeField, Range(0f, 0.30f)]
        private float sidewalkWidthCells = 0.075f;

        [SerializeField, Min(0.005f)]
        private float sidewalkHeight = 0.035f;

        [SerializeField]
        private Color sidewalkColor = new Color(0.40f, 0.40f, 0.39f, 1f);

        [Header("Building Classification")]
        [SerializeField] private bool createDenseSmallBuildings = true;

        [Tooltip("기존 BuildingSpaces를 장식 건물과 분리된 역할 건물로 시각화합니다.")]
        [SerializeField] private bool createLandmarkBuildings = true;

        [SerializeField, Min(0)]
        private int maximumDecorativeBuildingCount = 8200;

        [SerializeField] private int smallBuildingSeed = 19331;

        [Header("Decorative Roadside Buildings")]
        [SerializeField, Range(0f, 1f)]
        private float roadsidePlacementChance = 0.999f;

        [SerializeField, Range(1, 4)]
        private int roadsideRows = 3;

        [SerializeField, Range(0.12f, 1.0f)]
        private float minimumSampleSpacingCells = 0.14f;

        [SerializeField, Range(0.16f, 1.4f)]
        private float maximumSampleSpacingCells = 0.24f;

        [Tooltip("장식 건물의 최소 폭입니다. 여러 매스가 겹쳐도 이 외곽 범위를 기준으로 생성됩니다.")]
        [SerializeField, Range(0.12f, 0.8f)]
        private float decorativeMinimumWidthCells = 0.24f;

        [SerializeField, Range(0.16f, 1.0f)]
        private float decorativeMaximumWidthCells = 0.48f;

        [SerializeField, Range(0.12f, 0.8f)]
        private float decorativeMinimumDepthCells = 0.22f;

        [SerializeField, Range(0.16f, 1.0f)]
        private float decorativeMaximumDepthCells = 0.42f;

        [SerializeField, Range(1, 5)]
        private int decorativeMinimumFloors = 1;

        [SerializeField, Range(1, 8)]
        private int decorativeMaximumFloors = 3;

        [Tooltip("장식 건물의 최대 폭/깊이를 역할 건물 최대 크기의 이 비율 이하로 강제합니다.")]
        [SerializeField, Range(0.20f, 0.50f)]
        private float decorativeMaximumLandmarkRatio = 0.50f;

        [Header("Landmark Building Footprint")]
        [Tooltip("기존 BuildingSpace 하나를 나타내는 역할 건물의 폭 범위입니다.")]
        [SerializeField, Range(0.55f, 1.8f)]
        private float landmarkMinimumWidthCells = 0.78f;

        [SerializeField, Range(0.65f, 2.2f)]
        private float landmarkMaximumWidthCells = 1.12f;

        [SerializeField, Range(0.50f, 1.6f)]
        private float landmarkMinimumDepthCells = 0.68f;

        [SerializeField, Range(0.60f, 2.0f)]
        private float landmarkMaximumDepthCells = 0.98f;

        [SerializeField, Range(1, 8)]
        private int landmarkMinimumFloors = 4;

        [SerializeField, Range(2, 14)]
        private int landmarkMaximumFloors = 11;

        [Header("Building Height And Shape Variety")]
        [Tooltip("0이면 구역 높이 성향 위주, 1이면 개별 건물 랜덤성이 강해집니다.")]
        [SerializeField, Range(0f, 1f)]
        private float buildingHeightRandomness = 0.58f;

        [Tooltip("일부 건물을 상한 층수에 가까운 고층으로 올릴 확률입니다.")]
        [SerializeField, Range(0f, 0.6f)]
        private float tallBuildingChance = 0.08f;

        [Tooltip("도로 방향을 유지하면서 건물마다 추가되는 미세 회전 각도입니다.")]
        [SerializeField, Range(0f, 20f)]
        private float buildingAngleJitterDegrees = 10f;

        [Tooltip("단일 박스 대신 서로 겹친 2~4개 매스로 건물을 만들 확률입니다.")]
        [SerializeField, Range(0f, 1f)]
        private float compositeMassChance = 0.16f;

        [SerializeField, Range(0f, 1f)]
        private float steppedSilhouetteChance = 0.10f;

        [SerializeField, Range(0f, 1f)]
        private float terraceChance = 0.06f;

        [SerializeField, Range(0f, 1f)]
        private float rooftopDetailChance = 0.10f;

        [SerializeField, Min(0.06f)]
        private float buildingFloorWorldHeight = 0.27f;

        [SerializeField, Range(0f, 0.35f)]
        private float buildingSetbackCells = 0.025f;

        [SerializeField, Range(0f, 0.35f)]
        private float buildingRowGapCells = 0.004f;

        [SerializeField, Range(0f, 0.35f)]
        private float generatedBuildingGapCells = 0.006f;

        [Header("Landmark Neighborhood Density")]
        [Tooltip("역할 건물 주변에 별도의 소형 건물 군집을 생성합니다.")]
        [SerializeField] private bool densifyLandmarkNeighborhoods = true;

        [SerializeField, Range(4, 32)]
        private int landmarkNeighborAttemptsPerBuilding = 18;

        [SerializeField, Range(0f, 1f)]
        private float landmarkNeighborPlacementChance = 0.92f;

        [SerializeField, Range(0.25f, 1.8f)]
        private float landmarkNeighborhoodRadiusCells = 1.10f;

        [SerializeField, Range(0f, 2f)]
        private float buildingIntersectionClearanceCells = 0.16f;

        [Header("Strict Building Placement")]
        [Tooltip("장식 건물 중심이 도로에서 이 거리보다 멀면 생성하지 않습니다.")]
        [SerializeField, Range(0.45f, 2.5f)]
        private float maximumDecorativeRoadDistanceCells = 1.48f;

        [Tooltip("역할 건물 비주얼도 도로에서 너무 멀면 도로 쪽으로 당겨 배치합니다.")]
        [SerializeField, Range(0.75f, 3f)]
        private float maximumLandmarkRoadDistanceCells = 1.65f;

        [Tooltip("복합 매스와 테라스 돌출까지 포함한 보수적 충돌 판정 배율입니다.")]
        [SerializeField, Range(1.0f, 1.6f)]
        private float buildingCollisionEnvelopeMultiplier = 1.01f;

        [Tooltip("같은 실루엣 템플릿이 이 거리 안에서 이웃하지 않도록 합니다.")]
        [SerializeField, Range(0.15f, 3f)]
        private float sameTemplateSeparationCells = 0.30f;

        [Header("Interior Lot Fill")]
        [Tooltip("도로에서 먼 내부를 건물로 채우지 않고 녹지로 남기는 것이 기본입니다.")]
        [SerializeField] private bool fillInteriorLots = false;

        [SerializeField, Range(0f, 1f)]
        private float interiorFillChance = 0.30f;

        [SerializeField, Range(0.20f, 1.2f)]
        private float interiorGridStepCells = 0.58f;

        [Tooltip("활성화하더라도 이 거리 안쪽까지만 내부 장식 건물을 허용합니다.")]
        [SerializeField, Range(0.5f, 2.5f)]
        private float interiorMaximumRoadDistanceCells = 1.25f;

        [SerializeField]
        private float buildingBaseYOffset = 0.035f;

        [SerializeField]
        private Color smallBuildingColorA = new Color(0.48f, 0.49f, 0.50f, 1f);

        [SerializeField]
        private Color smallBuildingColorB = new Color(0.39f, 0.42f, 0.45f, 1f);

        [SerializeField]
        private Color smallBuildingColorC = new Color(0.56f, 0.54f, 0.50f, 1f);

        [Header("Landmark Role Colors")]
        [Tooltip("대형 아파트 / 주거 타워")]
        [SerializeField]
        private Color apartmentColor = new Color(0.34f, 0.46f, 0.62f, 1f);

        [Tooltip("쇼핑몰 / 상업 시설")]
        [SerializeField]
        private Color shoppingMallColor = new Color(0.74f, 0.48f, 0.22f, 1f);

        [Tooltip("병원 / 의료 시설")]
        [SerializeField]
        private Color hospitalColor = new Color(0.72f, 0.82f, 0.80f, 1f);

        [Tooltip("오피스 / 업무 시설")]
        [SerializeField]
        private Color officeColor = new Color(0.29f, 0.55f, 0.61f, 1f);

        [Tooltip("공장 / 물류 시설")]
        [SerializeField]
        private Color industrialColor = new Color(0.54f, 0.34f, 0.29f, 1f);

        [Tooltip("공공 / 문화 시설")]
        [SerializeField]
        private Color civicColor = new Color(0.47f, 0.40f, 0.61f, 1f);

        [Header("Distinct Logical Building Palette")]
        [Tooltip("기존 씬에 저장된 회색 계열 색상 대신, 아래의 논리 건물 전용 팔레트를 우선 사용합니다.")]
        [SerializeField] private bool useDistinctLogicalBuildingPalette = true;

        [SerializeField]
        private Color logicalGenericColorA =
            new Color(0.36f, 0.49f, 0.58f, 1f);

        [SerializeField]
        private Color logicalGenericColorB =
            new Color(0.52f, 0.43f, 0.31f, 1f);

        [SerializeField]
        private Color logicalGenericColorC =
            new Color(0.40f, 0.51f, 0.39f, 1f);

        [SerializeField]
        private Color logicalApartmentColor =
            new Color(0.27f, 0.48f, 0.72f, 1f);

        [SerializeField]
        private Color logicalShoppingMallColor =
            new Color(0.86f, 0.48f, 0.18f, 1f);

        [SerializeField]
        private Color logicalHospitalColor =
            new Color(0.65f, 0.84f, 0.79f, 1f);

        [SerializeField]
        private Color logicalOfficeColor =
            new Color(0.20f, 0.63f, 0.72f, 1f);

        [SerializeField]
        private Color logicalIndustrialColor =
            new Color(0.67f, 0.31f, 0.24f, 1f);

        [SerializeField]
        private Color logicalCivicColor =
            new Color(0.58f, 0.37f, 0.72f, 1f);

        [SerializeField]
        private Color logicalBuildingExtraColorA =
            new Color(0.70f, 0.59f, 0.24f, 1f);

        [SerializeField]
        private Color logicalBuildingExtraColorB =
            new Color(0.70f, 0.38f, 0.50f, 1f);

        [Header("Logical Building Color Rules")]
        [Tooltip("카드가 붙는 모든 대형 건물은 기단, 타워, 옥상까지 단일 색으로 통일합니다.")]
        [SerializeField] private bool uniformLogicalBuildingColor = true;

        [Tooltip("인접하거나 가까운 논리 건물끼리는 동일한 색상을 사용하지 않습니다.")]
        [SerializeField] private bool preventNeighboringLogicalBuildingColors = true;

        [Tooltip("두 논리 건물을 색상상 이웃으로 판정하는 보드 셀 거리입니다. 2면 한 칸의 빈 공간을 사이에 둔 건물도 다른 색으로 처리합니다.")]
        [SerializeField, Range(1f, 4f)]
        private float logicalBuildingColorNeighborDistanceCells = 2.10f;

        [Header("Decorative Achromatic Palette")]
        [Tooltip("도로변 장식 건물과 나무는 색조를 제거하고 중간 회색 범위로 강제합니다.")]
        [SerializeField] private bool forceDecorativeAchromaticPalette = true;

        [Tooltip("장식 요소에 허용되는 가장 어두운 회색입니다. 0이나 검정은 허용하지 않습니다.")]
        [SerializeField, Range(0.12f, 0.45f)]
        private float decorativeMinimumGrayValue = 0.22f;

        [Tooltip("장식 요소에 허용되는 가장 밝은 회색입니다. 1이나 순백색은 허용하지 않습니다.")]
        [SerializeField, Range(0.50f, 0.85f)]
        private float decorativeMaximumGrayValue = 0.68f;

        [Header("Dense Gray Urban Woodland")]
        [Tooltip("도로 주변의 제한된 띠 영역만 회색 수목으로 채웁니다. 지면/바다/잔디는 생성하지 않습니다.")]
        [SerializeField] private bool createUrbanWoodland = true;

        [SerializeField] private int woodlandSeed = 74219;

        [Tooltip("기존 보드 외곽에서 회색 수목지대가 확장되는 폭입니다.")]
        [SerializeField, Range(1.5f, 16f)]
        private float woodlandMarginCells = 1.75f;

        [SerializeField, Range(0f, 0.22f)]
        private float woodlandBoundaryIrregularity = 0.075f;

        [SerializeField, Range(16, 128)]
        private int woodlandBoundarySegments = 64;

        [Tooltip("도로에서 이 거리보다 가까운 곳에는 수목을 생성하지 않습니다.")]
        [SerializeField, Range(0.15f, 2f)]
        private float woodlandMinimumRoadDistanceCells = 0.62f;

        [Tooltip("수목은 도로에서 이 거리 이내에만 생성됩니다. 이 값을 넘는 깊은 외곽은 비워둡니다.")]
        [SerializeField, Range(0.5f, 3f)]
        private float woodlandMaximumRoadDistanceCells = 1.50f;

        [SerializeField, Range(0.1f, 2f)]
        private float woodlandBuildingClearanceCells = 0.24f;

        [SerializeField, Range(0.20f, 1.2f)]
        private float woodlandSampleStepCells = 0.34f;

        [SerializeField, Range(0f, 1f)]
        private float innerWoodlandChance = 0.56f;

        [SerializeField, Range(0f, 1f)]
        private float outerWoodlandChance = 0.56f;

        [SerializeField, Min(0)]
        private int maximumTreeCount = 2200;

        [SerializeField, Range(0.05f, 0.36f)]
        private float minimumTreeRadiusCells = 0.09f;

        [SerializeField, Range(0.08f, 0.50f)]
        private float maximumTreeRadiusCells = 0.18f;

        [SerializeField, Range(0.12f, 1.2f)]
        private float minimumTreeHeightCells = 0.25f;

        [SerializeField, Range(0.20f, 1.6f)]
        private float maximumTreeHeightCells = 0.58f;

        [SerializeField]
        private Color woodlandCanopyColor = new Color(0.31f, 0.33f, 0.35f, 1f);

        [SerializeField]
        private Color woodlandTrunkColor = new Color(0.22f, 0.23f, 0.24f, 1f);

        [Header("Logical Movement Tile Overlay")]
        [Tooltip("기존 바닥을 생성하거나 덮지 않고, 실제 BoardSpacePrefab 논리 좌표 아래에만 1x1 이동 칸 표시를 생성합니다.")]
        [SerializeField] private bool createMovementTileOverlay = true;

        [Tooltip("한 칸 전체 외곽 크기입니다. 1에 가까울수록 칸이 촘촘하게 붙습니다.")]
        [SerializeField, Range(0.82f, 1.0f)]
        private float movementTileOuterSizeCells = 0.96f;

        [Tooltip("외곽선 안쪽의 실제 색상 면 크기입니다.")]
        [SerializeField, Range(0.60f, 0.98f)]
        private float movementTileInnerSizeCells = 0.86f;

        [Tooltip("기존 바닥과 Z-Fighting이 생기지 않도록 논리 노드 높이에서 살짝 올리는 값입니다.")]
        [SerializeField, Range(-0.05f, 0.08f)]
        private float movementTileSurfaceOffset = 0.004f;

        [SerializeField]
        private Color movementTileBorderColor = new Color(0.075f, 0.080f, 0.086f, 1f);

        [SerializeField]
        private Color roadMovementTileColor = new Color(0.22f, 0.235f, 0.25f, 1f);

        [Tooltip("역할 색상을 찾지 못한 일반 건물 노드용 타일 색상입니다.")]
        [SerializeField]
        private Color neutralBuildingTileColor = new Color(0.46f, 0.47f, 0.49f, 1f);

        [Header("Movement Tile Inspection Mode")]
        [Tooltip("true면 이동 타일을 표시하고 코드 생성 건물을 반투명하게 만듭니다. false면 타일을 숨기고 건물을 정상 불투명 상태로 복원합니다.")]
        [SerializeField]
        private bool movementTileInspectionMode;

        [Tooltip("이동 타일 확인 모드에서 건물에 적용할 알파값입니다.")]
        [SerializeField, Range(0.0f, 1.0f)]
        private float movementTileViewBuildingAlpha = 0.22f;

        [Tooltip("이동 타일 확인 모드에서 도로에 적용할 알파값입니다. 0이면 완전히 숨깁니다.")]
        [SerializeField, Range(0.0f, 1.0f)]
        private float movementTileViewRoadAlpha = 0.0f;

        [Tooltip("건물/도로/타일 전환에 걸리는 시간입니다.")]
        [SerializeField, Min(0.0f)]
        private float movementTileTransitionDuration = 0.38f;

        [Tooltip("타임스케일이 0이어도 이동 타일 전환 애니메이션을 재생합니다.")]
        [SerializeField]
        private bool useUnscaledTimeForTileTransition = true;

        [Tooltip("이동 타일 전환의 보간 곡선입니다.")]
        [SerializeField]
        private AnimationCurve movementTileTransitionCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Source Visual Handling")]
        [SerializeField] private bool hideOriginalRoadRenderers = true;
        [SerializeField] private bool hideOriginalBuildingRenderers = true;

        [Header("Guaranteed Logical Building Visuals")]
        [Tooltip("논리 건물 BoardSpace 하나마다 대표 건물 비주얼을 정확히 하나 생성합니다. 장식 건물 배치 성공 여부와 무관하게 모든 논리 건물이 보이도록 보장합니다.")]
        [SerializeField] private bool generateGuaranteedLogicalBuildings = true;

        [Tooltip("보장형 논리 건물을 사용할 때 기존 레이아웃 빌더의 랜드마크 메시 그룹은 렌더링하지 않습니다. 같은 건물이 두 번 보이는 것을 막습니다.")]
        [SerializeField] private bool suppressLegacyLandmarkMeshes = true;

        [Tooltip("대표 비주얼 생성에 실패한 건물은 원본 프리팹 Renderer를 숨기지 않습니다.")]
        [SerializeField] private bool keepSourceBuildingWhenGenerationFails = true;

        [Header("Logical Building Ground Alignment")]
        [Tooltip("보장형 논리 건물의 바닥을 실제 생성된 도로/보도 Mesh의 윗면에 맞춥니다.")]
        [SerializeField] private bool alignLogicalBuildingsToGeneratedSurface = true;

        [Tooltip("실제 도로/보도 윗면에서 추가로 적용할 미세 높이 보정입니다.")]
        [SerializeField, Range(-0.10f, 0.10f)]
        private float logicalBuildingGroundOffset = 0f;

        [Tooltip("건물 전면과 도로/보도 외곽 사이의 최소 간격입니다.")]
        [SerializeField, Range(0f, 0.25f)]
        private float logicalBuildingRoadGapCells = 0.035f;

        [Tooltip("도로에 붙는 건물의 최소 깊이입니다.")]
        [SerializeField, Range(0.45f, 1.25f)]
        private float logicalBuildingMinimumDepthCells = 0.72f;

        [Tooltip("1x2 건물의 긴 변까지 허용하는 최대 깊이입니다.")]
        [SerializeField, Range(1.0f, 2.4f)]
        private float logicalBuildingMaximumDepthCells = 1.92f;

        [Tooltip("도로와 나란한 방향의 최소 폭입니다.")]
        [SerializeField, Range(0.45f, 1.25f)]
        private float logicalBuildingMinimumWidthCells = 0.78f;

        [Tooltip("도로와 나란한 방향의 최대 폭입니다.")]
        [SerializeField, Range(1.0f, 2.4f)]
        private float logicalBuildingMaximumWidthCells = 1.78f;

        [Tooltip("논리 건물 footprint 외곽에서 안쪽으로 줄이는 여백입니다. 생성 매스가 이웃 타일을 침범하지 않도록 합니다.")]
        [SerializeField, Range(0.04f, 0.30f)]
        private float logicalBuildingFootprintInsetCells = 0.12f;

        [Tooltip("논리 건물별 생성 결과와 원본 유지 여부를 Console에 요약합니다.")]
        [SerializeField] private bool logLogicalBuildingDiagnostics = true;

        private readonly List<RendererState> sourceRendererStates =
            new List<RendererState>();

        private Transform generatedVisualRoot;
        private Material roadMaterial;
        private Material sidewalkMaterial;
        private Material woodlandCanopyMaterial;
        private Material woodlandTrunkMaterial;
        private Material movementTileBorderMaterial;
        private Material roadMovementTileMaterial;
        private Material neutralBuildingTileMaterial;
        private readonly Material[] landmarkTileMaterials = new Material[6];
        private readonly Material[] buildingMaterials = new Material[9];

        private readonly Material[] logicalBuildingPaletteMaterials =
            new Material[8];

        private readonly Dictionary<BoardSpacePrefab, Material>
            logicalBuildingColorAssignments =
                new Dictionary<BoardSpacePrefab, Material>();

        private readonly List<Material> logicalBuildingOverflowMaterials =
            new List<Material>();

        private int lastBoardSignature;
        private float nextRefreshCheckTime;
        private Coroutine movementTileTransitionCoroutine;

        private static readonly BindingFlags ReflectionFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private void Awake()
        {
            ApplyUrbanLowRisePresetIfNeeded();
            ResolveReferences();

            if (controlSourceGeneration && sourceBoardManager != null)
            {
                sourceBoardManager.enabled = false;
            }
        }

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateFromGameBoardSettingManager();
            }
            else
            {
                ApplyMovementTileInspectionMode(true);
            }
        }

        public bool MovementTileInspectionMode
        {
            get { return movementTileInspectionMode; }
        }

        /// <summary>
        /// true면 이동 타일을 표시하고 코드 생성 건물을 반투명하게 만듭니다.
        /// false면 이동 타일을 숨기고 건물을 정상 불투명 상태로 복원합니다.
        /// UI Toggle의 bool 이벤트에 직접 연결할 수 있습니다.
        /// </summary>
        public void SetMovementTileInspectionMode(bool enabled)
        {
            movementTileInspectionMode = enabled;
            ApplyMovementTileInspectionMode(false);
        }

        [ContextMenu("Show Movement Tiles")]
        public void ShowMovementTiles()
        {
            SetMovementTileInspectionMode(true);
        }

        [ContextMenu("Hide Movement Tiles")]
        public void HideMovementTiles()
        {
            SetMovementTileInspectionMode(false);
        }

        [ContextMenu("Toggle Movement Tile Inspection Mode")]
        public void ToggleMovementTileInspectionMode()
        {
            SetMovementTileInspectionMode(!movementTileInspectionMode);
        }

        private void Update()
        {
            if (!autoRefreshWhenBoardChanges ||
                Time.unscaledTime < nextRefreshCheckTime)
            {
                return;
            }

            nextRefreshCheckTime =
                Time.unscaledTime + refreshCheckInterval;

            ResolveReferences();

            if (gridManager == null)
            {
                return;
            }

            GameBoardRuntimeSnapshot snapshot =
                GameBoardRuntimeSnapshotBuilder.Build(gridManager);

            if (!snapshot.IsValid ||
                snapshot.Signature == lastBoardSignature)
            {
                return;
            }

            RebuildVisuals(snapshot);
        }

        [ContextMenu("Generate From GameBoardSettingManager")]
        public void GenerateFromGameBoardSettingManager()
        {
            ResolveReferences();

            if (sourceBoardManager == null)
            {
                Debug.LogError(
                    "[ProceduralCityBoardManager] GameBoardSettingManager를 찾지 못했습니다.",
                    this);
                return;
            }

            ClearGeneratedVisuals();

            if (controlSourceGeneration)
            {
                sourceBoardManager.enabled = false;
            }

            sourceBoardManager.CreateAndStartGame();
            ResolveReferences();

            if (gridManager == null)
            {
                Debug.LogError(
                    "[ProceduralCityBoardManager] 보드 생성 후 GridManager를 찾지 못했습니다.",
                    this);
                return;
            }

            GameBoardRuntimeSnapshot snapshot =
                GameBoardRuntimeSnapshotBuilder.Build(gridManager);

            if (!snapshot.IsValid)
            {
                Debug.LogError(
                    "[ProceduralCityBoardManager] GameBoardSettingManager가 생성한 ROAD_x_y 노드를 읽지 못했습니다.",
                    this);
                return;
            }

            int addedRoadCount = 0;

            if (expandGameRoadNetwork)
            {
                addedRoadCount = SecondaryRoadNetworkGenerator.Expand(
                    snapshot,
                    gridManager,
                    ReadGeneratedBoardRoot(),
                    secondaryRoadSeed,
                    secondaryRoadBranchChance,
                    secondaryRoadMinimumLength,
                    secondaryRoadMaximumLength,
                    secondaryRoadTurnChance,
                    secondaryRoadDiagonalChance,
                    maximumSecondaryRoadCells,
                    secondaryRoadBoundsExpansionCells,
                    secondaryRoadBuildingClearanceCells,
                    secondaryRoadMovementAPCost);

                if (addedRoadCount > 0)
                {
                    snapshot = GameBoardRuntimeSnapshotBuilder.Build(gridManager);
                }
            }

            // GameBoardSettingManager 생성 이후 실제 보조 도로가 추가될 수 있으므로,
            // 1x2 건물의 모든 점유 셀 주변 도로를 다시 연결합니다.
            ConnectBuildingsToAllAdjacentRoads();
            snapshot = GameBoardRuntimeSnapshotBuilder.Build(gridManager);

            RebuildVisuals(snapshot);

            Debug.Log(
                "[ProceduralCityBoardManager] 실제 보조 도로 노드 추가: " +
                addedRoadCount,
                this);
        }

        [ContextMenu("Rebuild Visuals From Existing Board")]
        public void RebuildVisualsFromExistingBoard()
        {
            ResolveReferences();

            if (gridManager == null)
            {
                Debug.LogError(
                    "[ProceduralCityBoardManager] GridManager가 없습니다.",
                    this);
                return;
            }

            ConnectBuildingsToAllAdjacentRoads();

            GameBoardRuntimeSnapshot snapshot =
                GameBoardRuntimeSnapshotBuilder.Build(gridManager);

            if (!snapshot.IsValid)
            {
                Debug.LogError(
                    "[ProceduralCityBoardManager] 현재 생성된 도로 노드가 없습니다.",
                    this);
                return;
            }

            RebuildVisuals(snapshot);
        }

        private void ConnectBuildingsToAllAdjacentRoads()
        {
            if (gridManager == null)
            {
                return;
            }

            Dictionary<Vector2Int, BoardSpacePrefab> roadsByCoordinate =
                new Dictionary<Vector2Int, BoardSpacePrefab>();

            for (int roadIndex = 0;
                 roadIndex < gridManager.RoadSpaces.Count;
                 roadIndex++)
            {
                BoardSpacePrefab road = gridManager.RoadSpaces[roadIndex];

                if (road != null)
                {
                    roadsByCoordinate[road.BoardCoordinate] = road;
                }
            }

            for (int buildingIndex = 0;
                 buildingIndex < gridManager.BuildingSpaces.Count;
                 buildingIndex++)
            {
                BoardSpacePrefab building =
                    gridManager.BuildingSpaces[buildingIndex];

                if (building == null)
                {
                    continue;
                }

                IList<Vector2Int> occupied =
                    building.OccupiedBoardCoordinates;

                if (occupied == null || occupied.Count == 0)
                {
                    ConnectBuildingCoordinateToRoads(
                        building,
                        building.BoardCoordinate,
                        roadsByCoordinate);
                    continue;
                }

                for (int occupiedIndex = 0;
                     occupiedIndex < occupied.Count;
                     occupiedIndex++)
                {
                    ConnectBuildingCoordinateToRoads(
                        building,
                        occupied[occupiedIndex],
                        roadsByCoordinate);
                }
            }
        }

        private static void ConnectBuildingCoordinateToRoads(
            BoardSpacePrefab building,
            Vector2Int occupiedCoordinate,
            Dictionary<Vector2Int, BoardSpacePrefab> roadsByCoordinate)
        {
            if (building == null || roadsByCoordinate == null)
            {
                return;
            }

            for (int directionIndex = 0;
                 directionIndex < RuntimeNeighborDirections.Length;
                 directionIndex++)
            {
                BoardSpacePrefab road;

                if (roadsByCoordinate.TryGetValue(
                        occupiedCoordinate +
                        RuntimeNeighborDirections[directionIndex],
                        out road) &&
                    road != null)
                {
                    building.ConnectBidirectional(road);
                }
            }
        }

        [ContextMenu("Clear Procedural Visuals")]
        public void ClearGeneratedVisuals()
        {
            StopMovementTileTransition();
            RestoreSourceRenderers();

            if (generatedVisualRoot != null)
            {
                DestroyObject(generatedVisualRoot.gameObject);
                generatedVisualRoot = null;
            }

            DestroyRuntimeMaterial(ref roadMaterial);
            DestroyRuntimeMaterial(ref sidewalkMaterial);
            DestroyRuntimeMaterial(ref woodlandCanopyMaterial);
            DestroyRuntimeMaterial(ref woodlandTrunkMaterial);
            DestroyRuntimeMaterial(ref movementTileBorderMaterial);
            DestroyRuntimeMaterial(ref roadMovementTileMaterial);
            DestroyRuntimeMaterial(ref neutralBuildingTileMaterial);

            for (int index = 0; index < landmarkTileMaterials.Length; index++)
            {
                DestroyRuntimeMaterial(ref landmarkTileMaterials[index]);
            }

            for (int index = 0; index < buildingMaterials.Length; index++)
            {
                DestroyRuntimeMaterial(ref buildingMaterials[index]);
            }

            for (int index = 0;
                 index < logicalBuildingPaletteMaterials.Length;
                 index++)
            {
                DestroyRuntimeMaterial(
                    ref logicalBuildingPaletteMaterials[index]);
            }

            for (int index = 0;
                 index < logicalBuildingOverflowMaterials.Count;
                 index++)
            {
                Material overflowMaterial =
                    logicalBuildingOverflowMaterials[index];

                if (overflowMaterial != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(overflowMaterial);
                    }
                    else
                    {
                        DestroyImmediate(overflowMaterial);
                    }
                }
            }

            logicalBuildingOverflowMaterials.Clear();
            logicalBuildingColorAssignments.Clear();

            lastBoardSignature = 0;
        }

        private void RebuildVisuals(GameBoardRuntimeSnapshot snapshot)
        {
            ClearGeneratedVisuals();
            CreateVisualRoot();
            CreateMaterials();

            if (hideOriginalRoadRenderers)
            {
                HideSourceRenderers(snapshot.Roads);
            }

            // 건물 원본 Renderer는 대표 비주얼 생성 성공 여부를 확인한 뒤
            // 건물별로 숨깁니다. 먼저 전부 숨기면 배치 실패 건물이 완전히 사라집니다.
            ProceduralRoadVisualLayout roadLayout =
                ProceduralCityVisualLayoutBuilder.BuildRoadLayout(
                    snapshot,
                    roadCrookednessCells,
                    roadWobbleWavelengthCells,
                    roadCrookednessSeed,
                    intersectionStability,
                    roadSmoothingIterations,
                    roadSamplesPerCell,
                    roadEdgeBendChance,
                    roadEdgeBendCells,
                    roadEdgeLongitudinalJitterCells);

            List<DenseSmallBuildingPlacement> buildingPlacements =
                createDenseSmallBuildings
                    ? BuildDenseBuildingPlacements(snapshot, roadLayout)
                    : new List<DenseSmallBuildingPlacement>();

            ProceduralIslandLandscapeLayout landscapeLayout = null;

            if (createUrbanWoodland)
            {
                landscapeLayout = ProceduralCityLandscapeBuilder.Build(
                    snapshot,
                    roadLayout,
                    buildingPlacements,
                    woodlandSeed,
                    woodlandMarginCells,
                    woodlandBoundaryIrregularity,
                    woodlandBoundarySegments,
                    woodlandMinimumRoadDistanceCells,
                    woodlandMaximumRoadDistanceCells,
                    woodlandBuildingClearanceCells,
                    woodlandSampleStepCells,
                    innerWoodlandChance,
                    outerWoodlandChance,
                    maximumTreeCount,
                    minimumTreeRadiusCells,
                    maximumTreeRadiusCells,
                    minimumTreeHeightCells,
                    maximumTreeHeightCells,
                    0f);

                CreateUrbanWoodlandVisuals(landscapeLayout);
            }

            if (createMovementTileOverlay)
            {
                CreateMovementTileVisuals(snapshot, buildingPlacements);
            }

            CreateRoadVisual(snapshot, roadLayout);

            if (createSidewalks)
            {
                CreateSidewalkVisual(snapshot, roadLayout);
            }

            if (createDenseSmallBuildings)
            {
                CreateDenseBuildingVisuals(buildingPlacements);
            }

            HashSet<BoardSpacePrefab> generatedLogicalBuildings =
                new HashSet<BoardSpacePrefab>();

            int generatedLogicalBuildingCount = 0;

            if (generateGuaranteedLogicalBuildings)
            {
                generatedLogicalBuildingCount =
                    CreateGuaranteedLogicalBuildingVisuals(
                        snapshot,
                        generatedLogicalBuildings);
            }

            if (hideOriginalBuildingRenderers)
            {
                if (generateGuaranteedLogicalBuildings)
                {
                    HideSourceRenderers(
                        snapshot.Buildings,
                        generatedLogicalBuildings,
                        keepSourceBuildingWhenGenerationFails);
                }
                else
                {
                    HideSourceRenderers(snapshot.Buildings);
                }
            }

            ApplyMovementTileInspectionMode(true);

            lastBoardSignature = snapshot.Signature;

            Debug.Log(
                "[ProceduralCityBoardManager] 연속 도로 체인 " +
                roadLayout.Splines.Count +
                " / 도로 노드 " + snapshot.Roads.Count +
                " / 장식·예약 배치 " + buildingPlacements.Count +
                " / 논리 건물 비주얼 " + generatedLogicalBuildingCount +
                "/" + snapshot.Buildings.Count +
                " / 나무 " +
                (landscapeLayout != null ? landscapeLayout.Trees.Count : 0),
                this);
        }

        private void Reset()
        {
            appliedVisualPresetVersion = 0;
            ApplyUrbanLowRisePresetIfNeeded();
        }

        private void OnValidate()
        {
            ApplyUrbanLowRisePresetIfNeeded();

            if (generatedVisualRoot != null)
            {
                ApplyMovementTileInspectionMode(true);
            }
        }

        private void ApplyUrbanLowRisePresetIfNeeded()
        {
            if (appliedVisualPresetVersion >= CurrentVisualPresetVersion)
            {
                return;
            }

            secondaryRoadBranchChance = 0.42f;
            secondaryRoadMinimumLength = 3;
            secondaryRoadMaximumLength = 9;
            secondaryRoadTurnChance = 0.22f;
            secondaryRoadDiagonalChance = 0.62f;
            maximumSecondaryRoadCells = 520;

            roadWidthCells = 0.21f;
            roadCrookednessCells = 0.16f;
            roadWobbleWavelengthCells = 5.8f;
            roadSmoothingIterations = 3;
            roadSamplesPerCell = 7;
            roadEdgeBendChance = 0.82f;
            roadEdgeBendCells = 0.20f;
            roadEdgeLongitudinalJitterCells = 0.12f;

            maximumDecorativeBuildingCount = 8200;
            roadsidePlacementChance = 0.999f;
            roadsideRows = 3;
            minimumSampleSpacingCells = 0.14f;
            maximumSampleSpacingCells = 0.24f;
            decorativeMinimumWidthCells = 0.24f;
            decorativeMaximumWidthCells = 0.48f;
            decorativeMinimumDepthCells = 0.22f;
            decorativeMaximumDepthCells = 0.42f;
            decorativeMinimumFloors = 1;
            decorativeMaximumFloors = 3;
            landmarkMinimumWidthCells = 0.78f;
            landmarkMaximumWidthCells = 1.12f;
            landmarkMinimumDepthCells = 0.68f;
            landmarkMaximumDepthCells = 0.98f;
            landmarkMinimumFloors = 4;
            landmarkMaximumFloors = 11;

            buildingHeightRandomness = 0.58f;
            tallBuildingChance = 0.08f;
            buildingAngleJitterDegrees = 10f;
            compositeMassChance = 0.16f;
            steppedSilhouetteChance = 0.10f;
            terraceChance = 0.06f;
            rooftopDetailChance = 0.10f;
            buildingSetbackCells = 0.025f;
            buildingRowGapCells = 0.004f;
            generatedBuildingGapCells = 0.006f;
            buildingIntersectionClearanceCells = 0.16f;
            buildingCollisionEnvelopeMultiplier = 1.01f;
            sameTemplateSeparationCells = 0.30f;
            maximumDecorativeRoadDistanceCells = 1.48f;
            densifyLandmarkNeighborhoods = true;
            landmarkNeighborAttemptsPerBuilding = 18;
            landmarkNeighborPlacementChance = 0.92f;
            landmarkNeighborhoodRadiusCells = 1.10f;
            fillInteriorLots = false;

            createMovementTileOverlay = true;
            movementTileOuterSizeCells = 0.96f;
            movementTileInnerSizeCells = 0.86f;
            movementTileSurfaceOffset = 0.004f;
            movementTileInspectionMode = false;
            movementTileViewBuildingAlpha = 0.22f;
            movementTileViewRoadAlpha = 0.0f;
            movementTileTransitionDuration = 0.38f;
            useUnscaledTimeForTileTransition = true;
            movementTileTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            createUrbanWoodland = true;
            woodlandMarginCells = 1.75f;
            woodlandMinimumRoadDistanceCells = 0.62f;
            woodlandMaximumRoadDistanceCells = 1.50f;
            woodlandBuildingClearanceCells = 0.24f;
            woodlandSampleStepCells = 0.34f;
            innerWoodlandChance = 0.56f;
            outerWoodlandChance = 0.56f;
            maximumTreeCount = 2200;

            appliedVisualPresetVersion = CurrentVisualPresetVersion;
        }

        private void ResolveReferences()
        {
            if (sourceBoardManager == null)
            {
                sourceBoardManager = FindObjectOfType<GameBoardSettingManager>();
            }

            if (gridManager == null)
            {
                gridManager = FindObjectOfType<GridManager>();
            }
        }

        private void CreateVisualRoot()
        {
            Transform parent = ReadGeneratedBoardRoot();

            if (parent == null)
            {
                parent = transform;
            }

            GameObject rootObject =
                new GameObject("__ProceduralCityVisuals_UrbanLowRise_v11");

            rootObject.transform.SetParent(parent, false);
            generatedVisualRoot = rootObject.transform;

            ProceduralGeneratedVisualMarker marker =
                rootObject.AddComponent<ProceduralGeneratedVisualMarker>();

            marker.Initialize(
                ProceduralGeneratedVisualType.Root,
                string.Empty);
        }

        private Transform ReadGeneratedBoardRoot()
        {
            if (sourceBoardManager == null)
            {
                return null;
            }

            FieldInfo field = sourceBoardManager.GetType().GetField(
                "generatedBoardRoot",
                ReflectionFlags);

            if (field == null || field.FieldType != typeof(Transform))
            {
                return null;
            }

            return field.GetValue(sourceBoardManager) as Transform;
        }

        private void CreateMaterials()
        {
            float minimumGray =
                Mathf.Min(
                    decorativeMinimumGrayValue,
                    decorativeMaximumGrayValue);

            float maximumGray =
                Mathf.Max(
                    decorativeMinimumGrayValue,
                    decorativeMaximumGrayValue);

            Color runtimeDecorativeA =
                forceDecorativeAchromaticPalette
                    ? ToClampedAchromatic(
                        smallBuildingColorA,
                        minimumGray,
                        maximumGray)
                    : smallBuildingColorA;

            Color runtimeDecorativeB =
                forceDecorativeAchromaticPalette
                    ? ToClampedAchromatic(
                        smallBuildingColorB,
                        minimumGray,
                        maximumGray)
                    : smallBuildingColorB;

            Color runtimeDecorativeC =
                forceDecorativeAchromaticPalette
                    ? ToClampedAchromatic(
                        smallBuildingColorC,
                        minimumGray,
                        maximumGray)
                    : smallBuildingColorC;

            Color runtimeCanopy =
                forceDecorativeAchromaticPalette
                    ? ToClampedAchromatic(
                        woodlandCanopyColor,
                        minimumGray,
                        maximumGray)
                    : woodlandCanopyColor;

            Color runtimeTrunk =
                forceDecorativeAchromaticPalette
                    ? ToClampedAchromatic(
                        woodlandTrunkColor,
                        minimumGray,
                        maximumGray)
                    : woodlandTrunkColor;

            Color runtimeApartment =
                useDistinctLogicalBuildingPalette
                    ? logicalApartmentColor
                    : apartmentColor;

            Color runtimeShoppingMall =
                useDistinctLogicalBuildingPalette
                    ? logicalShoppingMallColor
                    : shoppingMallColor;

            Color runtimeHospital =
                useDistinctLogicalBuildingPalette
                    ? logicalHospitalColor
                    : hospitalColor;

            Color runtimeOffice =
                useDistinctLogicalBuildingPalette
                    ? logicalOfficeColor
                    : officeColor;

            Color runtimeIndustrial =
                useDistinctLogicalBuildingPalette
                    ? logicalIndustrialColor
                    : industrialColor;

            Color runtimeCivic =
                useDistinctLogicalBuildingPalette
                    ? logicalCivicColor
                    : civicColor;

            roadMaterial = CreateRuntimeMaterial(
                "ProceduralContinuousRoadMaterial",
                roadColor);

            sidewalkMaterial = CreateRuntimeMaterial(
                "ProceduralContinuousSidewalkMaterial",
                sidewalkColor);

            woodlandCanopyMaterial = CreateRuntimeMaterial(
                "ProceduralUrbanWoodlandCanopyMaterial",
                runtimeCanopy);

            woodlandTrunkMaterial = CreateRuntimeMaterial(
                "ProceduralUrbanWoodlandTrunkMaterial",
                runtimeTrunk);

            movementTileBorderMaterial = CreateRuntimeUnlitMaterial(
                "ProceduralMovementTileBorderMaterial",
                movementTileBorderColor);

            roadMovementTileMaterial = CreateRuntimeUnlitMaterial(
                "ProceduralRoadMovementTileMaterial",
                roadMovementTileColor);

            neutralBuildingTileMaterial = CreateRuntimeUnlitMaterial(
                "ProceduralNeutralBuildingTileMaterial",
                neutralBuildingTileColor);

            landmarkTileMaterials[0] = CreateRuntimeUnlitMaterial(
                "ProceduralMovementTile_Apartment",
                runtimeApartment);

            landmarkTileMaterials[1] = CreateRuntimeUnlitMaterial(
                "ProceduralMovementTile_ShoppingMall",
                runtimeShoppingMall);

            landmarkTileMaterials[2] = CreateRuntimeUnlitMaterial(
                "ProceduralMovementTile_Hospital",
                runtimeHospital);

            landmarkTileMaterials[3] = CreateRuntimeUnlitMaterial(
                "ProceduralMovementTile_Office",
                runtimeOffice);

            landmarkTileMaterials[4] = CreateRuntimeUnlitMaterial(
                "ProceduralMovementTile_Industrial",
                runtimeIndustrial);

            landmarkTileMaterials[5] = CreateRuntimeUnlitMaterial(
                "ProceduralMovementTile_Civic",
                runtimeCivic);

            buildingMaterials[0] = CreateRuntimeMaterial(
                "ProceduralSmallBuildingMaterial_GrayLight",
                runtimeDecorativeA);

            buildingMaterials[1] = CreateRuntimeMaterial(
                "ProceduralSmallBuildingMaterial_GrayDark",
                runtimeDecorativeB);

            buildingMaterials[2] = CreateRuntimeMaterial(
                "ProceduralSmallBuildingMaterial_GrayMid",
                runtimeDecorativeC);

            buildingMaterials[3] = CreateRuntimeMaterial(
                "ProceduralLandmark_Apartment",
                runtimeApartment);

            buildingMaterials[4] = CreateRuntimeMaterial(
                "ProceduralLandmark_ShoppingMall",
                runtimeShoppingMall);

            buildingMaterials[5] = CreateRuntimeMaterial(
                "ProceduralLandmark_Hospital",
                runtimeHospital);

            buildingMaterials[6] = CreateRuntimeMaterial(
                "ProceduralLandmark_Office",
                runtimeOffice);

            buildingMaterials[7] = CreateRuntimeMaterial(
                "ProceduralLandmark_Industrial",
                runtimeIndustrial);

            buildingMaterials[8] = CreateRuntimeMaterial(
                "ProceduralLandmark_Civic",
                runtimeCivic);

            logicalBuildingPaletteMaterials[0] =
                CreateRuntimeMaterial(
                    "ProceduralLogicalBuildingPalette_Blue",
                    runtimeApartment);

            logicalBuildingPaletteMaterials[1] =
                CreateRuntimeMaterial(
                    "ProceduralLogicalBuildingPalette_Orange",
                    runtimeShoppingMall);

            logicalBuildingPaletteMaterials[2] =
                CreateRuntimeMaterial(
                    "ProceduralLogicalBuildingPalette_Mint",
                    runtimeHospital);

            logicalBuildingPaletteMaterials[3] =
                CreateRuntimeMaterial(
                    "ProceduralLogicalBuildingPalette_Cyan",
                    runtimeOffice);

            logicalBuildingPaletteMaterials[4] =
                CreateRuntimeMaterial(
                    "ProceduralLogicalBuildingPalette_Brick",
                    runtimeIndustrial);

            logicalBuildingPaletteMaterials[5] =
                CreateRuntimeMaterial(
                    "ProceduralLogicalBuildingPalette_Purple",
                    runtimeCivic);

            logicalBuildingPaletteMaterials[6] =
                CreateRuntimeMaterial(
                    "ProceduralLogicalBuildingPalette_Ochre",
                    logicalBuildingExtraColorA);

            logicalBuildingPaletteMaterials[7] =
                CreateRuntimeMaterial(
                    "ProceduralLogicalBuildingPalette_Rose",
                    logicalBuildingExtraColorB);
        }

        private void CreateMovementTileVisuals(
            GameBoardRuntimeSnapshot snapshot,
            IList<DenseSmallBuildingPlacement> buildingPlacements)
        {
            List<ProceduralBoardTilePlacement> tiles =
                ProceduralBoardTileLayoutBuilder.Build(
                    snapshot,
                    buildingPlacements);

            if (tiles.Count == 0)
            {
                return;
            }

            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            float outerSize = cell * Mathf.Clamp(
                movementTileOuterSizeCells,
                0.82f,
                1f);

            float innerSize = cell * Mathf.Min(
                Mathf.Clamp(movementTileInnerSizeCells, 0.60f, 0.98f),
                Mathf.Clamp(movementTileOuterSizeCells, 0.82f, 1f) - 0.02f);

            CreateMovementTileGroup(
                "MovementTiles_Border_AllLogicalCells",
                tiles,
                -1,
                outerSize,
                movementTileSurfaceOffset,
                movementTileBorderMaterial);

            CreateMovementTileGroup(
                "MovementTiles_Road",
                tiles,
                (int)ProceduralBoardTileKind.Road,
                innerSize,
                movementTileSurfaceOffset + 0.0015f,
                roadMovementTileMaterial);

            CreateMovementTileGroup(
                "MovementTiles_Building_Neutral",
                tiles,
                (int)ProceduralBoardTileKind.NeutralBuilding,
                innerSize,
                movementTileSurfaceOffset + 0.0015f,
                neutralBuildingTileMaterial);

            for (int roleIndex = 0; roleIndex < 6; roleIndex++)
            {
                ProceduralBoardTileKind kind =
                    (ProceduralBoardTileKind)((int)ProceduralBoardTileKind.Apartment + roleIndex);

                CreateMovementTileGroup(
                    "MovementTiles_Landmark_" + kind,
                    tiles,
                    (int)kind,
                    innerSize,
                    movementTileSurfaceOffset + 0.0015f,
                    landmarkTileMaterials[roleIndex]);
            }
        }

        private void CreateMovementTileGroup(
            string objectName,
            IList<ProceduralBoardTilePlacement> tiles,
            int kindFilter,
            float tileSize,
            float surfaceOffset,
            Material material)
        {
            if (material == null)
            {
                return;
            }

            Mesh mesh = ProceduralCityMeshFactory.CreateBoardTileMesh(
                tiles,
                generatedVisualRoot,
                tileSize,
                surfaceOffset,
                kindFilter);

            if (mesh == null || mesh.vertexCount == 0)
            {
                if (mesh != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(mesh);
                    }
                    else
                    {
                        DestroyImmediate(mesh);
                    }
                }

                return;
            }

            GameObject tileObject = new GameObject(objectName);
            tileObject.transform.SetParent(generatedVisualRoot, false);

            MeshFilter filter = tileObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = tileObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.enabled = movementTileInspectionMode;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ProceduralGeneratedVisualMarker marker =
                tileObject.AddComponent<ProceduralGeneratedVisualMarker>();

            marker.Initialize(
                ProceduralGeneratedVisualType.MovementTile,
                string.Empty);
        }

        private void CreateRoadVisual(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout)
        {
            GameObject roadObject =
                new GameObject("Roads_ContinuousCurved_CodeGenerated");

            roadObject.transform.SetParent(generatedVisualRoot, false);

            MeshFilter filter = roadObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = roadObject.AddComponent<MeshRenderer>();

            filter.sharedMesh = ProceduralCityMeshFactory.CreateRoadMesh(
                roadLayout,
                generatedVisualRoot,
                roadWidthCells * snapshot.CellWorldSize,
                roadThickness,
                roadSurfaceOffset,
                roadJunctionRadiusMultiplier);

            renderer.sharedMaterial = roadMaterial;

            ProceduralGeneratedVisualMarker marker =
                roadObject.AddComponent<ProceduralGeneratedVisualMarker>();

            marker.Initialize(
                ProceduralGeneratedVisualType.Road,
                string.Empty);
        }

        private void CreateSidewalkVisual(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout)
        {
            GameObject sidewalkObject =
                new GameObject("Sidewalks_ContinuousCurved_CodeGenerated");

            sidewalkObject.transform.SetParent(generatedVisualRoot, false);

            MeshFilter filter = sidewalkObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = sidewalkObject.AddComponent<MeshRenderer>();

            filter.sharedMesh = ProceduralCityMeshFactory.CreateSidewalkMesh(
                roadLayout,
                generatedVisualRoot,
                roadWidthCells * snapshot.CellWorldSize,
                sidewalkWidthCells * snapshot.CellWorldSize,
                sidewalkHeight,
                roadSurfaceOffset - 0.008f,
                roadJunctionRadiusMultiplier);

            renderer.sharedMaterial = sidewalkMaterial;

            ProceduralGeneratedVisualMarker marker =
                sidewalkObject.AddComponent<ProceduralGeneratedVisualMarker>();

            marker.Initialize(
                ProceduralGeneratedVisualType.Sidewalk,
                string.Empty);
        }

        private List<DenseSmallBuildingPlacement> BuildDenseBuildingPlacements(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout)
        {
            return ProceduralCityVisualLayoutBuilder.BuildDenseSmallBuildings(
                snapshot,
                roadLayout,
                smallBuildingSeed,
                maximumDecorativeBuildingCount,
                createLandmarkBuildings,
                roadsidePlacementChance,
                roadsideRows,
                minimumSampleSpacingCells,
                maximumSampleSpacingCells,
                decorativeMinimumWidthCells,
                decorativeMaximumWidthCells,
                decorativeMinimumDepthCells,
                decorativeMaximumDepthCells,
                decorativeMinimumFloors,
                decorativeMaximumFloors,
                landmarkMinimumWidthCells,
                landmarkMaximumWidthCells,
                landmarkMinimumDepthCells,
                landmarkMaximumDepthCells,
                landmarkMinimumFloors,
                landmarkMaximumFloors,
                decorativeMaximumLandmarkRatio,
                roadWidthCells,
                createSidewalks ? sidewalkWidthCells : 0f,
                buildingSetbackCells,
                buildingRowGapCells,
                generatedBuildingGapCells,
                buildingIntersectionClearanceCells,
                maximumDecorativeRoadDistanceCells,
                maximumLandmarkRoadDistanceCells,
                buildingCollisionEnvelopeMultiplier,
                sameTemplateSeparationCells,
                densifyLandmarkNeighborhoods,
                landmarkNeighborAttemptsPerBuilding,
                landmarkNeighborPlacementChance,
                landmarkNeighborhoodRadiusCells,
                fillInteriorLots,
                interiorFillChance,
                interiorGridStepCells,
                Mathf.Min(
                    interiorMaximumRoadDistanceCells,
                    maximumDecorativeRoadDistanceCells),
                new ProceduralBuildingVariationSettings
                {
                    HeightRandomness = buildingHeightRandomness,
                    TallBuildingChance = tallBuildingChance,
                    AngleJitterDegrees = buildingAngleJitterDegrees,
                    CompositeMassChance = compositeMassChance,
                    SteppedSilhouetteChance = steppedSilhouetteChance,
                    TerraceChance = terraceChance,
                    RooftopDetailChance = rooftopDetailChance
                },
                buildingBaseYOffset);
        }

        private void CreateDenseBuildingVisuals(
            IList<DenseSmallBuildingPlacement> placements)
        {
            string[] materialGroupNames =
            {
                "Decorative_A",
                "Decorative_B",
                "Decorative_C",
                "Landmark_Apartment",
                "Landmark_ShoppingMall",
                "Landmark_Hospital",
                "Landmark_Office",
                "Landmark_Industrial",
                "Landmark_Civic"
            };

            int visibleMaterialGroupCount =
                generateGuaranteedLogicalBuildings &&
                suppressLegacyLandmarkMeshes
                    ? 3
                    : buildingMaterials.Length;

            for (int materialIndex = 0;
                 materialIndex < visibleMaterialGroupCount;
                 materialIndex++)
            {
                GameObject buildingObject = new GameObject(
                    "Buildings_" + materialGroupNames[materialIndex]);

                buildingObject.transform.SetParent(
                    generatedVisualRoot,
                    false);

                MeshFilter filter = buildingObject.AddComponent<MeshFilter>();
                MeshRenderer renderer =
                    buildingObject.AddComponent<MeshRenderer>();

                filter.sharedMesh =
                    ProceduralCityMeshFactory.CreateDenseSmallBuildingMesh(
                        placements,
                        generatedVisualRoot,
                        buildingFloorWorldHeight,
                        materialIndex);

                renderer.sharedMaterial = buildingMaterials[materialIndex];

                ProceduralGeneratedVisualMarker marker =
                    buildingObject.AddComponent<ProceduralGeneratedVisualMarker>();

                marker.Initialize(
                    ProceduralGeneratedVisualType.DenseSmallBuilding,
                    string.Empty);
            }
        }

        private int CreateGuaranteedLogicalBuildingVisuals(
            GameBoardRuntimeSnapshot snapshot,
            HashSet<BoardSpacePrefab> generatedSpaces)
        {
            if (generatedVisualRoot == null ||
                snapshot.Buildings == null)
            {
                return 0;
            }

            BuildLogicalBuildingColorAssignments(
                snapshot);

            GameObject logicalRootObject =
                new GameObject("LogicalBuildings_Guaranteed_CodeGenerated");

            logicalRootObject.transform.SetParent(
                generatedVisualRoot,
                false);

            int generatedCount = 0;
            int failedCount = 0;
            string failedNodeIds = string.Empty;

            for (int index = 0;
                 index < snapshot.Buildings.Count;
                 index++)
            {
                RuntimeBuildingNode node = snapshot.Buildings[index];

                if (node == null || node.Space == null)
                {
                    failedCount++;
                    continue;
                }

                if (TryCreateGuaranteedLogicalBuildingVisual(
                        node,
                        snapshot,
                        logicalRootObject.transform))
                {
                    generatedSpaces.Add(node.Space);
                    generatedCount++;
                }
                else
                {
                    failedCount++;

                    if (failedCount <= 8)
                    {
                        if (failedNodeIds.Length > 0)
                        {
                            failedNodeIds += ", ";
                        }

                        failedNodeIds += node.Space.NodeId;
                    }
                }
            }

            if (logLogicalBuildingDiagnostics)
            {
                if (failedCount > 0)
                {
                    Debug.LogWarning(
                        "[ProceduralCityBoardManager] 논리 건물 대표 비주얼 생성 " +
                        generatedCount + "/" + snapshot.Buildings.Count +
                        " 성공. 실패 건물은 원본 Renderer를 유지합니다. " +
                        "실패 NodeId: " + failedNodeIds,
                        this);
                }
                else
                {
                    Debug.Log(
                        "[ProceduralCityBoardManager] 모든 논리 건물 대표 비주얼 생성 완료: " +
                        generatedCount,
                        this);
                }
            }

            return generatedCount;
        }

        private bool TryCreateGuaranteedLogicalBuildingVisual(
            RuntimeBuildingNode node,
            GameBoardRuntimeSnapshot snapshot,
            Transform parent)
        {
            BoardSpacePrefab space = node.Space;

            if (space == null ||
                parent == null ||
                buildingMaterials == null ||
                buildingMaterials.Length < 3)
            {
                return false;
            }

            GameObject buildingRoot = null;

            try
            {
                int stableHash = GetStableHash(
                    string.IsNullOrEmpty(space.NodeId)
                        ? space.gameObject.name
                        : space.NodeId);

                System.Random random =
                    new System.Random(stableHash);

                float cellSize = Mathf.Max(
                    0.01f,
                    snapshot.CellWorldSize);

                Vector3 sourcePosition;
                Vector3 directionTowardRoad;
                float width;
                float depth;

                bool resolvedFromBoardPlacement =
                    TryResolveLogicalBuildingFootprint(
                        space,
                        snapshot,
                        out sourcePosition,
                        out directionTowardRoad,
                        out width,
                        out depth);

                if (!resolvedFromBoardPlacement)
                {
                    ResolveLegacyLogicalBuildingFrame(
                        space,
                        snapshot,
                        random,
                        out sourcePosition,
                        out directionTowardRoad,
                        out width,
                        out depth);
                }

                string buildingTypeName =
                    space.BuildingType.ToString();

                Material mainMaterial;

                if (!logicalBuildingColorAssignments.TryGetValue(
                        space,
                        out mainMaterial) ||
                    mainMaterial == null)
                {
                    int fallbackPaletteIndex =
                        ResolvePreferredLogicalPaletteIndex(
                            buildingTypeName,
                            stableHash);

                    mainMaterial =
                        fallbackPaletteIndex >= 0 &&
                        fallbackPaletteIndex <
                            logicalBuildingPaletteMaterials.Length
                            ? logicalBuildingPaletteMaterials[
                                fallbackPaletteIndex]
                            : null;
                }

                if (mainMaterial == null)
                {
                    return false;
                }

                space.SetRuntimeBuildingColor(
                    mainMaterial.color);

                Material accentMaterial =
                    uniformLogicalBuildingColor
                        ? mainMaterial
                        : ResolveLogicalBuildingAccentMaterial(
                            mainMaterial,
                            stableHash);

                int minimumFloors;
                int maximumFloors;

                ResolveLogicalBuildingFloorRange(
                    buildingTypeName,
                    out minimumFloors,
                    out maximumFloors);

                int floors = random.Next(
                    minimumFloors,
                    maximumFloors + 1);

                float totalHeight = Mathf.Max(
                    buildingFloorWorldHeight * floors,
                    buildingFloorWorldHeight * 2f);

                buildingRoot = new GameObject(
                    "LogicalBuilding_" +
                    (
                        string.IsNullOrEmpty(space.NodeId)
                            ? MakeSafeObjectName(space.gameObject.name)
                            : MakeSafeObjectName(space.NodeId)
                    ));

                buildingRoot.transform.SetParent(
                    parent,
                    true);

                float logicalGroundY =
                    ResolveLogicalBuildingGroundY(
                        space,
                        snapshot,
                        sourcePosition.y);

                buildingRoot.transform.position =
                    new Vector3(
                        sourcePosition.x,
                        logicalGroundY,
                        sourcePosition.z);

                // 로컬 +Z가 BoardSpace에 기록된 실제 입구 도로를 향합니다.
                if (directionTowardRoad.sqrMagnitude <= 0.000001f)
                {
                    directionTowardRoad = Vector3.forward;
                }

                buildingRoot.transform.rotation =
                    Quaternion.LookRotation(
                        directionTowardRoad,
                        Vector3.up);

                CreateLogicalBuildingMasses(
                    buildingRoot.transform,
                    buildingTypeName,
                    width,
                    depth,
                    totalHeight,
                    cellSize,
                    random,
                    mainMaterial,
                    accentMaterial,
                    space.NodeId);

                Renderer[] generatedRenderers =
                    buildingRoot.GetComponentsInChildren<Renderer>(true);

                if (generatedRenderers.Length == 0)
                {
                    DestroyObject(buildingRoot);
                    return false;
                }

                return true;
            }
            catch (System.Exception exception)
            {
                if (buildingRoot != null)
                {
                    DestroyObject(buildingRoot);
                }

                Debug.LogError(
                    "[ProceduralCityBoardManager] 논리 건물 비주얼 생성 실패: " +
                    (
                        space != null
                            ? space.NodeId
                            : "(null)"
                    ) +
                    "\n" + exception,
                    this);

                return false;
            }
        }

        private float ResolveLogicalBuildingGroundY(
            BoardSpacePrefab space,
            GameBoardRuntimeSnapshot snapshot,
            float fallbackY)
        {
            if (!alignLogicalBuildingsToGeneratedSurface)
            {
                return
                    fallbackY +
                    logicalBuildingGroundOffset;
            }

            float generatedSurfaceY;

            if (TryGetGeneratedRoadSurfaceY(
                out generatedSurfaceY))
            {
                return
                    generatedSurfaceY +
                    logicalBuildingGroundOffset;
            }

            BoardSpacePrefab nearestRoad;

            if (space != null &&
                TryFindNearestRoadSpace(
                    space,
                    snapshot,
                    out nearestRoad) &&
                nearestRoad != null)
            {
                return
                    nearestRoad.transform.position.y +
                    roadSurfaceOffset +
                    logicalBuildingGroundOffset;
            }

            if (generatedVisualRoot != null)
            {
                return
                    generatedVisualRoot.position.y +
                    roadSurfaceOffset +
                    logicalBuildingGroundOffset;
            }

            return
                roadSurfaceOffset +
                logicalBuildingGroundOffset;
        }

        private bool TryGetGeneratedRoadSurfaceY(
            out float surfaceY)
        {
            surfaceY =
                float.NegativeInfinity;

            if (generatedVisualRoot == null)
            {
                return false;
            }

            Renderer[] renderers =
                generatedVisualRoot.GetComponentsInChildren<Renderer>(
                    true);

            bool found = false;

            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                Renderer renderer =
                    renderers[index];

                if (renderer == null)
                {
                    continue;
                }

                string objectName =
                    renderer.gameObject.name;

                bool isRoadSurface =
                    objectName.StartsWith(
                        "Roads_",
                        System.StringComparison.Ordinal) ||
                    objectName.StartsWith(
                        "Sidewalks_",
                        System.StringComparison.Ordinal);

                if (!isRoadSurface)
                {
                    continue;
                }

                surfaceY =
                    Mathf.Max(
                        surfaceY,
                        renderer.bounds.max.y);

                found = true;
            }

            return found;
        }

        private bool TryResolveLogicalBuildingFootprint(
            BoardSpacePrefab space,
            GameBoardRuntimeSnapshot snapshot,
            out Vector3 centerWorld,
            out Vector3 directionTowardRoad,
            out float width,
            out float depth)
        {
            centerWorld =
                space != null
                    ? space.transform.position
                    : Vector3.zero;

            directionTowardRoad = Vector3.forward;
            width = 0f;
            depth = 0f;

            if (space == null ||
                !space.HasBuildingPlacementData ||
                space.OccupiedBoardCells == null ||
                space.OccupiedBoardCells.Count == 0)
            {
                return false;
            }

            int minimumX = int.MaxValue;
            int maximumX = int.MinValue;
            int minimumY = int.MaxValue;
            int maximumY = int.MinValue;

            float totalX = 0f;
            float totalY = 0f;

            for (int index = 0;
                 index < space.OccupiedBoardCells.Count;
                 index++)
            {
                Vector2Int cell = space.OccupiedBoardCells[index];

                minimumX = Mathf.Min(minimumX, cell.x);
                maximumX = Mathf.Max(maximumX, cell.x);
                minimumY = Mathf.Min(minimumY, cell.y);
                maximumY = Mathf.Max(maximumY, cell.y);

                totalX += cell.x;
                totalY += cell.y;
            }

            float cellSize = Mathf.Max(0.01f, snapshot.CellWorldSize);
            float count = Mathf.Max(1, space.OccupiedBoardCells.Count);

            centerWorld = new Vector3(
                totalX / count * cellSize,
                space.transform.position.y,
                totalY / count * cellSize);

            Vector3 entranceWorld = new Vector3(
                space.EntranceBoardCell.x * cellSize,
                centerWorld.y,
                space.EntranceBoardCell.y * cellSize);

            Vector3 toEntrance = entranceWorld - centerWorld;
            toEntrance.y = 0f;

            if (toEntrance.sqrMagnitude <= 0.000001f)
            {
                toEntrance = space.transform.forward;
                toEntrance.y = 0f;
            }

            directionTowardRoad =
                CardinalizeHorizontalDirection(toEntrance);

            float footprintSizeX =
                (maximumX - minimumX + 1) * cellSize;

            float footprintSizeZ =
                (maximumY - minimumY + 1) * cellSize;

            float inset =
                logicalBuildingFootprintInsetCells * cellSize;

            if (Mathf.Abs(directionTowardRoad.x) > 0.5f)
            {
                // 로컬 +Z가 전역 X 방향이므로 로컬 폭은 전역 Z, 깊이는 전역 X입니다.
                width = footprintSizeZ - inset;
                depth = footprintSizeX - inset;
            }
            else
            {
                width = footprintSizeX - inset;
                depth = footprintSizeZ - inset;
            }

            width = Mathf.Max(cellSize * 0.34f, width);
            depth = Mathf.Max(cellSize * 0.34f, depth);

            return true;
        }

        private void ResolveLegacyLogicalBuildingFrame(
            BoardSpacePrefab space,
            GameBoardRuntimeSnapshot snapshot,
            System.Random random,
            out Vector3 sourcePosition,
            out Vector3 directionTowardRoad,
            out float width,
            out float depth)
        {
            float cellSize = Mathf.Max(0.01f, snapshot.CellWorldSize);

            BoardSpacePrefab nearestRoad;
            bool hasRoad = TryFindNearestRoadSpace(
                space,
                snapshot,
                out nearestRoad);

            sourcePosition = space.transform.position;
            Vector3 outwardFromRoad = Vector3.forward;
            float roadDistance = cellSize;

            if (hasRoad && nearestRoad != null)
            {
                Vector3 roadToBuilding =
                    sourcePosition - nearestRoad.transform.position;

                roadToBuilding.y = 0f;

                if (roadToBuilding.sqrMagnitude > 0.000001f)
                {
                    outwardFromRoad =
                        CardinalizeHorizontalDirection(roadToBuilding);
                }

                roadDistance = Mathf.Max(
                    0.01f,
                    Mathf.Abs(Vector3.Dot(roadToBuilding, outwardFromRoad)));
            }
            else
            {
                Vector3 fallbackForward = space.transform.forward;
                fallbackForward.y = 0f;

                if (fallbackForward.sqrMagnitude > 0.000001f)
                {
                    outwardFromRoad =
                        -CardinalizeHorizontalDirection(fallbackForward);
                }
            }

            float roadAndSidewalkHalfWidth =
                (
                    roadWidthCells * 0.5f +
                    (createSidewalks ? sidewalkWidthCells : 0f) +
                    logicalBuildingRoadGapCells
                ) * cellSize;

            float inferredHalfDepth = Mathf.Max(
                logicalBuildingMinimumDepthCells * cellSize * 0.5f,
                roadDistance - roadAndSidewalkHalfWidth);

            depth = Mathf.Clamp(
                inferredHalfDepth * 2f,
                logicalBuildingMinimumDepthCells * cellSize,
                logicalBuildingMaximumDepthCells * cellSize);

            bool longAxisPointsTowardRoad = depth > cellSize * 1.18f;
            float widthMinimum = logicalBuildingMinimumWidthCells * cellSize;
            float widthMaximum = logicalBuildingMaximumWidthCells * cellSize;

            if (longAxisPointsTowardRoad)
            {
                width = Mathf.Lerp(
                    widthMinimum,
                    Mathf.Min(widthMaximum, cellSize * 1.02f),
                    (float)random.NextDouble());
            }
            else
            {
                width = Mathf.Lerp(
                    Mathf.Max(widthMinimum, cellSize * 1.34f),
                    widthMaximum,
                    (float)random.NextDouble());
            }

            directionTowardRoad = -outwardFromRoad;
        }

        private void CreateLogicalBuildingMasses(
            Transform root,
            string buildingTypeName,
            float width,
            float depth,
            float totalHeight,
            float cellSize,
            System.Random random,
            Material mainMaterial,
            Material accentMaterial,
            string nodeId)
        {
            bool isApartment =
                ContainsTypeName(
                    buildingTypeName,
                    "Apartment");

            bool isShopping =
                ContainsTypeName(
                    buildingTypeName,
                    "Shopping") ||
                ContainsTypeName(
                    buildingTypeName,
                    "Mall") ||
                ContainsTypeName(
                    buildingTypeName,
                    "Commercial");

            bool isHospital =
                ContainsTypeName(
                    buildingTypeName,
                    "Hospital") ||
                ContainsTypeName(
                    buildingTypeName,
                    "Medical");

            bool isOffice =
                ContainsTypeName(
                    buildingTypeName,
                    "Office");

            bool isIndustrial =
                ContainsTypeName(
                    buildingTypeName,
                    "Industrial") ||
                ContainsTypeName(
                    buildingTypeName,
                    "Factory");

            bool isCivic =
                ContainsTypeName(
                    buildingTypeName,
                    "Civic") ||
                ContainsTypeName(
                    buildingTypeName,
                    "Public");

            float foundationHeight =
                Mathf.Max(
                    0.045f,
                    buildingFloorWorldHeight * 0.22f);

            CreateLogicalBuildingMass(
                root,
                "Foundation",
                new Vector3(
                    0f,
                    foundationHeight * 0.5f,
                    0f),
                new Vector3(
                    width * 1.035f,
                    foundationHeight,
                    depth * 1.035f),
                accentMaterial,
                nodeId);

            if (isShopping || isIndustrial)
            {
                float lowerHeight =
                    totalHeight *
                    (isShopping ? 0.68f : 0.74f);

                CreateLogicalBuildingMass(
                    root,
                    "WideMain",
                    new Vector3(
                        0f,
                        foundationHeight +
                        lowerHeight * 0.5f,
                        -depth * 0.025f),
                    new Vector3(
                        width * 0.96f,
                        lowerHeight,
                        depth * 0.92f),
                    mainMaterial,
                    nodeId);

                float upperHeight =
                    totalHeight - lowerHeight;

                if (upperHeight > buildingFloorWorldHeight * 0.7f)
                {
                    CreateLogicalBuildingMass(
                        root,
                        "UpperUtility",
                        new Vector3(
                            width * 0.12f,
                            foundationHeight +
                            lowerHeight +
                            upperHeight * 0.5f,
                            -depth * 0.08f),
                        new Vector3(
                            width * 0.48f,
                            upperHeight,
                            depth * 0.44f),
                        accentMaterial,
                        nodeId);
                }

                if (isIndustrial)
                {
                    float utilitySize =
                        Mathf.Min(
                            width,
                            depth) * 0.20f;

                    CreateLogicalBuildingMass(
                        root,
                        "IndustrialRoofUnit",
                        new Vector3(
                            -width * 0.22f,
                            foundationHeight +
                            lowerHeight +
                            utilitySize * 0.30f,
                            depth * 0.10f),
                        new Vector3(
                            utilitySize,
                            utilitySize * 0.60f,
                            utilitySize),
                        accentMaterial,
                        nodeId);
                }

                return;
            }

            if (isHospital || isCivic)
            {
                float publicPodiumHeight =
                    Mathf.Min(
                        totalHeight * 0.38f,
                        buildingFloorWorldHeight * 2.3f);

                CreateLogicalBuildingMass(
                    root,
                    "PublicPodium",
                    new Vector3(
                        0f,
                        foundationHeight +
                        publicPodiumHeight * 0.5f,
                        depth * 0.03f),
                    new Vector3(
                        width * 0.98f,
                        publicPodiumHeight,
                        depth * 0.93f),
                    mainMaterial,
                    nodeId);

                float upperHeight =
                    Mathf.Max(
                        buildingFloorWorldHeight,
                        totalHeight - publicPodiumHeight);

                CreateLogicalBuildingMass(
                    root,
                    "PublicUpper",
                    new Vector3(
                        width *
                        (isHospital ? -0.10f : 0.08f),
                        foundationHeight +
                        publicPodiumHeight +
                        upperHeight * 0.5f,
                        -depth * 0.10f),
                    new Vector3(
                        width *
                        (isHospital ? 0.62f : 0.68f),
                        upperHeight,
                        depth * 0.60f),
                    accentMaterial,
                    nodeId);

                if (isHospital)
                {
                    float crossThickness =
                        Mathf.Min(
                            width,
                            depth) * 0.10f;

                    float crossHeight =
                        Mathf.Max(
                            0.025f,
                            buildingFloorWorldHeight * 0.16f);

                    CreateLogicalBuildingMass(
                        root,
                        "HospitalRoofMarkHorizontal",
                        new Vector3(
                            -width * 0.10f,
                            foundationHeight +
                            totalHeight +
                            crossHeight * 0.5f,
                            -depth * 0.10f),
                        new Vector3(
                            width * 0.24f,
                            crossHeight,
                            crossThickness),
                        accentMaterial,
                        nodeId);

                    CreateLogicalBuildingMass(
                        root,
                        "HospitalRoofMarkVertical",
                        new Vector3(
                            -width * 0.10f,
                            foundationHeight +
                            totalHeight +
                            crossHeight * 0.5f,
                            -depth * 0.10f),
                        new Vector3(
                            crossThickness,
                            crossHeight,
                            depth * 0.24f),
                        accentMaterial,
                        nodeId);
                }

                return;
            }

            float podiumRatio =
                isApartment || isOffice
                    ? 0.18f
                    : Mathf.Lerp(
                        0.20f,
                        0.30f,
                        (float)random.NextDouble());

            float podiumHeight =
                Mathf.Max(
                    buildingFloorWorldHeight,
                    totalHeight * podiumRatio);

            CreateLogicalBuildingMass(
                root,
                "Podium",
                new Vector3(
                    0f,
                    foundationHeight +
                    podiumHeight * 0.5f,
                    depth * 0.025f),
                new Vector3(
                    width * 0.96f,
                    podiumHeight,
                    depth * 0.92f),
                accentMaterial,
                nodeId);

            float towerHeight =
                Mathf.Max(
                    buildingFloorWorldHeight,
                    totalHeight - podiumHeight);

            float towerWidthRatio =
                isOffice
                    ? 0.68f
                    : isApartment
                        ? 0.64f
                        : Mathf.Lerp(
                            0.58f,
                            0.74f,
                            (float)random.NextDouble());

            float towerDepthRatio =
                isOffice
                    ? 0.66f
                    : isApartment
                        ? 0.72f
                        : Mathf.Lerp(
                            0.58f,
                            0.74f,
                            (float)random.NextDouble());

            float towerOffsetX =
                Mathf.Lerp(
                    -width * 0.12f,
                    width * 0.12f,
                    (float)random.NextDouble());

            CreateLogicalBuildingMass(
                root,
                "Tower",
                new Vector3(
                    towerOffsetX,
                    foundationHeight +
                    podiumHeight +
                    towerHeight * 0.5f,
                    -depth * 0.08f),
                new Vector3(
                    width * towerWidthRatio,
                    towerHeight,
                    depth * towerDepthRatio),
                mainMaterial,
                nodeId);

            if (!isApartment && !isOffice)
            {
                float sideHeight =
                    totalHeight *
                    Mathf.Lerp(
                        0.38f,
                        0.62f,
                        (float)random.NextDouble());

                float sideWidth =
                    width *
                    Mathf.Lerp(
                        0.22f,
                        0.34f,
                        (float)random.NextDouble());

                float sideSign =
                    random.NextDouble() < 0.5
                        ? -1f
                        : 1f;

                CreateLogicalBuildingMass(
                    root,
                    "SideWing",
                    new Vector3(
                        sideSign *
                        (width * 0.42f - sideWidth * 0.45f),
                        foundationHeight +
                        sideHeight * 0.5f,
                        depth * 0.02f),
                    new Vector3(
                        sideWidth,
                        sideHeight,
                        depth * 0.66f),
                    accentMaterial,
                    nodeId);
            }

            float roofHeight =
                Mathf.Max(
                    0.035f,
                    buildingFloorWorldHeight * 0.20f);

            CreateLogicalBuildingMass(
                root,
                "RoofCap",
                new Vector3(
                    towerOffsetX,
                    foundationHeight +
                    podiumHeight +
                    towerHeight +
                    roofHeight * 0.5f,
                    -depth * 0.08f),
                new Vector3(
                    width * towerWidthRatio * 0.72f,
                    roofHeight,
                    depth * towerDepthRatio * 0.72f),
                accentMaterial,
                nodeId);
        }

        private GameObject CreateLogicalBuildingMass(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            string nodeId)
        {
            GameObject mass =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            mass.name = objectName;
            mass.transform.SetParent(parent, false);
            mass.transform.localPosition = localPosition;
            mass.transform.localRotation = Quaternion.identity;
            mass.transform.localScale = new Vector3(
                Mathf.Max(0.01f, localScale.x),
                Mathf.Max(0.01f, localScale.y),
                Mathf.Max(0.01f, localScale.z));

            Collider collider =
                mass.GetComponent<Collider>();

            if (collider != null)
            {
                DestroyComponent(collider);
            }

            MeshRenderer renderer =
                mass.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            ProceduralGeneratedVisualMarker marker =
                mass.AddComponent<ProceduralGeneratedVisualMarker>();

            marker.Initialize(
                ProceduralGeneratedVisualType.DenseSmallBuilding,
                nodeId ?? string.Empty);

            return mass;
        }

        private bool TryFindNearestRoadSpace(
            BoardSpacePrefab building,
            GameBoardRuntimeSnapshot snapshot,
            out BoardSpacePrefab nearestRoad)
        {
            nearestRoad = null;

            if (building == null)
            {
                return false;
            }

            float bestDistance = float.MaxValue;

            IList<BoardSpacePrefab> connected =
                building.ConnectedSpaces;

            if (connected != null)
            {
                for (int index = 0;
                     index < connected.Count;
                     index++)
                {
                    BoardSpacePrefab candidate =
                        connected[index];

                    if (candidate == null ||
                        candidate.SpaceType != BoardSpaceType.Road)
                    {
                        continue;
                    }

                    float distance =
                        HorizontalSquaredDistance(
                            building.transform.position,
                            candidate.transform.position);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        nearestRoad = candidate;
                    }
                }
            }

            if (nearestRoad != null)
            {
                return true;
            }

            if (snapshot.Roads == null)
            {
                return false;
            }

            for (int index = 0;
                 index < snapshot.Roads.Count;
                 index++)
            {
                RuntimeRoadNode roadNode =
                    snapshot.Roads[index];

                if (roadNode == null ||
                    roadNode.Space == null)
                {
                    continue;
                }

                float distance =
                    HorizontalSquaredDistance(
                        building.transform.position,
                        roadNode.Space.transform.position);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearestRoad = roadNode.Space;
                }
            }

            return nearestRoad != null;
        }

        private static Vector3 CardinalizeHorizontalDirection(
            Vector3 direction)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.000001f)
            {
                return Vector3.forward;
            }

            if (Mathf.Abs(direction.x) >=
                Mathf.Abs(direction.z))
            {
                return new Vector3(
                    direction.x >= 0f ? 1f : -1f,
                    0f,
                    0f);
            }

            return new Vector3(
                0f,
                0f,
                direction.z >= 0f ? 1f : -1f);
        }

        private static float HorizontalSquaredDistance(
            Vector3 first,
            Vector3 second)
        {
            float x = first.x - second.x;
            float z = first.z - second.z;
            return x * x + z * z;
        }

        private void BuildLogicalBuildingColorAssignments(
            GameBoardRuntimeSnapshot snapshot)
        {
            logicalBuildingColorAssignments.Clear();

            if (snapshot != null && snapshot.Buildings != null)
            {
                for (int clearIndex = 0;
                     clearIndex < snapshot.Buildings.Count;
                     clearIndex++)
                {
                    RuntimeBuildingNode clearNode =
                        snapshot.Buildings[clearIndex];

                    if (clearNode != null && clearNode.Space != null)
                    {
                        clearNode.Space.ClearRuntimeBuildingColor();
                    }
                }
            }

            if (snapshot == null ||
                snapshot.Buildings == null ||
                logicalBuildingPaletteMaterials == null ||
                logicalBuildingPaletteMaterials.Length == 0)
            {
                return;
            }

            List<BoardSpacePrefab> spaces =
                new List<BoardSpacePrefab>();

            for (int index = 0;
                 index < snapshot.Buildings.Count;
                 index++)
            {
                RuntimeBuildingNode node =
                    snapshot.Buildings[index];

                if (node != null &&
                    node.Space != null &&
                    !spaces.Contains(node.Space))
                {
                    spaces.Add(node.Space);
                }
            }

            spaces.Sort(
                delegate(
                    BoardSpacePrefab first,
                    BoardSpacePrefab second)
                {
                    int firstNeighborCount =
                        CountLogicalBuildingColorNeighbors(
                            first,
                            spaces,
                            snapshot.CellWorldSize);

                    int secondNeighborCount =
                        CountLogicalBuildingColorNeighbors(
                            second,
                            spaces,
                            snapshot.CellWorldSize);

                    int degreeCompare =
                        secondNeighborCount.CompareTo(
                            firstNeighborCount);

                    if (degreeCompare != 0)
                    {
                        return degreeCompare;
                    }

                    return string.CompareOrdinal(
                        first != null ? first.NodeId : string.Empty,
                        second != null ? second.NodeId : string.Empty);
                });

            for (int index = 0;
                 index < spaces.Count;
                 index++)
            {
                BoardSpacePrefab space =
                    spaces[index];

                if (space == null)
                {
                    continue;
                }

                HashSet<Material> neighborMaterials =
                    new HashSet<Material>();

                foreach (
                    KeyValuePair<BoardSpacePrefab, Material> pair
                    in logicalBuildingColorAssignments)
                {
                    if (pair.Key == null ||
                        pair.Value == null)
                    {
                        continue;
                    }

                    if (AreLogicalBuildingsColorNeighbors(
                            space,
                            pair.Key,
                            snapshot.CellWorldSize))
                    {
                        neighborMaterials.Add(
                            pair.Value);
                    }
                }

                int stableHash =
                    GetStableHash(
                        string.IsNullOrEmpty(space.NodeId)
                            ? space.gameObject.name
                            : space.NodeId);

                int preferredIndex =
                    ResolvePreferredLogicalPaletteIndex(
                        space.BuildingType.ToString(),
                        stableHash);

                Material selectedMaterial =
                    SelectAvailableLogicalBuildingMaterial(
                        preferredIndex,
                        neighborMaterials,
                        stableHash,
                        space.NodeId);

                if (selectedMaterial != null)
                {
                    logicalBuildingColorAssignments[space] =
                        selectedMaterial;
                    space.SetRuntimeBuildingColor(
                        selectedMaterial.color);
                }
            }
        }

        private int CountLogicalBuildingColorNeighbors(
            BoardSpacePrefab source,
            IList<BoardSpacePrefab> allSpaces,
            float cellWorldSize)
        {
            if (source == null ||
                allSpaces == null)
            {
                return 0;
            }

            int count = 0;

            for (int index = 0;
                 index < allSpaces.Count;
                 index++)
            {
                BoardSpacePrefab other =
                    allSpaces[index];

                if (other == null ||
                    other == source)
                {
                    continue;
                }

                if (AreLogicalBuildingsColorNeighbors(
                        source,
                        other,
                        cellWorldSize))
                {
                    count++;
                }
            }

            return count;
        }

        private bool AreLogicalBuildingsColorNeighbors(
            BoardSpacePrefab first,
            BoardSpacePrefab second,
            float cellWorldSize)
        {
            if (!preventNeighboringLogicalBuildingColors ||
                first == null ||
                second == null ||
                first == second)
            {
                return false;
            }

            float maximumCellDistance =
                Mathf.Max(
                    1f,
                    logicalBuildingColorNeighborDistanceCells);

            IList<Vector2Int> firstCells =
                first.OccupiedBoardCoordinates;

            IList<Vector2Int> secondCells =
                second.OccupiedBoardCoordinates;

            if (firstCells != null &&
                firstCells.Count > 0 &&
                secondCells != null &&
                secondCells.Count > 0)
            {
                float maximumSquaredCellDistance =
                    maximumCellDistance *
                    maximumCellDistance;

                for (int firstIndex = 0;
                     firstIndex < firstCells.Count;
                     firstIndex++)
                {
                    for (int secondIndex = 0;
                         secondIndex < secondCells.Count;
                         secondIndex++)
                    {
                        float deltaX =
                            firstCells[firstIndex].x -
                            secondCells[secondIndex].x;

                        float deltaY =
                            firstCells[firstIndex].y -
                            secondCells[secondIndex].y;

                        float squaredDistance =
                            deltaX * deltaX +
                            deltaY * deltaY;

                        if (squaredDistance <=
                            maximumSquaredCellDistance)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            float worldDistance =
                Mathf.Max(
                    0.01f,
                    cellWorldSize) *
                maximumCellDistance;

            return
                HorizontalSquaredDistance(
                    first.transform.position,
                    second.transform.position) <=
                worldDistance * worldDistance;
        }

        private Material SelectAvailableLogicalBuildingMaterial(
            int preferredIndex,
            HashSet<Material> neighborMaterials,
            int stableHash,
            string nodeId)
        {
            int paletteCount =
                logicalBuildingPaletteMaterials.Length;

            if (paletteCount <= 0)
            {
                return null;
            }

            preferredIndex =
                Mathf.Abs(preferredIndex) %
                paletteCount;

            for (int offset = 0;
                 offset < paletteCount;
                 offset++)
            {
                int paletteIndex =
                    (preferredIndex + offset) %
                    paletteCount;

                Material candidate =
                    logicalBuildingPaletteMaterials[
                        paletteIndex];

                if (candidate == null)
                {
                    continue;
                }

                if (neighborMaterials == null ||
                    !neighborMaterials.Contains(candidate))
                {
                    return candidate;
                }
            }

            // 매우 밀집된 배치에서 팔레트 8색이 모두 이웃에 사용된 경우에도
            // 동일 색을 허용하지 않고 해당 건물 전용 색을 생성합니다.
            Color overflowColor =
                CreateOverflowLogicalBuildingColor(
                    stableHash,
                    neighborMaterials);

            Material overflowMaterial =
                CreateRuntimeMaterial(
                    "ProceduralLogicalBuildingOverflow_" +
                    MakeSafeObjectName(
                        string.IsNullOrEmpty(nodeId)
                            ? stableHash.ToString()
                            : nodeId),
                    overflowColor);

            if (overflowMaterial != null)
            {
                logicalBuildingOverflowMaterials.Add(
                    overflowMaterial);
            }

            return overflowMaterial;
        }

        private Color CreateOverflowLogicalBuildingColor(
            int stableHash,
            HashSet<Material> neighborMaterials)
        {
            float baseHue =
                Mathf.Repeat(
                    Mathf.Abs(stableHash) * 0.61803398875f,
                    1f);

            Color bestColor =
                Color.HSVToRGB(
                    baseHue,
                    0.56f,
                    0.76f);

            float bestMinimumDistance =
                -1f;

            for (int attempt = 0;
                 attempt < 24;
                 attempt++)
            {
                float hue =
                    Mathf.Repeat(
                        baseHue +
                        attempt * 0.137f,
                        1f);

                Color candidate =
                    Color.HSVToRGB(
                        hue,
                        0.50f +
                        (attempt % 3) * 0.06f,
                        0.70f +
                        (attempt % 2) * 0.08f);

                float minimumDistance =
                    float.PositiveInfinity;

                if (neighborMaterials != null)
                {
                    foreach (Material material in neighborMaterials)
                    {
                        if (material == null)
                        {
                            continue;
                        }

                        Color neighborColor =
                            material.color;

                        float red =
                            candidate.r -
                            neighborColor.r;

                        float green =
                            candidate.g -
                            neighborColor.g;

                        float blue =
                            candidate.b -
                            neighborColor.b;

                        float squaredDistance =
                            red * red +
                            green * green +
                            blue * blue;

                        minimumDistance =
                            Mathf.Min(
                                minimumDistance,
                                squaredDistance);
                    }
                }

                if (minimumDistance >
                    bestMinimumDistance)
                {
                    bestMinimumDistance =
                        minimumDistance;

                    bestColor =
                        candidate;
                }
            }

            bestColor.a = 1f;
            return bestColor;
        }

        private int ResolvePreferredLogicalPaletteIndex(
            string buildingTypeName,
            int stableHash)
        {
            if (ContainsTypeName(buildingTypeName, "Apartment") ||
                ContainsTypeName(buildingTypeName, "Residential"))
            {
                return 0;
            }

            if (ContainsTypeName(buildingTypeName, "Shopping") ||
                ContainsTypeName(buildingTypeName, "Mall") ||
                ContainsTypeName(buildingTypeName, "Commercial"))
            {
                return 1;
            }

            if (ContainsTypeName(buildingTypeName, "Hospital") ||
                ContainsTypeName(buildingTypeName, "Medical"))
            {
                return 2;
            }

            if (ContainsTypeName(buildingTypeName, "Office"))
            {
                return 3;
            }

            if (ContainsTypeName(buildingTypeName, "Industrial") ||
                ContainsTypeName(buildingTypeName, "Factory"))
            {
                return 4;
            }

            if (ContainsTypeName(buildingTypeName, "Civic") ||
                ContainsTypeName(buildingTypeName, "Public"))
            {
                return 5;
            }

            return
                Mathf.Abs(stableHash) %
                logicalBuildingPaletteMaterials.Length;
        }

        private Material ResolveLogicalBuildingAccentMaterial(
            Material mainMaterial,
            int stableHash)
        {
            if (mainMaterial == null)
            {
                return null;
            }

            int paletteCount =
                logicalBuildingPaletteMaterials.Length;

            if (paletteCount <= 1)
            {
                return mainMaterial;
            }

            int startIndex =
                Mathf.Abs(stableHash / 7) %
                paletteCount;

            for (int offset = 0;
                 offset < paletteCount;
                 offset++)
            {
                Material candidate =
                    logicalBuildingPaletteMaterials[
                        (startIndex + offset) %
                        paletteCount];

                if (candidate != null &&
                    candidate != mainMaterial)
                {
                    return candidate;
                }
            }

            return mainMaterial;
        }

        private static Color ToClampedAchromatic(
            Color source,
            float minimumValue,
            float maximumValue)
        {
            float luminance =
                source.r * 0.2126f +
                source.g * 0.7152f +
                source.b * 0.0722f;

            float gray =
                Mathf.Clamp(
                    luminance,
                    minimumValue,
                    maximumValue);

            return new Color(
                gray,
                gray,
                gray,
                Mathf.Clamp01(source.a));
        }

        private static void ResolveLogicalBuildingFloorRange(
            string buildingTypeName,
            out int minimumFloors,
            out int maximumFloors)
        {
            if (ContainsTypeName(buildingTypeName, "Shopping") ||
                ContainsTypeName(buildingTypeName, "Mall"))
            {
                minimumFloors = 2;
                maximumFloors = 4;
                return;
            }

            if (ContainsTypeName(buildingTypeName, "Industrial") ||
                ContainsTypeName(buildingTypeName, "Factory"))
            {
                minimumFloors = 2;
                maximumFloors = 5;
                return;
            }

            if (ContainsTypeName(buildingTypeName, "Hospital") ||
                ContainsTypeName(buildingTypeName, "Medical"))
            {
                minimumFloors = 4;
                maximumFloors = 7;
                return;
            }

            if (ContainsTypeName(buildingTypeName, "Civic") ||
                ContainsTypeName(buildingTypeName, "Public"))
            {
                minimumFloors = 3;
                maximumFloors = 7;
                return;
            }

            if (ContainsTypeName(buildingTypeName, "Apartment") ||
                ContainsTypeName(buildingTypeName, "Residential"))
            {
                minimumFloors = 7;
                maximumFloors = 12;
                return;
            }

            if (ContainsTypeName(buildingTypeName, "Office"))
            {
                minimumFloors = 6;
                maximumFloors = 11;
                return;
            }

            minimumFloors = 4;
            maximumFloors = 9;
        }

        private static bool ContainsTypeName(
            string value,
            string token)
        {
            return
                !string.IsNullOrEmpty(value) &&
                !string.IsNullOrEmpty(token) &&
                value.IndexOf(
                    token,
                    System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetStableHash(
            string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string safeValue = value ?? string.Empty;

                for (int index = 0;
                     index < safeValue.Length;
                     index++)
                {
                    hash ^= safeValue[index];
                    hash *= 16777619u;
                }

                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static string MakeSafeObjectName(
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Unnamed";
            }

            return value.Replace(
                "/",
                "_").Replace(
                "\\",
                "_");
        }

        private void CreateUrbanWoodlandVisuals(
            ProceduralIslandLandscapeLayout landscape)
        {
            if (landscape == null || landscape.Trees.Count == 0)
            {
                return;
            }

            GameObject trunkObject =
                new GameObject("UrbanWoodland_GrayTrunks_CodeGenerated");

            trunkObject.transform.SetParent(generatedVisualRoot, false);

            MeshFilter trunkFilter = trunkObject.AddComponent<MeshFilter>();
            MeshRenderer trunkRenderer =
                trunkObject.AddComponent<MeshRenderer>();

            trunkFilter.sharedMesh =
                ProceduralCityMeshFactory.CreateForestTrunkMesh(
                    landscape.Trees,
                    generatedVisualRoot);

            trunkRenderer.sharedMaterial = woodlandTrunkMaterial;

            ProceduralGeneratedVisualMarker trunkMarker =
                trunkObject.AddComponent<ProceduralGeneratedVisualMarker>();

            trunkMarker.Initialize(
                ProceduralGeneratedVisualType.Forest,
                string.Empty);

            GameObject canopyObject =
                new GameObject("UrbanWoodland_GrayCanopies_CodeGenerated");

            canopyObject.transform.SetParent(generatedVisualRoot, false);

            MeshFilter canopyFilter = canopyObject.AddComponent<MeshFilter>();
            MeshRenderer canopyRenderer =
                canopyObject.AddComponent<MeshRenderer>();

            canopyFilter.sharedMesh =
                ProceduralCityMeshFactory.CreateForestCanopyMesh(
                    landscape.Trees,
                    generatedVisualRoot);

            canopyRenderer.sharedMaterial = woodlandCanopyMaterial;

            ProceduralGeneratedVisualMarker canopyMarker =
                canopyObject.AddComponent<ProceduralGeneratedVisualMarker>();

            canopyMarker.Initialize(
                ProceduralGeneratedVisualType.Forest,
                string.Empty);
        }

        private void ApplyMovementTileInspectionMode(bool immediate)
        {
            StopMovementTileTransition();

            if (!Application.isPlaying ||
                immediate ||
                movementTileTransitionDuration <= 0f)
            {
                ApplyMovementTileVisualState(
                    movementTileInspectionMode ? 1f : 0f,
                    true);
                return;
            }

            movementTileTransitionCoroutine =
                StartCoroutine(AnimateMovementTileInspectionMode());
        }

        private IEnumerator AnimateMovementTileInspectionMode()
        {
            float startBuildingAlpha = GetBuildingMaterialAlpha();
            float startRoadAlpha = GetMaterialAlpha(roadMaterial, 1f);
            float startTileAlpha = GetMovementTileMaterialAlpha();

            float targetBuildingAlpha = movementTileInspectionMode
                ? Mathf.Clamp01(movementTileViewBuildingAlpha)
                : 1f;

            float targetRoadAlpha = movementTileInspectionMode
                ? Mathf.Clamp01(movementTileViewRoadAlpha)
                : 1f;

            float targetTileAlpha = movementTileInspectionMode ? 1f : 0f;

            SetMovementTileRenderersEnabled(true);

            float elapsed = 0f;
            float duration = Mathf.Max(0.0001f, movementTileTransitionDuration);

            while (elapsed < duration)
            {
                elapsed += useUnscaledTimeForTileTransition
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;

                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = movementTileTransitionCurve != null
                    ? movementTileTransitionCurve.Evaluate(normalized)
                    : normalized;

                ApplyVisualAlphaValues(
                    Mathf.Lerp(startBuildingAlpha, targetBuildingAlpha, eased),
                    Mathf.Lerp(startRoadAlpha, targetRoadAlpha, eased),
                    Mathf.Lerp(startTileAlpha, targetTileAlpha, eased));

                yield return null;
            }

            ApplyMovementTileVisualState(
                movementTileInspectionMode ? 1f : 0f,
                true);

            movementTileTransitionCoroutine = null;
        }

        private void ApplyMovementTileVisualState(
            float inspectionWeight,
            bool finalizeRendererState)
        {
            float weight = Mathf.Clamp01(inspectionWeight);

            float buildingAlpha = Mathf.Lerp(
                1f,
                Mathf.Clamp01(movementTileViewBuildingAlpha),
                weight);

            float roadAlpha = Mathf.Lerp(
                1f,
                Mathf.Clamp01(movementTileViewRoadAlpha),
                weight);

            float tileAlpha = weight;

            if (tileAlpha > 0.001f)
            {
                SetMovementTileRenderersEnabled(true);
            }

            ApplyVisualAlphaValues(
                buildingAlpha,
                roadAlpha,
                tileAlpha);

            if (finalizeRendererState)
            {
                SetMovementTileRenderersEnabled(
                    createMovementTileOverlay &&
                    movementTileInspectionMode);
            }
        }

        private void ApplyVisualAlphaValues(
            float buildingAlpha,
            float roadAlpha,
            float tileAlpha)
        {
            bool buildingsTransparent = buildingAlpha < 0.999f;
            bool roadTransparent = roadAlpha < 0.999f;
            bool tilesTransparent = tileAlpha < 0.999f;

            for (int index = 0; index < buildingMaterials.Length; index++)
            {
                SetMaterialTransparency(
                    buildingMaterials[index],
                    buildingAlpha,
                    buildingsTransparent);
            }

            SetMaterialTransparency(
                roadMaterial,
                roadAlpha,
                roadTransparent);

            SetMaterialTransparency(
                sidewalkMaterial,
                roadAlpha,
                roadTransparent);

            SetMaterialTransparency(
                movementTileBorderMaterial,
                tileAlpha,
                tilesTransparent);

            SetMaterialTransparency(
                roadMovementTileMaterial,
                tileAlpha,
                tilesTransparent);

            SetMaterialTransparency(
                neutralBuildingTileMaterial,
                tileAlpha,
                tilesTransparent);

            for (int index = 0; index < landmarkTileMaterials.Length; index++)
            {
                SetMaterialTransparency(
                    landmarkTileMaterials[index],
                    tileAlpha,
                    tilesTransparent);
            }

            ApplyGeneratedRendererState(
                buildingsTransparent,
                roadTransparent);
        }

        private void ApplyGeneratedRendererState(
            bool buildingsTransparent,
            bool roadTransparent)
        {
            if (generatedVisualRoot == null)
            {
                return;
            }

            ProceduralGeneratedVisualMarker[] markers =
                generatedVisualRoot.GetComponentsInChildren<ProceduralGeneratedVisualMarker>(true);

            for (int index = 0; index < markers.Length; index++)
            {
                ProceduralGeneratedVisualMarker marker = markers[index];

                if (marker == null)
                {
                    continue;
                }

                Renderer renderer = marker.GetComponent<Renderer>();

                if (renderer == null)
                {
                    continue;
                }

                if (marker.VisualType == ProceduralGeneratedVisualType.DenseSmallBuilding)
                {
                    renderer.enabled = true;
                    renderer.shadowCastingMode = buildingsTransparent
                        ? UnityEngine.Rendering.ShadowCastingMode.Off
                        : UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = !buildingsTransparent;
                }
                else if (marker.VisualType == ProceduralGeneratedVisualType.Road ||
                         marker.VisualType == ProceduralGeneratedVisualType.Sidewalk)
                {
                    renderer.enabled = true;
                    renderer.shadowCastingMode = roadTransparent
                        ? UnityEngine.Rendering.ShadowCastingMode.Off
                        : UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = !roadTransparent;
                }
            }
        }

        private void SetMovementTileRenderersEnabled(bool enabled)
        {
            if (generatedVisualRoot == null)
            {
                return;
            }

            ProceduralGeneratedVisualMarker[] markers =
                generatedVisualRoot.GetComponentsInChildren<ProceduralGeneratedVisualMarker>(true);

            for (int index = 0; index < markers.Length; index++)
            {
                ProceduralGeneratedVisualMarker marker = markers[index];

                if (marker == null ||
                    marker.VisualType != ProceduralGeneratedVisualType.MovementTile)
                {
                    continue;
                }

                Renderer renderer = marker.GetComponent<Renderer>();

                if (renderer != null)
                {
                    renderer.enabled = enabled && createMovementTileOverlay;
                }
            }
        }

        private float GetBuildingMaterialAlpha()
        {
            for (int index = 0; index < buildingMaterials.Length; index++)
            {
                if (buildingMaterials[index] != null)
                {
                    return GetMaterialAlpha(buildingMaterials[index], 1f);
                }
            }

            return movementTileInspectionMode
                ? Mathf.Clamp01(movementTileViewBuildingAlpha)
                : 1f;
        }

        private float GetMovementTileMaterialAlpha()
        {
            if (movementTileBorderMaterial != null)
            {
                return GetMaterialAlpha(
                    movementTileBorderMaterial,
                    movementTileInspectionMode ? 1f : 0f);
            }

            return movementTileInspectionMode ? 1f : 0f;
        }

        private static float GetMaterialAlpha(
            Material material,
            float fallback)
        {
            if (material == null)
            {
                return fallback;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor").a;
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color").a;
            }

            return fallback;
        }

        private void StopMovementTileTransition()
        {
            if (movementTileTransitionCoroutine == null)
            {
                return;
            }

            StopCoroutine(movementTileTransitionCoroutine);
            movementTileTransitionCoroutine = null;
        }

        private static void SetMaterialTransparency(
            Material material,
            float alpha,
            bool transparent)
        {
            if (material == null)
            {
                return;
            }

            Color color = Color.white;
            bool hasBaseColor = material.HasProperty("_BaseColor");
            bool hasColor = material.HasProperty("_Color");

            if (hasBaseColor)
            {
                color = material.GetColor("_BaseColor");
            }
            else if (hasColor)
            {
                color = material.GetColor("_Color");
            }

            color.a = transparent ? Mathf.Clamp01(alpha) : 1f;

            if (hasBaseColor)
            {
                material.SetColor("_BaseColor", color);
            }

            if (hasColor)
            {
                material.SetColor("_Color", color);
            }

            if (transparent)
            {
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                }

                if (material.HasProperty("_Mode"))
                {
                    material.SetFloat("_Mode", 3f);
                }

                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetFloat(
                        "_SrcBlend",
                        (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                }

                if (material.HasProperty("_DstBlend"))
                {
                    material.SetFloat(
                        "_DstBlend",
                        (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }

                if (material.HasProperty("_ZWrite"))
                {
                    material.SetFloat("_ZWrite", 0f);
                }

                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 0f);
                }

                if (material.HasProperty("_Mode"))
                {
                    material.SetFloat("_Mode", 0f);
                }

                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetFloat(
                        "_SrcBlend",
                        (float)UnityEngine.Rendering.BlendMode.One);
                }

                if (material.HasProperty("_DstBlend"))
                {
                    material.SetFloat(
                        "_DstBlend",
                        (float)UnityEngine.Rendering.BlendMode.Zero);
                }

                if (material.HasProperty("_ZWrite"))
                {
                    material.SetFloat("_ZWrite", 1f);
                }

                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
            }
        }

        private void HideSourceRenderers(IList<RuntimeRoadNode> roads)
        {
            for (int index = 0; index < roads.Count; index++)
            {
                RuntimeRoadNode node = roads[index];

                if (node != null && node.Space != null)
                {
                    HideRenderers(node.Space.gameObject);
                }
            }
        }

        private void HideSourceRenderers(IList<RuntimeBuildingNode> buildings)
        {
            for (int index = 0; index < buildings.Count; index++)
            {
                RuntimeBuildingNode node = buildings[index];

                if (node != null && node.Space != null)
                {
                    HideRenderers(node.Space.gameObject);
                }
            }
        }

        private void HideSourceRenderers(
            IList<RuntimeBuildingNode> buildings,
            ISet<BoardSpacePrefab> generatedSpaces,
            bool preserveFailedSources)
        {
            if (buildings == null)
            {
                return;
            }

            for (int index = 0;
                 index < buildings.Count;
                 index++)
            {
                RuntimeBuildingNode node = buildings[index];

                if (node == null || node.Space == null)
                {
                    continue;
                }

                bool generated =
                    generatedSpaces != null &&
                    generatedSpaces.Contains(node.Space);

                if (generated || !preserveFailedSources)
                {
                    HideRenderers(node.Space.gameObject);
                }
            }
        }

        private void HideRenderers(GameObject source)
        {
            Renderer[] renderers =
                source.GetComponentsInChildren<Renderer>(true);

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];

                if (renderer == null ||
                    renderer.GetComponentInParent<ProceduralGeneratedVisualMarker>() != null)
                {
                    continue;
                }

                sourceRendererStates.Add(
                    new RendererState
                    {
                        Renderer = renderer,
                        WasEnabled = renderer.enabled
                    });

                renderer.enabled = false;
            }
        }

        private void RestoreSourceRenderers()
        {
            for (int index = 0;
                 index < sourceRendererStates.Count;
                 index++)
            {
                RendererState state = sourceRendererStates[index];

                if (state.Renderer != null)
                {
                    state.Renderer.enabled = state.WasEnabled;
                }
            }

            sourceRendererStates.Clear();
        }

        private static Material CreateRuntimeUnlitMaterial(
            string materialName,
            Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return CreateRuntimeMaterial(materialName, color);
            }

            Material material = new Material(shader);
            material.name = materialName;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private static Material CreateRuntimeMaterial(
            string materialName,
            Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader);
            material.name = materialName;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private static void DestroyRuntimeMaterial(ref Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(material);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(material);
            }

            material = null;
        }

        private static void DestroyComponent(Component target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private sealed class RendererState
        {
            public Renderer Renderer;
            public bool WasEnabled;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace BoardOfDead
{
    /// <summary>
    /// 이동 가능 AP 마커 전용 원형 UI Graphic입니다.
    /// Sprite 리소스 없이 정사각 RectTransform 안에 원을 그립니다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class MoveRangeCircleGraphic : MaskableGraphic
    {
        [SerializeField, Range(12, 64)] private int segmentCount = 32;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;

            UIVertex centerVertex = UIVertex.simpleVert;
            centerVertex.color = color;
            centerVertex.position = center;
            vertexHelper.AddVert(centerVertex);

            int segments = Mathf.Clamp(segmentCount, 12, 64);

            for (int index = 0; index <= segments; index++)
            {
                float angle = index / (float)segments * Mathf.PI * 2f;
                UIVertex edgeVertex = UIVertex.simpleVert;
                edgeVertex.color = color;
                edgeVertex.position = center + new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)) * radius;
                vertexHelper.AddVert(edgeVertex);
            }

            for (int index = 1; index <= segments; index++)
            {
                vertexHelper.AddTriangle(0, index, index + 1);
            }
        }
    }

    /// <summary>
    /// 런타임에 생성되는 카드 UI의 포인터 입력을 Ui_Util로 전달합니다.
    /// 별도 파일 없이 Ui_Util.cs 안에서 함께 사용합니다.
    /// </summary>
    internal sealed class BoardCardHoverRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler
    {
        private Action<PointerEventData> entered;
        private Action<PointerEventData> exited;
        private Action<PointerEventData> moved;

        public void Configure(
            Action<PointerEventData> onEntered,
            Action<PointerEventData> onExited,
            Action<PointerEventData> onMoved)
        {
            entered = onEntered;
            exited = onExited;
            moved = onMoved;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            entered?.Invoke(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            exited?.Invoke(eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            moved?.Invoke(eventData);
        }
    }

    /// <summary>
    /// 자동 생성 보드용 런타임 UI 통합 컴포넌트입니다.
    ///
    /// 주요 규칙:
    /// - 라운드 시작 시 특정 플레이어의 턴을 자동 확정하지 않습니다.
    /// - 우측 하단 플레이어 선택 버튼을 클릭하면 행동 후보로 선택됩니다.
    /// - 월드의 말 클릭은 보조 입력이며, UI만으로도 항상 선택할 수 있습니다.
    /// - 이동/탐색 등 AP 행동 버튼을 누르는 순간 해당 캐릭터의 턴이 확정됩니다.
    /// - 턴이 확정된 동안에는 다른 캐릭터로 교체할 수 없습니다.
    /// - 모든 플레이어 선택 버튼은 우측 하단에 세로 목록으로 항상 표시됩니다.
    /// - 선택한 플레이어의 상세 정보는 선택 목록 왼쪽의 우측 하단 패널에 표시됩니다.
    /// - 이동 선택 시 현재 AP로 도달 가능한 모든 공간을 표시합니다.
    /// - 이동 범위는 가중치 탐색으로 계산하고, 커서가 올라간 목적지 경로는 A*로 계산합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class Ui_Util : MonoBehaviour
    {
        private enum UIFontRole
        {
            Title,
            Regular,
            Scene
        }

        [Header("Build")]
        [SerializeField] private bool autoBuildUI = true;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Camera worldCamera;
        [Tooltip("이동 모드에서 건물을 투명하게 하고 1x1 이동 타일을 표시합니다.")]
        [SerializeField] private ProceduralCityBoardManager proceduralCityBoardManager;

        [Header("TMP Fonts - Ui_Util Only")]
        [Tooltip("라운드, 현재 턴, 카드 제목 등 강조 제목에 사용합니다.")]
        [SerializeField] private TMP_FontAsset titleFontAsset;
        [Tooltip("플레이어 정보, 버튼, 본문, 로그 등 일반 UI에 사용합니다.")]
        [SerializeField] private TMP_FontAsset regularFontAsset;
        [Tooltip("보드 위 이동 AP, 카드 슬롯 등 씬 추적 UI에 사용합니다.")]
        [SerializeField] private TMP_FontAsset sceneFontAsset;

        [Header("Player Selector - Bottom Right")]
        [SerializeField] private bool autoFocusSelectedPlayer = true;
        [Tooltip("우측 하단에 항상 노출되는 플레이어 선택 버튼 크기입니다.")]
        [SerializeField] private Vector2 playerSelectorButtonSize = new Vector2(286f, 58f);
        [Tooltip("플레이어 선택 버튼 사이 간격입니다.")]
        [SerializeField, Min(0f)] private float playerSelectorSpacing = 8f;
        [Tooltip("우측 하단 화면 가장자리와 선택 목록 사이 여백입니다.")]
        [SerializeField] private Vector2 playerSelectorScreenOffset = new Vector2(-20f, 20f);
        [Tooltip("선택된 플레이어 상세 패널 크기입니다.")]
        [SerializeField] private Vector2 playerDetailPanelSize = new Vector2(326f, 334f);

        [Header("Board Player Tokens")]
        [Tooltip("월드 모델과 별개로 실제 논리 타일 위치에 원형 플레이어 토큰을 항상 표시합니다.")]
        [SerializeField] private bool showBoardPlayerTokens = true;

        [Tooltip("원형 토큰을 사용할 때 기존 3D 플레이어 모델과 Collider를 숨깁니다.")]
        [SerializeField] private bool hideWorldPlayerModelsWhenTokensVisible = true;

        [Tooltip("플레이어별 프로필 이미지를 지정합니다. Player Id가 우선이며, 비어 있으면 Preset Id로 찾습니다.")]
        [SerializeField] private List<PlayerTokenProfileSetting> playerTokenProfiles =
            new List<PlayerTokenProfileSetting>();

        [SerializeField] private Sprite defaultPlayerTokenSprite;

        [Tooltip("보드 위 원형 플레이어 토큰 지름입니다.")]
        [SerializeField, Range(32f, 96f)] private float playerTokenDiameter = 56f;

        [Tooltip("최대 줌 인 상태에서 플레이어 토큰에 적용할 배율입니다. 카메라가 가까울수록 크게 표시합니다.")]
        [SerializeField, Range(0.80f, 1.80f)]
        private float playerTokenNearZoomScale = 1.30f;

        [Tooltip("최대 줌 아웃 상태에서 플레이어 토큰에 적용할 배율입니다. 카메라가 멀수록 작게 표시합니다.")]
        [SerializeField, Range(0.65f, 1.25f)]
        private float playerTokenFarZoomScale = 0.90f;

        [Tooltip("현재 선택 또는 턴 진행 중인 토큰 확대 배율입니다.")]
        [SerializeField, Range(1f, 1.5f)] private float selectedPlayerTokenScale = 1.18f;

        [Tooltip("논리 타일 중심에서 토큰 추적 기준점을 위로 올리는 월드 높이입니다.")]
        [SerializeField, Range(0f, 3f)] private float playerTokenWorldHeight = 0.42f;

        [Tooltip("같은 칸에 여러 명이 있을 때 토큰 중심을 벌리는 화면 픽셀 거리입니다.")]
        [SerializeField, Range(0f, 64f)] private float sameSpaceTokenSpread = 20f;

        [Tooltip("토큰이 화면 바깥으로 빠지지 않게 유지하는 가장자리 여백입니다.")]
        [SerializeField, Range(0f, 96f)] private float playerTokenScreenPadding = 30f;

        [SerializeField] private Color playerTokenNormalColor =
            new Color(0.18f, 0.22f, 0.28f, 0.98f);

        [SerializeField] private Color playerTokenSelectedColor =
            new Color(0.18f, 0.58f, 0.86f, 1f);

        [SerializeField] private Color playerTokenActiveColor =
            new Color(0.92f, 0.30f, 0.14f, 1f);

        [Header("Movement Range / A Star")]
        [SerializeField, Min(0.05f)] private float pathStepDelay = 0.24f;
        [Tooltip("이동 가능 칸 위에 표시되는 원형 AP 아이콘 지름입니다.")]
        [SerializeField, Range(24f, 72f)] private float moveMarkerDiameter = 42f;
        [Tooltip("커서가 올라가지 않은 이동 마커 숫자의 투명도입니다.")]
        [SerializeField, Range(0f, 1f)] private float moveMarkerIdleTextAlpha = 0.16f;
        [Tooltip("A* 미리보기 경로에 포함된 마커 숫자의 투명도입니다.")]
        [SerializeField, Range(0f, 1f)] private float moveMarkerPathTextAlpha = 0.48f;
        [Tooltip("커서가 올라간 마커 숫자의 투명도입니다.")]
        [SerializeField, Range(0f, 1f)] private float moveMarkerHoverTextAlpha = 1f;
        [Tooltip("평상시 원형 마커 배경의 투명도입니다.")]
        [SerializeField, Range(0f, 1f)] private float moveMarkerIdleBackgroundAlpha = 0.34f;
        [Tooltip("경로 미리보기 원형 마커 배경의 투명도입니다.")]
        [SerializeField, Range(0f, 1f)] private float moveMarkerPathBackgroundAlpha = 0.72f;
        [SerializeField] private Vector2 moveTooltipSize = new Vector2(250f, 112f);
        [SerializeField] private Vector2 moveTooltipOffset = new Vector2(24f, -18f);
        [SerializeField] private Color reachableMoveColor = new Color(0.10f, 0.44f, 0.62f, 1f);
        [SerializeField] private Color pathMoveColor = new Color(0.88f, 0.58f, 0.10f, 1f);
        [SerializeField] private Color hoveredMoveColor = new Color(0.90f, 0.24f, 0.10f, 1f);

        [Header("Board Card Icons")]
        [SerializeField] private bool showFaceDownCardSlots = true;
        [SerializeField] private bool showResolvedCardSlots = true;

        [Tooltip("최대 줌 인 상태에서의 정사각형 카드 아이콘 크기입니다.")]
        [SerializeField, Min(16f)] private float nearCardIconSize = 72f;

        [Tooltip("최대 줌 아웃 상태에서의 정사각형 카드 아이콘 크기입니다.")]
        [SerializeField, Min(12f)] private float farCardIconSize = 34f;

        [Tooltip("줌 아웃 비율에 따른 카드 축소 곡선입니다. 0은 최대 줌 인, 1은 최대 줌 아웃입니다.")]
        [SerializeField] private AnimationCurve cardSizeByZoom =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("같은 건물에 카드가 여러 장 있을 때 카드 중심 사이 간격 비율입니다.")]
        [SerializeField, Range(0.35f, 1.5f)] private float cardSlotSpacingMultiplier = 0.88f;

        [Tooltip("호버 시 기본 아이콘 크기에 곱하는 배율입니다.")]
        [SerializeField, Min(1f)] private float cardHoverScale = 1.85f;

        [Tooltip("줌 아웃 상태에서도 호버 카드가 최소한 확보할 크기입니다.")]
        [SerializeField, Min(32f)] private float minimumHoveredCardSize = 112f;

        [Tooltip("호버 시 카드가 위로 떠오르는 화면 픽셀 값입니다.")]
        [SerializeField, Min(0f)] private float cardHoverLift = 18f;

        [Tooltip("커서 위치에 따라 적용되는 최대 X/Y 3D 기울기입니다.")]
        [SerializeField, Range(0f, 30f)] private float cardMaximumTilt = 13f;

        [Tooltip("카드 크기와 회전이 목표값으로 따라가는 속도입니다.")]
        [SerializeField, Min(1f)] private float cardHoverTweenSpeed = 16f;

        [Header("Discovered Card Badges")]
        [Tooltip("탐색으로 공개된 카드를 기본 건물 카드 위에 표시할 때의 크기 비율입니다.")]
        [SerializeField, Range(0.18f, 0.65f)] private float discoveredCardBadgeScale = 0.34f;
        [SerializeField, Min(12f)] private float minimumDiscoveredBadgeSize = 20f;
        [SerializeField, Min(16f)] private float maximumDiscoveredBadgeSize = 44f;
        [Tooltip("작은 카드 배지끼리 겹쳐지는 간격입니다.")]
        [SerializeField, Range(0.35f, 1.25f)] private float discoveredBadgeSpacingMultiplier = 0.72f;
        [Tooltip("작은 카드 배지의 세로 위치입니다. 0이면 기본 카드 중앙, 0.5면 상단입니다.")]
        [SerializeField, Range(0.15f, 0.75f)] private float discoveredBadgeVerticalRatio = 0.46f;
        [SerializeField, Range(1f, 1.8f)] private float discoveredBadgeHoverScale = 1.28f;

        [Header("Board Card Sprites - Optional")]
        [SerializeField] private Sprite faceDownCardSprite;
        [SerializeField] private Sprite revealedCardSprite;
        [SerializeField] private Sprite resolvedCardSprite;

        [Header("District Center Markers")]
        [SerializeField] private bool showDistrictCenterMarkers = true;
        [Tooltip("최대 줌 인 상태의 지구 아이콘 크기입니다.")]
        [SerializeField, Range(24f, 80f)] private float nearDistrictMarkerSize = 48f;
        [Tooltip("최대 줌 아웃 상태의 지구 아이콘 크기입니다.")]
        [SerializeField, Range(18f, 64f)] private float farDistrictMarkerSize = 30f;
        [Tooltip("세 건물 중심 평균 위치에서 위로 올리는 월드 높이입니다.")]
        [SerializeField, Range(0f, 3f)] private float districtMarkerWorldHeight = 0.18f;
        [SerializeField] private Color districtMarkerBackgroundColor =
            new Color(0.10f, 0.12f, 0.15f, 0.92f);
        [SerializeField] private Color districtMarkerForegroundColor = Color.white;

        [Header("Log")]
        [SerializeField, Min(1)] private int maximumLogLines = 8;

        [Header("Optional Existing UI")]
        [SerializeField] private RectTransform playerLeftRoot;
        [SerializeField] private RectTransform playerRightRoot;
        [SerializeField] private RectTransform moveMarkerRoot;
        [SerializeField] private RectTransform cardSlotUIRoot;
        [SerializeField] private RectTransform districtMarkerUIRoot;
        [SerializeField] private RectTransform playerTokenUIRoot;
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text currentPlayerText;
        [SerializeField] private TMP_Text apText;
        [SerializeField] private TMP_Text actionStateText;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button searchButton;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private RectTransform playerDetailPanel;
        [SerializeField] private TMP_Text playerDetailText;
        [SerializeField] private RectTransform moveTooltipPanel;
        [SerializeField] private TMP_Text moveTooltipText;
        [SerializeField] private RectTransform cardPopup;
        [SerializeField] private TMP_Text cardPopupTitle;
        [SerializeField] private TMP_Text cardPopupBody;
        [SerializeField] private Button cardResolveButton;
        [SerializeField] private Button cardCloseButton;
        [SerializeField] private TMP_Text logText;

        [Header("Runtime Style")]
        [SerializeField] private Color panelColor = new Color(0.055f, 0.065f, 0.08f, 0.92f);
        [SerializeField] private Color buttonColor = new Color(0.16f, 0.19f, 0.23f, 0.96f);
        [SerializeField] private Color selectedColor = new Color(0.18f, 0.42f, 0.62f, 1f);
        [SerializeField] private Color activeColor = new Color(0.72f, 0.18f, 0.12f, 1f);
        [SerializeField] private Color completedColor = new Color(0.18f, 0.18f, 0.18f, 0.76f);
        [SerializeField] private Color disabledColor = new Color(0.18f, 0.18f, 0.18f, 0.72f);
        [SerializeField] private Color buildingInfoCardColor = new Color(0.12f, 0.32f, 0.46f, 0.98f);
        [SerializeField] private Color faceDownCardColor = new Color(0.13f, 0.16f, 0.22f, 0.96f);
        [SerializeField] private Color revealedCardColor = new Color(0.42f, 0.26f, 0.10f, 0.98f);
        [SerializeField] private Color resolvedCardColor = new Color(0.18f, 0.28f, 0.20f, 0.86f);

        private GridManager gridManager;
        private PlayerManager playerManager;
        private TurnManager turnManager;
        private SearchManager searchManager;
        private CardManager cardManager;
        private BoardCameraController cameraController;
        private GameLogManager gameLogManager;

        private readonly Dictionary<string, PlayerEntry> playerEntries =
            new Dictionary<string, PlayerEntry>();

        private readonly Dictionary<string, PlayerTokenEntry> playerTokenEntries =
            new Dictionary<string, PlayerTokenEntry>();

        private readonly Dictionary<Renderer, bool> hiddenPlayerRendererStates =
            new Dictionary<Renderer, bool>();

        private readonly Dictionary<Collider, bool> hiddenPlayerColliderStates =
            new Dictionary<Collider, bool>();

        private readonly Dictionary<Collider2D, bool> hiddenPlayerCollider2DStates =
            new Dictionary<Collider2D, bool>();

        private readonly Dictionary<string, CardSlotEntry> cardEntries =
            new Dictionary<string, CardSlotEntry>();

        private readonly Dictionary<string, DistrictMarkerEntry>
            districtMarkerEntries =
                new Dictionary<string, DistrictMarkerEntry>();

        private readonly Dictionary<string, CardSlotEntry> buildingCardEntriesByNodeId =
            new Dictionary<string, CardSlotEntry>();

        private readonly List<CardSlotEntry> sortableCardEntries =
            new List<CardSlotEntry>();

        private readonly List<MoveEntry> moveEntries =
            new List<MoveEntry>();

        private readonly Dictionary<BoardSpacePrefab, float> reachableMoveCosts =
            new Dictionary<BoardSpacePrefab, float>();

        private readonly Queue<string> logLines =
            new Queue<string>();

        private readonly List<BoardSpacePrefab> hoveredPath =
            new List<BoardSpacePrefab>();

        private bool initialized;
        private bool isPathMoving;
        private string selectedPlayerId;
        private BoardSpacePrefab moveStartSpace;
        private MoveEntry hoveredMoveEntry;
        private BoardCardSlotData popupSlot;
        private Coroutine pathMoveRoutine;

        private void Awake()
        {
            ResolveWorldCamera();
            ResolveProceduralCityBoardManager();

            if (autoBuildUI)
            {
                BuildRuntimeUIIfNeeded();
            }

            ApplyConfiguredFonts();
        }

        private void OnValidate()
        {
            playerSelectorButtonSize.x = Mathf.Max(180f, playerSelectorButtonSize.x);
            playerSelectorButtonSize.y = Mathf.Max(44f, playerSelectorButtonSize.y);
            playerSelectorSpacing = Mathf.Max(0f, playerSelectorSpacing);
            playerDetailPanelSize.x = Mathf.Max(260f, playerDetailPanelSize.x);
            playerDetailPanelSize.y = Mathf.Max(260f, playerDetailPanelSize.y);
            playerTokenDiameter = Mathf.Clamp(playerTokenDiameter, 32f, 96f);
            playerTokenNearZoomScale = Mathf.Clamp(playerTokenNearZoomScale, 0.80f, 1.80f);
            playerTokenFarZoomScale = Mathf.Clamp(playerTokenFarZoomScale, 0.65f, 1.25f);
            selectedPlayerTokenScale = Mathf.Clamp(selectedPlayerTokenScale, 1f, 1.5f);
            playerTokenWorldHeight = Mathf.Max(0f, playerTokenWorldHeight);
            sameSpaceTokenSpread = Mathf.Max(0f, sameSpaceTokenSpread);
            playerTokenScreenPadding = Mathf.Max(0f, playerTokenScreenPadding);
            pathStepDelay = Mathf.Max(0.05f, pathStepDelay);

            nearCardIconSize = Mathf.Max(16f, nearCardIconSize);
            farCardIconSize = Mathf.Clamp(farCardIconSize, 12f, nearCardIconSize);
            cardHoverScale = Mathf.Max(1f, cardHoverScale);
            minimumHoveredCardSize = Mathf.Max(32f, minimumHoveredCardSize);
            cardHoverLift = Mathf.Max(0f, cardHoverLift);
            cardHoverTweenSpeed = Mathf.Max(1f, cardHoverTweenSpeed);
            discoveredCardBadgeScale = Mathf.Clamp(discoveredCardBadgeScale, 0.18f, 0.65f);
            minimumDiscoveredBadgeSize = Mathf.Max(12f, minimumDiscoveredBadgeSize);
            maximumDiscoveredBadgeSize = Mathf.Max(
                minimumDiscoveredBadgeSize,
                maximumDiscoveredBadgeSize);
            discoveredBadgeSpacingMultiplier = Mathf.Clamp(
                discoveredBadgeSpacingMultiplier,
                0.35f,
                1.25f);
            discoveredBadgeVerticalRatio = Mathf.Clamp(
                discoveredBadgeVerticalRatio,
                0.15f,
                0.75f);
            discoveredBadgeHoverScale = Mathf.Clamp(
                discoveredBadgeHoverScale,
                1f,
                1.8f);
            nearDistrictMarkerSize = Mathf.Clamp(
                nearDistrictMarkerSize,
                24f,
                80f);
            farDistrictMarkerSize = Mathf.Clamp(
                farDistrictMarkerSize,
                18f,
                nearDistrictMarkerSize);
            districtMarkerWorldHeight = Mathf.Max(
                0f,
                districtMarkerWorldHeight);

            if (Application.isPlaying)
            {
                ApplyConfiguredFonts();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            RestoreWorldPlayerPresentation();
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            UpdateMoveMarkerPositions();
            UpdateDistrictMarkerPositions();
            UpdateCardSlotPositions();
            UpdatePlayerTokenPositions();
        }

        public void Initialize(
            GridManager runtimeGridManager,
            PlayerManager runtimePlayerManager,
            TurnManager runtimeTurnManager,
            SearchManager runtimeSearchManager,
            CardManager runtimeCardManager,
            BoardCameraController runtimeCameraController,
            GameLogManager runtimeGameLogManager)
        {
            Unsubscribe();

            gridManager = runtimeGridManager;
            playerManager = runtimePlayerManager;
            turnManager = runtimeTurnManager;
            searchManager = runtimeSearchManager;
            cardManager = runtimeCardManager;
            cameraController = runtimeCameraController;
            gameLogManager = runtimeGameLogManager;

            ResolveWorldCamera();
            ResolveProceduralCityBoardManager();

            if (autoBuildUI)
            {
                BuildRuntimeUIIfNeeded();
            }

            ApplyConfiguredFonts();
            BindButtonListeners();
            Subscribe();
            RebuildPlayerEntries();
            RebuildCardEntries();

            selectedPlayerId = null;
            initialized = true;
            HideMoveTooltip();
            RefreshAll();
        }

        public void BeginMoveSelection()
        {
            if (turnManager == null ||
                playerManager == null ||
                gridManager == null ||
                isPathMoving)
            {
                return;
            }

            PlayerData candidate;

            if (!TryGetActionCandidate(out candidate))
            {
                AddLocalLog("먼저 보드의 말이나 플레이어 탭을 선택하십시오.");
                return;
            }

            BoardSpacePrefab previewStart;
            float previewBudget =
                turnManager.CurrentPlayer == candidate
                    ? candidate.CurrentAP
                    : candidate.Speed;

            if (!TryGetPlayerSpace(candidate, out previewStart))
            {
                AddLocalLog("선택한 플레이어의 현재 위치를 찾을 수 없습니다.");
                return;
            }

            Dictionary<BoardSpacePrefab, float> previewReachable =
                CalculateReachableSpaces(previewStart, previewBudget);

            if (previewReachable.Count == 0)
            {
                AddLocalLog("현재 AP로 이동할 수 있는 공간이 없습니다.");
                return;
            }

            if (!TryClaimCandidateForAction(out candidate))
            {
                return;
            }

            if (!turnManager.TryBeginAction(TurnActionState.SelectingMove))
            {
                return;
            }

            ClearMoveEntries();

            if (!TryGetPlayerSpace(candidate, out moveStartSpace))
            {
                turnManager.CancelAction();
                return;
            }

            Dictionary<BoardSpacePrefab, float> reachable =
                CalculateReachableSpaces(
                    moveStartSpace,
                    candidate.CurrentAP);

            List<MoveCostRecord> ordered =
                new List<MoveCostRecord>();

            foreach (KeyValuePair<BoardSpacePrefab, float> pair in reachable)
            {
                if (pair.Key != null && pair.Key != moveStartSpace)
                {
                    ordered.Add(new MoveCostRecord(pair.Key, pair.Value));
                }
            }

            ordered.Sort(
                delegate(MoveCostRecord left, MoveCostRecord right)
                {
                    return left.Cost.CompareTo(right.Cost);
                });

            for (int index = 0; index < ordered.Count; index++)
            {
                reachableMoveCosts[ordered[index].Space] = ordered[index].Cost;
                CreateMoveEntry(
                    ordered[index].Space,
                    ordered[index].Cost);
            }

            if (moveEntries.Count == 0)
            {
                AddLocalLog("현재 AP로 이동할 수 있는 공간이 없습니다.");
                turnManager.CancelAction();
            }
            else
            {
                SetMovementInspectionVisible(true);

                AddLocalLog(
                    candidate.DisplayName +
                    " 이동 범위 표시 / 남은 AP " +
                    candidate.CurrentAP.ToString("0.##"));
            }

            RefreshAll();
        }

        public void CancelCurrentSelection()
        {
            if (isPathMoving)
            {
                return;
            }

            ClearMoveEntries();
            turnManager?.CancelAction();
            RefreshAll();
        }

        public void SearchCurrentBuilding()
        {
            if (turnManager == null ||
                searchManager == null ||
                isPathMoving)
            {
                return;
            }

            PlayerData candidate;

            if (!TryGetActionCandidate(out candidate))
            {
                AddLocalLog("먼저 탐색할 캐릭터를 선택하십시오.");
                return;
            }

            float previewAP =
                turnManager.CurrentPlayer == candidate
                    ? candidate.CurrentAP
                    : candidate.Speed;

            float requiredAP;
            string failureReason;

            if (!searchManager.CanPlayerSearch(
                    candidate,
                    previewAP,
                    out requiredAP,
                    out failureReason))
            {
                AddLocalLog(failureReason);
                return;
            }

            if (!TryClaimCandidateForAction(out candidate))
            {
                return;
            }

            if (!turnManager.TryBeginAction(TurnActionState.ResolvingCard))
            {
                return;
            }

            BoardCardSlotData result;
            bool success = searchManager.TrySearchCurrentPlayer(out result);

            // 공개 직후 카드 해결 버튼을 사용할 수 있도록 AP가 0이어도 즉시 턴을 종료하지 않습니다.
            turnManager.CompleteAction(!success);

            if (success && result != null)
            {
                ShowCardPopup(result);
            }

            RefreshAll();
        }

        public void EndCurrentTurn()
        {
            if (isPathMoving)
            {
                return;
            }

            ClearMoveEntries();
            turnManager?.TryEndCurrentTurn();
            RefreshAll();
        }

        private void Subscribe()
        {
            if (turnManager != null)
            {
                turnManager.OnCurrentPlayerChanged += HandleCurrentPlayerChanged;
                turnManager.OnAPChanged += HandleAPChanged;
                turnManager.OnActionStateChanged += HandleActionStateChanged;
                turnManager.OnRoundStarted += HandleRoundStarted;
                turnManager.OnRoundCompleted += HandleRoundCompleted;
                turnManager.OnTurnEnded += HandleTurnEnded;
                turnManager.OnGameTurnsCompleted += HandleGameTurnsCompleted;
            }

            if (playerManager != null)
            {
                playerManager.OnPlayersRebuilt += RebuildPlayerEntries;
                playerManager.OnPlayerMoved += HandlePlayerMoved;
                playerManager.OnPlayerClicked += HandlePlayerClicked;
            }

            if (cardManager != null)
            {
                cardManager.OnBoardSlotsReset += RebuildCardEntries;
                cardManager.OnBoardSlotChanged += HandleCardSlotChanged;
            }

            if (searchManager != null)
            {
                searchManager.OnSearchResolved += HandleSearchResolved;
                searchManager.OnSearchFailed += HandleSearchFailed;
            }

            if (gameLogManager != null)
            {
                gameLogManager.OnLogAdded += HandleLogAdded;
            }
        }

        private void Unsubscribe()
        {
            if (turnManager != null)
            {
                turnManager.OnCurrentPlayerChanged -= HandleCurrentPlayerChanged;
                turnManager.OnAPChanged -= HandleAPChanged;
                turnManager.OnActionStateChanged -= HandleActionStateChanged;
                turnManager.OnRoundStarted -= HandleRoundStarted;
                turnManager.OnRoundCompleted -= HandleRoundCompleted;
                turnManager.OnTurnEnded -= HandleTurnEnded;
                turnManager.OnGameTurnsCompleted -= HandleGameTurnsCompleted;
            }

            if (playerManager != null)
            {
                playerManager.OnPlayersRebuilt -= RebuildPlayerEntries;
                playerManager.OnPlayerMoved -= HandlePlayerMoved;
                playerManager.OnPlayerClicked -= HandlePlayerClicked;
            }

            if (cardManager != null)
            {
                cardManager.OnBoardSlotsReset -= RebuildCardEntries;
                cardManager.OnBoardSlotChanged -= HandleCardSlotChanged;
            }

            if (searchManager != null)
            {
                searchManager.OnSearchResolved -= HandleSearchResolved;
                searchManager.OnSearchFailed -= HandleSearchFailed;
            }

            if (gameLogManager != null)
            {
                gameLogManager.OnLogAdded -= HandleLogAdded;
            }
        }

        private void BindButtonListeners()
        {
            if (moveButton != null)
            {
                moveButton.onClick.RemoveListener(BeginMoveSelection);
                moveButton.onClick.AddListener(BeginMoveSelection);
            }

            if (searchButton != null)
            {
                searchButton.onClick.RemoveListener(SearchCurrentBuilding);
                searchButton.onClick.AddListener(SearchCurrentBuilding);
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(EndCurrentTurn);
                endTurnButton.onClick.AddListener(EndCurrentTurn);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(CancelCurrentSelection);
                cancelButton.onClick.AddListener(CancelCurrentSelection);
            }

            if (cardResolveButton != null)
            {
                cardResolveButton.onClick.RemoveListener(ResolvePopupCard);
                cardResolveButton.onClick.AddListener(ResolvePopupCard);
            }

            if (cardCloseButton != null)
            {
                cardCloseButton.onClick.RemoveListener(HideCardPopup);
                cardCloseButton.onClick.AddListener(HideCardPopup);
            }
        }

        private void RebuildPlayerEntries()
        {
            foreach (PlayerEntry entry in playerEntries.Values)
            {
                if (entry != null && entry.Root != null)
                {
                    Destroy(entry.Root.gameObject);
                }
            }

            playerEntries.Clear();

            if (playerManager == null)
            {
                return;
            }

            for (int index = 0; index < playerManager.Players.Count; index++)
            {
                PlayerData player = playerManager.Players[index];

                if (player == null || playerRightRoot == null)
                {
                    continue;
                }

                Button button = CreateButton(
                    "PlayerSelector_" + player.PlayerId,
                    playerRightRoot,
                    player.DisplayName,
                    playerSelectorButtonSize,
                    UIFontRole.Regular);

                RectTransform root = button.GetComponent<RectTransform>();
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

                if (label != null)
                {
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    RectTransform labelRect = label.rectTransform;
                    labelRect.offsetMin = new Vector2(58f, 6f);
                    labelRect.offsetMax = new Vector2(-10f, -6f);
                }

                TextMeshProUGUI tabLabel = CreateText(
                    "PlayerNumber",
                    root,
                    "P" + (index + 1),
                    19,
                    TextAnchor.MiddleCenter,
                    UIFontRole.Title);

                RectTransform tabRect = tabLabel.rectTransform;
                tabRect.anchorMin = new Vector2(0f, 0f);
                tabRect.anchorMax = new Vector2(0f, 1f);
                tabRect.pivot = new Vector2(0f, 0.5f);
                tabRect.anchoredPosition = Vector2.zero;
                tabRect.sizeDelta = new Vector2(52f, 0f);

                string capturedId = player.PlayerId;
                button.onClick.AddListener(
                    delegate { SelectPlayerAndFocus(capturedId); });

                playerEntries[player.PlayerId] =
                    new PlayerEntry(
                        root,
                        button,
                        label,
                        tabLabel,
                        player,
                        true);
            }

            RebuildPlayerTokenEntries();
            RefreshPlayerEntries();
        }

        private void RefreshPlayerEntries()
        {
            PlayerData current =
                turnManager != null
                    ? turnManager.CurrentPlayer
                    : null;

            PlayerData selected = GetSelectedPlayer();

            foreach (PlayerEntry entry in playerEntries.Values)
            {
                if (entry == null || entry.Player == null)
                {
                    continue;
                }

                PlayerData player = entry.Player;
                bool completed =
                    turnManager != null &&
                    turnManager.HasPlayerCompletedTurn(player.PlayerId);

                if (entry.Label != null)
                {
                    bool isDefaultAgent =
                        player.PlayerPresetId == "DEFAULT_TEST_AGENT";

                    string apState;

                    if (current == player)
                    {
                        apState = "AP " + player.CurrentAP.ToString("0.##");
                    }
                    else if (completed)
                    {
                        apState = "이번 라운드 완료";
                    }
                    else
                    {
                        apState = "예상 AP " + player.Speed;
                    }

                    entry.Label.text =
                        player.DisplayName +
                        (isDefaultAgent ? " [기본]" : string.Empty) +
                        "\n" +
                        "HP " + player.CurrentHP + "/" + player.MaxHP +
                        "  SAN " + player.CurrentSAN + "/" + player.MaxSAN +
                        "  " + apState;
                }

                if (entry.TabLabel != null)
                {
                    entry.TabLabel.text =
                        "P" + (GetPlayerIndex(player.PlayerId) + 1) +
                        (completed ? "\n✓" : string.Empty);
                }

                Color color = buttonColor;

                if (current == player)
                {
                    color = activeColor;
                }
                else if (selected == player)
                {
                    color = selectedColor;
                }
                else if (completed)
                {
                    color = completedColor;
                }

                SetButtonColor(entry.Button, color);
                entry.Button.interactable =
                    player.CanTakeTurn &&
                    (current == null
                        ? !completed
                        : current == player);
            }
        }

        private void HandlePlayerClicked(
            PlayerData player,
            PlayerBoardPrefab view)
        {
            if (player == null)
            {
                return;
            }

            SelectPlayerAndFocus(player.PlayerId, view);
        }

        private void SelectPlayerAndFocus(string playerId)
        {
            PlayerBoardPrefab view =
                playerManager != null
                    ? playerManager.GetPlayerView(playerId)
                    : null;

            SelectPlayerAndFocus(playerId, view);
        }

        private void SelectPlayerAndFocus(
            string playerId,
            PlayerBoardPrefab view)
        {
            PlayerData player =
                playerManager != null
                    ? playerManager.FindPlayer(playerId)
                    : null;

            if (player == null)
            {
                return;
            }

            PlayerData current =
                turnManager != null
                    ? turnManager.CurrentPlayer
                    : null;

            if (current == null)
            {
                if (turnManager != null &&
                    turnManager.CanSelectPlayerForTurn(player.PlayerId))
                {
                    selectedPlayerId = player.PlayerId;
                }
                else if (turnManager != null &&
                         turnManager.HasPlayerCompletedTurn(player.PlayerId))
                {
                    AddLocalLog(player.DisplayName + "은 이번 라운드 행동을 마쳤습니다.");
                }
            }
            else if (current != player)
            {
                AddLocalLog(
                    current.DisplayName +
                    "의 턴이 이미 확정되어 다른 말로 교체할 수 없습니다.");
            }

            if (view != null &&
                cameraController != null &&
                autoFocusSelectedPlayer)
            {
                cameraController.FocusTransform(view.transform, true);
            }

            ShowPlayerDetail(player);
            HighlightDisplayPlayer();
            RefreshAll();
        }

        private void ShowPlayerDetail(PlayerData player)
        {
            if (playerDetailPanel == null ||
                playerDetailText == null ||
                player == null)
            {
                return;
            }

            playerDetailPanel.gameObject.SetActive(true);
            bool isDefaultAgent =
                player.PlayerPresetId == "DEFAULT_TEST_AGENT";

            bool completed =
                turnManager != null &&
                turnManager.HasPlayerCompletedTurn(player.PlayerId);

            playerDetailText.text =
                player.DisplayName + "\n" +
                "캐릭터: " +
                (isDefaultAgent
                    ? "테스트 요원 (기본)"
                    : player.PlayerPresetId) +
                "\n상태: " +
                (turnManager != null && turnManager.CurrentPlayer == player
                    ? "턴 진행 중"
                    : completed
                        ? "이번 라운드 완료"
                        : "선택 가능") +
                "\n\n" +
                "HP  " + player.CurrentHP + " / " + player.MaxHP + "\n" +
                "SAN " + player.CurrentSAN + " / " + player.MaxSAN + "\n" +
                "AP  " +
                (turnManager != null && turnManager.CurrentPlayer == player
                    ? player.CurrentAP.ToString("0.##")
                    : player.Speed.ToString()) +
                "\n\n" +
                "SPD " + player.Speed + "  STR " + player.Strength + "\n" +
                "INT " + player.Intelligence + "  RES " + player.Resistance + "\n" +
                "CHA " + player.Charisma + "  BODY " + player.Body + "\n\n" +
                "위치: " + player.CurrentNodeId;
        }

        private bool TryGetActionCandidate(out PlayerData player)
        {
            player = null;

            if (turnManager == null || playerManager == null)
            {
                return false;
            }

            if (turnManager.CurrentPlayer != null)
            {
                player = turnManager.CurrentPlayer;
                return true;
            }

            PlayerData selected = GetSelectedPlayer();

            if (selected == null ||
                !turnManager.CanSelectPlayerForTurn(selected.PlayerId))
            {
                return false;
            }

            player = selected;
            return true;
        }

        private bool TryClaimCandidateForAction(out PlayerData player)
        {
            player = null;

            if (!TryGetActionCandidate(out player))
            {
                AddLocalLog("행동할 캐릭터를 먼저 선택하십시오.");
                return false;
            }

            if (turnManager.CurrentPlayer != null)
            {
                return turnManager.CurrentPlayer == player;
            }

            if (!turnManager.TryClaimPlayerTurn(player.PlayerId))
            {
                AddLocalLog("선택한 캐릭터의 턴을 확정할 수 없습니다.");
                return false;
            }

            player = turnManager.CurrentPlayer;
            selectedPlayerId = player != null ? player.PlayerId : null;
            return player != null;
        }

        private PlayerData GetSelectedPlayer()
        {
            if (playerManager == null || string.IsNullOrWhiteSpace(selectedPlayerId))
            {
                return null;
            }

            return playerManager.FindPlayer(selectedPlayerId);
        }

        private PlayerData GetDisplayPlayer()
        {
            if (turnManager != null && turnManager.CurrentPlayer != null)
            {
                return turnManager.CurrentPlayer;
            }

            return GetSelectedPlayer();
        }

        private bool TryGetPlayerSpace(
            PlayerData player,
            out BoardSpacePrefab space)
        {
            space = null;

            return player != null &&
                   gridManager != null &&
                   !string.IsNullOrWhiteSpace(player.CurrentNodeId) &&
                   gridManager.TryGetSpace(player.CurrentNodeId, out space) &&
                   space != null;
        }

        private int GetPlayerIndex(string playerId)
        {
            if (playerManager == null)
            {
                return 0;
            }

            for (int index = 0; index < playerManager.Players.Count; index++)
            {
                PlayerData player = playerManager.Players[index];

                if (player != null && player.PlayerId == playerId)
                {
                    return index;
                }
            }

            return 0;
        }

        private void RebuildCardEntries()
        {
            foreach (CardSlotEntry entry in cardEntries.Values)
            {
                if (entry != null && entry.Root != null)
                {
                    Destroy(entry.Root.gameObject);
                }
            }

            cardEntries.Clear();
            buildingCardEntriesByNodeId.Clear();
            sortableCardEntries.Clear();

            if (cardManager == null || cardSlotUIRoot == null)
            {
                return;
            }

            float initialSize = Mathf.Max(16f, nearCardIconSize);

            for (int index = 0; index < cardManager.AllBoardSlots.Count; index++)
            {
                BoardCardSlotData slot = cardManager.AllBoardSlots[index];

                // 보드 좌표를 직접 따라다니는 카드는 건물 정보 카드 한 장뿐입니다.
                // 탐색 카드는 이 카드의 자식 배지로 생성됩니다.
                if (slot == null || !slot.IsBuildingInformation)
                {
                    continue;
                }

                Button button = CreateButton(
                    "BuildingCard_" + slot.SlotId,
                    cardSlotUIRoot,
                    slot.DisplayName,
                    new Vector2(initialSize, initialSize),
                    UIFontRole.Scene);

                RectTransform root = button.GetComponent<RectTransform>();
                root.pivot = new Vector2(0.5f, 0.5f);
                root.localRotation = Quaternion.identity;

                Image image = button.targetGraphic as Image;

                if (image != null)
                {
                    image.preserveAspect = true;
                }

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                Image iconImage = CreateBuildingCardIcon(root);
                string capturedSlotId = slot.SlotId;

                button.onClick.AddListener(
                    delegate { HandleCardSlotClicked(capturedSlotId); });

                GameObject badgeRootObject = new GameObject(
                    "DiscoveredCardBadges",
                    typeof(RectTransform));

                RectTransform badgeRoot =
                    badgeRootObject.GetComponent<RectTransform>();

                badgeRoot.SetParent(root, false);
                badgeRoot.anchorMin = new Vector2(0.5f, 0.5f);
                badgeRoot.anchorMax = new Vector2(0.5f, 0.5f);
                badgeRoot.pivot = new Vector2(0.5f, 0.5f);
                badgeRoot.anchoredPosition = Vector2.zero;
                badgeRoot.sizeDelta = Vector2.zero;

                CardSlotEntry entry =
                    new CardSlotEntry(
                        root,
                        button,
                        image,
                        iconImage,
                        label,
                        badgeRoot,
                        slot,
                        initialSize);

                BoardCardHoverRelay relay =
                    button.gameObject.AddComponent<BoardCardHoverRelay>();

                relay.Configure(
                    delegate(PointerEventData eventData)
                    {
                        HandleCardPointerEnter(entry, eventData);
                    },
                    delegate(PointerEventData eventData)
                    {
                        HandleCardPointerExit(entry, eventData);
                    },
                    delegate(PointerEventData eventData)
                    {
                        HandleCardPointerMove(entry, eventData);
                    });

                cardEntries[slot.SlotId] = entry;
                buildingCardEntriesByNodeId[slot.NodeId] = entry;
                sortableCardEntries.Add(entry);
            }

            RefreshCardEntries();
            RebuildDistrictMarkerEntries();
        }

        private void RefreshCardEntries()
        {
            foreach (CardSlotEntry entry in cardEntries.Values)
            {
                RefreshCardEntry(entry);
            }
        }

        private void RefreshCardEntry(CardSlotEntry entry)
        {
            if (entry == null || entry.Slot == null)
            {
                return;
            }

            entry.Root.gameObject.SetActive(true);
            RefreshDiscoveredCardBadges(entry);

            Sprite stateSprite = GetCardStateSprite(entry.Slot);

            if (entry.IconImage != null)
            {
                entry.IconImage.sprite = stateSprite;
                entry.IconImage.enabled = stateSprite != null;
                entry.IconImage.preserveAspect = true;
            }

            Color cardColor =
                ResolveBuildingCardColor(entry.Slot.NodeId);
            Color foregroundColor =
                ResolveReadableForegroundColor(cardColor);

            if (entry.IconImage != null)
            {
                entry.IconImage.color = foregroundColor;
            }

            if (entry.Label != null)
            {
                entry.Label.text = GetCardCompactLabel(entry);
                entry.Label.fontSize = entry.IsHovered ? 15f : 11f;
                entry.Label.color = foregroundColor;
                entry.Label.enabled = true;
                UpdateBuildingCardContentLayout(
                    entry,
                    stateSprite != null);
            }

            SetButtonColor(
                entry.Button,
                cardColor);
        }

        private Image CreateBuildingCardIcon(RectTransform parent)
        {
            GameObject iconObject = new GameObject(
                "BuildingCardIcon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            RectTransform iconRect =
                iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(parent, false);
            iconRect.anchorMin = new Vector2(0.20f, 0.20f);
            iconRect.anchorMax = new Vector2(0.80f, 0.80f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.enabled = false;
            iconRect.SetAsFirstSibling();
            return iconImage;
        }

        private static void UpdateBuildingCardContentLayout(
            CardSlotEntry entry,
            bool hasIcon)
        {
            if (entry == null || entry.Label == null)
            {
                return;
            }

            RectTransform labelRect = entry.Label.rectTransform;

            if (hasIcon)
            {
                if (entry.IconImage != null)
                {
                    RectTransform iconRect =
                        entry.IconImage.rectTransform;
                    iconRect.anchorMin = new Vector2(0.24f, 0.48f);
                    iconRect.anchorMax = new Vector2(0.76f, 0.90f);
                    iconRect.offsetMin = Vector2.zero;
                    iconRect.offsetMax = Vector2.zero;
                }

                labelRect.anchorMin = new Vector2(0.05f, 0.03f);
                labelRect.anchorMax = new Vector2(0.95f, 0.47f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                entry.Label.alignment =
                    TextAlignmentOptions.Center;
            }
            else
            {
                labelRect.anchorMin = new Vector2(0.06f, 0.06f);
                labelRect.anchorMax = new Vector2(0.94f, 0.94f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                entry.Label.alignment =
                    TextAlignmentOptions.Center;
            }
        }

        private Color ResolveBuildingCardColor(string nodeId)
        {
            BoardSpacePrefab space;

            if (gridManager != null &&
                !string.IsNullOrWhiteSpace(nodeId) &&
                gridManager.TryGetSpace(nodeId, out space) &&
                space != null &&
                space.HasRuntimeBuildingColor)
            {
                Color color = space.RuntimeBuildingColor;
                color.a = 0.98f;
                return color;
            }

            return buildingInfoCardColor;
        }

        private static Color ResolveReadableForegroundColor(Color background)
        {
            float luminance =
                background.r * 0.2126f +
                background.g * 0.7152f +
                background.b * 0.0722f;

            return luminance >= 0.58f
                ? new Color(0.055f, 0.065f, 0.08f, 1f)
                : Color.white;
        }

        private void RefreshDiscoveredCardBadges(CardSlotEntry entry)
        {
            if (entry == null ||
                entry.BadgeRoot == null ||
                cardManager == null)
            {
                return;
            }

            IList<BoardCardSlotData> slots =
                cardManager.GetSlotsAtNode(entry.Slot.NodeId);

            HashSet<string> visibleSlotIds = new HashSet<string>();

            for (int index = 0; index < slots.Count; index++)
            {
                BoardCardSlotData slot = slots[index];

                if (slot == null ||
                    !slot.IsSearchResult ||
                    !slot.Revealed ||
                    (slot.Resolved && !showResolvedCardSlots))
                {
                    continue;
                }

                visibleSlotIds.Add(slot.SlotId);

                SearchCardBadgeEntry badge;

                if (!entry.Badges.TryGetValue(slot.SlotId, out badge) ||
                    badge == null ||
                    badge.Root == null)
                {
                    badge = CreateDiscoveredCardBadge(entry, slot);
                    entry.Badges[slot.SlotId] = badge;
                }

                RefreshDiscoveredCardBadge(entry, badge);
            }

            List<string> removeIds = new List<string>();

            foreach (KeyValuePair<string, SearchCardBadgeEntry> pair in entry.Badges)
            {
                if (!visibleSlotIds.Contains(pair.Key))
                {
                    if (pair.Value != null && pair.Value.Root != null)
                    {
                        Destroy(pair.Value.Root.gameObject);
                    }

                    removeIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < removeIds.Count; index++)
            {
                entry.Badges.Remove(removeIds[index]);
            }
        }

        private SearchCardBadgeEntry CreateDiscoveredCardBadge(
            CardSlotEntry owner,
            BoardCardSlotData slot)
        {
            float initialBadgeSize = Mathf.Clamp(
                nearCardIconSize * discoveredCardBadgeScale,
                minimumDiscoveredBadgeSize,
                maximumDiscoveredBadgeSize);

            Button button = CreateButton(
                "Discovered_" + slot.SlotId,
                owner.BadgeRoot,
                "!",
                new Vector2(initialBadgeSize, initialBadgeSize),
                UIFontRole.Scene);

            RectTransform root = button.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            Image image = button.targetGraphic as Image;

            if (image != null)
            {
                image.preserveAspect = true;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            string capturedSlotId = slot.SlotId;

            button.onClick.AddListener(
                delegate { HandleCardSlotClicked(capturedSlotId); });

            SearchCardBadgeEntry badge =
                new SearchCardBadgeEntry(
                    root,
                    button,
                    image,
                    label,
                    slot);

            BoardCardHoverRelay relay =
                button.gameObject.AddComponent<BoardCardHoverRelay>();

            relay.Configure(
                delegate(PointerEventData eventData)
                {
                    HandleSearchBadgePointerEnter(owner, badge, eventData);
                },
                delegate(PointerEventData eventData)
                {
                    HandleSearchBadgePointerExit(owner, badge, eventData);
                },
                delegate(PointerEventData eventData)
                {
                    HandleSearchBadgePointerMove(owner, badge, eventData);
                });

            return badge;
        }

        private void RefreshDiscoveredCardBadge(
            CardSlotEntry owner,
            SearchCardBadgeEntry badge)
        {
            if (owner == null || badge == null || badge.Slot == null)
            {
                return;
            }

            Sprite stateSprite = GetCardStateSprite(badge.Slot);

            if (badge.Image != null)
            {
                badge.Image.sprite = stateSprite;
                badge.Image.preserveAspect = stateSprite != null;
            }

            if (badge.Label != null)
            {
                badge.Label.text = badge.Slot.Resolved ? "✓" : "!";
                badge.Label.fontSize = 15f;
                badge.Label.enabled = stateSprite == null;
            }

            Color stateColor =
                badge.Slot.Resolved
                    ? resolvedCardColor
                    : revealedCardColor;

            SetButtonColor(
                badge.Button,
                stateSprite != null ? Color.white : stateColor);
        }

        private Sprite GetCardStateSprite(BoardCardSlotData slot)
        {
            if (slot == null)
            {
                return null;
            }

            if (slot.IsBuildingInformation)
            {
                return slot.IconSprite;
            }

            if (slot.IconSprite != null)
            {
                return slot.IconSprite;
            }

            if (slot.Resolved && resolvedCardSprite != null)
            {
                return resolvedCardSprite;
            }

            if (slot.Revealed && revealedCardSprite != null)
            {
                return revealedCardSprite;
            }

            if (!slot.Revealed && faceDownCardSprite != null)
            {
                return faceDownCardSprite;
            }

            return null;
        }

        private string GetCardCompactLabel(CardSlotEntry entry)
        {
            if (entry == null || entry.Slot == null)
            {
                return string.Empty;
            }

            if (entry.HoveredBadgeSlot != null)
            {
                return entry.HoveredBadgeSlot.DisplayName;
            }

            if (!entry.IsHovered)
            {
                return entry.Slot.DisplayName;
            }

            int discoveredCount =
                cardManager != null
                    ? cardManager.GetRevealedSearchCardCount(entry.Slot.NodeId)
                    : 0;

            int hiddenCount =
                cardManager != null
                    ? cardManager.GetHiddenSearchCardCount(entry.Slot.NodeId)
                    : 0;

            return entry.Slot.DisplayName + "\n" +
                   "발견 " + discoveredCount +
                   (hiddenCount > 0 ? " / 미확인 " + hiddenCount : string.Empty);
        }

        private void HandleCardPointerEnter(
            CardSlotEntry entry,
            PointerEventData eventData)
        {
            if (entry == null || entry.Root == null)
            {
                return;
            }

            entry.IsHovered = true;
            entry.HoveredBadgeSlot = null;
            UpdateCardPointerNormalized(entry, eventData);

            float kickDirection =
                Mathf.Abs(entry.PointerNormalized.x) > 0.05f
                    ? -Mathf.Sign(entry.PointerNormalized.x)
                    : 1f;

            entry.HoverKickRoll = kickDirection * cardMaximumTilt * 0.7f;
            entry.Root.SetAsLastSibling();
            RefreshCardEntry(entry);
        }

        private void HandleCardPointerExit(
            CardSlotEntry entry,
            PointerEventData eventData)
        {
            if (entry == null)
            {
                return;
            }

            entry.IsHovered = false;
            entry.HoveredBadgeSlot = null;
            entry.PointerNormalized = Vector2.zero;
            entry.HoverKickRoll = 0f;
            RefreshCardEntry(entry);
        }

        private void HandleCardPointerMove(
            CardSlotEntry entry,
            PointerEventData eventData)
        {
            if (entry == null || !entry.IsHovered)
            {
                return;
            }

            UpdateCardPointerNormalized(entry, eventData);
        }

        private void HandleSearchBadgePointerEnter(
            CardSlotEntry owner,
            SearchCardBadgeEntry badge,
            PointerEventData eventData)
        {
            if (owner == null || badge == null)
            {
                return;
            }

            badge.IsHovered = true;
            owner.IsHovered = true;
            owner.HoveredBadgeSlot = badge.Slot;
            UpdateCardPointerNormalized(owner, eventData);
            owner.Root.SetAsLastSibling();
            badge.Root.SetAsLastSibling();
            RefreshCardEntry(owner);
        }

        private void HandleSearchBadgePointerExit(
            CardSlotEntry owner,
            SearchCardBadgeEntry badge,
            PointerEventData eventData)
        {
            if (owner == null || badge == null)
            {
                return;
            }

            badge.IsHovered = false;

            if (owner.HoveredBadgeSlot == badge.Slot)
            {
                owner.HoveredBadgeSlot = null;
            }

            owner.IsHovered = false;
            owner.PointerNormalized = Vector2.zero;
            owner.HoverKickRoll = 0f;
            RefreshCardEntry(owner);
        }

        private void HandleSearchBadgePointerMove(
            CardSlotEntry owner,
            SearchCardBadgeEntry badge,
            PointerEventData eventData)
        {
            if (owner == null || badge == null || !badge.IsHovered)
            {
                return;
            }

            UpdateCardPointerNormalized(owner, eventData);
        }

        private static void UpdateCardPointerNormalized(
            CardSlotEntry entry,
            PointerEventData eventData)
        {
            if (entry == null ||
                entry.Root == null ||
                eventData == null)
            {
                return;
            }

            Vector2 localPoint;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    entry.Root,
                    eventData.position,
                    eventData.pressEventCamera,
                    out localPoint))
            {
                return;
            }

            Rect rect = entry.Root.rect;
            float halfWidth = Mathf.Max(1f, rect.width * 0.5f);
            float halfHeight = Mathf.Max(1f, rect.height * 0.5f);

            entry.PointerNormalized =
                new Vector2(
                    Mathf.Clamp(localPoint.x / halfWidth, -1f, 1f),
                    Mathf.Clamp(localPoint.y / halfHeight, -1f, 1f));
        }

        private void HandleCardSlotClicked(string slotId)
        {
            BoardCardSlotData slot;

            if (cardManager == null ||
                !cardManager.TryGetSlot(slotId, out slot) ||
                slot == null ||
                (!slot.IsBuildingInformation && !slot.Revealed))
            {
                return;
            }

            BoardSpacePrefab space;

            if (gridManager != null &&
                gridManager.TryGetSpace(slot.NodeId, out space) &&
                space != null &&
                cameraController != null)
            {
                Transform focusTarget =
                    space.CardSlotRoot != null
                        ? space.CardSlotRoot
                        : space.transform;

                cameraController.FocusTransform(focusTarget, true);
            }

            ShowCardPopup(slot);
        }

        private void ShowCardPopup(BoardCardSlotData slot)
        {
            if (slot == null || cardPopup == null)
            {
                return;
            }

            popupSlot = slot;
            cardPopup.gameObject.SetActive(true);

            if (cardPopupTitle != null)
            {
                cardPopupTitle.text = slot.DisplayName;
            }

            if (cardPopupBody != null)
            {
                if (slot.IsBuildingInformation)
                {
                    cardPopupBody.text =
                        slot.Description + "\n\n" +
                        BuildBuildingCardSummary(slot.NodeId);
                }
                else
                {
                    bool canInspectDetails =
                        CanInspectCardDetailsAtCurrentLocation(slot);

                    cardPopupBody.text =
                        "위치: " + slot.NodeId + "\n" +
                        "상태: " +
                        (slot.Resolved ? "해결 완료" : "발견됨") +
                        "\n\n" +
                        (canInspectDetails
                            ? slot.Description
                            : slot.Summary +
                              "\n\n상세 내용은 캐릭터가 해당 건물에 있을 때 확인할 수 있습니다.");
                }
            }

            RefreshCardResolveButton();
        }

        private string BuildBuildingCardSummary(string nodeId)
        {
            if (cardManager == null)
            {
                return "발견 카드 정보를 불러올 수 없습니다.";
            }

            IList<BoardCardSlotData> slots =
                cardManager.GetSlotsAtNode(nodeId);

            List<string> discoveredNames = new List<string>();
            int hiddenCount = 0;

            for (int index = 0; index < slots.Count; index++)
            {
                BoardCardSlotData slot = slots[index];

                if (slot == null || !slot.IsSearchResult)
                {
                    continue;
                }

                if (slot.Revealed)
                {
                    discoveredNames.Add("• " + slot.DisplayName);
                }
                else
                {
                    hiddenCount++;
                }
            }

            string discoveredText =
                discoveredNames.Count > 0
                    ? string.Join("\n", discoveredNames.ToArray())
                    : "• 아직 발견된 카드가 없습니다.";

            return "발견된 카드\n" + discoveredText +
                   (hiddenCount > 0
                       ? "\n\n미확인 탐색 카드: " + hiddenCount + "장"
                       : string.Empty);
        }

        private bool CanInspectCardDetailsAtCurrentLocation(
            BoardCardSlotData slot)
        {
            if (slot == null || !slot.IsSearchResult)
            {
                return true;
            }

            PlayerData candidate;

            return TryGetActionCandidate(out candidate) &&
                   candidate != null &&
                   candidate.CurrentNodeId == slot.NodeId;
        }

        private void RefreshCardResolveButton()
        {
            if (cardResolveButton == null || popupSlot == null)
            {
                return;
            }

            PlayerData candidate;
            bool hasCandidate = TryGetActionCandidate(out candidate);

            cardResolveButton.gameObject.SetActive(
                popupSlot.IsSearchResult);

            cardResolveButton.interactable =
                popupSlot.CanResolve &&
                hasCandidate &&
                candidate != null &&
                candidate.CurrentNodeId == popupSlot.NodeId &&
                turnManager != null &&
                (turnManager.ActionState == TurnActionState.AwaitingAction ||
                 turnManager.ActionState == TurnActionState.AwaitingPlayerSelection);
        }

        private void ResolvePopupCard()
        {
            if (popupSlot == null ||
                !popupSlot.CanResolve ||
                cardManager == null ||
                turnManager == null)
            {
                return;
            }

            PlayerData candidate;

            if (!TryGetActionCandidate(out candidate) ||
                candidate.CurrentNodeId != popupSlot.NodeId ||
                !TryClaimCandidateForAction(out candidate) ||
                !turnManager.TryBeginAction(TurnActionState.ResolvingCard))
            {
                return;
            }

            cardManager.ResolveSlot(popupSlot);
            turnManager.CompleteAction(true);
            HideCardPopup();
            RefreshAll();
        }

        private void HideCardPopup()
        {
            popupSlot = null;

            if (cardPopup != null)
            {
                cardPopup.gameObject.SetActive(false);
            }
        }

        private Dictionary<BoardSpacePrefab, float> CalculateReachableSpaces(
            BoardSpacePrefab start,
            float apBudget)
        {
            Dictionary<BoardSpacePrefab, float> costs =
                new Dictionary<BoardSpacePrefab, float>();

            if (start == null || apBudget < 0f)
            {
                return costs;
            }

            List<BoardSpacePrefab> open = new List<BoardSpacePrefab>();
            costs[start] = 0f;
            open.Add(start);

            while (open.Count > 0)
            {
                int bestIndex = 0;
                float bestCost = costs[open[0]];

                for (int index = 1; index < open.Count; index++)
                {
                    float candidateCost = costs[open[index]];

                    if (candidateCost < bestCost)
                    {
                        bestCost = candidateCost;
                        bestIndex = index;
                    }
                }

                BoardSpacePrefab current = open[bestIndex];
                open.RemoveAt(bestIndex);

                for (int neighborIndex = 0;
                     neighborIndex < current.ConnectedSpaces.Count;
                     neighborIndex++)
                {
                    BoardSpacePrefab neighbor =
                        current.ConnectedSpaces[neighborIndex];

                    if (!CanUseSpaceForPath(neighbor, start))
                    {
                        continue;
                    }

                    float nextCost =
                        bestCost + Mathf.Max(0f, neighbor.MovementAPCost);

                    if (nextCost > apBudget + 0.0001f)
                    {
                        continue;
                    }

                    float existing;

                    if (!costs.TryGetValue(neighbor, out existing) ||
                        nextCost + 0.0001f < existing)
                    {
                        costs[neighbor] = nextCost;

                        if (!open.Contains(neighbor))
                        {
                            open.Add(neighbor);
                        }
                    }
                }
            }

            costs.Remove(start);
            return costs;
        }

        private bool TryFindPathAStar(
            BoardSpacePrefab start,
            BoardSpacePrefab goal,
            float apBudget,
            out List<BoardSpacePrefab> path,
            out float totalCost)
        {
            path = new List<BoardSpacePrefab>();
            totalCost = 0f;

            if (start == null || goal == null)
            {
                return false;
            }

            if (start == goal)
            {
                return true;
            }

            List<BoardSpacePrefab> openSet =
                new List<BoardSpacePrefab> { start };

            HashSet<BoardSpacePrefab> closedSet =
                new HashSet<BoardSpacePrefab>();

            Dictionary<BoardSpacePrefab, BoardSpacePrefab> cameFrom =
                new Dictionary<BoardSpacePrefab, BoardSpacePrefab>();

            Dictionary<BoardSpacePrefab, float> gScore =
                new Dictionary<BoardSpacePrefab, float>();

            Dictionary<BoardSpacePrefab, float> fScore =
                new Dictionary<BoardSpacePrefab, float>();

            float minimumCost = GetMinimumPositiveMovementCost();
            gScore[start] = 0f;
            fScore[start] = Heuristic(start, goal, minimumCost);

            while (openSet.Count > 0)
            {
                int bestIndex = 0;
                float bestScore = GetScore(fScore, openSet[0]);

                for (int index = 1; index < openSet.Count; index++)
                {
                    float candidateScore = GetScore(fScore, openSet[index]);

                    if (candidateScore < bestScore)
                    {
                        bestScore = candidateScore;
                        bestIndex = index;
                    }
                }

                BoardSpacePrefab current = openSet[bestIndex];

                if (current == goal)
                {
                    totalCost = GetScore(gScore, current);

                    if (totalCost > apBudget + 0.0001f)
                    {
                        return false;
                    }

                    ReconstructPath(cameFrom, start, goal, path);
                    return path.Count > 0;
                }

                openSet.RemoveAt(bestIndex);
                closedSet.Add(current);

                for (int neighborIndex = 0;
                     neighborIndex < current.ConnectedSpaces.Count;
                     neighborIndex++)
                {
                    BoardSpacePrefab neighbor =
                        current.ConnectedSpaces[neighborIndex];

                    if (!CanUseSpaceForPath(neighbor, start) ||
                        closedSet.Contains(neighbor))
                    {
                        continue;
                    }

                    float tentative =
                        GetScore(gScore, current) +
                        Mathf.Max(0f, neighbor.MovementAPCost);

                    if (tentative > apBudget + 0.0001f)
                    {
                        continue;
                    }

                    float oldScore = GetScore(gScore, neighbor);

                    if (tentative + 0.0001f >= oldScore)
                    {
                        continue;
                    }

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentative;
                    fScore[neighbor] =
                        tentative + Heuristic(neighbor, goal, minimumCost);

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }

            return false;
        }

        private static float GetScore(
            Dictionary<BoardSpacePrefab, float> scores,
            BoardSpacePrefab space)
        {
            float score;
            return scores.TryGetValue(space, out score)
                ? score
                : float.PositiveInfinity;
        }

        private float GetMinimumPositiveMovementCost()
        {
            float minimum = float.PositiveInfinity;

            if (gridManager != null)
            {
                foreach (BoardSpacePrefab space in gridManager.AllSpaces)
                {
                    if (space != null &&
                        space.MovementAPCost > 0.0001f &&
                        space.MovementAPCost < minimum)
                    {
                        minimum = space.MovementAPCost;
                    }
                }
            }

            return float.IsPositiveInfinity(minimum) ? 0f : minimum;
        }

        private static float Heuristic(
            BoardSpacePrefab from,
            BoardSpacePrefab to,
            float minimumStepCost)
        {
            if (from == null || to == null || minimumStepCost <= 0f)
            {
                return 0f;
            }

            // 1x2 건물은 여러 인접 도로와 직접 연결될 수 있습니다.
            // 건물 노드에 단일 좌표만 두고 맨해튼 휴리스틱을 사용하면
            // 실제 한 번의 진입보다 거리를 크게 평가할 수 있으므로,
            // 건물이 경로의 시작 또는 목표일 때는 휴리스틱을 0으로 둡니다.
            if (from.IsBuilding || to.IsBuilding)
            {
                return 0f;
            }

            Vector2Int delta = from.BoardCoordinate - to.BoardCoordinate;
            return (Mathf.Abs(delta.x) + Mathf.Abs(delta.y)) * minimumStepCost;
        }

        private static void ReconstructPath(
            Dictionary<BoardSpacePrefab, BoardSpacePrefab> cameFrom,
            BoardSpacePrefab start,
            BoardSpacePrefab goal,
            List<BoardSpacePrefab> result)
        {
            result.Clear();
            BoardSpacePrefab current = goal;
            result.Add(current);

            while (current != start)
            {
                BoardSpacePrefab previous;

                if (!cameFrom.TryGetValue(current, out previous) ||
                    previous == null)
                {
                    result.Clear();
                    return;
                }

                current = previous;

                if (current != start)
                {
                    result.Add(current);
                }
            }

            result.Reverse();
        }

        private static bool CanUseSpaceForPath(
            BoardSpacePrefab space,
            BoardSpacePrefab start)
        {
            if (space == null || !space.Enterable)
            {
                return false;
            }

            return space == start || space.Players.Count < 6;
        }

        private void CreateMoveEntry(
            BoardSpacePrefab destination,
            float cost)
        {
            if (destination == null || moveMarkerRoot == null)
            {
                return;
            }

            Button button = CreateMoveCircleButton(
                "Move_" + destination.NodeId,
                moveMarkerRoot,
                cost.ToString("0.##"));

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            MoveEntry entry =
                new MoveEntry(
                    button.GetComponent<RectTransform>(),
                    button,
                    label,
                    destination,
                    cost);

            string capturedNodeId = destination.NodeId;
            button.onClick.AddListener(
                delegate { MoveCurrentPlayerTo(capturedNodeId); });

            AddMovePointerEvents(entry);
            moveEntries.Add(entry);
            ApplyMoveEntryVisual(entry, false, false);
        }

        private Button CreateMoveCircleButton(
            string objectName,
            Transform parent,
            string labelText)
        {
            GameObject go = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(MoveRangeCircleGraphic),
                typeof(Button));

            CanvasRenderer canvasRenderer = go.GetComponent<CanvasRenderer>();
            if (canvasRenderer == null)
            {
                canvasRenderer = go.AddComponent<CanvasRenderer>();
            }

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = Vector2.one * moveMarkerDiameter;

            MoveRangeCircleGraphic circle =
                go.GetComponent<MoveRangeCircleGraphic>();
            circle.raycastTarget = true;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = circle;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.disabledColor = disabledColor;
            button.colors = colors;

            TextMeshProUGUI text = CreateText(
                "AP",
                rect,
                labelText,
                16,
                TextAnchor.MiddleCenter,
                UIFontRole.Scene);

            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            Stretch(text.rectTransform, 2f);
            return button;
        }

        private void AddMovePointerEvents(MoveEntry entry)
        {
            if (entry == null || entry.Button == null)
            {
                return;
            }

            EventTrigger trigger =
                entry.Button.GetComponent<EventTrigger>();

            if (trigger == null)
            {
                trigger = entry.Button.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers = new List<EventTrigger.Entry>();

            AddEventTrigger(
                trigger,
                EventTriggerType.PointerEnter,
                delegate(BaseEventData data)
                {
                    HandleMovePointerEnter(entry, data as PointerEventData);
                });

            AddEventTrigger(
                trigger,
                EventTriggerType.PointerExit,
                delegate(BaseEventData data)
                {
                    HandleMovePointerExit(entry);
                });
        }

        private static void AddEventTrigger(
            EventTrigger trigger,
            EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            EventTrigger.Entry eventEntry = new EventTrigger.Entry();
            eventEntry.eventID = type;
            eventEntry.callback = new EventTrigger.TriggerEvent();
            eventEntry.callback.AddListener(callback);
            trigger.triggers.Add(eventEntry);
        }

        private void HandleMovePointerEnter(
            MoveEntry entry,
            PointerEventData eventData)
        {
            if (entry == null ||
                isPathMoving ||
                turnManager == null ||
                turnManager.ActionState != TurnActionState.SelectingMove)
            {
                return;
            }

            PlayerData current = turnManager.CurrentPlayer;
            List<BoardSpacePrefab> path;
            float cost;

            if (current == null ||
                !TryFindPathAStar(
                    moveStartSpace,
                    entry.Space,
                    current.CurrentAP,
                    out path,
                    out cost))
            {
                return;
            }

            hoveredMoveEntry = entry;
            hoveredPath.Clear();
            hoveredPath.AddRange(path);
            ApplyMovePathVisuals();

            if (eventData != null)
            {
                ShowMoveTooltip(entry, eventData.position);
            }
        }

        private void HandleMovePointerExit(MoveEntry entry)
        {
            if (entry != hoveredMoveEntry)
            {
                return;
            }

            hoveredMoveEntry = null;
            hoveredPath.Clear();
            ApplyMovePathVisuals();
            HideMoveTooltip();
        }

        private void ApplyMovePathVisuals()
        {
            for (int index = 0; index < moveEntries.Count; index++)
            {
                MoveEntry entry = moveEntries[index];

                if (entry == null)
                {
                    continue;
                }

                bool hovered = entry == hoveredMoveEntry;
                bool inPath = hoveredPath.Contains(entry.Space);
                ApplyMoveEntryVisual(entry, inPath, hovered);
            }
        }

        private void ApplyMoveEntryVisual(
            MoveEntry entry,
            bool inPath,
            bool hovered)
        {
            if (entry == null)
            {
                return;
            }

            Color backgroundColor = reachableMoveColor;
            float backgroundAlpha = moveMarkerIdleBackgroundAlpha;
            float textAlpha = moveMarkerIdleTextAlpha;

            if (hovered)
            {
                backgroundColor = hoveredMoveColor;
                backgroundAlpha = 1f;
                textAlpha = moveMarkerHoverTextAlpha;
            }
            else if (inPath)
            {
                backgroundColor = pathMoveColor;
                backgroundAlpha = moveMarkerPathBackgroundAlpha;
                textAlpha = moveMarkerPathTextAlpha;
            }

            backgroundColor.a *= backgroundAlpha;
            SetButtonColor(entry.Button, backgroundColor);

            if (entry.Root != null)
            {
                entry.Root.localScale =
                    hovered ? Vector3.one * 1.14f : Vector3.one;
            }

            if (entry.Label != null)
            {
                entry.Label.text = entry.Cost.ToString("0.##");
                Color labelColor = entry.Label.color;
                labelColor.a = textAlpha;
                entry.Label.color = labelColor;
            }
        }

        private void ShowMoveTooltip(
            MoveEntry entry,
            Vector2 screenPosition)
        {
            if (moveTooltipPanel == null ||
                moveTooltipText == null ||
                entry == null ||
                turnManager == null ||
                turnManager.CurrentPlayer == null)
            {
                return;
            }

            PlayerData player = turnManager.CurrentPlayer;
            float remain = Mathf.Max(0f, player.CurrentAP - entry.Cost);

            moveTooltipText.text =
                (entry.Space.IsBuilding ? "건물" : "도로") +
                "  " + entry.Space.NodeId + "\n" +
                "필요 AP  " + entry.Cost.ToString("0.##") + "\n" +
                "이동 후 AP  " + remain.ToString("0.##") + "\n" +
                "클릭하여 이동";

            moveTooltipPanel.gameObject.SetActive(true);
            PositionMoveTooltip(screenPosition);
        }

        private void PositionMoveTooltip(Vector2 screenPosition)
        {
            if (moveTooltipPanel == null || targetCanvas == null)
            {
                return;
            }

            RectTransform canvasRect =
                targetCanvas.GetComponent<RectTransform>();

            Vector2 localPoint;
            Camera canvasCamera =
                targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : targetCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition + moveTooltipOffset,
                    canvasCamera,
                    out localPoint))
            {
                moveTooltipPanel.anchoredPosition = localPoint;
            }
        }

        private void HideMoveTooltip()
        {
            if (moveTooltipPanel != null)
            {
                moveTooltipPanel.gameObject.SetActive(false);
            }
        }

        private void MoveCurrentPlayerTo(string destinationNodeId)
        {
            if (turnManager == null ||
                playerManager == null ||
                isPathMoving ||
                turnManager.ActionState != TurnActionState.SelectingMove)
            {
                return;
            }

            PlayerData current = turnManager.CurrentPlayer;
            BoardSpacePrefab destination;
            List<BoardSpacePrefab> path;
            float totalCost;

            if (current == null ||
                gridManager == null ||
                !gridManager.TryGetSpace(destinationNodeId, out destination) ||
                destination == null ||
                !TryFindPathAStar(
                    moveStartSpace,
                    destination,
                    current.CurrentAP,
                    out path,
                    out totalCost))
            {
                AddLocalLog("선택한 공간까지 유효한 경로가 없습니다.");
                return;
            }

            if (pathMoveRoutine != null)
            {
                StopCoroutine(pathMoveRoutine);
            }

            pathMoveRoutine =
                StartCoroutine(
                    MoveAlongPathRoutine(
                        current.PlayerId,
                        path,
                        totalCost));
        }

        private IEnumerator MoveAlongPathRoutine(
            string playerId,
            List<BoardSpacePrefab> path,
            float expectedTotalCost)
        {
            isPathMoving = true;
            HideMoveTooltip();

            for (int index = 0; index < moveEntries.Count; index++)
            {
                if (moveEntries[index] != null &&
                    moveEntries[index].Button != null)
                {
                    moveEntries[index].Button.interactable = false;
                }
            }

            float spentCost = 0f;
            bool completed = true;

            for (int stepIndex = 0; stepIndex < path.Count; stepIndex++)
            {
                BoardSpacePrefab step = path[stepIndex];
                PlayerData validatedPlayer;
                BoardSpacePrefab currentSpace;
                BoardSpacePrefab destination;

                if (step == null ||
                    !playerManager.CanMovePlayer(
                        playerId,
                        step.NodeId,
                        true,
                        out validatedPlayer,
                        out currentSpace,
                        out destination))
                {
                    completed = false;
                    break;
                }

                float stepCost = Mathf.Max(0f, step.MovementAPCost);

                if (!turnManager.TrySpendCurrentPlayerAP(stepCost))
                {
                    completed = false;
                    break;
                }

                if (!playerManager.TryMovePlayer(
                        playerId,
                        step.NodeId,
                        true))
                {
                    turnManager.RestoreCurrentPlayerAP(stepCost);
                    completed = false;
                    break;
                }

                spentCost += stepCost;

                PlayerBoardPrefab view =
                    playerManager.GetPlayerView(playerId);

                float waitDuration = pathStepDelay;

                if (view != null)
                {
                    waitDuration = Mathf.Max(waitDuration, view.MoveDuration);
                }

                if (waitDuration > 0f)
                {
                    yield return new WaitForSecondsRealtime(waitDuration);
                }
            }

            isPathMoving = false;
            pathMoveRoutine = null;
            ClearMoveEntries();

            if (completed)
            {
                AddLocalLog(
                    "이동 완료 / 사용 AP " +
                    spentCost.ToString("0.##") +
                    " / 예상 " +
                    expectedTotalCost.ToString("0.##"));
            }
            else
            {
                AddLocalLog(
                    "경로 이동이 중단되었습니다. 실제 사용 AP " +
                    spentCost.ToString("0.##"));
            }

            turnManager.CompleteAction(true);
            RefreshAll();
        }

        private void ClearMoveEntries()
        {
            if (pathMoveRoutine != null && !isPathMoving)
            {
                StopCoroutine(pathMoveRoutine);
                pathMoveRoutine = null;
            }

            for (int index = 0; index < moveEntries.Count; index++)
            {
                MoveEntry entry = moveEntries[index];

                if (entry != null && entry.Root != null)
                {
                    Destroy(entry.Root.gameObject);
                }
            }

            moveEntries.Clear();
            reachableMoveCosts.Clear();
            hoveredPath.Clear();
            hoveredMoveEntry = null;
            moveStartSpace = null;
            HideMoveTooltip();
            SetMovementInspectionVisible(false);
        }

        private void UpdateMoveMarkerPositions()
        {
            for (int index = 0; index < moveEntries.Count; index++)
            {
                MoveEntry entry = moveEntries[index];

                if (entry == null ||
                    entry.Root == null ||
                    entry.Space == null)
                {
                    continue;
                }

                SetTrackedUIPosition(
                    entry.Root,
                    entry.Space.transform.position + Vector3.up * 0.35f,
                    moveMarkerRoot,
                    Vector2.zero);
            }
        }

        private void RebuildDistrictMarkerEntries()
        {
            foreach (DistrictMarkerEntry entry in districtMarkerEntries.Values)
            {
                if (entry != null && entry.Root != null)
                {
                    Destroy(entry.Root.gameObject);
                }
            }

            districtMarkerEntries.Clear();

            if (!showDistrictCenterMarkers ||
                gridManager == null ||
                districtMarkerUIRoot == null)
            {
                return;
            }

            Dictionary<string, List<BoardSpacePrefab>> grouped =
                new Dictionary<string, List<BoardSpacePrefab>>();

            for (int index = 0;
                 index < gridManager.BuildingSpaces.Count;
                 index++)
            {
                BoardSpacePrefab building =
                    gridManager.BuildingSpaces[index];

                if (building == null)
                {
                    continue;
                }

                string districtId =
                    string.IsNullOrWhiteSpace(building.DistrictId)
                        ? "CITY"
                        : building.DistrictId;

                List<BoardSpacePrefab> buildings;

                if (!grouped.TryGetValue(districtId, out buildings))
                {
                    buildings = new List<BoardSpacePrefab>();
                    grouped[districtId] = buildings;
                }

                buildings.Add(building);
            }

            foreach (KeyValuePair<string, List<BoardSpacePrefab>> pair in grouped)
            {
                if (pair.Value == null || pair.Value.Count == 0)
                {
                    continue;
                }

                BoardSpacePrefab sample = pair.Value[0];
                DistrictMarkerEntry entry =
                    CreateDistrictMarkerEntry(
                        pair.Key,
                        sample,
                        pair.Value);

                if (entry != null)
                {
                    districtMarkerEntries[pair.Key] = entry;
                }
            }
        }

        private DistrictMarkerEntry CreateDistrictMarkerEntry(
            string districtId,
            BoardSpacePrefab sample,
            List<BoardSpacePrefab> buildings)
        {
            GameObject rootObject = new GameObject(
                "DistrictMarker_" + districtId,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(MoveRangeCircleGraphic));

            RectTransform root =
                rootObject.GetComponent<RectTransform>();
            root.SetParent(districtMarkerUIRoot, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            MoveRangeCircleGraphic background =
                rootObject.GetComponent<MoveRangeCircleGraphic>();
            background.color = districtMarkerBackgroundColor;
            background.raycastTarget = false;

            GameObject iconObject = new GameObject(
                "DistrictIcon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform iconRect =
                iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(root, false);
            iconRect.anchorMin = new Vector2(0.18f, 0.18f);
            iconRect.anchorMax = new Vector2(0.82f, 0.82f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = sample != null ? sample.DistrictIcon : null;
            icon.color = districtMarkerForegroundColor;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = icon.sprite != null;

            TMP_Text fallbackLabel = CreateText(
                "DistrictFallbackLabel",
                root,
                GetDistrictMarkerFallbackLabel(sample),
                18,
                TextAnchor.MiddleCenter,
                UIFontRole.Title);
            Stretch(fallbackLabel.rectTransform, 3f);
            fallbackLabel.color = districtMarkerForegroundColor;
            fallbackLabel.enabled = icon.sprite == null;

            return new DistrictMarkerEntry(
                districtId,
                root,
                icon,
                fallbackLabel,
                buildings);
        }

        private static string GetDistrictMarkerFallbackLabel(
            BoardSpacePrefab sample)
        {
            string displayName =
                sample != null
                    ? sample.DistrictDisplayName
                    : "지구";

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "구";
            }

            if (displayName.Contains("주거")) return "주";
            if (displayName.Contains("상업")) return "상";
            if (displayName.Contains("의료")) return "의";
            if (displayName.Contains("산업")) return "산";
            if (displayName.Contains("공공")) return "공";
            if (displayName.Contains("복합")) return "복";

            return displayName.Substring(0, 1);
        }

        private void UpdateDistrictMarkerPositions()
        {
            if (!showDistrictCenterMarkers ||
                districtMarkerUIRoot == null)
            {
                return;
            }

            float zoomNormalized = GetCardZoomNormalized();
            float markerSize = Mathf.Lerp(
                nearDistrictMarkerSize,
                farDistrictMarkerSize,
                zoomNormalized);

            foreach (DistrictMarkerEntry entry in districtMarkerEntries.Values)
            {
                if (entry == null || entry.Root == null)
                {
                    continue;
                }

                Vector3 total = Vector3.zero;
                int count = 0;

                for (int index = 0;
                     index < entry.Buildings.Count;
                     index++)
                {
                    BoardSpacePrefab building = entry.Buildings[index];

                    if (building == null)
                    {
                        continue;
                    }

                    total += building.transform.position;
                    count++;
                }

                if (count == 0)
                {
                    entry.Root.gameObject.SetActive(false);
                    continue;
                }

                entry.Root.gameObject.SetActive(true);
                entry.Root.sizeDelta = new Vector2(markerSize, markerSize);

                Vector3 center = total / count;
                center += Vector3.up * districtMarkerWorldHeight;

                SetTrackedUIPosition(
                    entry.Root,
                    center,
                    districtMarkerUIRoot,
                    Vector2.zero);
            }
        }

        private void UpdateCardSlotPositions()
        {
            ResolveWorldCamera();

            float zoomNormalized = GetCardZoomNormalized();
            float zoomCurveValue =
                cardSizeByZoom != null
                    ? Mathf.Clamp01(cardSizeByZoom.Evaluate(zoomNormalized))
                    : zoomNormalized;

            float baseCardSize =
                Mathf.Lerp(
                    nearCardIconSize,
                    farCardIconSize,
                    zoomCurveValue);

            float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            float blend =
                1f - Mathf.Exp(-cardHoverTweenSpeed * deltaTime);

            foreach (CardSlotEntry entry in cardEntries.Values)
            {
                if (entry == null ||
                    entry.Root == null ||
                    entry.Slot == null)
                {
                    continue;
                }

                BoardSpacePrefab space;

                if (gridManager == null ||
                    !gridManager.TryGetSpace(entry.Slot.NodeId, out space) ||
                    space == null)
                {
                    entry.Root.gameObject.SetActive(false);
                    continue;
                }

                if (!entry.Root.gameObject.activeSelf)
                {
                    continue;
                }

                Color runtimeCardColor =
                    ResolveBuildingCardColor(entry.Slot.NodeId);
                SetButtonColor(entry.Button, runtimeCardColor);

                Color runtimeForeground =
                    ResolveReadableForegroundColor(runtimeCardColor);

                if (entry.IconImage != null)
                {
                    entry.IconImage.color = runtimeForeground;
                }

                if (entry.Label != null)
                {
                    entry.Label.color = runtimeForeground;
                }

                float targetHoverAmount = entry.IsHovered ? 1f : 0f;

                entry.CurrentHoverAmount =
                    Mathf.Lerp(
                        entry.CurrentHoverAmount,
                        targetHoverAmount,
                        blend);

                float targetSize =
                    entry.IsHovered
                        ? Mathf.Max(
                            baseCardSize * cardHoverScale,
                            minimumHoveredCardSize)
                        : baseCardSize;

                entry.CurrentSize =
                    Mathf.Lerp(
                        entry.CurrentSize,
                        targetSize,
                        blend);

                entry.Root.sizeDelta =
                    new Vector2(
                        entry.CurrentSize,
                        entry.CurrentSize);

                LayoutElement layoutElement =
                    entry.Root.GetComponent<LayoutElement>();

                if (layoutElement != null)
                {
                    layoutElement.preferredWidth = entry.CurrentSize;
                    layoutElement.preferredHeight = entry.CurrentSize;
                    layoutElement.minWidth = entry.CurrentSize;
                    layoutElement.minHeight = entry.CurrentSize;
                }

                Vector2 screenOffset =
                    new Vector2(
                        0f,
                        entry.CurrentHoverAmount * cardHoverLift);

                Vector3 anchorPosition =
                    space.CardSlotRoot != null
                        ? space.CardSlotRoot.position
                        : space.transform.position;

                SetTrackedUIPosition(
                    entry.Root,
                    anchorPosition + Vector3.up * 0.9f,
                    cardSlotUIRoot,
                    screenOffset);

                UpdateDiscoveredBadgeLayout(entry, baseCardSize, blend);

                Vector2 targetTilt =
                    entry.IsHovered
                        ? new Vector2(
                            -entry.PointerNormalized.y * cardMaximumTilt,
                            entry.PointerNormalized.x * cardMaximumTilt)
                        : Vector2.zero;

                entry.CurrentTilt =
                    Vector2.Lerp(
                        entry.CurrentTilt,
                        targetTilt,
                        blend);

                entry.HoverKickRoll =
                    Mathf.Lerp(
                        entry.HoverKickRoll,
                        0f,
                        Mathf.Clamp01(blend * 0.72f));

                float targetRoll =
                    entry.IsHovered
                        ? -entry.PointerNormalized.x * cardMaximumTilt * 0.16f +
                          entry.HoverKickRoll
                        : 0f;

                entry.CurrentRoll =
                    Mathf.Lerp(
                        entry.CurrentRoll,
                        targetRoll,
                        blend);

                entry.Root.localRotation =
                    Quaternion.Euler(
                        entry.CurrentTilt.x,
                        entry.CurrentTilt.y,
                        entry.CurrentRoll);

                entry.CameraDistanceSqr =
                    worldCamera != null
                        ? (worldCamera.transform.position - anchorPosition).sqrMagnitude
                        : 0f;
            }

            SortCardEntriesByCameraDistance();
        }

        private void UpdateDiscoveredBadgeLayout(
            CardSlotEntry entry,
            float baseCardSize,
            float blend)
        {
            if (entry == null || entry.BadgeRoot == null)
            {
                return;
            }

            List<SearchCardBadgeEntry> badges =
                new List<SearchCardBadgeEntry>(entry.Badges.Values);

            badges.Sort(
                delegate(SearchCardBadgeEntry left, SearchCardBadgeEntry right)
                {
                    if (left == null || left.Slot == null) return -1;
                    if (right == null || right.Slot == null) return 1;
                    return left.Slot.SlotIndex.CompareTo(right.Slot.SlotIndex);
                });

            float badgeSize = Mathf.Clamp(
                baseCardSize * discoveredCardBadgeScale,
                minimumDiscoveredBadgeSize,
                maximumDiscoveredBadgeSize);

            float spacing =
                Mathf.Max(8f, badgeSize * discoveredBadgeSpacingMultiplier);

            for (int index = 0; index < badges.Count; index++)
            {
                SearchCardBadgeEntry badge = badges[index];

                if (badge == null || badge.Root == null)
                {
                    continue;
                }

                float centerOffset =
                    index - (badges.Count - 1) * 0.5f;

                float targetScale =
                    badge.IsHovered
                        ? discoveredBadgeHoverScale
                        : 1f;

                badge.CurrentScale =
                    Mathf.Lerp(
                        badge.CurrentScale,
                        targetScale,
                        blend);

                badge.Root.sizeDelta =
                    new Vector2(badgeSize, badgeSize);

                badge.Root.localScale =
                    Vector3.one * badge.CurrentScale;

                badge.Root.anchoredPosition =
                    new Vector2(
                        centerOffset * spacing,
                        entry.CurrentSize * discoveredBadgeVerticalRatio);

                badge.Root.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        centerOffset * -4f);

                LayoutElement layoutElement =
                    badge.Root.GetComponent<LayoutElement>();

                if (layoutElement != null)
                {
                    layoutElement.preferredWidth = badgeSize;
                    layoutElement.preferredHeight = badgeSize;
                    layoutElement.minWidth = badgeSize;
                    layoutElement.minHeight = badgeSize;
                }

                if (badge.IsHovered)
                {
                    badge.Root.SetAsLastSibling();
                }
            }
        }

        private float GetCardZoomNormalized()
        {
            if (cameraController != null)
            {
                return Mathf.Clamp01(
                    cameraController.CurrentZoomNormalized);
            }

            return 0.5f;
        }

        private void SortCardEntriesByCameraDistance()
        {
            sortableCardEntries.Sort(
                delegate(CardSlotEntry left, CardSlotEntry right)
                {
                    if (ReferenceEquals(left, right))
                    {
                        return 0;
                    }

                    if (left == null)
                    {
                        return -1;
                    }

                    if (right == null)
                    {
                        return 1;
                    }

                    if (left.IsHovered != right.IsHovered)
                    {
                        return left.IsHovered ? 1 : -1;
                    }

                    // 먼 카드를 먼저 그리고, 카메라에 가까운 카드를 나중에 그립니다.
                    return right.CameraDistanceSqr.CompareTo(
                        left.CameraDistanceSqr);
                });

            for (int index = 0; index < sortableCardEntries.Count; index++)
            {
                CardSlotEntry entry = sortableCardEntries[index];

                if (entry != null && entry.Root != null)
                {
                    entry.Root.SetSiblingIndex(index);
                }
            }
        }


        private void RebuildPlayerTokenEntries()
        {
            foreach (PlayerTokenEntry entry in playerTokenEntries.Values)
            {
                if (entry != null && entry.Root != null)
                {
                    Destroy(entry.Root.gameObject);
                }
            }

            playerTokenEntries.Clear();

            if (playerManager == null ||
                playerTokenUIRoot == null ||
                !showBoardPlayerTokens)
            {
                RestoreWorldPlayerPresentation();
                return;
            }

            for (int index = 0; index < playerManager.Players.Count; index++)
            {
                PlayerData player = playerManager.Players[index];

                if (player == null)
                {
                    continue;
                }

                PlayerTokenEntry entry =
                    CreatePlayerTokenEntry(player, index);

                if (entry != null)
                {
                    playerTokenEntries[player.PlayerId] = entry;
                }
            }

            ApplyWorldPlayerPresentation();
        }

        private PlayerTokenEntry CreatePlayerTokenEntry(
            PlayerData player,
            int playerIndex)
        {
            if (player == null || playerTokenUIRoot == null)
            {
                return null;
            }

            GameObject rootObject = new GameObject(
                "PlayerToken_" + player.PlayerId,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(MoveRangeCircleGraphic),
                typeof(Button));

            RectTransform root =
                rootObject.GetComponent<RectTransform>();

            root.SetParent(playerTokenUIRoot, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta =
                new Vector2(playerTokenDiameter, playerTokenDiameter);

            MoveRangeCircleGraphic border =
                rootObject.GetComponent<MoveRangeCircleGraphic>();

            border.color = GetPlayerTokenBaseColor(player, playerIndex);
            border.raycastTarget = true;

            Button button = rootObject.GetComponent<Button>();
            button.targetGraphic = border;
            button.transition = Selectable.Transition.None;

            string capturedPlayerId = player.PlayerId;
            button.onClick.AddListener(
                delegate { SelectPlayerAndFocus(capturedPlayerId); });

            GameObject maskObject = new GameObject(
                "PortraitMask",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(MoveRangeCircleGraphic),
                typeof(Mask));

            RectTransform maskRect =
                maskObject.GetComponent<RectTransform>();

            maskRect.SetParent(root, false);
            maskRect.anchorMin = new Vector2(0.5f, 0.5f);
            maskRect.anchorMax = new Vector2(0.5f, 0.5f);
            maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = Vector2.zero;
            maskRect.sizeDelta =
                Vector2.one * Mathf.Max(4f, playerTokenDiameter - 8f);

            MoveRangeCircleGraphic maskGraphic =
                maskObject.GetComponent<MoveRangeCircleGraphic>();

            maskGraphic.color = Color.white;
            maskGraphic.raycastTarget = false;

            Mask mask = maskObject.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject portraitObject = new GameObject(
                "Portrait",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            RectTransform portraitRect =
                portraitObject.GetComponent<RectTransform>();

            portraitRect.SetParent(maskRect, false);
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;

            Image portrait = portraitObject.GetComponent<Image>();
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;
            portrait.sprite = ResolvePlayerTokenSprite(player);

            TextMeshProUGUI initials = CreateText(
                "Initials",
                maskRect,
                "P" + (playerIndex + 1),
                22,
                TextAnchor.MiddleCenter,
                UIFontRole.Title);

            Stretch(initials.rectTransform, 0f);
            initials.raycastTarget = false;
            initials.gameObject.SetActive(portrait.sprite == null);

            return new PlayerTokenEntry(
                root,
                border,
                portrait,
                initials,
                button,
                player,
                playerIndex);
        }

        private void UpdatePlayerTokenPositions()
        {
            if (playerManager == null || playerTokenUIRoot == null)
            {
                return;
            }

            if (!showBoardPlayerTokens)
            {
                foreach (PlayerTokenEntry entry in playerTokenEntries.Values)
                {
                    if (entry != null && entry.Root != null)
                    {
                        entry.Root.gameObject.SetActive(false);
                    }
                }

                RestoreWorldPlayerPresentation();
                return;
            }

            ApplyWorldPlayerPresentation();
            ResolveWorldCamera();

            if (worldCamera == null)
            {
                return;
            }

            PlayerData current =
                turnManager != null ? turnManager.CurrentPlayer : null;

            PlayerData selected = GetSelectedPlayer();

            float tokenZoomScale = Mathf.Lerp(
                playerTokenNearZoomScale,
                playerTokenFarZoomScale,
                GetCardZoomNormalized());

            foreach (PlayerTokenEntry entry in playerTokenEntries.Values)
            {
                if (entry == null ||
                    entry.Root == null ||
                    entry.Player == null)
                {
                    continue;
                }

                BoardSpacePrefab space;

                if (!TryGetPlayerSpace(entry.Player, out space))
                {
                    entry.Root.gameObject.SetActive(false);
                    continue;
                }

                Vector3 worldPosition =
                    space.transform.position +
                    Vector3.up * playerTokenWorldHeight;

                Vector3 screenPoint =
                    worldCamera.WorldToScreenPoint(worldPosition);

                if (screenPoint.z <= 0f)
                {
                    entry.Root.gameObject.SetActive(false);
                    continue;
                }

                entry.Root.gameObject.SetActive(true);

                int sameSpaceIndex;
                int sameSpaceCount;

                GetSameSpaceTokenOrder(
                    entry.Player,
                    out sameSpaceIndex,
                    out sameSpaceCount);

                Vector2 spreadOffset = Vector2.zero;

                if (sameSpaceCount > 1)
                {
                    float angle =
                        Mathf.PI * 2f *
                        sameSpaceIndex /
                        sameSpaceCount;

                    spreadOffset =
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle)) *
                        sameSpaceTokenSpread *
                        tokenZoomScale;
                }

                SetTrackedUIPosition(
                    entry.Root,
                    worldPosition,
                    playerTokenUIRoot,
                    spreadOffset);

                ClampPlayerTokenToScreen(entry.Root);

                bool isActive = current == entry.Player;
                bool isSelected =
                    isActive ||
                    (current == null && selected == entry.Player);

                float selectionScale =
                    isSelected ? selectedPlayerTokenScale : 1f;

                entry.Root.localScale =
                    Vector3.one *
                    tokenZoomScale *
                    selectionScale;

                entry.Border.color =
                    isActive
                        ? playerTokenActiveColor
                        : isSelected
                            ? playerTokenSelectedColor
                            : GetPlayerTokenBaseColor(
                                entry.Player,
                                entry.PlayerIndex);

                entry.Button.interactable =
                    current == null || current == entry.Player;

                if (isSelected)
                {
                    entry.Root.SetAsLastSibling();
                }
            }
        }

        private void GetSameSpaceTokenOrder(
            PlayerData target,
            out int index,
            out int count)
        {
            index = 0;
            count = 0;

            if (target == null ||
                playerManager == null ||
                string.IsNullOrWhiteSpace(target.CurrentNodeId))
            {
                return;
            }

            for (int playerIndex = 0;
                 playerIndex < playerManager.Players.Count;
                 playerIndex++)
            {
                PlayerData candidate =
                    playerManager.Players[playerIndex];

                if (candidate == null ||
                    candidate.CurrentNodeId != target.CurrentNodeId)
                {
                    continue;
                }

                if (candidate == target)
                {
                    index = count;
                }

                count++;
            }
        }

        private void ClampPlayerTokenToScreen(RectTransform token)
        {
            if (token == null || playerTokenUIRoot == null)
            {
                return;
            }

            Rect bounds = playerTokenUIRoot.rect;
            float scale =
                Mathf.Max(
                    Mathf.Abs(token.localScale.x),
                    Mathf.Abs(token.localScale.y));

            float halfSize =
                playerTokenDiameter * scale * 0.5f +
                playerTokenScreenPadding;

            Vector2 position = token.anchoredPosition;

            position.x = Mathf.Clamp(
                position.x,
                bounds.xMin + halfSize,
                bounds.xMax - halfSize);

            position.y = Mathf.Clamp(
                position.y,
                bounds.yMin + halfSize,
                bounds.yMax - halfSize);

            token.anchoredPosition = position;
        }

        private Sprite ResolvePlayerTokenSprite(PlayerData player)
        {
            if (player != null && playerTokenProfiles != null)
            {
                for (int index = 0;
                     index < playerTokenProfiles.Count;
                     index++)
                {
                    PlayerTokenProfileSetting setting =
                        playerTokenProfiles[index];

                    if (setting == null || setting.ProfileSprite == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(setting.PlayerId) &&
                        setting.PlayerId == player.PlayerId)
                    {
                        return setting.ProfileSprite;
                    }
                }

                for (int index = 0;
                     index < playerTokenProfiles.Count;
                     index++)
                {
                    PlayerTokenProfileSetting setting =
                        playerTokenProfiles[index];

                    if (setting == null || setting.ProfileSprite == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(setting.PlayerPresetId) &&
                        setting.PlayerPresetId == player.PlayerPresetId)
                    {
                        return setting.ProfileSprite;
                    }
                }
            }

            return defaultPlayerTokenSprite;
        }

        private Color GetPlayerTokenBaseColor(
            PlayerData player,
            int playerIndex)
        {
            if (player != null && playerTokenProfiles != null)
            {
                for (int index = 0;
                     index < playerTokenProfiles.Count;
                     index++)
                {
                    PlayerTokenProfileSetting setting =
                        playerTokenProfiles[index];

                    if (setting == null)
                    {
                        continue;
                    }

                    bool playerIdMatches =
                        !string.IsNullOrWhiteSpace(setting.PlayerId) &&
                        setting.PlayerId == player.PlayerId;

                    bool presetMatches =
                        string.IsNullOrWhiteSpace(setting.PlayerId) &&
                        !string.IsNullOrWhiteSpace(setting.PlayerPresetId) &&
                        setting.PlayerPresetId == player.PlayerPresetId;

                    if (playerIdMatches || presetMatches)
                    {
                        return setting.BorderColor;
                    }
                }
            }

            Color[] fallbackColors =
            {
                new Color(0.26f, 0.62f, 0.90f, 1f),
                new Color(0.90f, 0.36f, 0.24f, 1f),
                new Color(0.32f, 0.76f, 0.48f, 1f),
                new Color(0.84f, 0.64f, 0.20f, 1f),
                new Color(0.62f, 0.42f, 0.86f, 1f),
                new Color(0.28f, 0.74f, 0.76f, 1f)
            };

            if (playerIndex >= 0 && playerIndex < fallbackColors.Length)
            {
                return fallbackColors[playerIndex];
            }

            return playerTokenNormalColor;
        }

        private void ApplyWorldPlayerPresentation()
        {
            bool hide =
                showBoardPlayerTokens &&
                hideWorldPlayerModelsWhenTokensVisible;

            if (!hide)
            {
                RestoreWorldPlayerPresentation();
                return;
            }

            if (playerManager == null)
            {
                return;
            }

            for (int index = 0; index < playerManager.Players.Count; index++)
            {
                PlayerData player = playerManager.Players[index];

                if (player == null)
                {
                    continue;
                }

                PlayerBoardPrefab view =
                    playerManager.GetPlayerView(player.PlayerId);

                if (view == null)
                {
                    continue;
                }

                Renderer[] renderers =
                    view.GetComponentsInChildren<Renderer>(true);

                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];

                    if (renderer == null)
                    {
                        continue;
                    }

                    if (!hiddenPlayerRendererStates.ContainsKey(renderer))
                    {
                        hiddenPlayerRendererStates.Add(
                            renderer,
                            renderer.enabled);
                    }

                    renderer.enabled = false;
                }

                Collider[] colliders =
                    view.GetComponentsInChildren<Collider>(true);

                for (int colliderIndex = 0;
                     colliderIndex < colliders.Length;
                     colliderIndex++)
                {
                    Collider collider = colliders[colliderIndex];

                    if (collider == null)
                    {
                        continue;
                    }

                    if (!hiddenPlayerColliderStates.ContainsKey(collider))
                    {
                        hiddenPlayerColliderStates.Add(
                            collider,
                            collider.enabled);
                    }

                    collider.enabled = false;
                }

                Collider2D[] colliders2D =
                    view.GetComponentsInChildren<Collider2D>(true);

                for (int colliderIndex = 0;
                     colliderIndex < colliders2D.Length;
                     colliderIndex++)
                {
                    Collider2D collider = colliders2D[colliderIndex];

                    if (collider == null)
                    {
                        continue;
                    }

                    if (!hiddenPlayerCollider2DStates.ContainsKey(collider))
                    {
                        hiddenPlayerCollider2DStates.Add(
                            collider,
                            collider.enabled);
                    }

                    collider.enabled = false;
                }
            }
        }

        private void RestoreWorldPlayerPresentation()
        {
            foreach (KeyValuePair<Renderer, bool> pair
                     in hiddenPlayerRendererStates)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                }
            }

            hiddenPlayerRendererStates.Clear();

            foreach (KeyValuePair<Collider, bool> pair
                     in hiddenPlayerColliderStates)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                }
            }

            hiddenPlayerColliderStates.Clear();

            foreach (KeyValuePair<Collider2D, bool> pair
                     in hiddenPlayerCollider2DStates)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                }
            }

            hiddenPlayerCollider2DStates.Clear();
        }

        // 이전 좌/우 슬라이드 방식과의 직렬화 호환을 위해 메서드 이름만 남깁니다.
        // 플레이어 선택 버튼은 이제 우측 하단에 항상 표시되므로 위치를 숨기지 않습니다.
        private void UpdatePlayerPanelSlides()
        {
        }

        private void SetTrackedUIPosition(
            RectTransform target,
            Vector3 worldPosition,
            RectTransform parent,
            Vector2 screenOffset)
        {
            if (target == null ||
                parent == null ||
                targetCanvas == null)
            {
                return;
            }

            ResolveWorldCamera();

            if (worldCamera == null)
            {
                return;
            }

            Vector3 screenPoint =
                worldCamera.WorldToScreenPoint(worldPosition);

            if (screenPoint.z <= 0f)
            {
                target.anchoredPosition = new Vector2(100000f, 100000f);
                return;
            }

            Vector2 localPoint;
            Camera canvasCamera =
                targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : targetCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    (Vector2)screenPoint + screenOffset,
                    canvasCamera,
                    out localPoint))
            {
                target.anchoredPosition = localPoint;
            }
        }

        private void HandleCurrentPlayerChanged(PlayerData player)
        {
            if (player != null)
            {
                selectedPlayerId = player.PlayerId;
            }

            ClearMoveEntries();
            HighlightDisplayPlayer();

            if (autoFocusSelectedPlayer &&
                player != null &&
                playerManager != null &&
                cameraController != null)
            {
                PlayerBoardPrefab view =
                    playerManager.GetPlayerView(player.PlayerId);

                if (view != null)
                {
                    cameraController.FocusTransform(view.transform, true);
                }
            }

            RefreshAll();
        }

        private void HighlightDisplayPlayer()
        {
            if (playerManager == null)
            {
                return;
            }

            PlayerData display = GetDisplayPlayer();

            for (int index = 0; index < playerManager.Players.Count; index++)
            {
                PlayerData player = playerManager.Players[index];

                if (player == null)
                {
                    continue;
                }

                PlayerBoardPrefab view =
                    playerManager.GetPlayerView(player.PlayerId);

                if (view != null)
                {
                    view.SetTurnHighlighted(player == display);
                }
            }
        }

        private void HandleAPChanged(PlayerData player, float remainingAP)
        {
            RefreshAll();
        }

        private void HandleActionStateChanged(TurnActionState state)
        {
            if (state == TurnActionState.AwaitingPlayerSelection &&
                turnManager != null &&
                turnManager.CurrentPlayer == null)
            {
                HighlightDisplayPlayer();
            }

            RefreshAll();
        }

        private void HandleRoundStarted(int round)
        {
            selectedPlayerId = null;
            ClearMoveEntries();
            HighlightDisplayPlayer();
            AddLocalLog("라운드 " + round + " 시작 / 사용할 말을 선택하십시오.");
            RefreshAll();
        }

        private void HandleRoundCompleted(int round)
        {
            selectedPlayerId = null;
            AddLocalLog("라운드 " + round + " 종료");
            RefreshAll();
        }

        private void HandleTurnEnded(PlayerData player)
        {
            ClearMoveEntries();
            selectedPlayerId = null;
            HighlightDisplayPlayer();
            RefreshAll();
        }

        private void HandleGameTurnsCompleted()
        {
            AddLocalLog("최대 라운드 또는 생존자 조건으로 턴 진행이 종료되었습니다.");
            RefreshAll();
        }

        private void HandlePlayerMoved(
            PlayerData player,
            BoardSpacePrefab from,
            BoardSpacePrefab to)
        {
            AddLocalLog(
                player.DisplayName +
                " 이동: " +
                (from != null ? from.NodeId : "NONE") +
                " → " +
                (to != null ? to.NodeId : "NONE"));

            RefreshAll();
        }

        private void HandleCardSlotChanged(BoardCardSlotData slot)
        {
            CardSlotEntry entry;

            if (slot != null &&
                buildingCardEntriesByNodeId.TryGetValue(slot.NodeId, out entry))
            {
                RefreshCardEntry(entry);
            }

            RefreshAll();
        }

        private void HandleSearchResolved(
            PlayerData player,
            BoardSpacePrefab space,
            BoardCardSlotData slot)
        {
            AddLocalLog(
                player.DisplayName +
                " 탐색: " +
                slot.DisplayName);
        }

        private void HandleSearchFailed(string reason)
        {
            AddLocalLog(reason);
        }

        private void HandleLogAdded(LogCategory category, string message)
        {
            AddLocalLog("[" + category + "] " + message);
        }

        private void RefreshAll()
        {
            if (!initialized && turnManager == null)
            {
                return;
            }

            PlayerData current =
                turnManager != null
                    ? turnManager.CurrentPlayer
                    : null;

            PlayerData selected = GetSelectedPlayer();
            PlayerData candidate = current != null ? current : selected;

            if (roundText != null)
            {
                roundText.text =
                    "ROUND " +
                    (turnManager != null
                        ? turnManager.RoundNumber.ToString()
                        : "-");
            }

            if (currentPlayerText != null)
            {
                currentPlayerText.text =
                    current != null
                        ? "TURN  " + current.DisplayName
                        : selected != null
                            ? "SELECT  " + selected.DisplayName
                            : "우측 하단에서 캐릭터 선택";
            }

            if (apText != null)
            {
                apText.text =
                    current != null
                        ? "AP " + current.CurrentAP.ToString("0.##") +
                          " / " + current.Speed
                        : selected != null
                            ? "예상 AP " + selected.Speed
                            : "AP -";
            }

            if (actionStateText != null)
            {
                actionStateText.text =
                    turnManager != null
                        ? GetActionStateLabel(turnManager.ActionState)
                        : "미초기화";
            }

            bool canStartAction =
                !isPathMoving &&
                candidate != null &&
                turnManager != null &&
                (turnManager.ActionState == TurnActionState.AwaitingPlayerSelection ||
                 turnManager.ActionState == TurnActionState.AwaitingAction);

            if (moveButton != null)
            {
                moveButton.interactable = canStartAction;
            }

            float searchCost = 0f;
            string searchFailure = string.Empty;
            float candidateAP =
                current != null
                    ? current.CurrentAP
                    : selected != null
                        ? selected.Speed
                        : 0f;

            bool canSearch =
                canStartAction &&
                searchManager != null &&
                searchManager.CanPlayerSearch(
                    candidate,
                    candidateAP,
                    out searchCost,
                    out searchFailure);

            if (searchButton != null)
            {
                searchButton.interactable = canSearch;

                TMP_Text searchLabel =
                    searchButton.GetComponentInChildren<TMP_Text>(true);

                if (searchLabel != null)
                {
                    searchLabel.text =
                        canSearch
                            ? "탐색 " + searchCost.ToString("0.##") + " AP"
                            : "탐색";
                }
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable =
                    !isPathMoving &&
                    turnManager != null &&
                    turnManager.CanEndCurrentTurn;
            }

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(
                    !isPathMoving &&
                    turnManager != null &&
                    turnManager.ActionState == TurnActionState.SelectingMove);
            }

            RefreshPlayerEntries();
            RefreshCardEntries();
            RefreshCardResolveButton();
            HighlightDisplayPlayer();
        }

        private static string GetActionStateLabel(TurnActionState state)
        {
            switch (state)
            {
                case TurnActionState.TurnStarting:
                    return "턴 확정";
                case TurnActionState.AwaitingPlayerSelection:
                    return "말 선택";
                case TurnActionState.AwaitingAction:
                    return "행동 선택";
                case TurnActionState.SelectingMove:
                    return "이동 범위 / 경로 선택";
                case TurnActionState.ResolvingCard:
                    return "카드 해결";
                case TurnActionState.Locked:
                    return "입력 잠금";
                case TurnActionState.TurnEnding:
                    return "턴 종료";
                case TurnActionState.RoundEnding:
                    return "라운드 종료";
                case TurnActionState.GameEnded:
                    return "게임 종료";
                default:
                    return "대기";
            }
        }

        private void AddLocalLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            logLines.Enqueue(message);

            while (logLines.Count > Mathf.Max(1, maximumLogLines))
            {
                logLines.Dequeue();
            }

            if (logText != null)
            {
                logText.text = string.Join("\n", logLines.ToArray());
            }
        }

        private void ResolveProceduralCityBoardManager()
        {
            if (proceduralCityBoardManager != null)
            {
                return;
            }

#if UNITY_2023_1_OR_NEWER
            proceduralCityBoardManager =
                FindFirstObjectByType<ProceduralCityBoardManager>();
#else
            proceduralCityBoardManager =
                FindObjectOfType<ProceduralCityBoardManager>();
#endif
        }

        private void SetMovementInspectionVisible(bool visible)
        {
            ResolveProceduralCityBoardManager();

            if (proceduralCityBoardManager == null)
            {
                return;
            }

            if (visible)
            {
                proceduralCityBoardManager.ShowMovementTiles();
            }
            else
            {
                proceduralCityBoardManager.HideMovementTiles();
            }
        }

        private void ResolveWorldCamera()
        {
            if (worldCamera != null)
            {
                return;
            }

            if (cameraController != null)
            {
                worldCamera = cameraController.TargetCamera;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void BuildRuntimeUIIfNeeded()
        {
            EnsureEventSystem();
            EnsureCanvas();

            if (targetCanvas == null)
            {
                return;
            }

            RectTransform canvasRoot =
                targetCanvas.GetComponent<RectTransform>();

            // 플레이어 선택 목록은 우측 하단에 한 줄씩 항상 노출합니다.
            // 월드 말 Collider 상태와 관계없이 이 UI로 반드시 캐릭터를 선택할 수 있습니다.
            if (playerLeftRoot != null)
            {
                playerLeftRoot.gameObject.SetActive(false);
            }

            if (playerRightRoot == null)
            {
                playerRightRoot = CreatePanel(
                    "PlayerSelector_BottomRight",
                    canvasRoot,
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    playerSelectorScreenOffset,
                    new Vector2(playerSelectorButtonSize.x + 20f, 80f),
                    new Vector2(1f, 0f));
            }

            PrepareBottomRightPlayerSelector(playerRightRoot);

            RectTransform topPanel =
                FindOrCreateTopPanel(canvasRoot);

            if (roundText == null)
            {
                roundText = CreateText(
                    "RoundText",
                    topPanel,
                    "ROUND -",
                    21,
                    TextAnchor.MiddleLeft,
                    UIFontRole.Title);
            }

            if (currentPlayerText == null)
            {
                currentPlayerText = CreateText(
                    "CurrentPlayerText",
                    topPanel,
                    "우측 하단에서 캐릭터 선택",
                    22,
                    TextAnchor.MiddleCenter,
                    UIFontRole.Title);
            }

            if (apText == null)
            {
                apText = CreateText(
                    "APText",
                    topPanel,
                    "AP -",
                    21,
                    TextAnchor.MiddleCenter,
                    UIFontRole.Regular);
            }

            if (actionStateText == null)
            {
                actionStateText = CreateText(
                    "StateText",
                    topPanel,
                    "말 선택",
                    19,
                    TextAnchor.MiddleRight,
                    UIFontRole.Title);
            }

            RectTransform actionPanel =
                FindOrCreateActionPanel(canvasRoot);

            if (moveButton == null)
            {
                moveButton = CreateButton(
                    "MoveButton",
                    actionPanel,
                    "이동",
                    new Vector2(150f, 54f),
                    UIFontRole.Regular);
            }

            if (searchButton == null)
            {
                searchButton = CreateButton(
                    "SearchButton",
                    actionPanel,
                    "탐색",
                    new Vector2(150f, 54f),
                    UIFontRole.Regular);
            }

            if (endTurnButton == null)
            {
                endTurnButton = CreateButton(
                    "EndTurnButton",
                    actionPanel,
                    "턴 종료",
                    new Vector2(150f, 54f),
                    UIFontRole.Regular);
            }

            if (cancelButton == null)
            {
                cancelButton = CreateButton(
                    "CancelButton",
                    actionPanel,
                    "선택 취소",
                    new Vector2(150f, 54f),
                    UIFontRole.Regular);
            }

            if (moveMarkerRoot == null)
            {
                moveMarkerRoot = CreateFullScreenRoot(
                    "MoveMarkerLayer",
                    canvasRoot);
            }

            if (cardSlotUIRoot == null)
            {
                cardSlotUIRoot = CreateFullScreenRoot(
                    "CardSlotLayer",
                    canvasRoot);

                cardSlotUIRoot.SetAsFirstSibling();
            }

            if (districtMarkerUIRoot == null)
            {
                districtMarkerUIRoot = CreateFullScreenRoot(
                    "DistrictMarkerLayer",
                    canvasRoot);

                districtMarkerUIRoot.SetAsFirstSibling();
            }

            if (playerTokenUIRoot == null)
            {
                playerTokenUIRoot = CreateFullScreenRoot(
                    "PlayerTokenLayer",
                    canvasRoot);
            }

            if (playerDetailPanel == null)
            {
                float selectorWidth = playerSelectorButtonSize.x + 20f;

                playerDetailPanel = CreatePanel(
                    "PlayerDetail_BottomRight",
                    canvasRoot,
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(
                        playerSelectorScreenOffset.x - selectorWidth - 14f,
                        playerSelectorScreenOffset.y),
                    playerDetailPanelSize,
                    new Vector2(1f, 0f));

                playerDetailText = CreateText(
                    "PlayerDetailText",
                    playerDetailPanel,
                    string.Empty,
                    18,
                    TextAnchor.UpperLeft,
                    UIFontRole.Regular);

                Stretch(playerDetailText.rectTransform, 14f);
                playerDetailPanel.gameObject.SetActive(false);
            }

            PrepareBottomRightPlayerDetail(playerDetailPanel);

            BuildMoveTooltip(canvasRoot);
            BuildCardPopup(canvasRoot);
            BuildLogPanel(canvasRoot);

            if (playerTokenUIRoot != null)
            {
                playerTokenUIRoot.SetAsLastSibling();
            }

            if (playerRightRoot != null)
            {
                playerRightRoot.SetAsLastSibling();
            }

            if (playerDetailPanel != null)
            {
                playerDetailPanel.SetAsLastSibling();
            }

            BindButtonListeners();
            ApplyConfiguredFonts();
        }

        private void PrepareBottomRightPlayerDetail(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            float selectorWidth = playerSelectorButtonSize.x + 20f;
            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(
                playerSelectorScreenOffset.x - selectorWidth - 14f,
                playerSelectorScreenOffset.y);
            root.sizeDelta = playerDetailPanelSize;
        }

        private void PrepareBottomRightPlayerSelector(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            root.anchorMin = new Vector2(1f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = playerSelectorScreenOffset;
            root.sizeDelta = new Vector2(playerSelectorButtonSize.x + 20f, 80f);

            Image image = root.GetComponent<Image>();

            if (image != null)
            {
                image.color = panelColor;
                image.raycastTarget = true;
            }

            RectMask2D mask = root.GetComponent<RectMask2D>();

            if (mask != null)
            {
                Destroy(mask);
            }

            VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();

            if (layout == null)
            {
                layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            int padding = 10;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = playerSelectorSpacing;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();

            if (fitter == null)
            {
                fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void PrepareSlidingPlayerRoot(RectTransform root)
        {
            PrepareBottomRightPlayerSelector(root);
        }

        private void EnsureCanvas()
        {
            if (targetCanvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject(
                "BoardOfDead_RuntimeUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            targetCanvas = canvasObject.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventObject = new GameObject(
                "EventSystem",
                typeof(EventSystem));

#if ENABLE_INPUT_SYSTEM
            eventObject.AddComponent<InputSystemUIInputModule>();
#else
            eventObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private RectTransform FindOrCreateTopPanel(RectTransform canvasRoot)
        {
            Transform found = canvasRoot.Find("TopTurnPanel");

            if (found != null)
            {
                return found as RectTransform;
            }

            RectTransform panel = CreatePanel(
                "TopTurnPanel",
                canvasRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -18f),
                new Vector2(820f, 66f),
                new Vector2(0.5f, 1f));

            HorizontalLayoutGroup layout =
                panel.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return panel;
        }

        private RectTransform FindOrCreateActionPanel(RectTransform canvasRoot)
        {
            Transform found = canvasRoot.Find("ActionPanel");

            if (found != null)
            {
                return found as RectTransform;
            }

            RectTransform panel = CreatePanel(
                "ActionPanel",
                canvasRoot,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(700f, 78f),
                new Vector2(0.5f, 0f));

            HorizontalLayoutGroup layout =
                panel.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return panel;
        }

        private void BuildMoveTooltip(RectTransform canvasRoot)
        {
            if (moveTooltipPanel != null)
            {
                return;
            }

            moveTooltipPanel = CreatePanel(
                "MoveTooltip",
                canvasRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                moveTooltipSize,
                new Vector2(0f, 1f));

            moveTooltipText = CreateText(
                "MoveTooltipText",
                moveTooltipPanel,
                string.Empty,
                17,
                TextAnchor.UpperLeft,
                UIFontRole.Scene);

            Stretch(moveTooltipText.rectTransform, 10f);
            moveTooltipPanel.gameObject.SetActive(false);
            moveTooltipPanel.SetAsLastSibling();
        }

        private void BuildCardPopup(RectTransform canvasRoot)
        {
            if (cardPopup != null)
            {
                return;
            }

            cardPopup = CreatePanel(
                "CardPopup",
                canvasRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(500f, 340f),
                new Vector2(0.5f, 0.5f));

            VerticalLayoutGroup layout =
                cardPopup.gameObject.AddComponent<VerticalLayoutGroup>();

            layout.padding = new RectOffset(24, 24, 22, 22);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            cardPopupTitle = CreateText(
                "CardTitle",
                cardPopup,
                "CARD",
                28,
                TextAnchor.MiddleCenter,
                UIFontRole.Title);

            SetLayoutHeight(cardPopupTitle.gameObject, 48f);

            cardPopupBody = CreateText(
                "CardBody",
                cardPopup,
                string.Empty,
                19,
                TextAnchor.UpperLeft,
                UIFontRole.Regular);

            SetLayoutHeight(cardPopupBody.gameObject, 178f);

            RectTransform buttons = CreatePlainRect(
                "PopupButtons",
                cardPopup,
                new Vector2(450f, 58f));

            HorizontalLayoutGroup buttonLayout =
                buttons.gameObject.AddComponent<HorizontalLayoutGroup>();

            buttonLayout.spacing = 12f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = false;
            buttonLayout.childControlHeight = false;
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;

            cardResolveButton = CreateButton(
                "ResolveCardButton",
                buttons,
                "카드 해결",
                new Vector2(180f, 48f),
                UIFontRole.Regular);

            cardCloseButton = CreateButton(
                "CloseCardButton",
                buttons,
                "닫기",
                new Vector2(180f, 48f),
                UIFontRole.Regular);

            cardPopup.gameObject.SetActive(false);
        }

        private void BuildLogPanel(RectTransform canvasRoot)
        {
            if (logText != null)
            {
                return;
            }

            RectTransform panel = CreatePanel(
                "GameLogPanel",
                canvasRoot,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(16f, 16f),
                new Vector2(430f, 180f),
                new Vector2(0f, 0f));

            logText = CreateText(
                "LogText",
                panel,
                string.Empty,
                15,
                TextAnchor.LowerLeft,
                UIFontRole.Regular);

            Stretch(logText.rectTransform, 12f);
        }

        private RectTransform CreatePanel(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 pivot)
        {
            GameObject go = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image));

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = panelColor;
            return rect;
        }

        private RectTransform CreateFullScreenRoot(
            string objectName,
            Transform parent)
        {
            GameObject go = new GameObject(
                objectName,
                typeof(RectTransform));

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private RectTransform CreatePlainRect(
            string objectName,
            Transform parent,
            Vector2 size)
        {
            GameObject go = new GameObject(
                objectName,
                typeof(RectTransform));

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            SetLayoutHeight(go, size.y);
            return rect;
        }

        private Button CreateButton(
            string objectName,
            Transform parent,
            string labelText,
            Vector2 size,
            UIFontRole fontRole)
        {
            GameObject go = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = buttonColor;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = disabledColor;
            button.colors = colors;

            TextMeshProUGUI text = CreateText(
                "Label",
                rect,
                labelText,
                17,
                TextAnchor.MiddleCenter,
                fontRole);

            Stretch(text.rectTransform, 6f);
            SetLayoutSize(go, size);
            return button;
        }

        private TextMeshProUGUI CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            UIFontRole fontRole)
        {
            GameObject go = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(120f, 44f);

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset fontAsset = ResolveFontAsset(fontRole);

            if (fontAsset != null)
            {
                text.font = fontAsset;
            }

            text.fontSize = fontSize;
            text.alignment = ConvertAlignment(alignment);
            text.color = Color.white;
            text.text = value;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private TMP_FontAsset ResolveFontAsset(UIFontRole role)
        {
            TMP_FontAsset selected = null;

            switch (role)
            {
                case UIFontRole.Title:
                    selected = titleFontAsset != null
                        ? titleFontAsset
                        : regularFontAsset != null
                            ? regularFontAsset
                            : sceneFontAsset;
                    break;
                case UIFontRole.Scene:
                    selected = sceneFontAsset != null
                        ? sceneFontAsset
                        : regularFontAsset != null
                            ? regularFontAsset
                            : titleFontAsset;
                    break;
                default:
                    selected = regularFontAsset != null
                        ? regularFontAsset
                        : titleFontAsset != null
                            ? titleFontAsset
                            : sceneFontAsset;
                    break;
            }

            if (selected == null)
            {
                selected = TMP_Settings.defaultFontAsset;
            }

            return selected;
        }

        private void ApplyConfiguredFonts()
        {
            ApplyFont(roundText, UIFontRole.Title);
            ApplyFont(currentPlayerText, UIFontRole.Title);
            ApplyFont(actionStateText, UIFontRole.Title);
            ApplyFont(cardPopupTitle, UIFontRole.Title);

            ApplyFont(apText, UIFontRole.Regular);
            ApplyFont(playerDetailText, UIFontRole.Regular);
            ApplyFont(cardPopupBody, UIFontRole.Regular);
            ApplyFont(logText, UIFontRole.Regular);

            ApplyFontToRoot(playerLeftRoot, UIFontRole.Regular);
            ApplyFontToRoot(playerRightRoot, UIFontRole.Regular);
            ApplyFontToRoot(moveMarkerRoot, UIFontRole.Scene);
            ApplyFontToRoot(cardSlotUIRoot, UIFontRole.Scene);
            ApplyFontToRoot(districtMarkerUIRoot, UIFontRole.Scene);
            ApplyFontToRoot(moveTooltipPanel, UIFontRole.Scene);
        }

        private void ApplyFont(TMP_Text text, UIFontRole role)
        {
            if (text == null)
            {
                return;
            }

            TMP_FontAsset font = ResolveFontAsset(role);

            if (font != null)
            {
                text.font = font;
            }
        }

        private void ApplyFontToRoot(RectTransform root, UIFontRole role)
        {
            if (root == null)
            {
                return;
            }

            TMP_FontAsset font = ResolveFontAsset(role);

            if (font == null)
            {
                return;
            }

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);

            for (int index = 0; index < texts.Length; index++)
            {
                if (texts[index] != null)
                {
                    texts[index].font = font;
                }
            }
        }

        private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
        }

        private static void AddVerticalLayout(
            RectTransform root,
            float spacing,
            int padding)
        {
            VerticalLayoutGroup layout =
                root.GetComponent<VerticalLayoutGroup>();

            if (layout == null)
            {
                layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding =
                new RectOffset(
                    padding,
                    padding,
                    padding,
                    padding);

            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void Stretch(
            RectTransform rect,
            float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static void SetLayoutSize(
            GameObject target,
            Vector2 size)
        {
            LayoutElement element =
                target.GetComponent<LayoutElement>();

            if (element == null)
            {
                element = target.AddComponent<LayoutElement>();
            }

            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            element.minWidth = size.x;
            element.minHeight = size.y;
        }

        private static void SetLayoutHeight(
            GameObject target,
            float height)
        {
            LayoutElement element =
                target.GetComponent<LayoutElement>();

            if (element == null)
            {
                element = target.AddComponent<LayoutElement>();
            }

            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void SetButtonColor(
            Button button,
            Color color)
        {
            if (button == null || button.targetGraphic == null)
            {
                return;
            }

            button.targetGraphic.color = color;
        }

        [Serializable]
        private sealed class PlayerTokenProfileSetting
        {
            [SerializeField] private string playerId;
            [SerializeField] private string playerPresetId;
            [SerializeField] private Sprite profileSprite;
            [SerializeField] private Color borderColor =
                new Color(0.18f, 0.58f, 0.86f, 1f);

            public string PlayerId => playerId;
            public string PlayerPresetId => playerPresetId;
            public Sprite ProfileSprite => profileSprite;
            public Color BorderColor => borderColor;
        }

        private sealed class PlayerTokenEntry
        {
            public readonly RectTransform Root;
            public readonly MoveRangeCircleGraphic Border;
            public readonly Image Portrait;
            public readonly TMP_Text Initials;
            public readonly Button Button;
            public readonly PlayerData Player;
            public readonly int PlayerIndex;

            public PlayerTokenEntry(
                RectTransform root,
                MoveRangeCircleGraphic border,
                Image portrait,
                TMP_Text initials,
                Button button,
                PlayerData player,
                int playerIndex)
            {
                Root = root;
                Border = border;
                Portrait = portrait;
                Initials = initials;
                Button = button;
                Player = player;
                PlayerIndex = playerIndex;
            }
        }

        private sealed class PlayerEntry
        {
            public readonly RectTransform Root;
            public readonly Button Button;
            public readonly TMP_Text Label;
            public readonly TMP_Text TabLabel;
            public readonly PlayerData Player;
            public readonly bool RightSide;
            public float CurrentSlideX;

            public PlayerEntry(
                RectTransform root,
                Button button,
                TMP_Text label,
                TMP_Text tabLabel,
                PlayerData player,
                bool rightSide)
            {
                Root = root;
                Button = button;
                Label = label;
                TabLabel = tabLabel;
                Player = player;
                RightSide = rightSide;
                CurrentSlideX = 0f;
            }
        }

        private sealed class CardSlotEntry
        {
            public readonly RectTransform Root;
            public readonly Button Button;
            public readonly Image Image;
            public readonly Image IconImage;
            public readonly TMP_Text Label;
            public readonly RectTransform BadgeRoot;
            public readonly BoardCardSlotData Slot;
            public readonly Dictionary<string, SearchCardBadgeEntry> Badges =
                new Dictionary<string, SearchCardBadgeEntry>();

            public bool IsHovered;
            public BoardCardSlotData HoveredBadgeSlot;
            public Vector2 PointerNormalized;
            public float CurrentSize;
            public float CurrentHoverAmount;
            public Vector2 CurrentTilt;
            public float CurrentRoll;
            public float HoverKickRoll;
            public float CameraDistanceSqr;

            public CardSlotEntry(
                RectTransform root,
                Button button,
                Image image,
                Image iconImage,
                TMP_Text label,
                RectTransform badgeRoot,
                BoardCardSlotData slot,
                float initialSize)
            {
                Root = root;
                Button = button;
                Image = image;
                IconImage = iconImage;
                Label = label;
                BadgeRoot = badgeRoot;
                Slot = slot;
                CurrentSize = initialSize;
                CurrentHoverAmount = 0f;
                CurrentTilt = Vector2.zero;
                CurrentRoll = 0f;
                HoverKickRoll = 0f;
                CameraDistanceSqr = float.MaxValue;
            }
        }

        private sealed class DistrictMarkerEntry
        {
            public readonly string DistrictId;
            public readonly RectTransform Root;
            public readonly Image Icon;
            public readonly TMP_Text FallbackLabel;
            public readonly List<BoardSpacePrefab> Buildings;

            public DistrictMarkerEntry(
                string districtId,
                RectTransform root,
                Image icon,
                TMP_Text fallbackLabel,
                List<BoardSpacePrefab> buildings)
            {
                DistrictId = districtId;
                Root = root;
                Icon = icon;
                FallbackLabel = fallbackLabel;
                Buildings = buildings != null
                    ? new List<BoardSpacePrefab>(buildings)
                    : new List<BoardSpacePrefab>();
            }
        }

        private sealed class SearchCardBadgeEntry
        {
            public readonly RectTransform Root;
            public readonly Button Button;
            public readonly Image Image;
            public readonly TMP_Text Label;
            public readonly BoardCardSlotData Slot;

            public bool IsHovered;
            public float CurrentScale = 1f;

            public SearchCardBadgeEntry(
                RectTransform root,
                Button button,
                Image image,
                TMP_Text label,
                BoardCardSlotData slot)
            {
                Root = root;
                Button = button;
                Image = image;
                Label = label;
                Slot = slot;
            }
        }

        private sealed class MoveEntry
        {
            public readonly RectTransform Root;
            public readonly Button Button;
            public readonly TMP_Text Label;
            public readonly BoardSpacePrefab Space;
            public readonly float Cost;

            public MoveEntry(
                RectTransform root,
                Button button,
                TMP_Text label,
                BoardSpacePrefab space,
                float cost)
            {
                Root = root;
                Button = button;
                Label = label;
                Space = space;
                Cost = cost;
            }
        }

        private struct MoveCostRecord
        {
            public BoardSpacePrefab Space;
            public float Cost;

            public MoveCostRecord(BoardSpacePrefab space, float cost)
            {
                Space = space;
                Cost = cost;
            }
        }
    }
}

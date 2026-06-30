using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public enum BoardCardSlotKind
    {
        BuildingInformation,
        SearchResult
    }

    [Serializable]
    public sealed class BuildingInformationDefinition
    {
        [Tooltip("비워두면 모든 지구에 공통 적용됩니다. DistrictPrefab의 District Name과 비교합니다.")]
        [SerializeField] private string districtNameFilter;
        [SerializeField] private BuildingType buildingType = BuildingType.Generic;
        [SerializeField] private string displayName;
        [TextArea(2, 6)]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        public string DistrictNameFilter => districtNameFilter;
        public BuildingType BuildingType => buildingType;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
    }

    [Serializable]
    public sealed class SearchCardPresentationDefinition
    {
        [Tooltip("표현 정보를 덮어쓸 탐색 카드 원본입니다.")]
        [SerializeField] private CardSOBJ cardDefinition;
        [Tooltip("비워두면 CardSOBJ의 DisplayName을 사용합니다.")]
        [SerializeField] private string displayNameOverride;
        [Tooltip("원격 호버/클릭에서 보여줄 짧은 설명입니다.")]
        [TextArea(1, 3)]
        [SerializeField] private string summary;
        [Tooltip("해당 건물에 있는 캐릭터가 확인할 상세 설명입니다.")]
        [TextArea(2, 8)]
        [SerializeField] private string detailedDescription;
        [SerializeField] private Sprite icon;

        public CardSOBJ CardDefinition => cardDefinition;
        public string DisplayNameOverride => displayNameOverride;
        public string Summary => summary;
        public string DetailedDescription => detailedDescription;
        public Sprite Icon => icon;
    }

    [Serializable]
    public class BoardCardSlotData
    {
        [SerializeField] private string slotId;
        [SerializeField] private string nodeId;
        [SerializeField] private int slotIndex;
        [SerializeField] private BoardCardSlotKind slotKind;
        [SerializeField] private CardSOBJ cardDefinition;
        [SerializeField] private string displayNameOverride;
        [TextArea(1, 4)]
        [SerializeField] private string summaryOverride;
        [TextArea(2, 8)]
        [SerializeField] private string descriptionOverride;
        [SerializeField] private Sprite iconOverride;
        [SerializeField] private bool revealed;
        [SerializeField] private bool resolved;

        public string SlotId => slotId;
        public string NodeId => nodeId;
        public int SlotIndex => slotIndex;
        public BoardCardSlotKind SlotKind => slotKind;
        public CardSOBJ CardDefinition => cardDefinition;
        public Sprite IconSprite => iconOverride;
        public bool Revealed => revealed;
        public bool Resolved => resolved;
        public bool IsBuildingInformation =>
            slotKind == BoardCardSlotKind.BuildingInformation;
        public bool IsSearchResult =>
            slotKind == BoardCardSlotKind.SearchResult;
        public bool HasCard =>
            IsBuildingInformation || cardDefinition != null;
        public bool IsVisibleOnBoard =>
            IsBuildingInformation || revealed;
        public bool CanReveal =>
            IsSearchResult && HasCard && !revealed;
        public bool CanResolve =>
            IsSearchResult && revealed && !resolved;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(displayNameOverride))
                {
                    return displayNameOverride;
                }

                if (cardDefinition == null)
                {
                    return IsBuildingInformation ? "건물 정보" : "빈 슬롯";
                }

                return string.IsNullOrWhiteSpace(cardDefinition.DisplayName)
                    ? cardDefinition.name
                    : cardDefinition.DisplayName;
            }
        }

        /// <summary>
        /// 보드 밖이나 다른 위치에서도 볼 수 있는 최소 정보입니다.
        /// 카드 효과나 상세 사건 내용은 포함하지 않습니다.
        /// </summary>
        public string Summary
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(summaryOverride))
                {
                    return summaryOverride;
                }

                if (IsBuildingInformation)
                {
                    return "건물 종류와 발견된 카드 수를 확인할 수 있습니다.";
                }

                return DisplayName + " 카드가 발견되어 있습니다.";
            }
        }

        /// <summary>
        /// 현재 캐릭터가 해당 건물에 있을 때 표시할 상세 정보입니다.
        /// </summary>
        public string Description
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(descriptionOverride))
                {
                    return descriptionOverride;
                }

                return IsBuildingInformation
                    ? "건물 정보를 확인할 수 있습니다."
                    : "카드 상세 설명은 CardManager의 Search Card Presentation에서 설정할 수 있습니다.";
            }
        }

        private BoardCardSlotData()
        {
        }

        public static BoardCardSlotData CreateBuildingInformation(
            string runtimeSlotId,
            string runtimeNodeId,
            string runtimeDisplayName,
            string runtimeDescription,
            Sprite runtimeIcon)
        {
            return new BoardCardSlotData
            {
                slotId = runtimeSlotId ?? string.Empty,
                nodeId = runtimeNodeId ?? string.Empty,
                slotIndex = 0,
                slotKind = BoardCardSlotKind.BuildingInformation,
                cardDefinition = null,
                displayNameOverride = runtimeDisplayName ?? string.Empty,
                summaryOverride = string.Empty,
                descriptionOverride = runtimeDescription ?? string.Empty,
                iconOverride = runtimeIcon,
                revealed = true,
                resolved = false
            };
        }

        public static BoardCardSlotData CreateSearchResult(
            string runtimeSlotId,
            string runtimeNodeId,
            int runtimeSlotIndex,
            CardSOBJ runtimeCardDefinition,
            string runtimeDisplayName,
            string runtimeSummary,
            string runtimeDescription,
            Sprite runtimeIcon)
        {
            return new BoardCardSlotData
            {
                slotId = runtimeSlotId ?? string.Empty,
                nodeId = runtimeNodeId ?? string.Empty,
                slotIndex = Mathf.Max(0, runtimeSlotIndex),
                slotKind = BoardCardSlotKind.SearchResult,
                cardDefinition = runtimeCardDefinition,
                displayNameOverride = runtimeDisplayName ?? string.Empty,
                summaryOverride = runtimeSummary ?? string.Empty,
                descriptionOverride = runtimeDescription ?? string.Empty,
                iconOverride = runtimeIcon,
                revealed = false,
                resolved = false
            };
        }

        public void Reveal()
        {
            if (CanReveal)
            {
                revealed = true;
            }
        }

        public void Resolve()
        {
            if (CanResolve)
            {
                resolved = true;
            }
        }
    }

    public class CardManager : MonoBehaviour
    {
        [Header("Search Card Pool")]
        [SerializeField] private CardSOBJ[] searchCardPool;

        [Header("Search Card Presentation")]
        [Tooltip("탐색 카드별 짧은 요약, 상세 설명, 아이콘을 지정합니다. 등록하지 않아도 기본 문구로 동작합니다.")]
        [SerializeField] private SearchCardPresentationDefinition[] searchCardPresentationDefinitions;

        [Header("Building Information Cards")]
        [Tooltip("건물 종류별로 항상 표시할 기본 카드의 이름, 설명, 아이콘을 지정합니다.")]
        [SerializeField] private BuildingInformationDefinition[] buildingInformationDefinitions;

        [Header("Hidden Search Cards")]
        [Tooltip("탐색할 때마다 기본 건물 카드 위에 작은 카드 배지로 한 장씩 공개됩니다.")]
        [SerializeField, Range(1, 6)] private int hiddenSearchCardsPerBuilding = 3;
        [SerializeField] private bool populateEveryBuilding = true;

        public event Action OnBoardSlotsReset;
        public event Action<BoardCardSlotData> OnBoardSlotChanged;

        private readonly Dictionary<string, List<BoardCardSlotData>> slotsByNodeId =
            new Dictionary<string, List<BoardCardSlotData>>();

        private readonly List<BoardCardSlotData> allBoardSlots =
            new List<BoardCardSlotData>();

        private CardSOBJ runtimeFallbackCard;
        private System.Random random;

        public IList<BoardCardSlotData> AllBoardSlots => allBoardSlots.AsReadOnly();

        public void InitializeBoardSlots(
            GridManager gridManager,
            System.Random runtimeRandom)
        {
            slotsByNodeId.Clear();
            allBoardSlots.Clear();
            random = runtimeRandom ?? new System.Random(Environment.TickCount);

            if (gridManager != null && populateEveryBuilding)
            {
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

                    CreateSlotsForBuilding(building);
                }
            }

            OnBoardSlotsReset?.Invoke();
        }

        public CardSOBJ DrawRandomSearchCard(System.Random runtimeRandom)
        {
            System.Random drawRandom =
                runtimeRandom ??
                random ??
                new System.Random(Environment.TickCount);

            if (searchCardPool != null &&
                searchCardPool.Length > 0)
            {
                int startIndex =
                    drawRandom.Next(0, searchCardPool.Length);

                for (int offset = 0;
                     offset < searchCardPool.Length;
                     offset++)
                {
                    int index =
                        (startIndex + offset) %
                        searchCardPool.Length;

                    if (searchCardPool[index] != null)
                    {
                        return searchCardPool[index];
                    }
                }
            }

            if (runtimeFallbackCard == null)
            {
                runtimeFallbackCard =
                    ScriptableObject.CreateInstance<CardSOBJ>();

                runtimeFallbackCard.name = "TEST-SUPPLY";
                runtimeFallbackCard.SetRuntimeFallback(
                    "TEST-SUPPLY",
                    "테스트 보급품",
                    "카드 SOBJ가 없어 자동 생성된 테스트 카드입니다.");
            }

            return runtimeFallbackCard;
        }

        public IList<BoardCardSlotData> GetSlotsAtNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return Array.Empty<BoardCardSlotData>();
            }

            List<BoardCardSlotData> slots;

            return slotsByNodeId.TryGetValue(nodeId, out slots)
                ? slots.AsReadOnly()
                : Array.Empty<BoardCardSlotData>();
        }

        public bool TryGetBuildingInformationSlot(
            string nodeId,
            out BoardCardSlotData slot)
        {
            slot = null;
            List<BoardCardSlotData> slots;

            if (string.IsNullOrWhiteSpace(nodeId) ||
                !slotsByNodeId.TryGetValue(nodeId, out slots))
            {
                return false;
            }

            for (int index = 0; index < slots.Count; index++)
            {
                BoardCardSlotData candidate = slots[index];

                if (candidate != null && candidate.IsBuildingInformation)
                {
                    slot = candidate;
                    return true;
                }
            }

            return false;
        }

        public int GetRevealedSearchCardCount(string nodeId)
        {
            int count = 0;
            IList<BoardCardSlotData> slots = GetSlotsAtNode(nodeId);

            for (int index = 0; index < slots.Count; index++)
            {
                BoardCardSlotData slot = slots[index];

                if (slot != null && slot.IsSearchResult && slot.Revealed)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetHiddenSearchCardCount(string nodeId)
        {
            int count = 0;
            IList<BoardCardSlotData> slots = GetSlotsAtNode(nodeId);

            for (int index = 0; index < slots.Count; index++)
            {
                BoardCardSlotData slot = slots[index];

                if (slot != null && slot.IsSearchResult && !slot.Revealed)
                {
                    count++;
                }
            }

            return count;
        }

        public bool TryGetRevealableSlot(
            string nodeId,
            out BoardCardSlotData slot)
        {
            slot = null;

            List<BoardCardSlotData> slots;

            if (string.IsNullOrWhiteSpace(nodeId) ||
                !slotsByNodeId.TryGetValue(nodeId, out slots))
            {
                return false;
            }

            for (int index = 0; index < slots.Count; index++)
            {
                BoardCardSlotData candidate = slots[index];

                if (candidate != null && candidate.CanReveal)
                {
                    slot = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool RevealSlot(BoardCardSlotData slot)
        {
            if (slot == null || !slot.CanReveal)
            {
                return false;
            }

            slot.Reveal();
            OnBoardSlotChanged?.Invoke(slot);
            return true;
        }

        public bool ResolveSlot(BoardCardSlotData slot)
        {
            if (slot == null || !slot.CanResolve)
            {
                return false;
            }

            slot.Resolve();
            OnBoardSlotChanged?.Invoke(slot);
            return true;
        }

        public bool TryGetSlot(
            string slotId,
            out BoardCardSlotData slot)
        {
            slot = null;

            if (string.IsNullOrWhiteSpace(slotId))
            {
                return false;
            }

            for (int index = 0; index < allBoardSlots.Count; index++)
            {
                BoardCardSlotData candidate = allBoardSlots[index];

                if (candidate != null &&
                    candidate.SlotId == slotId)
                {
                    slot = candidate;
                    return true;
                }
            }

            return false;
        }

        private void CreateSlotsForBuilding(BoardSpacePrefab building)
        {
            if (building == null ||
                string.IsNullOrWhiteSpace(building.NodeId))
            {
                return;
            }

            List<BoardCardSlotData> slots =
                new List<BoardCardSlotData>();

            BuildingInformationDefinition definition =
                FindBuildingInformationDefinition(
                    building.BuildingType,
                    building.DistrictName);

            string buildingName =
                definition != null &&
                !string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? definition.DisplayName
                    : GetDefaultBuildingName(
                        building.BuildingType,
                        building.DistrictType);

            string districtName =
                string.IsNullOrWhiteSpace(building.DistrictName)
                    ? building.DistrictId
                    : building.DistrictName;

            string districtTypeName =
                string.IsNullOrWhiteSpace(building.DistrictDisplayName)
                    ? DistrictPrefab.GetDistrictTypeDisplayName(
                        building.DistrictType)
                    : building.DistrictDisplayName;

            string cardDisplayName =
                districtTypeName + "\n" + buildingName;

            string description =
                definition != null &&
                !string.IsNullOrWhiteSpace(definition.Description)
                    ? definition.Description
                    : GetDefaultBuildingDescription(building.BuildingType);

            string fullDescription =
                "지구 종류: " + districtTypeName + "\n" +
                "지구 이름: " + districtName + "\n" +
                "건물 종류: " + buildingName + "\n\n" +
                description;

            BoardCardSlotData informationSlot =
                BoardCardSlotData.CreateBuildingInformation(
                    building.NodeId + "_INFO",
                    building.NodeId,
                    cardDisplayName,
                    fullDescription,
                    definition != null && definition.Icon != null
                        ? definition.Icon
                        : building.DistrictIcon);

            slots.Add(informationSlot);
            allBoardSlots.Add(informationSlot);

            int searchCount =
                Mathf.Clamp(hiddenSearchCardsPerBuilding, 1, 6);

            for (int slotIndex = 0;
                 slotIndex < searchCount;
                 slotIndex++)
            {
                CardSOBJ card = DrawRandomSearchCard(random);
                SearchCardPresentationDefinition presentation =
                    FindSearchCardPresentation(card);

                string displayName =
                    presentation != null &&
                    !string.IsNullOrWhiteSpace(presentation.DisplayNameOverride)
                        ? presentation.DisplayNameOverride
                        : card != null &&
                          !string.IsNullOrWhiteSpace(card.DisplayName)
                            ? card.DisplayName
                            : card != null
                                ? card.name
                                : "미확인 카드";

                string summary =
                    presentation != null &&
                    !string.IsNullOrWhiteSpace(presentation.Summary)
                        ? presentation.Summary
                        : displayName + " 카드가 이 건물에서 발견되어 있습니다.";

                string detailedDescription =
                    presentation != null &&
                    !string.IsNullOrWhiteSpace(presentation.DetailedDescription)
                        ? presentation.DetailedDescription
                        : displayName +
                          " 카드의 상세 효과입니다. " +
                          "실제 카드 효과 데이터는 별도의 해결 시스템과 연결하십시오.";

                BoardCardSlotData searchSlot =
                    BoardCardSlotData.CreateSearchResult(
                        building.NodeId + "_SEARCH_" +
                        (slotIndex + 1).ToString("00"),
                        building.NodeId,
                        slotIndex + 1,
                        card,
                        displayName,
                        summary,
                        detailedDescription,
                        presentation != null ? presentation.Icon : null);

                slots.Add(searchSlot);
                allBoardSlots.Add(searchSlot);
            }

            slotsByNodeId[building.NodeId] = slots;
        }

        private SearchCardPresentationDefinition FindSearchCardPresentation(
            CardSOBJ card)
        {
            if (card == null || searchCardPresentationDefinitions == null)
            {
                return null;
            }

            for (int index = 0;
                 index < searchCardPresentationDefinitions.Length;
                 index++)
            {
                SearchCardPresentationDefinition definition =
                    searchCardPresentationDefinitions[index];

                if (definition != null &&
                    definition.CardDefinition == card)
                {
                    return definition;
                }
            }

            return null;
        }

        private BuildingInformationDefinition FindBuildingInformationDefinition(
            BuildingType buildingType,
            string districtName)
        {
            if (buildingInformationDefinitions == null)
            {
                return null;
            }

            BuildingInformationDefinition commonDefinition = null;

            for (int index = 0;
                 index < buildingInformationDefinitions.Length;
                 index++)
            {
                BuildingInformationDefinition definition =
                    buildingInformationDefinitions[index];

                if (definition == null ||
                    !EqualityComparer<BuildingType>.Default.Equals(
                        definition.BuildingType,
                        buildingType))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(definition.DistrictNameFilter))
                {
                    if (string.Equals(
                            definition.DistrictNameFilter.Trim(),
                            districtName != null ? districtName.Trim() : string.Empty,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return definition;
                    }

                    continue;
                }

                if (commonDefinition == null)
                {
                    commonDefinition = definition;
                }
            }

            return commonDefinition;
        }

        private static string GetDefaultBuildingName(
            BuildingType buildingType,
            DistrictType districtType)
        {
            string raw = buildingType.ToString();
            string normalized = raw.ToLowerInvariant();

            if (normalized.Contains("apartment")) return "아파트";
            if (normalized.Contains("hospital")) return "병원";
            if (normalized.Contains("shopping") || normalized.Contains("mall")) return "쇼핑몰";
            if (normalized.Contains("office")) return "사무실";
            if (normalized.Contains("industrial") || normalized.Contains("factory")) return "산업 시설";
            if (normalized.Contains("civic") || normalized.Contains("public")) return "공공 시설";
            if (normalized.Contains("police")) return "경찰서";
            if (normalized.Contains("fire")) return "소방서";
            if (normalized.Contains("school")) return "학교";
            if (normalized.Contains("mart") || normalized.Contains("market")) return "상점";

            if (raw == "Generic")
            {
                string district =
                    districtType.ToString().ToLowerInvariant();

                if (district.Contains("residential") || district.Contains("housing"))
                    return "주거 건물";
                if (district.Contains("commercial") || district.Contains("shopping"))
                    return "상업 시설";
                if (district.Contains("medical") || district.Contains("hospital"))
                    return "의료 시설";
                if (district.Contains("industrial") || district.Contains("factory"))
                    return "산업 시설";
                if (district.Contains("civic") || district.Contains("public"))
                    return "공공 시설";
                if (district.Contains("mixed"))
                    return "복합 건물";

                return "일반 건물";
            }

            return raw;
        }

        private static string GetDefaultBuildingDescription(BuildingType buildingType)
        {
            string normalized = buildingType.ToString().ToLowerInvariant();

            if (normalized.Contains("apartment"))
                return "주거 밀집 건물입니다. 생활 물자와 생존자를 발견할 가능성이 있습니다.";
            if (normalized.Contains("hospital"))
                return "의료 시설입니다. 의약품과 치료 관련 자원을 찾을 가능성이 있습니다.";
            if (normalized.Contains("shopping") || normalized.Contains("mall"))
                return "상업 시설입니다. 다양한 소비재가 남아 있을 가능성이 있습니다.";
            if (normalized.Contains("office"))
                return "업무 시설입니다. 문서, 전자 장비, 열쇠류를 발견할 가능성이 있습니다.";
            if (normalized.Contains("industrial") || normalized.Contains("factory"))
                return "산업 시설입니다. 공구, 연료, 기계 부품을 발견할 가능성이 있습니다.";
            if (normalized.Contains("civic") || normalized.Contains("public"))
                return "공공 시설입니다. 도시 정보와 특수 설비가 남아 있을 가능성이 있습니다.";

            return "건물 내부를 탐색하면 숨겨진 카드가 한 장씩 공개됩니다.";
        }
    }
}

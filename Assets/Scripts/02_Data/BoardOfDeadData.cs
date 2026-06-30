using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    // =====================================================================
    // 00_Common/GameEnums.cs
    // =====================================================================

    public enum GamePhase
    {
        None,
        Setup,
        RadioBroadcast,
        PlayerTurnQueue,
        Environment,
        Doom,
        EndCheck,
        GameOver
    }

    public enum PlayerTurnPhase
    {
        None,
        TurnStart,
        MoveSelect,
        Moving,
        NodeEnter,
        CardReveal,
        SanCheck,
        ActionSelect,
        ResolvingAction,
        TurnEnd
    }

    public enum PlayerLifeState
    {
        Survivor,
        Infected,
        Incapacitated,
        Escaped,
        Dead
    }

    public enum BoardSpaceType
    {
        Empty,
        Road,
        Building
    }

    public enum BuildingType
    {
        Generic,
        Residential,
        Commercial,
        Industrial,
        Hospital,
        PoliceStation,
        FireStation,
        GasStation,
        Warehouse,
        Market,
        PublicFacility,
        Other
    }

    public enum CardType
    {
        Zombie,
        Crisis,
        Supply,
        Story,
        EscapeRoute,
        Vehicle
    }

    public enum DistrictType
    {
        Residential,
        Commercial,
        Industrial,
        Medical,
        Government,
        Mixed
    }

    public enum BoardNodeType
    {
        ResidentialBuilding,
        CommercialBuilding,
        IndustrialBuilding,
        Hospital,
        PoliceStation,
        FireStation,
        GasStation,
        Warehouse,
        Market,
        PublicFacility,
        ParkingArea,
        Other
    }

    public enum BoardConnectionType
    {
        NormalRoad,
        ElevatedRoad
    }

    public enum ItemType
    {
        General,
        Consumable,
        Weapon,
        Ammo,
        RepairPart,
        Fuel,
        EscapeMaterial,
        Quest
    }

    public enum VehicleType
    {
        Sedan,
        Van,
        PickupTruck,
        Motorcycle,
        UtilityVehicle,
        Other
    }

    public enum EscapeRouteType
    {
        Helicopter,
        ArmoredVehicle,
        LightAircraft,
        Sewer
    }

    public enum LogCategory
    {
        System,
        Round,
        Radio,
        Turn,
        Movement,
        Search,
        Encounter,
        Card,
        Vehicle,
        Escape,
        Environment,
        Doom,
        Sanity
    }

    public enum DistrictDirection
    {
        North,
        East,
        South,
        West
    }

    public enum DistrictLinkDirection
    {
        East,
        North
    }

    [Flags]
    public enum RoadConnectionMask
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3
    }

    // =====================================================================
    // 00_Common/ItemRequirementSOBJEntry.cs
    // =====================================================================

    [Serializable]
    public class ItemRequirementSOBJEntry
    {
        [SerializeField] private ItemSOBJ item;
        [SerializeField, Min(1)] private int requiredAmount = 1;

        public ItemSOBJ Item => item;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);
    }

    // =====================================================================
    // 02_Data/BoardConnectionData.cs
    // =====================================================================

    /// <summary>
    /// 고정 보드 프리팹에서 읽어 온 노드 간 연결의 런타임 데이터입니다.
    /// </summary>
    [Serializable]
    public class BoardConnectionData
    {
        [SerializeField] private string connectionId;
        [SerializeField] private string fromNodeId;
        [SerializeField] private string toNodeId;
        [SerializeField] private BoardConnectionType connectionType;
        [SerializeField] private bool bidirectional;
        [SerializeField] private float movementAPCost;
        [SerializeField] private int visualTileLength;

        public string ConnectionId => connectionId;
        public string FromNodeId => fromNodeId;
        public string ToNodeId => toNodeId;
        public BoardConnectionType ConnectionType => connectionType;
        public bool Bidirectional => bidirectional;
        public float MovementAPCost => Mathf.Max(0.1f, movementAPCost);
        public int VisualTileLength => Mathf.Max(1, visualTileLength);

        public BoardConnectionData(BoardConnectionPrefab source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            connectionId = source.ConnectionId;
            fromNodeId = source.FromNodeId;
            toNodeId = source.ToNodeId;
            connectionType = source.ConnectionType;
            bidirectional = source.Bidirectional;
            movementAPCost = source.MovementAPCost;
            visualTileLength = source.VisualTileLength;
        }

        public bool Connects(string startNodeId, string destinationNodeId)
        {
            if (fromNodeId == startNodeId && toNodeId == destinationNodeId)
            {
                return true;
            }

            return bidirectional &&
                   fromNodeId == destinationNodeId &&
                   toNodeId == startNodeId;
        }

        public string GetOtherNodeId(string nodeId)
        {
            if (fromNodeId == nodeId)
            {
                return toNodeId;
            }

            if (bidirectional && toNodeId == nodeId)
            {
                return fromNodeId;
            }

            return string.Empty;
        }
    }

    // =====================================================================
    // 02_Data/BoardRuntimeData.cs
    // =====================================================================

    [Serializable]
    public class DistrictRuntimeData
    {
        public string districtId;
        public string districtName;
        public DistrictType districtType;
        public int threat;
        public int noise;
        public int zombieCount;
        public bool hasHorde;
        public List<string> flags = new List<string>();
        public List<string> completedEventIds = new List<string>();
    }

    [Serializable]
    public class BuildingRuntimeData
    {
        public string nodeId;
        public string districtId;
        public string buildingDefinitionId;
        public string buildingTypeId;
        public string buildingRoleId;
        public string buildingState = "Normal";
        public int searchCount;
        public List<string> tags = new List<string>();
        public List<string> flags = new List<string>();
        public List<string> completedEventIds = new List<string>();
    }

    // =====================================================================
    // 02_Data/BuildingEventData.cs
    // =====================================================================

    public enum BuildingEventRepeatPolicy
    {
        Always,
        OncePerBuilding,
        OncePerDistrict,
        OncePerGame,
        OncePerPlayer,
        CooldownRounds
    }

    public enum BuildingEventDifficulty
    {
        Easy,
        Normal,
        Hard,
        Extreme
    }

    public enum BuildingEventSuccessLevel
    {
        None = 0,
        CriticalFailure = 1,
        Failure = 2,
        NormalSuccess = 3,
        HardSuccess = 4,
        ExtremeSuccess = 5
    }

    public enum BuildingEventAbilityType
    {
        Strength,
        Intelligence,
        Search,
        Combat,
        Body,
        Dexterity,
        Willpower,
        Custom1,
        Custom2
    }

    public enum BuildingEventConditionType
    {
        BuildingDefinitionId,
        BuildingTypeId,
        BuildingRoleId,
        BuildingTag,
        BuildingState,
        DistrictId,
        DistrictType,
        MinimumRound,
        MaximumRound,
        MinimumThreat,
        MaximumThreat,
        HasFlag,
        MissingFlag,
        MinimumAbility,
        MaximumAbility,
        HasTrait,
        HasItem,
        MissingItem,
        HasStatus,
        MissingStatus,
        MinimumAP,
        MinimumHealth,
        MaximumHealth,
        MinimumInfection,
        MaximumInfection,
        MinimumStress,
        MaximumStress
    }

    public enum BuildingEventEffectType
    {
        ChangeHealth,
        AddBleeding,
        RemoveBleeding,
        ChangeInfection,
        ChangeStress,
        ChangeSupplies,
        AddItem,
        RemoveItem,
        AddRandomItem,
        ChangeNoise,
        SpawnZombies,
        ChangeDistrictThreat,
        ChangeBuildingState,
        AddFlag,
        RemoveFlag,
        ScheduleFollowUp
    }

    public enum BuildingEventFollowUpTrigger
    {
        Immediate,
        NextRound,
        AfterRounds,
        RevisitBuilding,
        ReenterDistrict,
        ThreatAtLeast,
        FlagAdded
    }

    public enum BuildingEventUnavailableChoiceMode
    {
        Hide,
        Disable
    }

    [Serializable]
    public class BuildingEventConditionData
    {
        [Tooltip("검사할 조건 종류입니다.")]
        public BuildingEventConditionType conditionType;

        [Tooltip("ID, 태그, 상태명 등에 사용하는 문자열 값입니다.")]
        public string stringValue;

        [Tooltip("라운드, 위협도, 능력치 등 정수형 비교 값입니다.")]
        public int intValue;

        [Tooltip("AP처럼 소수점이 필요한 비교 값입니다.")]
        public float floatValue;

        [Tooltip("능력치 조건에서 사용할 능력치입니다.")]
        public BuildingEventAbilityType abilityType;
    }

    [Serializable]
    public class BuildingEventItemRequirement
    {
        public string itemId;
        [Min(1)] public int amount = 1;
    }

    [Serializable]
    public class BuildingEventFollowUpData
    {
        [Tooltip("Database에 등록된 후속 Archetype의 Event ID입니다.")]
        public string followUpEventId;

        public BuildingEventFollowUpTrigger trigger =
            BuildingEventFollowUpTrigger.NextRound;

        [Min(0)] public int delayRounds = 1;
        [Min(0)] public int expiryAfterRounds = 5;
        public bool priorityExecution = true;
        public bool discardWhenConditionFails;

        [Tooltip("ThreatAtLeast 트리거에서 사용하는 최소 위협도입니다.")]
        public int threatThreshold;

        [Tooltip("FlagAdded 트리거에서 사용하는 플래그 ID입니다.")]
        public string flagId;

        public bool targetCurrentPlayer = true;
        public bool targetCurrentBuilding = true;
        public bool targetCurrentDistrict = true;
    }

    [Serializable]
    public class BuildingEventEffectData
    {
        public BuildingEventEffectType effectType;

        [Tooltip("같은 결과 안에서 낮은 우선순위부터 적용됩니다.")]
        public int priority;

        [Tooltip("체력, 감염, 스트레스, 물자, 소음, 위협도, 좀비 수 등에 사용합니다.")]
        public int amount;

        [Tooltip("아이템, 상태, 건물 상태, 플래그 ID 등에 사용합니다.")]
        public string id;

        [Tooltip("AddRandomItem에서 무작위 후보로 사용합니다.")]
        public List<string> candidateIds = new List<string>();

        [Tooltip("ScheduleFollowUp에서 사용합니다.")]
        public BuildingEventFollowUpData followUp;
    }

    [Serializable]
    public class BuildingEventResultData
    {
        [TextArea(2, 8)] public string resultText;
        public List<BuildingEventEffectData> effects =
            new List<BuildingEventEffectData>();

        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrWhiteSpace(resultText) &&
                       (effects == null || effects.Count == 0);
            }
        }
    }

    [Serializable]
    public class BuildingEventChoiceData
    {
        [Header("Identity")]
        public string choiceId;
        [TextArea(1, 4)] public string displayText;
        [TextArea(1, 3)] public string conditionDescription;

        [Header("Availability")]
        public BuildingEventUnavailableChoiceMode unavailableMode =
            BuildingEventUnavailableChoiceMode.Disable;
        public List<BuildingEventConditionData> conditions =
            new List<BuildingEventConditionData>();
        [Min(0f)] public float apCost;
        [Min(0)] public int suppliesCost;
        public List<BuildingEventItemRequirement> requiredItems =
            new List<BuildingEventItemRequirement>();
        public List<BuildingEventItemRequirement> consumedItems =
            new List<BuildingEventItemRequirement>();
        public List<string> requiredTraits = new List<string>();
        public List<string> requiredFlags = new List<string>();
        public List<string> forbiddenFlags = new List<string>();

        [Header("Check")]
        public bool useCheck = true;
        public BuildingEventAbilityType abilityType =
            BuildingEventAbilityType.Search;
        public BuildingEventDifficulty difficulty =
            BuildingEventDifficulty.Normal;
        [Range(-20, 20)] public int situationModifier;
        public bool showSuccessProbability = true;

        [Header("Push")]
        public bool allowPush = true;
        [Min(0f)] public float pushAPCost = 1f;
        public bool useActualSuccessLevelOnPush = true;

        [Header("Results")]
        public BuildingEventResultData normalSuccess =
            new BuildingEventResultData();
        public BuildingEventResultData hardSuccess =
            new BuildingEventResultData();
        public BuildingEventResultData extremeSuccess =
            new BuildingEventResultData();
        public BuildingEventResultData failure =
            new BuildingEventResultData();

        [Tooltip("대실패 시 기본 실패 결과에 추가됩니다.")]
        public BuildingEventResultData criticalFailureAdditional =
            new BuildingEventResultData();

        [Tooltip("밀어붙이기 실패 시 기본 실패 결과에 추가됩니다.")]
        public BuildingEventResultData pushFailureAdditional =
            new BuildingEventResultData();
    }

    [Serializable]
    public class BuildingEventChoiceOverrideData
    {
        public string choiceId;
        public bool overrideDisplayText;
        [TextArea(1, 4)] public string displayText;

        public bool overrideNormalSuccess;
        public BuildingEventResultData normalSuccess =
            new BuildingEventResultData();

        public bool overrideHardSuccess;
        public BuildingEventResultData hardSuccess =
            new BuildingEventResultData();

        public bool overrideExtremeSuccess;
        public BuildingEventResultData extremeSuccess =
            new BuildingEventResultData();

        public bool overrideFailure;
        public BuildingEventResultData failure =
            new BuildingEventResultData();

        public bool overrideCriticalFailureAdditional;
        public BuildingEventResultData criticalFailureAdditional =
            new BuildingEventResultData();

        public bool overridePushFailureAdditional;
        public BuildingEventResultData pushFailureAdditional =
            new BuildingEventResultData();
    }

    [Serializable]
    public class BuildingEventContext
    {
        public PlayerData player;
        public BoardSpacePrefab boardSpace;
        public BoardCardSlotData cardSlot;
        public BuildingBoardPrefab building;
        public string playerId;
        public string nodeId;
        public string districtId;
        public DistrictType districtType;
        public string buildingDefinitionId;
        public string buildingTypeId;
        public string buildingRoleId;
        public List<string> buildingTags = new List<string>();
        public string buildingState;
        public int roundNumber;
        public int districtThreat;
    }

    public sealed class BuildingEventCheckResult
    {
        public int BaseAbility;
        public int SituationModifier;
        public int FinalAbility;
        public int NormalThreshold;
        public int HardThreshold;
        public int ExtremeThreshold;
        public int Roll;
        public int SuccessProbability;
        public BuildingEventDifficulty Difficulty;
        public BuildingEventSuccessLevel SuccessLevel;
        public bool MeetsDifficulty;
        public bool IsCriticalFailure;
    }

    public sealed class ResolvedBuildingEventChoice
    {
        public BuildingEventChoiceData Source;
        public BuildingEventChoiceOverrideData Override;
        public string DisplayText;
        public bool Available;
        public string UnavailableReason;
        public int SuccessProbability;
    }

    public sealed class PreparedBuildingEvent
    {
        public BuildingEventContext Context;
        public BuildingEventArchetypeSOBJ Archetype;
        public BuildingEventVariantSOBJ Variant;
        public string Title;
        public string Body;
        public Sprite Illustration;
        public string IllustrationId;
        public List<ResolvedBuildingEventChoice> Choices =
            new List<ResolvedBuildingEventChoice>();
    }

    // =====================================================================
    // 02_Data/BuildingEventRuntimeData.cs
    // =====================================================================

    public interface IBuildingEventRuntimeService
    {
        int GetAbility(PlayerData player, BuildingEventAbilityType abilityType);
        int GetHealth(PlayerData player);
        int GetInfection(PlayerData player);
        int GetStress(PlayerData player);
        float GetAP(PlayerData player);
        int GetSupplies();
        bool HasTrait(PlayerData player, string traitId);
        bool HasItem(PlayerData player, string itemId, int amount);
        bool HasStatus(PlayerData player, string statusId);
        bool TrySpendAP(PlayerData player, float amount);
        bool TrySpendSupplies(int amount);
        bool TryRemoveItem(PlayerData player, string itemId, int amount);
        void ChangeHealth(PlayerData player, int amount);
        void ChangeInfection(PlayerData player, int amount);
        void ChangeStress(PlayerData player, int amount);
        void ChangeSupplies(int amount);
        void AddItem(PlayerData player, string itemId, int amount);
        void AddStatus(PlayerData player, string statusId);
        void RemoveStatus(PlayerData player, string statusId);
        void ChangeNoise(string districtId, int amount);
        void SpawnZombies(string districtId, int amount);
        void ChangeThreat(string districtId, int amount);
        void ChangeBuildingState(string nodeId, string stateId);
        string GetBuildingState(string nodeId);
        bool HasFlag(BuildingEventContext context, string flagId);
        void AddFlag(BuildingEventContext context, string flagId);
        void RemoveFlag(BuildingEventContext context, string flagId);
    }

    [Serializable]
    public class PlayerEventProfileBinding
    {
        [Tooltip("비워두면 Default Profile을 사용합니다.")]
        public string playerId;
        public PlayerEventProfileSOBJ profile;
    }

    [Serializable]
    internal class PlayerEventRuntimeState
    {
        public string playerId;
        public int maximumHealth = 10;
        public int health = 10;
        public int infection;
        public int stress;
        public Dictionary<BuildingEventAbilityType, int> abilities =
            new Dictionary<BuildingEventAbilityType, int>();
        public HashSet<string> traits = new HashSet<string>();
        public HashSet<string> statuses = new HashSet<string>();
        public Dictionary<string, int> items =
            new Dictionary<string, int>();
    }

    // =====================================================================
    // 02_Data/BuildingEventSessionData.cs
    // =====================================================================

    [Serializable]
    public class ScheduledBuildingEvent
    {
        public string eventId;
        public BuildingEventFollowUpTrigger trigger;
        public int dueRound;
        public int expiryRound;
        public bool priorityExecution;
        public bool discardWhenConditionFails;
        public int threatThreshold;
        public string flagId;
        public string playerId;
        public string nodeId;
        public string districtId;
    }

    [Serializable]
    public class AssignedBuildingEvent
    {
        public string slotId;
        public string eventId;
        public string variantId;
        public bool resolved;
    }

    // =====================================================================
    // 02_Data/CardData.cs
    // =====================================================================

    [Serializable]
    public class CardData
    {
        [SerializeField] private string cardInstanceId;
        [SerializeField] private string cardSOBJId;
        [SerializeField] private string nodeId;
        [SerializeField] private string ownerPlayerId;
        [SerializeField] private bool revealed;
        [SerializeField] private bool resolved;

        public string CardInstanceId => cardInstanceId;
        public string CardSOBJId => cardSOBJId;
        public string NodeId => nodeId;
        public string OwnerPlayerId => ownerPlayerId;
        public bool Revealed => revealed;
        public bool Resolved => resolved;

        public CardData(string instanceId, string sobjId, string targetNodeId)
            : this(instanceId, sobjId, targetNodeId, string.Empty, false)
        {
        }

        public CardData(
            string instanceId,
            string sobjId,
            string targetNodeId,
            string playerId,
            bool isRevealed)
        {
            cardInstanceId = instanceId;
            cardSOBJId = sobjId;
            nodeId = targetNodeId;
            ownerPlayerId = playerId ?? string.Empty;
            revealed = isRevealed;
            resolved = false;
        }

        public void Reveal()
        {
            revealed = true;
        }

        public void Resolve()
        {
            resolved = true;
        }
    }

    // =====================================================================
    // 02_Data/DefaultPlayerSettingData.cs
    // =====================================================================

    /// <summary>
    /// 테스트 단계에서 PlayerPresetSOBJ 없이 기본 플레이어를 생성하기 위한 설정입니다.
    /// 기존 코드와 신규 코드 양쪽에서 사용할 수 있도록 호환 프로퍼티를 함께 제공합니다.
    /// </summary>
    [Serializable]
    public class DefaultPlayerSettingData
    {
        [Header("Identity")]
        [SerializeField] private string playerNamePrefix = "테스트 요원";
        [SerializeField] private string presetId = "DEFAULT_TEST_AGENT";

        [Header("Prefab - Optional")]
        [SerializeField] private PlayerPrefab playerPrefab;
        [SerializeField, Range(0.05f, 1f)]
        private float boardScaleMultiplier = 0.2f;

        [Header("Default Stat")]
        [SerializeField, Min(1)] private int maxHP = 10;
        [SerializeField, Min(1)] private int maxSAN = 50;
        [SerializeField, Min(1)] private int speed = 6;
        [SerializeField, Range(1, 99)] private int resistance = 50;
        [SerializeField, Range(1, 99)] private int strength = 50;
        [SerializeField, Range(1, 99)] private int intelligence = 50;
        [SerializeField, Range(1, 99)] private int charisma = 50;
        [SerializeField, Range(1, 99)] private int body = 50;

        public string PlayerNamePrefix
        {
            get
            {
                return string.IsNullOrWhiteSpace(playerNamePrefix)
                    ? "테스트 요원"
                    : playerNamePrefix;
            }
        }

        public string PresetId
        {
            get
            {
                return string.IsNullOrWhiteSpace(presetId)
                    ? "DEFAULT_TEST_AGENT"
                    : presetId;
            }
        }

        public PlayerPrefab PlayerPrefab
        {
            get { return playerPrefab; }
        }

        public GameObject PlayerPrefabObject
        {
            get { return playerPrefab != null ? playerPrefab.gameObject : null; }
        }

        public float BoardScaleMultiplier
        {
            get { return Mathf.Clamp(boardScaleMultiplier, 0.05f, 1f); }
        }

        // 신규 코드 호환 별칭
        public float BoardScale
        {
            get { return BoardScaleMultiplier; }
        }

        public int MaxHP { get { return Mathf.Max(1, maxHP); } }
        public int MaxSAN { get { return Mathf.Max(1, maxSAN); } }
        public int Speed { get { return Mathf.Max(1, speed); } }
        public int Resistance { get { return Mathf.Clamp(resistance, 1, 99); } }
        public int Strength { get { return Mathf.Clamp(strength, 1, 99); } }
        public int Intelligence { get { return Mathf.Clamp(intelligence, 1, 99); } }
        public int Charisma { get { return Mathf.Clamp(charisma, 1, 99); } }
        public int Body { get { return Mathf.Clamp(body, 1, 99); } }

        public string GetPlayerName(int oneBasedIndex)
        {
            return PlayerNamePrefix + " " + Mathf.Max(1, oneBasedIndex);
        }
    }

    // =====================================================================
    // 02_Data/EscapeRouteData.cs
    // =====================================================================

    [Serializable]
    public class EscapeRouteData
    {
        [SerializeField] private string escapeRouteInstanceId;
        [SerializeField] private string escapeRouteSOBJId;
        [SerializeField] private string sourceCardInstanceId;
        [SerializeField] private string nodeId;
        [SerializeField] private List<ItemAmountData> installedMaterials =
            new List<ItemAmountData>();
        [SerializeField] private int remainingDefenseRounds;
        [SerializeField] private bool activated;
        [SerializeField] private bool completed;

        public string EscapeRouteInstanceId => escapeRouteInstanceId;
        public string EscapeRouteSOBJId => escapeRouteSOBJId;
        public string SourceCardInstanceId => sourceCardInstanceId;
        public string NodeId => nodeId;
        public IReadOnlyList<ItemAmountData> InstalledMaterials => installedMaterials;
        public int RemainingDefenseRounds => remainingDefenseRounds;
        public bool Activated => activated;
        public bool Completed => completed;

        public EscapeRouteData(
            string instanceId,
            string escapeRouteSOBJId,
            string sourceCardInstanceId,
            string nodeId,
            int defenseRounds)
        {
            escapeRouteInstanceId = instanceId;
            this.escapeRouteSOBJId = escapeRouteSOBJId;
            this.sourceCardInstanceId = sourceCardInstanceId;
            this.nodeId = nodeId;
            remainingDefenseRounds = Mathf.Max(0, defenseRounds);
            activated = false;
            completed = false;
        }

        public int GetInstalledAmount(string itemId)
        {
            ItemAmountData entry = installedMaterials.Find(x => x.ItemId == itemId);
            return entry != null ? entry.Amount : 0;
        }

        public void InstallMaterial(string itemId, int amount)
        {
            ItemAmountData entry = installedMaterials.Find(x => x.ItemId == itemId);
            if (entry == null)
            {
                installedMaterials.Add(new ItemAmountData(itemId, amount));
                return;
            }

            entry.Add(amount);
        }

        public void Activate()
        {
            activated = true;
        }

        public void TickDefenseRound()
        {
            if (!activated || completed)
            {
                return;
            }

            remainingDefenseRounds = Mathf.Max(0, remainingDefenseRounds - 1);
            if (remainingDefenseRounds == 0)
            {
                completed = true;
            }
        }
    }

    // =====================================================================
    // 02_Data/GameSessionData.cs
    // =====================================================================

    [Serializable]
    public class GameSessionData
    {
        [SerializeField] private int currentRound;
        [SerializeField] private int maxRounds;
        [SerializeField] private int configuredPlayerCount;
        [SerializeField] private int randomSeed;
        [SerializeField] private GamePhase currentGamePhase;
        [SerializeField] private PlayerTurnPhase currentPlayerTurnPhase;
        [SerializeField] private bool gameEnded;
        [SerializeField] private List<PlayerData> players = new List<PlayerData>();
        [SerializeField] private List<NodeData> nodes = new List<NodeData>();
        [SerializeField] private List<CardData> cards = new List<CardData>();
        [SerializeField] private List<RadioCardData> radioCards =
            new List<RadioCardData>();
        [SerializeField] private List<BoardConnectionData> connections =
            new List<BoardConnectionData>();
        [SerializeField] private List<VehicleData> vehicles =
            new List<VehicleData>();
        [SerializeField] private List<EscapeRouteData> escapeRoutes =
            new List<EscapeRouteData>();
        [SerializeField] private TurnQueueData turnQueue = new TurnQueueData();

        public int CurrentRound => currentRound;
        public int MaxRounds => maxRounds;
        public int ConfiguredPlayerCount => configuredPlayerCount;
        public int RandomSeed => randomSeed;
        public GamePhase CurrentGamePhase => currentGamePhase;
        public PlayerTurnPhase CurrentPlayerTurnPhase => currentPlayerTurnPhase;
        public bool GameEnded => gameEnded;
        public List<PlayerData> Players => players;
        public List<NodeData> Nodes => nodes;
        public List<CardData> Cards => cards;
        public List<RadioCardData> RadioCards => radioCards;
        public List<BoardConnectionData> Connections => connections;
        public List<VehicleData> Vehicles => vehicles;
        public List<EscapeRouteData> EscapeRoutes => escapeRoutes;
        public TurnQueueData TurnQueue => turnQueue;

        public GameSessionData(int maxRoundCount, int playerCount, int seed)
        {
            maxRounds = Mathf.Max(1, maxRoundCount);
            configuredPlayerCount = Mathf.Clamp(playerCount, 1, 6);
            randomSeed = seed;
            currentRound = 0;
            currentGamePhase = GamePhase.None;
            currentPlayerTurnPhase = PlayerTurnPhase.None;
            gameEnded = false;
        }

        public void SetRound(int value) => currentRound = Mathf.Max(0, value);
        public void SetGamePhase(GamePhase phase) => currentGamePhase = phase;
        public void SetPlayerTurnPhase(PlayerTurnPhase phase) =>
            currentPlayerTurnPhase = phase;
        public void EndGame() => gameEnded = true;

        public PlayerData FindPlayer(string playerId) =>
            players.Find(x => x.PlayerId == playerId);

        public NodeData FindNode(string nodeId) =>
            nodes.Find(x => x.NodeId == nodeId);

        public CardData FindCard(string instanceId) =>
            cards.Find(x => x.CardInstanceId == instanceId);

        public RadioCardData FindRadioCard(string instanceId) =>
            radioCards.Find(x => x.RadioCardInstanceId == instanceId);

        public VehicleData FindVehicle(string instanceId) =>
            vehicles.Find(x => x.VehicleInstanceId == instanceId);

        public EscapeRouteData FindEscapeRoute(string instanceId) =>
            escapeRoutes.Find(x => x.EscapeRouteInstanceId == instanceId);

        public BoardConnectionData FindConnection(
            string startNodeId,
            string destinationNodeId) =>
            connections.Find(x => x.Connects(startNodeId, destinationNodeId));
    }

    // =====================================================================
    // 02_Data/ItemAmountData.cs
    // =====================================================================

    [Serializable]
    public class ItemAmountData
    {
        [SerializeField] private string itemId;
        [SerializeField] private int amount;

        public string ItemId => itemId;
        public int Amount => amount;

        public ItemAmountData(string itemId, int amount = 0)
        {
            this.itemId = itemId;
            this.amount = Mathf.Max(0, amount);
        }

        public void SetAmount(int value)
        {
            amount = Mathf.Max(0, value);
        }

        public void Add(int value)
        {
            amount = Mathf.Max(0, amount + value);
        }
    }

    // =====================================================================
    // 02_Data/NodeData.cs
    // =====================================================================

    /// <summary>
    /// 자동 생성된 보드 스페이스의 런타임 상태입니다.
    /// BoardCoordinate는 Inspector 입력값이 아니라 보드 생성기가 자동으로 넣습니다.
    /// </summary>
    [Serializable]
    public class NodeData
    {
        public const int MaxPlayerCount = 6;

        [SerializeField] private string nodeId;
        [SerializeField] private string districtId;
        [SerializeField] private Vector2Int boardCoordinate;
        [SerializeField] private BoardSpaceType spaceType;
        [SerializeField] private BuildingType buildingType;
        [SerializeField] private float movementAPCost;
        [SerializeField] private bool enterable;

        [Header("Runtime")]
        [SerializeField] private List<string> adjacentNodeIds = new List<string>();
        [SerializeField] private List<string> playerIds = new List<string>();
        [SerializeField] private List<string> activeRadioCardInstanceIds =
            new List<string>();

        [Header("Compatibility Runtime Lists")]
        [SerializeField] private List<string> hiddenCardInstanceIds =
            new List<string>();
        [SerializeField] private List<string> revealedCardInstanceIds =
            new List<string>();
        [SerializeField] private List<string> vehicleInstanceIds =
            new List<string>();
        [SerializeField] private List<string> escapeRouteInstanceIds =
            new List<string>();

        public string NodeId => nodeId;
        public string DisplayName => nodeId;
        public string DistrictId => districtId;

        public int DistrictNumber
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(districtId))
                {
                    string[] parts = districtId.Split('-');
                    if (parts.Length > 1 &&
                        int.TryParse(parts[parts.Length - 1], out int parsed))
                    {
                        return parsed;
                    }
                }

                return 0;
            }
        }

        public DistrictType DistrictType => DistrictType.Mixed;
        public BoardNodeType NodeType => BoardNodeType.Other;
        public bool Horde => false;
        public Vector2Int BoardCoordinate => boardCoordinate;
        public BoardSpaceType SpaceType => spaceType;
        public BuildingType BuildingType => buildingType;
        public float MovementAPCost => Mathf.Max(0.1f, movementAPCost);
        public bool Enterable => enterable;
        public bool IsBuilding => spaceType == BoardSpaceType.Building;
        public bool HasActiveRadioCard => activeRadioCardInstanceIds.Count > 0;
        public bool HasPlayerSpace => playerIds.Count < MaxPlayerCount;
        public IReadOnlyList<string> AdjacentNodeIds => adjacentNodeIds;
        public IReadOnlyList<string> PlayerIds => playerIds;
        public IReadOnlyList<string> ActiveRadioCardInstanceIds =>
            activeRadioCardInstanceIds;
        public int InitialCardCount => 0;
        public bool AllowVehicleCard => IsBuilding;
        public bool AllowEscapeRouteCard => IsBuilding;
        public IReadOnlyList<string> HiddenCardInstanceIds => hiddenCardInstanceIds;
        public IReadOnlyList<string> RevealedCardInstanceIds => revealedCardInstanceIds;
        public IReadOnlyList<string> VehicleInstanceIds => vehicleInstanceIds;
        public IReadOnlyList<string> EscapeRouteInstanceIds => escapeRouteInstanceIds;

        public NodeData(BoardSpacePrefab source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            nodeId = source.NodeId;
            districtId = source.DistrictId;
            boardCoordinate = source.BoardCoordinate;
            spaceType = source.SpaceType;
            buildingType = source.BuildingType;
            movementAPCost = source.MovementAPCost;
            enterable = source.Enterable;
        }

        public bool IsAdjacent(string otherNodeId)
        {
            return adjacentNodeIds.Contains(otherNodeId);
        }

        public void AddAdjacentNode(string otherNodeId)
        {
            if (!string.IsNullOrWhiteSpace(otherNodeId) &&
                !adjacentNodeIds.Contains(otherNodeId))
            {
                adjacentNodeIds.Add(otherNodeId);
            }
        }

        public bool AddPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            if (playerIds.Contains(playerId))
            {
                return true;
            }

            if (playerIds.Count >= MaxPlayerCount)
            {
                return false;
            }

            playerIds.Add(playerId);
            return true;
        }

        public void RemovePlayer(string playerId)
        {
            playerIds.Remove(playerId);
        }

        public void AddHiddenCard(string cardInstanceId)
        {
            if (!string.IsNullOrWhiteSpace(cardInstanceId) &&
                !hiddenCardInstanceIds.Contains(cardInstanceId))
            {
                hiddenCardInstanceIds.Add(cardInstanceId);
            }
        }

        public void RevealCard(string cardInstanceId)
        {
            hiddenCardInstanceIds.Remove(cardInstanceId);
            if (!string.IsNullOrWhiteSpace(cardInstanceId) &&
                !revealedCardInstanceIds.Contains(cardInstanceId))
            {
                revealedCardInstanceIds.Add(cardInstanceId);
            }
        }

        public void AddVehicle(string vehicleInstanceId)
        {
            if (!string.IsNullOrWhiteSpace(vehicleInstanceId) &&
                !vehicleInstanceIds.Contains(vehicleInstanceId))
            {
                vehicleInstanceIds.Add(vehicleInstanceId);
            }
        }

        public void RemoveVehicle(string vehicleInstanceId)
        {
            vehicleInstanceIds.Remove(vehicleInstanceId);
        }

        public void AddEscapeRoute(string escapeRouteInstanceId)
        {
            if (!string.IsNullOrWhiteSpace(escapeRouteInstanceId) &&
                !escapeRouteInstanceIds.Contains(escapeRouteInstanceId))
            {
                escapeRouteInstanceIds.Add(escapeRouteInstanceId);
            }
        }

        public void AddRadioCard(string radioCardInstanceId)
        {
            if (!string.IsNullOrWhiteSpace(radioCardInstanceId) &&
                !activeRadioCardInstanceIds.Contains(radioCardInstanceId))
            {
                activeRadioCardInstanceIds.Add(radioCardInstanceId);
            }
        }

        public void RemoveRadioCard(string radioCardInstanceId)
        {
            activeRadioCardInstanceIds.Remove(radioCardInstanceId);
        }
    }

    // =====================================================================
    // 02_Data/PlayerData.cs
    // =====================================================================

    /// <summary>
    /// 기존 차량/탈출/프리셋 시스템과 신규 자동 테스트 플레이어 시스템을
    /// 동시에 지원하는 호환 PlayerData입니다.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        [Header("Identity")]
        [SerializeField] private string playerId;
        [SerializeField] private string playerPresetId;
        [SerializeField] private string playerName;

        [Header("Location")]
        [SerializeField] private string currentNodeId;
        [SerializeField] private string currentVehicleInstanceId;

        [Header("State")]
        [SerializeField] private PlayerLifeState lifeState =
            PlayerLifeState.Survivor;
        [SerializeField] private int currentHP;
        [SerializeField] private int maxHP = 10;
        [SerializeField] private int currentSAN;
        [SerializeField] private int maxSAN = 50;
        [SerializeField] private float currentAP;

        [Header("Stat")]
        [SerializeField] private int speed = 6;
        [SerializeField] private int resistance = 50;
        [SerializeField] private int strength = 50;
        [SerializeField] private int intelligence = 50;
        [SerializeField] private int charisma = 50;
        [SerializeField] private int body = 50;

        [Header("Inventory")]
        [SerializeField] private List<ItemAmountData> inventory =
            new List<ItemAmountData>();

        public string PlayerId
        {
            get { return playerId; }
            set { playerId = value ?? string.Empty; }
        }

        public string PlayerPresetId
        {
            get { return playerPresetId; }
            set
            {
                playerPresetId = string.IsNullOrWhiteSpace(value)
                    ? "DEFAULT_TEST_AGENT"
                    : value;
            }
        }

        public string PlayerName
        {
            get { return playerName; }
            set { playerName = string.IsNullOrWhiteSpace(value) ? playerId : value; }
        }

        /// <summary>
        /// 신규 PlayerManager가 사용하는 이름 별칭입니다.
        /// </summary>
        public string DisplayName
        {
            get { return PlayerName; }
            set { PlayerName = value; }
        }

        public string CurrentNodeId
        {
            get { return currentNodeId; }
            set { currentNodeId = value ?? string.Empty; }
        }

        public string CurrentVehicleInstanceId
        {
            get { return currentVehicleInstanceId; }
            set { currentVehicleInstanceId = value ?? string.Empty; }
        }

        public PlayerLifeState LifeState
        {
            get { return lifeState; }
            set { lifeState = value; }
        }

        public int CurrentHP
        {
            get { return currentHP; }
            set { currentHP = Mathf.Clamp(value, 0, Mathf.Max(1, maxHP)); }
        }

        public int MaxHP
        {
            get { return maxHP; }
            set
            {
                maxHP = Mathf.Max(1, value);
                currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            }
        }

        public int CurrentSAN
        {
            get { return currentSAN; }
            set { currentSAN = Mathf.Clamp(value, 0, Mathf.Max(1, maxSAN)); }
        }

        public int MaxSAN
        {
            get { return maxSAN; }
            set
            {
                maxSAN = Mathf.Max(1, value);
                currentSAN = Mathf.Clamp(currentSAN, 0, maxSAN);
            }
        }

        public float CurrentAP
        {
            get { return currentAP; }
            set { currentAP = Mathf.Max(0f, value); }
        }

        public int Speed
        {
            get { return speed; }
            set { speed = Mathf.Max(1, value); }
        }

        public int Resistance
        {
            get { return resistance; }
            set { resistance = Mathf.Clamp(value, 1, 99); }
        }

        public int Strength
        {
            get { return strength; }
            set { strength = Mathf.Clamp(value, 1, 99); }
        }

        public int Intelligence
        {
            get { return intelligence; }
            set { intelligence = Mathf.Clamp(value, 1, 99); }
        }

        public int Charisma
        {
            get { return charisma; }
            set { charisma = Mathf.Clamp(value, 1, 99); }
        }

        public int Body
        {
            get { return body; }
            set { body = Mathf.Clamp(value, 1, 99); }
        }

        public IReadOnlyList<ItemAmountData> Inventory
        {
            get { return inventory; }
        }

        public bool CanTakeTurn
        {
            get
            {
                return lifeState != PlayerLifeState.Dead &&
                       lifeState != PlayerLifeState.Escaped;
            }
        }

        public PlayerData()
        {
            playerId = string.Empty;
            playerPresetId = "DEFAULT_TEST_AGENT";
            playerName = string.Empty;
            currentNodeId = string.Empty;
            currentVehicleInstanceId = string.Empty;
            maxHP = 10;
            currentHP = maxHP;
            maxSAN = 50;
            currentSAN = maxSAN;
        }

        public PlayerData(
            string id,
            PlayerSpawnSettingData spawnSetting,
            string sharedStartNodeId)
        {
            if (spawnSetting == null || spawnSetting.PlayerPresetSOBJ == null)
            {
                throw new ArgumentException(
                    "PlayerSpawnSettingData 또는 PlayerPresetSOBJ가 없습니다.");
            }

            PlayerPresetSOBJ preset = spawnSetting.PlayerPresetSOBJ;
            Initialize(
                id,
                preset.PresetId,
                spawnSetting.ResolvedPlayerName,
                sharedStartNodeId,
                preset.MaxHP,
                preset.MaxSAN,
                preset.Speed,
                preset.Resistance,
                preset.Strength,
                preset.Intelligence,
                preset.Charisma,
                preset.Body);
        }

        public PlayerData(
            string id,
            DefaultPlayerSettingData defaultSetting,
            int playerNumber,
            string sharedStartNodeId)
        {
            if (defaultSetting == null)
            {
                throw new ArgumentNullException("defaultSetting");
            }

            Initialize(
                id,
                defaultSetting.PresetId,
                defaultSetting.GetPlayerName(playerNumber),
                sharedStartNodeId,
                defaultSetting.MaxHP,
                defaultSetting.MaxSAN,
                defaultSetting.Speed,
                defaultSetting.Resistance,
                defaultSetting.Strength,
                defaultSetting.Intelligence,
                defaultSetting.Charisma,
                defaultSetting.Body);
        }

        private void Initialize(
            string id,
            string presetId,
            string displayName,
            string startNodeId,
            int hp,
            int san,
            int spd,
            int res,
            int str,
            int intel,
            int cha,
            int bod)
        {
            PlayerId = id;
            PlayerPresetId = presetId;
            PlayerName = displayName;
            CurrentNodeId = startNodeId;
            CurrentVehicleInstanceId = string.Empty;
            LifeState = PlayerLifeState.Survivor;
            MaxHP = hp;
            CurrentHP = MaxHP;
            MaxSAN = san;
            CurrentSAN = MaxSAN;
            Speed = spd;
            Resistance = res;
            Strength = str;
            Intelligence = intel;
            Charisma = cha;
            Body = bod;
            CurrentAP = 0f;
        }

        public void ResetAP()
        {
            CurrentAP = Speed;
        }

        public bool TrySpendAP(float amount)
        {
            float safeAmount = Mathf.Max(0f, amount);
            if (CurrentAP + 0.0001f < safeAmount)
            {
                return false;
            }

            CurrentAP -= safeAmount;
            return true;
        }

        public void SetCurrentNode(string nodeId)
        {
            CurrentNodeId = nodeId;
        }

        public void SetVehicle(string vehicleInstanceId)
        {
            CurrentVehicleInstanceId = vehicleInstanceId;
        }

        public int GetItemAmount(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return 0;
            }

            ItemAmountData entry = inventory.Find(
                delegate(ItemAmountData item)
                {
                    return item != null && item.ItemId == itemId;
                });

            return entry != null ? entry.Amount : 0;
        }

        public void AddItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                return;
            }

            ItemAmountData entry = inventory.Find(
                delegate(ItemAmountData item)
                {
                    return item != null && item.ItemId == itemId;
                });

            if (entry == null)
            {
                inventory.Add(new ItemAmountData(itemId, amount));
                return;
            }

            entry.Add(amount);
        }

        public bool RemoveItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                return false;
            }

            ItemAmountData entry = inventory.Find(
                delegate(ItemAmountData item)
                {
                    return item != null && item.ItemId == itemId;
                });

            if (entry == null || entry.Amount < amount)
            {
                return false;
            }

            entry.Add(-amount);
            return true;
        }
    }

    // =====================================================================
    // 02_Data/PlayerSpawnSettingData.cs
    // =====================================================================

    /// <summary>
    /// 향후 캐릭터 선택 UI 결과를 임시로 연결하기 위한 선택 데이터입니다.
    /// 현재 버전에서는 모든 플레이어가 GameBoardSettingManager의 공통 시작 노드를 사용합니다.
    /// Preset이 비어 있으면 해당 순번은 DefaultPlayerSettingData로 자동 생성됩니다.
    /// </summary>
    [Serializable]
    public class PlayerSpawnSettingData
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private PlayerPresetSOBJ playerPresetSOBJ;
        [SerializeField] private string playerNameOverride;

        public bool Enabled => enabled;
        public PlayerPresetSOBJ PlayerPresetSOBJ => playerPresetSOBJ;

        public string ResolvedPlayerName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(playerNameOverride))
                {
                    return playerNameOverride;
                }

                return playerPresetSOBJ != null
                    ? playerPresetSOBJ.DisplayName
                    : "Player";
            }
        }
    }

    // =====================================================================
    // 02_Data/PlayerStartData.cs
    // =====================================================================

    [Serializable]
    public class PlayerStartData
    {
        [SerializeField] private string playerName = "Player";
        [SerializeField] private string startNodeId;

        [Header("Stat")]
        [SerializeField, Min(1)] private int maxHP = 10;
        [SerializeField, Min(1)] private int maxSAN = 50;
        [SerializeField, Min(1)] private int speed = 3;
        [SerializeField, Range(1, 99)] private int resistance = 50;
        [SerializeField, Range(1, 99)] private int strength = 50;
        [SerializeField, Range(1, 99)] private int intelligence = 50;
        [SerializeField, Range(1, 99)] private int charisma = 50;
        [SerializeField, Range(1, 99)] private int body = 50;

        public string PlayerName => playerName;
        public string StartNodeId => startNodeId;
        public int MaxHP => Mathf.Max(1, maxHP);
        public int MaxSAN => Mathf.Max(1, maxSAN);
        public int Speed => Mathf.Max(1, speed);
        public int Resistance => Mathf.Clamp(resistance, 1, 99);
        public int Strength => Mathf.Clamp(strength, 1, 99);
        public int Intelligence => Mathf.Clamp(intelligence, 1, 99);
        public int Charisma => Mathf.Clamp(charisma, 1, 99);
        public int Body => Mathf.Clamp(body, 1, 99);
    }

    // =====================================================================
    // 02_Data/RadioCardData.cs
    // =====================================================================

    [Serializable]
    public class RadioCardData
    {
        [SerializeField] private string radioCardInstanceId;
        [SerializeField] private string radioCardSOBJId;
        [SerializeField] private string nodeId;
        [SerializeField] private int remainingRounds;

        public string RadioCardInstanceId => radioCardInstanceId;
        public string RadioCardSOBJId => radioCardSOBJId;
        public string NodeId => nodeId;
        public int RemainingRounds => remainingRounds;
        public bool IsExpired => remainingRounds <= 0;

        public RadioCardData(
            string instanceId,
            string sobjId,
            string targetNodeId,
            int durationRounds)
        {
            radioCardInstanceId = instanceId;
            radioCardSOBJId = sobjId;
            nodeId = targetNodeId;
            remainingRounds = Mathf.Max(1, durationRounds);
        }

        public void TickRound()
        {
            remainingRounds = Mathf.Max(0, remainingRounds - 1);
        }
    }

    // =====================================================================
    // 02_Data/TurnQueueData.cs
    // =====================================================================

    [Serializable]
    public class TurnQueueData
    {
        [SerializeField] private List<string> playerIds = new List<string>();
        [SerializeField] private int currentIndex = -1;

        public IReadOnlyList<string> PlayerIds => playerIds;
        public int CurrentIndex => currentIndex;
        public bool HasCurrent => currentIndex >= 0 && currentIndex < playerIds.Count;
        public string CurrentPlayerId => HasCurrent
            ? playerIds[currentIndex]
            : string.Empty;

        public void Build(IEnumerable<string> ids)
        {
            playerIds.Clear();
            playerIds.AddRange(ids);
            currentIndex = playerIds.Count > 0 ? 0 : -1;
        }

        public bool MoveNext()
        {
            if (currentIndex < 0)
            {
                return false;
            }

            currentIndex++;
            return currentIndex < playerIds.Count;
        }
    }

    // =====================================================================
    // 02_Data/VehicleData.cs
    // =====================================================================

    [Serializable]
    public class VehicleData
    {
        [SerializeField] private string vehicleInstanceId;
        [SerializeField] private string vehicleSOBJId;
        [SerializeField] private string sourceCardInstanceId;
        [SerializeField] private string currentNodeId;
        [SerializeField] private int currentFuel;
        [SerializeField] private bool destroyed;
        [SerializeField] private string driverPlayerId;
        [SerializeField] private List<string> occupantPlayerIds =
            new List<string>();
        [SerializeField] private List<ItemAmountData> installedParts =
            new List<ItemAmountData>();

        public string VehicleInstanceId => vehicleInstanceId;
        public string VehicleSOBJId => vehicleSOBJId;
        public string SourceCardInstanceId => sourceCardInstanceId;
        public string CurrentNodeId => currentNodeId;
        public int CurrentFuel => currentFuel;
        public bool Destroyed => destroyed;
        public string DriverPlayerId => driverPlayerId;
        public IReadOnlyList<string> OccupantPlayerIds => occupantPlayerIds;
        public IReadOnlyList<ItemAmountData> InstalledParts => installedParts;

        public VehicleData(
            string instanceId,
            string vehicleSOBJId,
            string sourceCardInstanceId,
            string nodeId)
        {
            vehicleInstanceId = instanceId;
            this.vehicleSOBJId = vehicleSOBJId;
            this.sourceCardInstanceId = sourceCardInstanceId;
            currentNodeId = nodeId;
            currentFuel = 0;
            destroyed = false;
            driverPlayerId = string.Empty;
        }

        public int GetInstalledPartAmount(string itemId)
        {
            ItemAmountData entry = installedParts.Find(x => x.ItemId == itemId);
            return entry != null ? entry.Amount : 0;
        }

        public void InstallPart(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                return;
            }

            ItemAmountData entry = installedParts.Find(x => x.ItemId == itemId);
            if (entry == null)
            {
                installedParts.Add(new ItemAmountData(itemId, amount));
                return;
            }

            entry.Add(amount);
        }

        public void AddFuel(int amount, int maxFuel)
        {
            currentFuel = Mathf.Clamp(
                currentFuel + Mathf.Max(0, amount),
                0,
                Mathf.Max(1, maxFuel));
        }

        public bool ConsumeFuel(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (currentFuel < amount)
            {
                return false;
            }

            currentFuel -= amount;
            return true;
        }

        public bool AddOccupant(string playerId, int seatCount)
        {
            if (occupantPlayerIds.Contains(playerId))
            {
                return true;
            }

            if (occupantPlayerIds.Count >= Mathf.Max(1, seatCount))
            {
                return false;
            }

            occupantPlayerIds.Add(playerId);
            if (string.IsNullOrEmpty(driverPlayerId))
            {
                driverPlayerId = playerId;
            }

            return true;
        }

        public void RemoveOccupant(string playerId)
        {
            occupantPlayerIds.Remove(playerId);
            if (driverPlayerId == playerId)
            {
                driverPlayerId = occupantPlayerIds.Count > 0
                    ? occupantPlayerIds[0]
                    : string.Empty;
            }
        }

        public void SetCurrentNode(string nodeId)
        {
            currentNodeId = nodeId;
        }
    }
}

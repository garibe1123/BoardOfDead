using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
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
}

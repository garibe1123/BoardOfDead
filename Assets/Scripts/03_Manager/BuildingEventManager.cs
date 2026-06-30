using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 건물 탐색 사건의 선택, Variant 결정, 판정, 밀어붙이기, 효과 적용과 종료를 담당합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingEventManager : MonoBehaviour
    {
        public event Action<PreparedBuildingEvent> OnEventStarted;
        public event Action<PreparedBuildingEvent> OnEventCompleted;
        public event Action<string> OnEventFailed;

        [Header("Data")]
        [SerializeField] private BuildingEventDatabaseSOBJ eventDatabase;

        [Header("Runtime References")]
        [SerializeField] private BuildingEventUIPrefab eventUI;
        [SerializeField] private BuildingEventSessionManager sessionManager;
        [SerializeField] private BoardRuntimeStateManager boardState;

        [Tooltip("IBuildingEventRuntimeService 구현 MonoBehaviour를 연결합니다.")]
        [SerializeField] private MonoBehaviour runtimeManagerBehaviour;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = true;
        [SerializeField] private bool logExcludedCandidates;

        private const string ActionOwnerId = "BuildingEventManager";

        private TurnManager turnManager;
        private CardManager cardManager;
        private IBuildingEventRuntimeService runtimeService;
        private BuildingEventConditionManager conditionEvaluator;
        private BuildingEventEffectManager effectApplier;
        private System.Random random;

        private PreparedBuildingEvent currentEvent;
        private ResolvedBuildingEventChoice currentChoice;
        private BuildingEventCheckResult firstRoll;
        private int actionToken;
        private bool hasActionToken;
        private bool isBusy;
        private bool hasPushed;
        private bool hasAppliedResult;

        public bool IsEventActive => currentEvent != null;
        public BuildingEventDatabaseSOBJ EventDatabase => eventDatabase;

        /// <summary>
        /// 자동 생성 보드가 필요한 기존 매니저를 연결합니다.
        /// </summary>
        public void Initialize(
            TurnManager runtimeTurnManager,
            CardManager runtimeCardManager,
            BoardRuntimeStateManager runtimeBoardState,
            BuildingEventSessionManager runtimeSessionManager,
            MonoBehaviour runtimeManagerComponent,
            System.Random runtimeRandom)
        {
            turnManager = runtimeTurnManager;
            cardManager = runtimeCardManager;
            boardState = runtimeBoardState != null ? runtimeBoardState : boardState;
            sessionManager = runtimeSessionManager != null ? runtimeSessionManager : sessionManager;

            if (runtimeManagerComponent != null)
            {
                runtimeManagerBehaviour = runtimeManagerComponent;
            }

            runtimeService = runtimeManagerBehaviour as IBuildingEventRuntimeService;
            random = runtimeRandom ?? new System.Random(Environment.TickCount);

            BuildingEventRuntimeManager runtimeManager =
                runtimeManagerBehaviour as BuildingEventRuntimeManager;

            if (runtimeManager != null)
            {
                runtimeManager.Initialize(turnManager, boardState);
            }

            conditionEvaluator = new BuildingEventConditionManager(runtimeService);
            effectApplier = new BuildingEventEffectManager(
                runtimeService,
                sessionManager,
                random,
                enableDebugLog);

            ResetCurrentEventState();
        }

        /// <summary>
        /// AP를 소비하거나 카드를 공개하기 전에 실행 가능한 사건을 미리 결정합니다.
        /// </summary>
        public bool TryPrepareEvent(
            PlayerData player,
            BoardSpacePrefab space,
            BoardCardSlotData slot,
            out PreparedBuildingEvent prepared,
            out string failureReason)
        {
            prepared = null;
            failureReason = string.Empty;

            if (currentEvent != null)
            {
                failureReason = "이미 다른 건물 사건을 처리 중입니다.";
                return false;
            }

            if (eventDatabase == null)
            {
                failureReason = "Building Event Database가 연결되지 않았습니다.";
                return false;
            }

            if (runtimeService == null || conditionEvaluator == null)
            {
                failureReason = "Building Event Runtime Service가 연결되지 않았습니다.";
                return false;
            }

            if (player == null || space == null || slot == null)
            {
                failureReason = "이벤트 Context에 필요한 플레이어, 건물 또는 카드가 없습니다.";
                return false;
            }

            BuildingBoardPrefab building = FindBuildingMetadata(space);

            if (building == null)
            {
                failureReason = "건물에 BuildingBoardPrefab이 없습니다.";
                return false;
            }

            BuildingEventContext context = BuildContext(player, space, slot, building);
            AssignedBuildingEvent assigned;

            if (sessionManager != null &&
                sessionManager.TryGetAssigned(slot.SlotId, out assigned) &&
                assigned != null &&
                !assigned.resolved)
            {
                BuildingEventArchetypeSOBJ assignedArchetype =
                    eventDatabase.FindArchetype(assigned.eventId);
                BuildingEventVariantSOBJ assignedVariant =
                    FindVariantById(assigned.variantId);

                if (assignedArchetype != null)
                {
                    prepared = BuildPreparedEvent(
                        context,
                        assignedArchetype,
                        assignedVariant);
                    return prepared != null;
                }
            }

            ScheduledBuildingEvent followUp;

            if (sessionManager != null &&
                sessionManager.TryTakeDueFollowUp(
                    context,
                    runtimeService,
                    out followUp) &&
                followUp != null)
            {
                BuildingEventArchetypeSOBJ followUpArchetype =
                    eventDatabase.FindArchetype(followUp.eventId);

                if (followUpArchetype != null)
                {
                    BuildingEventVariantSOBJ followUpVariant =
                        SelectVariant(followUpArchetype, context);
                    prepared = BuildPreparedEvent(
                        context,
                        followUpArchetype,
                        followUpVariant);
                    DebugCandidate("예약 후속 사건", followUpArchetype, followUpVariant, 1f);
                    return prepared != null;
                }
            }

            BuildingEventArchetypeSOBJ selectedArchetype;
            BuildingEventVariantSOBJ selectedVariant;

            if (!TrySelectRandomEvent(
                    context,
                    out selectedArchetype,
                    out selectedVariant))
            {
                selectedArchetype = eventDatabase.fallbackEvent;
                selectedVariant = SelectVariant(selectedArchetype, context);
            }

            if (selectedArchetype == null)
            {
                failureReason = "조건에 맞는 사건과 Fallback 사건이 모두 없습니다.";
                return false;
            }

            prepared = BuildPreparedEvent(
                context,
                selectedArchetype,
                selectedVariant);

            if (prepared == null)
            {
                failureReason = "선택된 사건의 표시 데이터를 구성하지 못했습니다.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 공개된 탐색 카드에 미리 결정한 사건을 고정하고 UI 처리를 시작합니다.
        /// </summary>
        public bool TryStartPreparedEvent(
            PreparedBuildingEvent prepared,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (prepared == null || prepared.Context == null)
            {
                failureReason = "시작할 사건 데이터가 없습니다.";
                return false;
            }

            if (currentEvent != null)
            {
                failureReason = "이미 사건을 처리 중입니다.";
                return false;
            }

            if (eventUI == null || !eventUI.CanOpen(out failureReason))
            {
                return false;
            }

            if (turnManager == null ||
                !turnManager.TryAdoptCurrentAction(ActionOwnerId, out actionToken))
            {
                failureReason = "현재 ResolvingCard 행동의 소유권을 가져오지 못했습니다.";
                return false;
            }

            hasActionToken = true;
            currentEvent = prepared;
            currentChoice = null;
            firstRoll = null;
            isBusy = false;
            hasPushed = false;
            hasAppliedResult = false;

            if (sessionManager != null)
            {
                sessionManager.AssignToSlot(
                    prepared.Context.cardSlot.SlotId,
                    prepared.Archetype.eventId,
                    prepared.Variant != null ? prepared.Variant.variantId : string.Empty);
            }

            eventUI.Open(prepared, HandleChoiceSelected);
            OnEventStarted?.Invoke(prepared);

            if (enableDebugLog)
            {
                Debug.Log(
                    "[BuildingEventManager] 사건 시작 / " +
                    prepared.Archetype.eventId +
                    " / Variant " +
                    (prepared.Variant != null
                        ? prepared.Variant.variantId
                        : "NONE"));
            }

            if (prepared.Choices.Count == 0)
            {
                ApplySingleResult(
                    prepared.Archetype.defaultResult,
                    null,
                    null);
            }

            return true;
        }

        public void AbortCurrentEvent(string reason)
        {
            if (enableDebugLog)
            {
                Debug.LogWarning("[BuildingEventManager] 사건 중단: " + reason, this);
            }

            eventUI?.CloseImmediate();

            if (hasActionToken && turnManager != null)
            {
                turnManager.ForceRecoverOwnedAction(
                    actionToken,
                    ActionOwnerId,
                    false);
            }

            OnEventFailed?.Invoke(reason);
            ResetCurrentEventState();
        }

        private void HandleChoiceSelected(int choiceIndex)
        {
            if (isBusy ||
                currentEvent == null ||
                choiceIndex < 0 ||
                choiceIndex >= currentEvent.Choices.Count)
            {
                return;
            }

            ResolvedBuildingEventChoice resolved =
                currentEvent.Choices[choiceIndex];

            if (resolved == null || resolved.Source == null)
            {
                return;
            }

            string failureReason;

            if (!conditionEvaluator.EvaluateChoice(
                    resolved.Source,
                    currentEvent.Context,
                    out failureReason))
            {
                RefreshChoiceAvailability();
                return;
            }

            isBusy = true;
            currentChoice = resolved;
            eventUI.SetChoicesInteractable(false);

            if (!TryPayChoiceCosts(resolved.Source, out failureReason))
            {
                isBusy = false;
                RefreshChoiceAvailability();
                return;
            }

            if (!resolved.Source.useCheck)
            {
                BuildingEventResultData immediate = GetResultForLevel(
                    resolved,
                    BuildingEventSuccessLevel.NormalSuccess);
                ApplySingleResult(immediate, null, null);
                return;
            }

            int ability = runtimeService.GetAbility(
                currentEvent.Context.player,
                resolved.Source.abilityType);
            firstRoll = D100CheckManager.Roll(
                ability,
                resolved.Source.situationModifier,
                resolved.Source.difficulty,
                random);
            eventUI.ShowRoll(firstRoll, false);

            if (enableDebugLog)
            {
                Debug.Log(
                    "[BuildingEventRoll] 능력 " + ability +
                    " / 최종 " + firstRoll.FinalAbility +
                    " / D100 " + firstRoll.Roll +
                    " / " + firstRoll.SuccessLevel +
                    " / 난이도 충족 " + firstRoll.MeetsDifficulty);
            }

            if (firstRoll.MeetsDifficulty)
            {
                ApplySuccessResult(firstRoll.SuccessLevel, false);
                return;
            }

            if (firstRoll.IsCriticalFailure)
            {
                ApplyFailureResult(true, false);
                return;
            }

            bool canPush = CanPushCurrentChoice();

            if (resolved.Source.allowPush &&
                currentEvent.Archetype.allowPushByDefault)
            {
                isBusy = false;
                eventUI.ShowPush(
                    resolved.Source.pushAPCost,
                    canPush,
                    HandlePushRequested,
                    HandleAcceptFailure);
                return;
            }

            ApplyFailureResult(false, false);
        }

        private void HandlePushRequested()
        {
            if (isBusy ||
                currentEvent == null ||
                currentChoice == null ||
                hasPushed ||
                !CanPushCurrentChoice())
            {
                return;
            }

            isBusy = true;

            if (!runtimeService.TrySpendAP(
                    currentEvent.Context.player,
                    currentChoice.Source.pushAPCost))
            {
                isBusy = false;
                return;
            }

            hasPushed = true;
            int ability = runtimeService.GetAbility(
                currentEvent.Context.player,
                currentChoice.Source.abilityType);
            BuildingEventCheckResult pushRoll = D100CheckManager.Roll(
                ability,
                currentChoice.Source.situationModifier,
                currentChoice.Source.difficulty,
                random);
            eventUI.ShowRoll(pushRoll, true);

            if (pushRoll.MeetsDifficulty)
            {
                BuildingEventSuccessLevel level =
                    currentChoice.Source.useActualSuccessLevelOnPush
                        ? pushRoll.SuccessLevel
                        : BuildingEventSuccessLevel.NormalSuccess;
                ApplySuccessResult(level, true);
                return;
            }

            ApplyFailureResult(pushRoll.IsCriticalFailure, true);
        }

        private void HandleAcceptFailure()
        {
            if (isBusy || currentChoice == null)
            {
                return;
            }

            isBusy = true;
            ApplyFailureResult(false, false);
        }

        private bool CanPushCurrentChoice()
        {
            if (currentEvent == null ||
                currentChoice == null ||
                currentChoice.Source == null ||
                hasPushed ||
                !currentChoice.Source.useCheck ||
                firstRoll == null ||
                firstRoll.IsCriticalFailure ||
                !currentChoice.Source.allowPush ||
                !currentEvent.Archetype.allowPushByDefault)
            {
                return false;
            }

            return runtimeService.GetAP(currentEvent.Context.player) + 0.0001f >=
                   currentChoice.Source.pushAPCost;
        }

        private void ApplySuccessResult(
            BuildingEventSuccessLevel level,
            bool pushed)
        {
            BuildingEventResultData result = GetResultForLevel(currentChoice, level);
            ApplySingleResult(result, null, null);
        }

        private void ApplyFailureResult(
            bool criticalFailure,
            bool pushFailure)
        {
            BuildingEventResultData baseFailure =
                GetFailureResult(currentChoice);
            BuildingEventResultData pushAdditional = pushFailure
                ? GetPushFailureAdditional(currentChoice)
                : null;
            BuildingEventResultData criticalAdditional = criticalFailure
                ? GetCriticalFailureAdditional(currentChoice)
                : null;

            ApplySingleResult(
                baseFailure,
                pushAdditional,
                criticalAdditional);
        }

        private void ApplySingleResult(
            BuildingEventResultData primary,
            BuildingEventResultData additionalA,
            BuildingEventResultData additionalB)
        {
            if (hasAppliedResult || currentEvent == null)
            {
                return;
            }

            hasAppliedResult = true;
            effectApplier.ApplyResult(primary, currentEvent.Context);
            effectApplier.ApplyResult(additionalA, currentEvent.Context);
            effectApplier.ApplyResult(additionalB, currentEvent.Context);

            string text = CombineResultText(primary, additionalA, additionalB);
            eventUI.ShowResult(text, CompleteCurrentEvent);
        }

        private void CompleteCurrentEvent()
        {
            if (currentEvent == null)
            {
                return;
            }

            PreparedBuildingEvent completed = currentEvent;

            try
            {
                if (sessionManager != null)
                {
                    sessionManager.RecordCompleted(
                        completed.Archetype,
                        completed.Variant,
                        completed.Context,
                        completed.IllustrationId);
                    sessionManager.MarkAssignedResolved(
                        completed.Context.cardSlot.SlotId);

                    for (int index = 0;
                         index < completed.Archetype.followUps.Count;
                         index++)
                    {
                        sessionManager.Schedule(
                            completed.Archetype.followUps[index],
                            completed.Context);
                    }
                }

                boardState?.IncrementSearchCount(completed.Context.nodeId);
                cardManager?.ResolveSlot(completed.Context.cardSlot);
                eventUI?.CloseImmediate();
                OnEventCompleted?.Invoke(completed);
            }
            finally
            {
                if (hasActionToken && turnManager != null)
                {
                    turnManager.CompleteOwnedAction(
                        actionToken,
                        ActionOwnerId,
                        true);
                }

                ResetCurrentEventState();
            }
        }

        private bool TryPayChoiceCosts(
            BuildingEventChoiceData choice,
            out string failureReason)
        {
            failureReason = string.Empty;

            if (choice.apCost > 0f &&
                !runtimeService.TrySpendAP(currentEvent.Context.player, choice.apCost))
            {
                failureReason = "선택지 AP 차감에 실패했습니다.";
                return false;
            }

            if (choice.suppliesCost > 0 &&
                !runtimeService.TrySpendSupplies(choice.suppliesCost))
            {
                failureReason = "물자 차감에 실패했습니다.";
                return false;
            }

            for (int index = 0; index < choice.consumedItems.Count; index++)
            {
                BuildingEventItemRequirement item = choice.consumedItems[index];

                if (item != null &&
                    !runtimeService.TryRemoveItem(
                        currentEvent.Context.player,
                        item.itemId,
                        item.amount))
                {
                    failureReason = "아이템 소모에 실패했습니다: " + item.itemId;
                    return false;
                }
            }

            return true;
        }

        private void RefreshChoiceAvailability()
        {
            if (currentEvent == null)
            {
                return;
            }

            ResolveChoices(currentEvent);
            eventUI.Open(currentEvent, HandleChoiceSelected);
        }

        private bool TrySelectRandomEvent(
            BuildingEventContext context,
            out BuildingEventArchetypeSOBJ selectedArchetype,
            out BuildingEventVariantSOBJ selectedVariant)
        {
            selectedArchetype = null;
            selectedVariant = null;
            List<EventCandidate> candidates = new List<EventCandidate>();
            int highestPriority = int.MinValue;

            for (int index = 0; index < eventDatabase.archetypes.Count; index++)
            {
                BuildingEventArchetypeSOBJ archetype =
                    eventDatabase.archetypes[index];

                if (archetype == null || archetype.baseWeight <= 0f)
                {
                    continue;
                }

                string exclusionReason;

                if (!conditionEvaluator.EvaluateAll(
                        archetype.conditions,
                        context,
                        out exclusionReason))
                {
                    LogExcluded(archetype, exclusionReason);
                    continue;
                }

                if (sessionManager != null &&
                    !sessionManager.IsRepeatAllowed(archetype, context))
                {
                    LogExcluded(archetype, "반복 정책");
                    continue;
                }

                List<BuildingEventVariantSOBJ> allVariants =
                    eventDatabase.GetVariants(archetype);
                List<BuildingEventVariantSOBJ> matchingVariants =
                    GetMatchingVariants(allVariants, context);

                if (allVariants.Count > 0 && matchingVariants.Count == 0)
                {
                    LogExcluded(archetype, "일치하는 Variant 없음");
                    continue;
                }

                int priority = CalculateSpecificityPriority(
                    archetype,
                    matchingVariants,
                    context);

                if (priority > highestPriority)
                {
                    candidates.Clear();
                    highestPriority = priority;
                }

                if (priority < highestPriority)
                {
                    continue;
                }

                float variantMultiplier = GetMaximumVariantWeight(matchingVariants);
                string illustrationId = GetCandidateIllustrationId(
                    archetype,
                    matchingVariants);
                float recentMultiplier = sessionManager != null
                    ? sessionManager.GetRecentWeightMultiplier(
                        archetype,
                        illustrationId)
                    : 1f;
                float weight = Mathf.Max(
                    0f,
                    archetype.baseWeight * variantMultiplier * recentMultiplier);

                if (weight <= 0f)
                {
                    LogExcluded(archetype, "최종 가중치 0");
                    continue;
                }

                EventCandidate candidate = new EventCandidate();
                candidate.Archetype = archetype;
                candidate.Variants = matchingVariants;
                candidate.Weight = weight;
                candidates.Add(candidate);
                DebugCandidate("후보", archetype, null, weight);
            }

            if (enableDebugLog)
            {
                Debug.Log("[BuildingEventManager] 최종 후보 수: " + candidates.Count);
            }

            EventCandidate selected = WeightedSelect(candidates);

            if (selected == null)
            {
                return false;
            }

            selectedArchetype = selected.Archetype;
            selectedVariant = WeightedSelectVariant(selected.Variants);
            DebugCandidate("선택", selectedArchetype, selectedVariant, selected.Weight);
            return true;
        }

        private PreparedBuildingEvent BuildPreparedEvent(
            BuildingEventContext context,
            BuildingEventArchetypeSOBJ archetype,
            BuildingEventVariantSOBJ variant)
        {
            if (context == null || archetype == null)
            {
                return null;
            }

            PreparedBuildingEvent prepared = new PreparedBuildingEvent();
            prepared.Context = context;
            prepared.Archetype = archetype;
            prepared.Variant = variant;
            prepared.Title = variant != null && !string.IsNullOrWhiteSpace(variant.title)
                ? variant.title
                : archetype.defaultTitle;
            prepared.Body = variant != null && !string.IsNullOrWhiteSpace(variant.body)
                ? variant.body
                : archetype.defaultBody;
            prepared.Illustration = ResolveIllustration(context, archetype, variant);
            prepared.IllustrationId = variant != null &&
                                      !string.IsNullOrWhiteSpace(variant.illustrationId)
                ? variant.illustrationId
                : archetype.illustrationId;

            ResolveChoices(prepared);
            return prepared;
        }

        private void ResolveChoices(PreparedBuildingEvent prepared)
        {
            prepared.Choices.Clear();

            for (int index = 0; index < prepared.Archetype.choices.Count; index++)
            {
                BuildingEventChoiceData choice = prepared.Archetype.choices[index];

                if (choice == null)
                {
                    continue;
                }

                ResolvedBuildingEventChoice resolved =
                    new ResolvedBuildingEventChoice();
                resolved.Source = choice;
                resolved.Override = FindChoiceOverride(
                    prepared.Variant,
                    choice.choiceId);
                resolved.DisplayText = resolved.Override != null &&
                                       resolved.Override.overrideDisplayText
                    ? resolved.Override.displayText
                    : choice.displayText;

                string unavailableReason;
                resolved.Available = conditionEvaluator.EvaluateChoice(
                    choice,
                    prepared.Context,
                    out unavailableReason);
                resolved.UnavailableReason = unavailableReason;

                if (choice.useCheck)
                {
                    int ability = runtimeService.GetAbility(
                        prepared.Context.player,
                        choice.abilityType);
                    BuildingEventCheckResult preview =
                        D100CheckManager.CalculatePreview(
                            ability,
                            choice.situationModifier,
                            choice.difficulty);
                    resolved.SuccessProbability = preview.SuccessProbability;
                }

                prepared.Choices.Add(resolved);
            }
        }

        private BuildingEventContext BuildContext(
            PlayerData player,
            BoardSpacePrefab space,
            BoardCardSlotData slot,
            BuildingBoardPrefab building)
        {
            BuildingEventContext context = new BuildingEventContext();
            context.player = player;
            context.boardSpace = space;
            context.cardSlot = slot;
            context.building = building;
            context.playerId = player.PlayerId;
            context.nodeId = space.NodeId;
            context.districtId = building.RuntimeDistrictId;
            context.districtType = building.RuntimeDistrictType;
            context.buildingDefinitionId = building.BuildingDefinitionId;
            context.buildingTypeId = building.BuildingTypeId;
            context.buildingRoleId = building.BuildingRoleId;
            context.buildingTags = new List<string>(building.BuildingTags);
            context.roundNumber = turnManager != null ? turnManager.RoundNumber : 0;
            context.districtThreat = boardState != null
                ? boardState.GetThreat(context.districtId)
                : 0;
            context.buildingState = runtimeService != null
                ? runtimeService.GetBuildingState(context.nodeId)
                : string.Empty;
            return context;
        }

        private List<BuildingEventVariantSOBJ> GetMatchingVariants(
            List<BuildingEventVariantSOBJ> variants,
            BuildingEventContext context)
        {
            List<BuildingEventVariantSOBJ> result =
                new List<BuildingEventVariantSOBJ>();

            for (int index = 0; index < variants.Count; index++)
            {
                BuildingEventVariantSOBJ variant = variants[index];

                if (VariantMatches(variant, context))
                {
                    result.Add(variant);
                }
            }

            return result;
        }

        private bool VariantMatches(
            BuildingEventVariantSOBJ variant,
            BuildingEventContext context)
        {
            if (variant == null || context == null)
            {
                return false;
            }

            if (context.districtThreat < variant.minimumThreat ||
                context.districtThreat > variant.maximumThreat)
            {
                return false;
            }

            if (!ListAllows(variant.allowedDistrictIds, context.districtId) ||
                !EnumListAllows(variant.allowedDistrictTypes, context.districtType) ||
                !ListAllows(variant.allowedBuildingDefinitionIds, context.buildingDefinitionId) ||
                !ListAllows(variant.allowedBuildingTypeIds, context.buildingTypeId) ||
                !ListAllows(variant.allowedBuildingRoleIds, context.buildingRoleId))
            {
                return false;
            }

            for (int index = 0; index < variant.requiredBuildingTags.Count; index++)
            {
                if (!ContainsId(context.buildingTags, variant.requiredBuildingTags[index]))
                {
                    return false;
                }
            }

            for (int index = 0; index < variant.forbiddenBuildingTags.Count; index++)
            {
                if (ContainsId(context.buildingTags, variant.forbiddenBuildingTags[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private int CalculateSpecificityPriority(
            BuildingEventArchetypeSOBJ archetype,
            List<BuildingEventVariantSOBJ> variants,
            BuildingEventContext context)
        {
            if (context.building != null &&
                context.building.HasExclusiveEvent(archetype.eventId))
            {
                return 4;
            }

            int best = 0;

            for (int index = 0; index < variants.Count; index++)
            {
                BuildingEventVariantSOBJ variant = variants[index];

                if (ContainsId(
                    variant.allowedBuildingDefinitionIds,
                    context.buildingDefinitionId))
                {
                    best = Mathf.Max(best, 3);
                }
                else if (ContainsId(
                    variant.allowedBuildingTypeIds,
                    context.buildingTypeId))
                {
                    best = Mathf.Max(best, 2);
                }
                else if (ContainsId(
                    variant.allowedDistrictIds,
                    context.districtId) ||
                    EnumContains(variant.allowedDistrictTypes, context.districtType))
                {
                    best = Mathf.Max(best, 1);
                }
            }

            return best;
        }

        private BuildingEventVariantSOBJ SelectVariant(
            BuildingEventArchetypeSOBJ archetype,
            BuildingEventContext context)
        {
            if (archetype == null)
            {
                return null;
            }

            return WeightedSelectVariant(
                GetMatchingVariants(
                    eventDatabase.GetVariants(archetype),
                    context));
        }

        private BuildingEventVariantSOBJ WeightedSelectVariant(
            List<BuildingEventVariantSOBJ> variants)
        {
            if (variants == null || variants.Count == 0)
            {
                return null;
            }

            float total = 0f;

            for (int index = 0; index < variants.Count; index++)
            {
                total += Mathf.Max(0f, variants[index].weightMultiplier);
            }

            if (total <= 0f)
            {
                return variants[0];
            }

            double roll = random.NextDouble() * total;

            for (int index = 0; index < variants.Count; index++)
            {
                roll -= Mathf.Max(0f, variants[index].weightMultiplier);

                if (roll <= 0d)
                {
                    return variants[index];
                }
            }

            return variants[variants.Count - 1];
        }

        private EventCandidate WeightedSelect(List<EventCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            float total = 0f;

            for (int index = 0; index < candidates.Count; index++)
            {
                total += Mathf.Max(0f, candidates[index].Weight);
            }

            if (total <= 0f)
            {
                return null;
            }

            double roll = random.NextDouble() * total;

            for (int index = 0; index < candidates.Count; index++)
            {
                roll -= Mathf.Max(0f, candidates[index].Weight);

                if (roll <= 0d)
                {
                    return candidates[index];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private Sprite ResolveIllustration(
            BuildingEventContext context,
            BuildingEventArchetypeSOBJ archetype,
            BuildingEventVariantSOBJ variant)
        {
            if (variant != null && variant.illustration != null)
            {
                return variant.illustration;
            }

            if (context.building != null &&
                context.building.DefaultEventIllustration != null)
            {
                return context.building.DefaultEventIllustration;
            }

            if (archetype.defaultIllustration != null)
            {
                return archetype.defaultIllustration;
            }

            return eventDatabase.fallbackIllustration;
        }

        private BuildingEventResultData GetResultForLevel(
            ResolvedBuildingEventChoice choice,
            BuildingEventSuccessLevel level)
        {
            if (choice == null || choice.Source == null)
            {
                return null;
            }

            if (level >= BuildingEventSuccessLevel.ExtremeSuccess)
            {
                BuildingEventResultData extreme = GetExtremeSuccess(choice);

                if (extreme != null && !extreme.IsEmpty)
                {
                    return extreme;
                }
            }

            if (level >= BuildingEventSuccessLevel.HardSuccess)
            {
                BuildingEventResultData hard = GetHardSuccess(choice);

                if (hard != null && !hard.IsEmpty)
                {
                    return hard;
                }
            }

            return GetNormalSuccess(choice);
        }

        private static BuildingEventResultData GetNormalSuccess(
            ResolvedBuildingEventChoice choice)
        {
            return choice.Override != null && choice.Override.overrideNormalSuccess
                ? choice.Override.normalSuccess
                : choice.Source.normalSuccess;
        }

        private static BuildingEventResultData GetHardSuccess(
            ResolvedBuildingEventChoice choice)
        {
            return choice.Override != null && choice.Override.overrideHardSuccess
                ? choice.Override.hardSuccess
                : choice.Source.hardSuccess;
        }

        private static BuildingEventResultData GetExtremeSuccess(
            ResolvedBuildingEventChoice choice)
        {
            return choice.Override != null && choice.Override.overrideExtremeSuccess
                ? choice.Override.extremeSuccess
                : choice.Source.extremeSuccess;
        }

        private static BuildingEventResultData GetFailureResult(
            ResolvedBuildingEventChoice choice)
        {
            return choice.Override != null && choice.Override.overrideFailure
                ? choice.Override.failure
                : choice.Source.failure;
        }

        private static BuildingEventResultData GetCriticalFailureAdditional(
            ResolvedBuildingEventChoice choice)
        {
            return choice.Override != null &&
                   choice.Override.overrideCriticalFailureAdditional
                ? choice.Override.criticalFailureAdditional
                : choice.Source.criticalFailureAdditional;
        }

        private static BuildingEventResultData GetPushFailureAdditional(
            ResolvedBuildingEventChoice choice)
        {
            return choice.Override != null &&
                   choice.Override.overridePushFailureAdditional
                ? choice.Override.pushFailureAdditional
                : choice.Source.pushFailureAdditional;
        }

        private static BuildingEventChoiceOverrideData FindChoiceOverride(
            BuildingEventVariantSOBJ variant,
            string choiceId)
        {
            if (variant == null || string.IsNullOrWhiteSpace(choiceId))
            {
                return null;
            }

            for (int index = 0; index < variant.choiceOverrides.Count; index++)
            {
                BuildingEventChoiceOverrideData item =
                    variant.choiceOverrides[index];

                if (item != null && item.choiceId == choiceId)
                {
                    return item;
                }
            }

            return null;
        }

        private BuildingEventVariantSOBJ FindVariantById(string variantId)
        {
            if (string.IsNullOrWhiteSpace(variantId))
            {
                return null;
            }

            for (int index = 0; index < eventDatabase.variants.Count; index++)
            {
                BuildingEventVariantSOBJ variant = eventDatabase.variants[index];

                if (variant != null && variant.variantId == variantId)
                {
                    return variant;
                }
            }

            return null;
        }

        private static BuildingBoardPrefab FindBuildingMetadata(
            BoardSpacePrefab space)
        {
            BuildingBoardPrefab building =
                space.GetComponent<BuildingBoardPrefab>();

            if (building == null)
            {
                building = space.GetComponentInChildren<BuildingBoardPrefab>(true);
            }

            if (building == null)
            {
                building = space.GetComponentInParent<BuildingBoardPrefab>();
            }

            return building;
        }

        private static bool ListAllows(List<string> list, string value)
        {
            return list == null || list.Count == 0 || ContainsId(list, value);
        }

        private static bool EnumListAllows(
            List<DistrictType> list,
            DistrictType value)
        {
            return list == null || list.Count == 0 || list.Contains(value);
        }

        private static bool EnumContains(
            List<DistrictType> list,
            DistrictType value)
        {
            return list != null && list.Contains(value);
        }

        private static bool ContainsId(IList<string> list, string value)
        {
            if (list == null || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (int index = 0; index < list.Count; index++)
            {
                if (string.Equals(
                    list[index],
                    value,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetMaximumVariantWeight(
            List<BuildingEventVariantSOBJ> variants)
        {
            if (variants == null || variants.Count == 0)
            {
                return 1f;
            }

            float result = 0f;

            for (int index = 0; index < variants.Count; index++)
            {
                result = Mathf.Max(result, variants[index].weightMultiplier);
            }

            return result;
        }

        private static string GetCandidateIllustrationId(
            BuildingEventArchetypeSOBJ archetype,
            List<BuildingEventVariantSOBJ> variants)
        {
            if (variants != null)
            {
                for (int index = 0; index < variants.Count; index++)
                {
                    if (!string.IsNullOrWhiteSpace(variants[index].illustrationId))
                    {
                        return variants[index].illustrationId;
                    }
                }
            }

            return archetype != null ? archetype.illustrationId : string.Empty;
        }

        private static string CombineResultText(
            BuildingEventResultData primary,
            BuildingEventResultData additionalA,
            BuildingEventResultData additionalB)
        {
            List<string> parts = new List<string>();
            AddResultText(parts, primary);
            AddResultText(parts, additionalA);
            AddResultText(parts, additionalB);
            return string.Join("\n\n", parts.ToArray());
        }

        private static void AddResultText(
            List<string> parts,
            BuildingEventResultData result)
        {
            if (result != null && !string.IsNullOrWhiteSpace(result.resultText))
            {
                parts.Add(result.resultText);
            }
        }

        private void LogExcluded(
            BuildingEventArchetypeSOBJ archetype,
            string reason)
        {
            if (enableDebugLog && logExcludedCandidates && archetype != null)
            {
                Debug.Log(
                    "[BuildingEventManager] 제외 / " +
                    archetype.eventId + " / " + reason);
            }
        }

        private void DebugCandidate(
            string prefix,
            BuildingEventArchetypeSOBJ archetype,
            BuildingEventVariantSOBJ variant,
            float weight)
        {
            if (!enableDebugLog || archetype == null)
            {
                return;
            }

            Debug.Log(
                "[BuildingEventManager] " + prefix +
                " / " + archetype.eventId +
                " / Variant " +
                (variant != null ? variant.variantId : "NONE") +
                " / Weight " + weight.ToString("0.###"));
        }

        private void ResetCurrentEventState()
        {
            currentEvent = null;
            currentChoice = null;
            firstRoll = null;
            actionToken = 0;
            hasActionToken = false;
            isBusy = false;
            hasPushed = false;
            hasAppliedResult = false;
        }

        private void OnDisable()
        {
            if (currentEvent != null || hasActionToken)
            {
                AbortCurrentEvent("BuildingEventManager가 비활성화되었습니다.");
            }
        }

        private sealed class EventCandidate
        {
            public BuildingEventArchetypeSOBJ Archetype;
            public List<BuildingEventVariantSOBJ> Variants;
            public float Weight;
        }
    }
}

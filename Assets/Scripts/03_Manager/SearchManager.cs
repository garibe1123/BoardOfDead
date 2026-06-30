using System;
using UnityEngine;

namespace BoardOfDead
{
    public class SearchManager : MonoBehaviour
    {
        [Header("Card Reveal")]
        [Tooltip("건물에서 숨겨진 탐색 카드를 뒤집는 행동력 비용입니다.")]
        [SerializeField, Min(0f)]
        private float cardRevealAPCost = 1f;

        public event Action<PlayerData, BoardSpacePrefab, BoardCardSlotData> OnSearchResolved;
        public event Action<PlayerData, BoardSpacePrefab, BoardCardSlotData> OnSearchCardRevealed;
        public event Action<string> OnSearchFailed;

        private GridManager gridManager;
        private TurnManager turnManager;
        private CardManager cardManager;
        private BuildingEventManager buildingEventManager;

        /// <summary>
        /// 기존 호출부 호환용 초기화입니다. BuildingEventManager가 없으면 기존 카드 팝업 흐름으로 작동합니다.
        /// </summary>
        public void Initialize(
            GridManager runtimeGridManager,
            TurnManager runtimeTurnManager,
            CardManager runtimeCardManager,
            System.Random runtimeRandom)
        {
            Initialize(
                runtimeGridManager,
                runtimeTurnManager,
                runtimeCardManager,
                null,
                runtimeRandom);
        }

        public void Initialize(
            GridManager runtimeGridManager,
            TurnManager runtimeTurnManager,
            CardManager runtimeCardManager,
            BuildingEventManager runtimeBuildingEventManager,
            System.Random runtimeRandom)
        {
            gridManager = runtimeGridManager;
            turnManager = runtimeTurnManager;
            cardManager = runtimeCardManager;
            buildingEventManager = runtimeBuildingEventManager;
        }

        [ContextMenu("Test/Search Current Player")]
        public bool TrySearchCurrentPlayer()
        {
            BoardCardSlotData ignored;
            return TrySearchCurrentPlayer(out ignored);
        }

        public bool TrySearchCurrentPlayer(
            out BoardCardSlotData revealedSlot)
        {
            revealedSlot = null;

            if (gridManager == null ||
                turnManager == null ||
                cardManager == null)
            {
                return Fail("탐색 매니저 초기화가 완료되지 않았습니다.");
            }

            PlayerData player = turnManager.CurrentPlayer;
            float requiredAP;
            string failureReason;
            BoardSpacePrefab space;
            BoardCardSlotData targetSlot;

            if (!TryGetSearchContext(
                    player,
                    player != null ? player.CurrentAP : 0f,
                    out space,
                    out targetSlot,
                    out requiredAP,
                    out failureReason))
            {
                return Fail(failureReason);
            }

            PreparedBuildingEvent preparedEvent = null;
            string eventPrepareReason = string.Empty;
            bool hasPreparedEvent =
                buildingEventManager != null &&
                buildingEventManager.TryPrepareEvent(
                    player,
                    space,
                    targetSlot,
                    out preparedEvent,
                    out eventPrepareReason);

            if (!turnManager.TrySpendCurrentPlayerAP(requiredAP))
            {
                return Fail("탐색 AP 차감에 실패했습니다.");
            }

            if (!cardManager.RevealSlot(targetSlot))
            {
                turnManager.RestoreCurrentPlayerAP(requiredAP);
                return Fail("카드 공개에 실패하여 AP를 복구했습니다.");
            }

            Debug.Log(
                "[SearchManager] " +
                player.DisplayName +
                " 탐색 성공 / AP " +
                requiredAP.ToString("0.##") +
                " 사용 / 카드: " +
                targetSlot.DisplayName);

            OnSearchCardRevealed?.Invoke(player, space, targetSlot);
            OnSearchResolved?.Invoke(player, space, targetSlot);

            if (hasPreparedEvent && preparedEvent != null)
            {
                string startFailureReason;

                if (buildingEventManager.TryStartPreparedEvent(
                        preparedEvent,
                        out startFailureReason))
                {
                    // Ui_Util이 기존 카드 팝업을 열지 않게 null을 반환합니다.
                    // TurnManager의 소유 행동 토큰이 기존 CompleteAction 호출도 차단합니다.
                    revealedSlot = null;
                    return true;
                }

                Debug.LogWarning(
                    "[SearchManager] 이벤트 UI 시작 실패. 기존 카드 팝업으로 전환합니다. / " +
                    startFailureReason,
                    this);
            }
            else if (buildingEventManager != null &&
                     !string.IsNullOrWhiteSpace(eventPrepareReason))
            {
                Debug.LogWarning(
                    "[SearchManager] 이벤트 준비 실패. 기존 카드 팝업으로 전환합니다. / " +
                    eventPrepareReason,
                    this);
            }

            revealedSlot = targetSlot;
            return true;
        }

        public bool CanCurrentPlayerSearch(
            out float requiredAP,
            out string failureReason)
        {
            PlayerData player =
                turnManager != null
                    ? turnManager.CurrentPlayer
                    : null;

            return CanPlayerSearch(
                player,
                player != null ? player.CurrentAP : 0f,
                out requiredAP,
                out failureReason);
        }

        /// <summary>
        /// 아직 턴이 확정되지 않은 선택 후보도 검사할 수 있도록 플레이어와 사용 가능 AP를 직접 받습니다.
        /// </summary>
        public bool CanPlayerSearch(
            PlayerData player,
            float availableAP,
            out float requiredAP,
            out string failureReason)
        {
            BoardSpacePrefab ignoredSpace;
            BoardCardSlotData ignoredSlot;

            return TryGetSearchContext(
                player,
                availableAP,
                out ignoredSpace,
                out ignoredSlot,
                out requiredAP,
                out failureReason);
        }

        private bool TryGetSearchContext(
            PlayerData player,
            float availableAP,
            out BoardSpacePrefab space,
            out BoardCardSlotData targetSlot,
            out float requiredAP,
            out string failureReason)
        {
            space = null;
            targetSlot = null;
            requiredAP = 0f;
            failureReason = string.Empty;

            if (gridManager == null || cardManager == null)
            {
                failureReason = "탐색 시스템 미초기화";
                return false;
            }

            if (player == null ||
                string.IsNullOrWhiteSpace(player.CurrentNodeId) ||
                !gridManager.TryGetSpace(player.CurrentNodeId, out space) ||
                space == null)
            {
                failureReason = "플레이어 위치를 찾을 수 없음";
                return false;
            }

            if (!space.IsBuilding)
            {
                failureReason = "건물에서만 탐색 가능";
                return false;
            }

            if (!cardManager.TryGetRevealableSlot(space.NodeId, out targetSlot))
            {
                failureReason = "이 건물에는 더 이상 공개할 탐색 카드가 없습니다.";
                return false;
            }

            requiredAP = space.GetSearchAPCost(cardRevealAPCost);

            if (availableAP + 0.0001f < requiredAP)
            {
                failureReason =
                    "카드를 뒤집으려면 AP " +
                    requiredAP.ToString("0.##") +
                    "이 필요합니다.";
                return false;
            }

            return true;
        }

        private bool Fail(string message)
        {
            Debug.LogWarning("[SearchManager] " + message);
            OnSearchFailed?.Invoke(message);
            return false;
        }
    }
}

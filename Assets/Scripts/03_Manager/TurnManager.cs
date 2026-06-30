using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public enum TurnActionState
    {
        None,
        TurnStarting,
        AwaitingPlayerSelection,
        AwaitingAction,
        SelectingMove,
        ResolvingCard,
        Locked,
        TurnEnding,
        RoundEnding,
        GameEnded
    }

    /// <summary>
    /// SessionQueue 방식과 자동 생성 보드의 SimplePlayerList 방식을 함께 지원합니다.
    ///
    /// SimplePlayerList 규칙:
    /// - 라운드 시작 시 현재 플레이어는 없습니다.
    /// - UI에서 말을 선택하는 것만으로는 턴이 시작되지 않습니다.
    /// - 이동/탐색 등 AP 행동을 결정하는 순간 TryClaimPlayerTurn으로 턴을 확정합니다.
    /// - 턴이 확정된 뒤에는 해당 플레이어가 턴을 종료할 때까지 다른 말로 교체할 수 없습니다.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        // 기존 호환 이벤트
        public event Action<PlayerData> OnCurrentPlayerChanged;
        public event Action OnAllTurnsCompleted;
        public event Action<int> RoundStarted;
        public event Action<PlayerData> CurrentPlayerChanged;

        // UI/행동 이벤트
        public event Action<int> OnRoundStarted;
        public event Action<int> OnRoundCompleted;
        public event Action<PlayerData> OnTurnStarted;
        public event Action<PlayerData> OnTurnEnded;
        public event Action<PlayerData, float> OnAPChanged;
        public event Action<TurnActionState> OnActionStateChanged;
        public event Action OnGameTurnsCompleted;

        [Header("Simple Turn Rules")]
        [SerializeField, Min(1)] private int maximumRoundCount = 20;
        [SerializeField] private bool autoStartNextRound = true;
        [SerializeField] private bool autoEndTurnWhenAPIsEmpty = true;

        private GameSessionData sessionData;
        private GameStateManager stateManager;
        private GameLogManager logManager;

        private PlayerManager playerManager;
        private RadioEventManager radioEventManager;

        private readonly HashSet<string> completedSimplePlayerIds =
            new HashSet<string>();

        private int currentPlayerIndex = -1;
        private int roundNumber;
        private TurnRuntimeMode runtimeMode;
        private TurnActionState actionState = TurnActionState.None;
        private bool roundWaitingForContinue;

        private string currentActionOwner = string.Empty;
        private int currentActionToken;
        private int nextActionToken = 1;

        public int RoundNumber => roundNumber;
        public TurnActionState ActionState => actionState;
        public bool HasOwnedAction => !string.IsNullOrWhiteSpace(currentActionOwner);
        public string CurrentActionOwner => currentActionOwner;

        public bool IsActionLocked =>
            actionState == TurnActionState.Locked ||
            actionState == TurnActionState.ResolvingCard ||
            actionState == TurnActionState.TurnEnding ||
            actionState == TurnActionState.RoundEnding ||
            actionState == TurnActionState.GameEnded;

        public bool IsAwaitingPlayerSelection =>
            runtimeMode == TurnRuntimeMode.SimplePlayerList &&
            CurrentPlayer == null &&
            actionState == TurnActionState.AwaitingPlayerSelection;

        public bool HasClaimedPlayerTurn => CurrentPlayer != null;

        public bool CanEndCurrentTurn =>
            CurrentPlayer != null &&
            actionState == TurnActionState.AwaitingAction;

        public PlayerData CurrentPlayer
        {
            get
            {
                if (runtimeMode == TurnRuntimeMode.SessionQueue)
                {
                    if (sessionData == null || sessionData.TurnQueue == null)
                    {
                        return null;
                    }

                    return sessionData.FindPlayer(
                        sessionData.TurnQueue.CurrentPlayerId);
                }

                if (runtimeMode == TurnRuntimeMode.SimplePlayerList)
                {
                    if (playerManager == null ||
                        playerManager.Players.Count == 0 ||
                        currentPlayerIndex < 0 ||
                        currentPlayerIndex >= playerManager.Players.Count)
                    {
                        return null;
                    }

                    return playerManager.Players[currentPlayerIndex];
                }

                return null;
            }
        }

        public void Initialize(
            GameSessionData session,
            GameStateManager gameState,
            GameLogManager log)
        {
            sessionData = session;
            stateManager = gameState;
            logManager = log;
            playerManager = null;
            radioEventManager = null;
            runtimeMode = TurnRuntimeMode.SessionQueue;
            currentPlayerIndex = -1;
            completedSimplePlayerIds.Clear();
            roundWaitingForContinue = false;
            ClearActionOwnership();
            SetActionState(TurnActionState.None);
        }

        public void Initialize(
            PlayerManager runtimePlayerManager,
            RadioEventManager runtimeRadioEventManager,
            GameLogManager runtimeLogManager = null)
        {
            playerManager = runtimePlayerManager;
            radioEventManager = runtimeRadioEventManager;
            logManager = runtimeLogManager;
            sessionData = null;
            stateManager = null;
            runtimeMode = TurnRuntimeMode.SimplePlayerList;
            currentPlayerIndex = -1;
            completedSimplePlayerIds.Clear();
            roundWaitingForContinue = false;
            ClearActionOwnership();
            SetActionState(TurnActionState.None);
        }

        public void BeginRoundQueue()
        {
            if (sessionData == null || sessionData.TurnQueue == null)
            {
                InvokeAllTurnsCompleted();
                return;
            }

            List<string> activeIds = new List<string>();

            for (int index = 0; index < sessionData.Players.Count; index++)
            {
                PlayerData player = sessionData.Players[index];

                if (player != null && player.CanTakeTurn)
                {
                    activeIds.Add(player.PlayerId);
                }
            }

            sessionData.TurnQueue.Build(activeIds);

            if (!sessionData.TurnQueue.HasCurrent)
            {
                InvokeAllTurnsCompleted();
                return;
            }

            StartCurrentSessionTurn();
        }

        public void StartGameTurns()
        {
            if (runtimeMode != TurnRuntimeMode.SimplePlayerList)
            {
                return;
            }

            roundNumber = 0;
            currentPlayerIndex = -1;
            completedSimplePlayerIds.Clear();
            roundWaitingForContinue = false;
            StartNextSimpleRound();
        }

        /// <summary>
        /// SimplePlayerList 모드에서 선택된 플레이어의 턴을 확정합니다.
        /// 말 클릭만으로는 호출하지 않고, 이동/탐색 등 행동 버튼을 누른 시점에 호출합니다.
        /// </summary>
        public bool TryClaimPlayerTurn(string playerId)
        {
            if (runtimeMode != TurnRuntimeMode.SimplePlayerList ||
                actionState != TurnActionState.AwaitingPlayerSelection ||
                CurrentPlayer != null ||
                !CanSelectPlayerForTurn(playerId))
            {
                return false;
            }

            int playerIndex = FindSimplePlayerIndex(playerId);

            if (playerIndex < 0)
            {
                return false;
            }

            currentPlayerIndex = playerIndex;
            BeginCurrentSimpleTurn();
            return CurrentPlayer != null;
        }

        public bool CanSelectPlayerForTurn(string playerId)
        {
            if (runtimeMode != TurnRuntimeMode.SimplePlayerList ||
                actionState != TurnActionState.AwaitingPlayerSelection ||
                string.IsNullOrWhiteSpace(playerId) ||
                playerManager == null)
            {
                return false;
            }

            PlayerData player = playerManager.FindPlayer(playerId);

            return player != null &&
                   player.CanTakeTurn &&
                   !completedSimplePlayerIds.Contains(player.PlayerId);
        }

        public bool HasPlayerCompletedTurn(string playerId)
        {
            return !string.IsNullOrWhiteSpace(playerId) &&
                   completedSimplePlayerIds.Contains(playerId);
        }

        public bool TryBeginAction(TurnActionState requestedState)
        {
            if (CurrentPlayer == null ||
                actionState != TurnActionState.AwaitingAction)
            {
                return false;
            }

            if (requestedState == TurnActionState.None ||
                requestedState == TurnActionState.AwaitingPlayerSelection ||
                requestedState == TurnActionState.AwaitingAction ||
                requestedState == TurnActionState.TurnEnding ||
                requestedState == TurnActionState.RoundEnding ||
                requestedState == TurnActionState.GameEnded)
            {
                return false;
            }

            ClearActionOwnership();
            SetActionState(requestedState);
            return true;
        }

        /// <summary>
        /// 이미 시작된 행동의 완료 권한을 특정 시스템에 귀속시킵니다.
        /// 사건 처리 중 기존 UI가 CompleteAction을 호출해도 잠금이 풀리지 않게 합니다.
        /// </summary>
        public bool TryAdoptCurrentAction(
            string ownerId,
            out int actionToken)
        {
            actionToken = 0;

            if (CurrentPlayer == null ||
                string.IsNullOrWhiteSpace(ownerId) ||
                actionState == TurnActionState.None ||
                actionState == TurnActionState.AwaitingAction ||
                actionState == TurnActionState.AwaitingPlayerSelection ||
                actionState == TurnActionState.TurnEnding ||
                actionState == TurnActionState.RoundEnding ||
                actionState == TurnActionState.GameEnded ||
                HasOwnedAction)
            {
                return false;
            }

            currentActionOwner = ownerId;
            currentActionToken = nextActionToken++;

            if (nextActionToken <= 0)
            {
                nextActionToken = 1;
            }

            actionToken = currentActionToken;
            return true;
        }

        /// <summary>
        /// 행동을 소유한 시스템만 현재 행동을 완료할 수 있습니다.
        /// </summary>
        public bool CompleteOwnedAction(
            int actionToken,
            string ownerId,
            bool autoEndIfNoAP)
        {
            if (!ValidateActionOwner(actionToken, ownerId))
            {
                return false;
            }

            ClearActionOwnership();
            CompleteActionInternal(autoEndIfNoAP);
            return true;
        }

        /// <summary>
        /// 오류나 비활성화 상황에서 소유 행동을 안전하게 복구합니다.
        /// </summary>
        public bool ForceRecoverOwnedAction(
            int actionToken,
            string ownerId,
            bool autoEndIfNoAP)
        {
            if (!ValidateActionOwner(actionToken, ownerId))
            {
                return false;
            }

            ClearActionOwnership();
            CompleteActionInternal(autoEndIfNoAP);
            return true;
        }

        public void CompleteAction()
        {
            CompleteAction(true);
        }

        /// <summary>
        /// 카드 팝업을 추가로 해결해야 하는 경우 autoEndIfNoAP를 false로 전달할 수 있습니다.
        /// </summary>
        public void CompleteAction(bool autoEndIfNoAP)
        {
            if (HasOwnedAction)
            {
                return;
            }

            CompleteActionInternal(autoEndIfNoAP);
        }

        private void CompleteActionInternal(bool autoEndIfNoAP)
        {
            PlayerData current = CurrentPlayer;

            if (current == null || actionState == TurnActionState.GameEnded)
            {
                return;
            }

            SetActionState(TurnActionState.AwaitingAction);

            if (runtimeMode == TurnRuntimeMode.SimplePlayerList &&
                autoEndTurnWhenAPIsEmpty &&
                autoEndIfNoAP &&
                current.CurrentAP <= 0.0001f)
            {
                TryEndCurrentTurn();
            }
        }

        public void CancelAction()
        {
            if (CurrentPlayer == null || HasOwnedAction)
            {
                return;
            }

            if (actionState == TurnActionState.SelectingMove ||
                actionState == TurnActionState.Locked)
            {
                SetActionState(TurnActionState.AwaitingAction);
            }
        }

        public bool TrySpendCurrentPlayerAP(float cost)
        {
            PlayerData current = CurrentPlayer;

            if (current == null || !current.TrySpendAP(cost))
            {
                return false;
            }

            OnAPChanged?.Invoke(current, current.CurrentAP);
            return true;
        }

        public void RestoreCurrentPlayerAP(float amount)
        {
            PlayerData current = CurrentPlayer;

            if (current == null || amount <= 0f)
            {
                return;
            }

            current.CurrentAP = Mathf.Min(
                current.Speed,
                current.CurrentAP + amount);
            OnAPChanged?.Invoke(current, current.CurrentAP);
        }

        [ContextMenu("Test/End Current Turn")]
        public void EndCurrentTurn()
        {
            TryEndCurrentTurn();
        }

        public bool TryEndCurrentTurn()
        {
            if (!CanEndCurrentTurn)
            {
                return false;
            }

            ClearActionOwnership();
            SetActionState(TurnActionState.TurnEnding);

            if (runtimeMode == TurnRuntimeMode.SessionQueue)
            {
                EndSessionQueueTurn();
                return true;
            }

            if (runtimeMode == TurnRuntimeMode.SimplePlayerList)
            {
                EndSimplePlayerTurn();
                return true;
            }

            SetActionState(TurnActionState.None);
            return false;
        }

        public bool ContinueToNextRound()
        {
            if (runtimeMode != TurnRuntimeMode.SimplePlayerList ||
                !roundWaitingForContinue ||
                actionState == TurnActionState.GameEnded)
            {
                return false;
            }

            roundWaitingForContinue = false;
            StartNextSimpleRound();
            return true;
        }

        private void EndSessionQueueTurn()
        {
            PlayerData current = CurrentPlayer;

            if (current == null ||
                sessionData == null ||
                sessionData.TurnQueue == null)
            {
                SetActionState(TurnActionState.AwaitingAction);
                return;
            }

            stateManager?.SetPlayerTurnPhase(PlayerTurnPhase.TurnEnd);
            logManager?.AddLog(
                LogCategory.Turn,
                current.PlayerName + " 턴 종료.");

            OnTurnEnded?.Invoke(current);

            if (sessionData.TurnQueue.MoveNext())
            {
                StartCurrentSessionTurn();
            }
            else
            {
                stateManager?.SetPlayerTurnPhase(PlayerTurnPhase.None);
                SetActionState(TurnActionState.RoundEnding);
                InvokeAllTurnsCompleted();
            }
        }

        private void StartCurrentSessionTurn()
        {
            PlayerData current = CurrentPlayer;

            if (current == null)
            {
                InvokeAllTurnsCompleted();
                return;
            }

            SetActionState(TurnActionState.TurnStarting);
            current.ResetAP();
            OnAPChanged?.Invoke(current, current.CurrentAP);

            stateManager?.SetPlayerTurnPhase(PlayerTurnPhase.TurnStart);
            logManager?.AddLog(
                LogCategory.Turn,
                current.PlayerName +
                " 턴 시작. AP " +
                current.CurrentAP.ToString("0.##"));

            InvokeCurrentPlayerChanged(current);
            OnTurnStarted?.Invoke(current);

            stateManager?.SetPlayerTurnPhase(PlayerTurnPhase.MoveSelect);
            SetActionState(TurnActionState.AwaitingAction);
        }

        private void EndSimplePlayerTurn()
        {
            PlayerData current = CurrentPlayer;

            if (current == null || playerManager == null)
            {
                SetActionState(TurnActionState.AwaitingAction);
                return;
            }

            logManager?.AddLog(
                LogCategory.Turn,
                current.PlayerName + " 턴 종료.");

            completedSimplePlayerIds.Add(current.PlayerId);
            OnTurnEnded?.Invoke(current);

            currentPlayerIndex = -1;
            InvokeCurrentPlayerChanged(null);

            if (AreAllActiveSimplePlayersCompleted())
            {
                FinishSimpleRound();
                return;
            }

            SetActionState(TurnActionState.AwaitingPlayerSelection);
        }

        private void FinishSimpleRound()
        {
            currentPlayerIndex = -1;
            SetActionState(TurnActionState.RoundEnding);
            radioEventManager?.ResolveRoundEnd();

            OnRoundCompleted?.Invoke(roundNumber);
            InvokeAllTurnsCompleted();

            if (roundNumber >= Mathf.Max(1, maximumRoundCount))
            {
                SetActionState(TurnActionState.GameEnded);
                OnGameTurnsCompleted?.Invoke();
                return;
            }

            roundWaitingForContinue = true;

            if (autoStartNextRound)
            {
                ContinueToNextRound();
            }
        }

        private void StartNextSimpleRound()
        {
            if (playerManager == null ||
                playerManager.Players.Count == 0 ||
                GetActiveSimplePlayerCount() == 0)
            {
                currentPlayerIndex = -1;
                SetActionState(TurnActionState.GameEnded);
                OnGameTurnsCompleted?.Invoke();
                return;
            }

            roundNumber++;
            currentPlayerIndex = -1;
            completedSimplePlayerIds.Clear();

            RoundStarted?.Invoke(roundNumber);
            OnRoundStarted?.Invoke(roundNumber);
            InvokeCurrentPlayerChanged(null);
            SetActionState(TurnActionState.AwaitingPlayerSelection);
        }

        private void BeginCurrentSimpleTurn()
        {
            PlayerData current = CurrentPlayer;

            if (current == null ||
                !current.CanTakeTurn ||
                completedSimplePlayerIds.Contains(current.PlayerId))
            {
                currentPlayerIndex = -1;
                SetActionState(TurnActionState.AwaitingPlayerSelection);
                return;
            }

            SetActionState(TurnActionState.TurnStarting);
            current.ResetAP();
            OnAPChanged?.Invoke(current, current.CurrentAP);

            InvokeCurrentPlayerChanged(current);
            OnTurnStarted?.Invoke(current);

            logManager?.AddLog(
                LogCategory.Turn,
                current.PlayerName +
                " 턴 확정. AP " +
                current.CurrentAP.ToString("0.##"));

            Debug.Log(
                "[TurnManager] " +
                current.PlayerName +
                " 턴 확정 / AP " +
                current.CurrentAP.ToString("0.##"));

            SetActionState(TurnActionState.AwaitingAction);
        }

        private int GetActiveSimplePlayerCount()
        {
            if (playerManager == null)
            {
                return 0;
            }

            int count = 0;

            for (int index = 0; index < playerManager.Players.Count; index++)
            {
                PlayerData player = playerManager.Players[index];

                if (player != null && player.CanTakeTurn)
                {
                    count++;
                }
            }

            return count;
        }

        private bool AreAllActiveSimplePlayersCompleted()
        {
            if (playerManager == null)
            {
                return true;
            }

            for (int index = 0; index < playerManager.Players.Count; index++)
            {
                PlayerData player = playerManager.Players[index];

                if (player == null || !player.CanTakeTurn)
                {
                    continue;
                }

                if (!completedSimplePlayerIds.Contains(player.PlayerId))
                {
                    return false;
                }
            }

            return true;
        }

        private int FindSimplePlayerIndex(string playerId)
        {
            if (playerManager == null || string.IsNullOrWhiteSpace(playerId))
            {
                return -1;
            }

            for (int index = 0; index < playerManager.Players.Count; index++)
            {
                PlayerData player = playerManager.Players[index];

                if (player != null && player.PlayerId == playerId)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool ValidateActionOwner(
            int actionToken,
            string ownerId)
        {
            return HasOwnedAction &&
                   currentActionToken == actionToken &&
                   string.Equals(
                       currentActionOwner,
                       ownerId,
                       StringComparison.Ordinal);
        }

        private void ClearActionOwnership()
        {
            currentActionOwner = string.Empty;
            currentActionToken = 0;
        }

        private void SetActionState(TurnActionState nextState)
        {
            if (actionState == nextState)
            {
                return;
            }

            actionState = nextState;
            OnActionStateChanged?.Invoke(actionState);
        }

        private void InvokeCurrentPlayerChanged(PlayerData player)
        {
            OnCurrentPlayerChanged?.Invoke(player);
            CurrentPlayerChanged?.Invoke(player);
        }

        private void InvokeAllTurnsCompleted()
        {
            OnAllTurnsCompleted?.Invoke();
        }

        private enum TurnRuntimeMode
        {
            None,
            SessionQueue,
            SimplePlayerList
        }
    }
}

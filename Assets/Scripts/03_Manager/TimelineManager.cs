using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 현재 우선 구현 범위만 처리합니다.
    /// 플레이어 전체 턴 -> 라디오 카드 갱신/3장 배치 -> 다음 라운드 순서입니다.
    /// </summary>
    public class TimelineManager : MonoBehaviour
    {
        private GameSessionData sessionData;
        private GameRuleSOBJ ruleSOBJ;
        private GameStateManager stateManager;
        private TurnManager turnManager;
        private RadioEventManager radioEventManager;
        private GameLogManager logManager;
        private bool initialized;

        public void Initialize(
            GameSessionData session,
            GameRuleSOBJ rules,
            GameStateManager gameState,
            TurnManager turns,
            RadioEventManager radioEvents,
            GameLogManager log)
        {
            if (turnManager != null)
            {
                turnManager.OnAllTurnsCompleted -= HandleAllTurnsCompleted;
            }

            sessionData = session;
            ruleSOBJ = rules;
            stateManager = gameState;
            turnManager = turns;
            radioEventManager = radioEvents;
            logManager = log;

            turnManager.OnAllTurnsCompleted += HandleAllTurnsCompleted;
            initialized = true;
        }

        private void OnDestroy()
        {
            if (turnManager != null)
            {
                turnManager.OnAllTurnsCompleted -= HandleAllTurnsCompleted;
            }
        }

        public void StartGame()
        {
            if (!initialized || sessionData.GameEnded)
            {
                return;
            }

            stateManager.SetGamePhase(GamePhase.Setup);
            logManager?.AddLog(LogCategory.System, "보드 생성 및 초기 배치 완료.");
            StartNextRound();
        }

        private void StartNextRound()
        {
            int nextRound = sessionData.CurrentRound + 1;
            if (nextRound > ruleSOBJ.MaxRounds)
            {
                EndGame("최대 라운드에 도달했습니다.");
                return;
            }

            sessionData.SetRound(nextRound);
            stateManager.SetGamePhase(GamePhase.PlayerTurnQueue);
            logManager?.AddLog(LogCategory.Round, $"라운드 {nextRound} 시작.");
            turnManager.BeginRoundQueue();
        }

        private void HandleAllTurnsCompleted()
        {
            stateManager.SetGamePhase(GamePhase.RadioBroadcast);
            logManager?.AddLog(LogCategory.Radio, "모든 플레이어 행동 종료. 라디오 카드 갱신을 시작합니다.");
            radioEventManager.ProcessEndOfRound();
            StartNextRound();
        }

        private void EndGame(string reason)
        {
            sessionData.EndGame();
            stateManager.SetGamePhase(GamePhase.GameOver);
            logManager?.AddLog(LogCategory.System, $"게임 종료: {reason}");
        }
    }
}

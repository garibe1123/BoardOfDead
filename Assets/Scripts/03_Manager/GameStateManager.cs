using System;
using UnityEngine;

namespace BoardOfDead
{
    public class GameStateManager : MonoBehaviour
    {
        public event Action<GamePhase> OnGamePhaseChanged;
        public event Action<PlayerTurnPhase> OnPlayerTurnPhaseChanged;

        private GameSessionData sessionData;

        public void Initialize(GameSessionData session)
        {
            sessionData = session;
            SetGamePhase(GamePhase.None);
            SetPlayerTurnPhase(PlayerTurnPhase.None);
        }

        public void SetGamePhase(GamePhase phase)
        {
            if (sessionData == null)
            {
                return;
            }

            sessionData.SetGamePhase(phase);
            OnGamePhaseChanged?.Invoke(phase);
        }

        public void SetPlayerTurnPhase(PlayerTurnPhase phase)
        {
            if (sessionData == null)
            {
                return;
            }

            sessionData.SetPlayerTurnPhase(phase);
            OnPlayerTurnPhaseChanged?.Invoke(phase);
        }
    }
}

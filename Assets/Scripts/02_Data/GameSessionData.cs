using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
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
        [SerializeField] private List<RadioCardData> radioCards = new List<RadioCardData>();
        [SerializeField] private List<BoardConnectionData> connections = new List<BoardConnectionData>();
        [SerializeField] private List<VehicleData> vehicles = new List<VehicleData>();
        [SerializeField] private List<EscapeRouteData> escapeRoutes = new List<EscapeRouteData>();
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
        public void SetPlayerTurnPhase(PlayerTurnPhase phase) => currentPlayerTurnPhase = phase;
        public void EndGame() => gameEnded = true;

        public PlayerData FindPlayer(string playerId) => players.Find(x => x.PlayerId == playerId);
        public NodeData FindNode(string nodeId) => nodes.Find(x => x.NodeId == nodeId);
        public CardData FindCard(string instanceId) => cards.Find(x => x.CardInstanceId == instanceId);
        public RadioCardData FindRadioCard(string instanceId) => radioCards.Find(x => x.RadioCardInstanceId == instanceId);
        public VehicleData FindVehicle(string instanceId) => vehicles.Find(x => x.VehicleInstanceId == instanceId);
        public EscapeRouteData FindEscapeRoute(string instanceId) => escapeRoutes.Find(x => x.EscapeRouteInstanceId == instanceId);
        public BoardConnectionData FindConnection(string startNodeId, string destinationNodeId) =>
            connections.Find(x => x.Connects(startNodeId, destinationNodeId));
    }
}

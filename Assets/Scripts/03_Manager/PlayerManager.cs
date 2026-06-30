using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public class PlayerManager : MonoBehaviour
    {
        public event Action OnPlayersRebuilt;
        public event Action<PlayerData, PlayerBoardPrefab> OnPlayerViewCreated;
        public event Action<PlayerData, PlayerBoardPrefab> OnPlayerClicked;
        public event Action<PlayerData, BoardSpacePrefab, BoardSpacePrefab> OnPlayerMoved;

        private readonly List<PlayerData> players =
            new List<PlayerData>();

        private readonly Dictionary<string, PlayerBoardPrefab> viewsById =
            new Dictionary<string, PlayerBoardPrefab>();

        private GridManager gridManager;

        public IList<PlayerData> Players
        {
            get { return players.AsReadOnly(); }
        }

        public void Initialize(GridManager runtimeGridManager)
        {
            gridManager = runtimeGridManager;
        }

        public void ClearPlayers()
        {
            foreach (KeyValuePair<string, PlayerBoardPrefab> pair in viewsById)
            {
                PlayerBoardPrefab view = pair.Value;

                if (view != null)
                {
                    view.Clicked -= HandlePlayerViewClicked;
                    Destroy(view.gameObject);
                }
            }

            viewsById.Clear();
            players.Clear();
            OnPlayersRebuilt?.Invoke();
        }

        public void CreateDefaultPlayers(
            int playerCount,
            DefaultPlayerSettingData setting,
            BoardSpacePrefab startSpace,
            Transform playerRoot)
        {
            CreatePlayers(
                playerCount,
                null,
                setting,
                startSpace,
                playerRoot);
        }

        public void CreatePlayers(
            int playerCount,
            IList<PlayerSpawnSettingData> selections,
            DefaultPlayerSettingData defaultSetting,
            BoardSpacePrefab startSpace,
            Transform playerRoot)
        {
            ClearPlayers();

            if (defaultSetting == null)
            {
                defaultSetting = new DefaultPlayerSettingData();
            }

            int safeCount = Mathf.Clamp(playerCount, 1, 6);
            string startNodeId =
                startSpace != null
                    ? startSpace.NodeId
                    : string.Empty;

            for (int index = 0; index < safeCount; index++)
            {
                string playerId =
                    "PLAYER-" + (index + 1).ToString("00");

                PlayerSpawnSettingData selection =
                    selections != null && index < selections.Count
                        ? selections[index]
                        : null;

                bool hasSelectedCharacter =
                    selection != null &&
                    selection.Enabled &&
                    selection.PlayerPresetSOBJ != null;

                PlayerData data =
                    hasSelectedCharacter
                        ? new PlayerData(
                            playerId,
                            selection,
                            startNodeId)
                        : new PlayerData(
                            playerId,
                            defaultSetting,
                            index + 1,
                            startNodeId);

                PlayerBoardPrefab view =
                    CreatePlayerView(
                        data,
                        defaultSetting,
                        playerRoot);

                players.Add(data);
                viewsById.Add(data.PlayerId, view);

                view.Clicked += HandlePlayerViewClicked;

                if (startSpace != null)
                {
                    startSpace.AddPlayer(view);
                }

                OnPlayerViewCreated?.Invoke(data, view);
            }

            OnPlayersRebuilt?.Invoke();
        }

        public bool CanMovePlayer(
            string playerId,
            string destinationNodeId,
            bool requireConnected,
            out PlayerData player,
            out BoardSpacePrefab current,
            out BoardSpacePrefab destination)
        {
            player = null;
            current = null;
            destination = null;

            if (gridManager == null ||
                string.IsNullOrWhiteSpace(playerId) ||
                string.IsNullOrWhiteSpace(destinationNodeId))
            {
                return false;
            }

            player = FindPlayer(playerId);

            if (player == null || !player.CanTakeTurn)
            {
                return false;
            }

            if (!gridManager.TryGetSpace(destinationNodeId, out destination) ||
                destination == null ||
                !destination.Enterable)
            {
                return false;
            }

            if (destination.Players.Count >= 6)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(player.CurrentNodeId))
            {
                gridManager.TryGetSpace(player.CurrentNodeId, out current);
            }

            if (current == destination)
            {
                return true;
            }

            if (requireConnected &&
                (current == null ||
                 !current.ConnectedSpaces.Contains(destination)))
            {
                return false;
            }

            return viewsById.ContainsKey(playerId);
        }

        public bool TryMovePlayer(
            string playerId,
            string destinationNodeId)
        {
            return TryMovePlayer(
                playerId,
                destinationNodeId,
                true);
        }

        public bool TryMovePlayer(
            string playerId,
            string destinationNodeId,
            bool requireConnected)
        {
            PlayerData player;
            BoardSpacePrefab current;
            BoardSpacePrefab destination;

            if (!CanMovePlayer(
                    playerId,
                    destinationNodeId,
                    requireConnected,
                    out player,
                    out current,
                    out destination))
            {
                return false;
            }

            if (current == destination)
            {
                return true;
            }

            PlayerBoardPrefab view;

            if (!viewsById.TryGetValue(playerId, out view) || view == null)
            {
                return false;
            }

            // 목적지 등록을 먼저 성공시켜 현재 칸에서 말이 유실되는 상황을 막습니다.
            if (!destination.AddPlayer(view))
            {
                return false;
            }

            if (current != null)
            {
                current.RemovePlayer(view);
            }

            player.CurrentNodeId = destination.NodeId;
            OnPlayerMoved?.Invoke(player, current, destination);
            return true;
        }

        public PlayerData FindPlayer(string playerId)
        {
            for (int index = 0; index < players.Count; index++)
            {
                if (players[index] != null &&
                    players[index].PlayerId == playerId)
                {
                    return players[index];
                }
            }

            return null;
        }

        public bool TryGetPlayerView(
            string playerId,
            out PlayerBoardPrefab view)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                view = null;
                return false;
            }

            return viewsById.TryGetValue(playerId, out view) &&
                   view != null;
        }

        public PlayerBoardPrefab GetPlayerView(string playerId)
        {
            PlayerBoardPrefab view;
            TryGetPlayerView(playerId, out view);
            return view;
        }

        private void HandlePlayerViewClicked(PlayerBoardPrefab view)
        {
            if (view == null || view.PlayerData == null)
            {
                return;
            }

            OnPlayerClicked?.Invoke(view.PlayerData, view);
        }

        private static PlayerBoardPrefab CreatePlayerView(
            PlayerData data,
            DefaultPlayerSettingData setting,
            Transform playerRoot)
        {
            GameObject playerObject;
            GameObject sourcePrefab = setting.PlayerPrefabObject;

            if (sourcePrefab != null)
            {
                playerObject =
                    UnityEngine.Object.Instantiate(
                        sourcePrefab,
                        playerRoot);
            }
            else
            {
                playerObject =
                    GameObject.CreatePrimitive(PrimitiveType.Capsule);

                playerObject.name = "Runtime_TestPlayer";
                playerObject.transform.SetParent(playerRoot, false);
            }

            PlayerBoardPrefab view =
                playerObject.GetComponent<PlayerBoardPrefab>();

            if (view == null)
            {
                view = playerObject.AddComponent<PlayerBoardPrefab>();
            }

            view.Bind(
                data,
                setting.BoardScaleMultiplier);

            return view;
        }
    }
}

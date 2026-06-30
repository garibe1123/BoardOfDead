using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
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
        [SerializeField] private List<string> activeRadioCardInstanceIds = new List<string>();

        [Header("Compatibility Runtime Lists")]
        [SerializeField] private List<string> hiddenCardInstanceIds = new List<string>();
        [SerializeField] private List<string> revealedCardInstanceIds = new List<string>();
        [SerializeField] private List<string> vehicleInstanceIds = new List<string>();
        [SerializeField] private List<string> escapeRouteInstanceIds = new List<string>();

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
                    if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int parsed))
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
        public IReadOnlyList<string> ActiveRadioCardInstanceIds => activeRadioCardInstanceIds;
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
            if (!string.IsNullOrWhiteSpace(otherNodeId) && !adjacentNodeIds.Contains(otherNodeId))
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
            if (!string.IsNullOrWhiteSpace(cardInstanceId) && !hiddenCardInstanceIds.Contains(cardInstanceId))
            {
                hiddenCardInstanceIds.Add(cardInstanceId);
            }
        }

        public void RevealCard(string cardInstanceId)
        {
            hiddenCardInstanceIds.Remove(cardInstanceId);
            if (!string.IsNullOrWhiteSpace(cardInstanceId) && !revealedCardInstanceIds.Contains(cardInstanceId))
            {
                revealedCardInstanceIds.Add(cardInstanceId);
            }
        }

        public void AddVehicle(string vehicleInstanceId)
        {
            if (!string.IsNullOrWhiteSpace(vehicleInstanceId) && !vehicleInstanceIds.Contains(vehicleInstanceId))
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
            if (!string.IsNullOrWhiteSpace(escapeRouteInstanceId) && !escapeRouteInstanceIds.Contains(escapeRouteInstanceId))
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
}

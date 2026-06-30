using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [DisallowMultipleComponent]
    public class GameBoardPrefab : MonoBehaviour
    {
        [SerializeField] private BoardSpacePrefab defaultStartSpace;
        [SerializeField] private List<DistrictPrefab> districts = new List<DistrictPrefab>();
        [SerializeField] private List<BoardSpacePrefab> boardSpaces = new List<BoardSpacePrefab>();

        private readonly Dictionary<string, BoardSpacePrefab> spaceByNodeId = new Dictionary<string, BoardSpacePrefab>();
        private readonly Dictionary<Vector2Int, BoardSpacePrefab> spaceByCoordinate = new Dictionary<Vector2Int, BoardSpacePrefab>();

        public IReadOnlyList<DistrictPrefab> Districts => districts;
        public IReadOnlyList<BoardSpacePrefab> BoardSpaces => boardSpaces;
        public BoardSpacePrefab DefaultStartSpace => defaultStartSpace;

        public void RebuildLookup()
        {
            districts.Clear();
            boardSpaces.Clear();
            spaceByNodeId.Clear();
            spaceByCoordinate.Clear();

            districts.AddRange(GetComponentsInChildren<DistrictPrefab>(true));
            boardSpaces.AddRange(GetComponentsInChildren<BoardSpacePrefab>(true));

            foreach (BoardSpacePrefab space in boardSpaces)
            {
                if (space == null || string.IsNullOrWhiteSpace(space.NodeId))
                {
                    continue;
                }

                if (!spaceByNodeId.ContainsKey(space.NodeId))
                {
                    spaceByNodeId.Add(space.NodeId, space);
                }

                if (!spaceByCoordinate.ContainsKey(space.BoardCoordinate))
                {
                    spaceByCoordinate.Add(space.BoardCoordinate, space);
                }
            }

            if (defaultStartSpace == null || defaultStartSpace.SpaceType == BoardSpaceType.Empty)
            {
                defaultStartSpace = boardSpaces.Find(x => x != null && x.SpaceType == BoardSpaceType.Road && x.Enterable);
            }

            if (defaultStartSpace == null)
            {
                defaultStartSpace = boardSpaces.Find(x => x != null && x.Enterable);
            }
        }

        public void SetDefaultStartSpace(BoardSpacePrefab space)
        {
            defaultStartSpace = space;
        }

        public BoardSpacePrefab FindSpace(string nodeId)
        {
            if (spaceByNodeId.Count == 0)
            {
                RebuildLookup();
            }

            spaceByNodeId.TryGetValue(nodeId, out BoardSpacePrefab result);
            return result;
        }

        public BoardSpacePrefab FindSpace(Vector2Int coordinate)
        {
            if (spaceByCoordinate.Count == 0)
            {
                RebuildLookup();
            }

            spaceByCoordinate.TryGetValue(coordinate, out BoardSpacePrefab result);
            return result;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public class GridManager : MonoBehaviour
    {
        private readonly Dictionary<string, BoardSpacePrefab> spacesById =
            new Dictionary<string, BoardSpacePrefab>();

        private readonly List<BoardSpacePrefab> roadSpaces =
            new List<BoardSpacePrefab>();

        private readonly List<BoardSpacePrefab> buildingSpaces =
            new List<BoardSpacePrefab>();

        public IList<BoardSpacePrefab> RoadSpaces
        {
            get { return roadSpaces.AsReadOnly(); }
        }

        public IList<BoardSpacePrefab> BuildingSpaces
        {
            get { return buildingSpaces.AsReadOnly(); }
        }

        public IEnumerable<BoardSpacePrefab> AllSpaces
        {
            get { return spacesById.Values; }
        }

        public void ClearAll()
        {
            spacesById.Clear();
            roadSpaces.Clear();
            buildingSpaces.Clear();
        }

        public bool RegisterSpace(BoardSpacePrefab space)
        {
            if (space == null || string.IsNullOrWhiteSpace(space.NodeId))
            {
                return false;
            }

            if (spacesById.ContainsKey(space.NodeId))
            {
                Debug.LogError(
                    "[GridManager] NodeId가 중복되었습니다: " + space.NodeId,
                    space);

                return false;
            }

            spacesById.Add(space.NodeId, space);

            if (space.SpaceType == BoardSpaceType.Road)
            {
                roadSpaces.Add(space);
            }
            else if (space.SpaceType == BoardSpaceType.Building)
            {
                buildingSpaces.Add(space);
            }

            return true;
        }

        public bool TryGetSpace(
            string nodeId,
            out BoardSpacePrefab space)
        {
            return spacesById.TryGetValue(nodeId, out space);
        }

        public BoardSpacePrefab GetNearestRoad(Vector3 worldPosition)
        {
            BoardSpacePrefab nearest = null;
            float nearestDistance = float.PositiveInfinity;

            for (int index = 0; index < roadSpaces.Count; index++)
            {
                BoardSpacePrefab road = roadSpaces[index];
                float distance =
                    (road.transform.position - worldPosition).sqrMagnitude;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = road;
                }
            }

            return nearest;
        }

        public List<BoardSpacePrefab> GetRandomDistinctBuildings(
            System.Random random,
            int count)
        {
            List<BoardSpacePrefab> candidates =
                new List<BoardSpacePrefab>(buildingSpaces);

            for (int index = candidates.Count - 1;
                 index > 0;
                 index--)
            {
                int swapIndex = random.Next(0, index + 1);
                BoardSpacePrefab temp = candidates[index];
                candidates[index] = candidates[swapIndex];
                candidates[swapIndex] = temp;
            }

            int takeCount = Mathf.Clamp(count, 0, candidates.Count);

            if (takeCount < candidates.Count)
            {
                candidates.RemoveRange(
                    takeCount,
                    candidates.Count - takeCount);
            }

            return candidates;
        }

        public void TickRadioCards()
        {
            for (int index = 0;
                 index < buildingSpaces.Count;
                 index++)
            {
                buildingSpaces[index].TickRadioCard();
            }
        }
    }
}

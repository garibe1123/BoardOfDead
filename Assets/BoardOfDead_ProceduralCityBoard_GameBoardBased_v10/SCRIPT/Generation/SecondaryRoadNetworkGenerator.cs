using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [DisallowMultipleComponent]
    public sealed class SupplementalRoadNodeMarker : MonoBehaviour
    {
    }

    public static class SecondaryRoadNetworkGenerator
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public static int Expand(
            GameBoardRuntimeSnapshot snapshot,
            GridManager gridManager,
            Transform parent,
            int seed,
            float branchChance,
            int minimumLength,
            int maximumLength,
            float turnChance,
            float diagonalChance,
            int maximumNewRoadCells,
            int boundsExpansionCells,
            int buildingClearanceCells,
            int movementAPCost)
        {
            if (snapshot == null ||
                !snapshot.IsValid ||
                gridManager == null ||
                maximumNewRoadCells <= 0)
            {
                return 0;
            }

            int safeMinimumLength = Mathf.Max(1, minimumLength);
            int safeMaximumLength = Mathf.Max(
                safeMinimumLength,
                maximumLength);

            float safeBranchChance = Mathf.Clamp01(branchChance);
            float safeTurnChance = Mathf.Clamp01(turnChance);
            float safeDiagonalChance = Mathf.Clamp01(diagonalChance);
            int safeBoundsExpansion = Mathf.Max(0, boundsExpansionCells);
            int safeBuildingClearance = Mathf.Max(0, buildingClearanceCells);

            HashSet<Vector2Int> occupiedRoads =
                new HashSet<Vector2Int>(snapshot.RoadByCoordinate.Keys);

            HashSet<Vector2Int> reservedBuildingCells =
                BuildReservedBuildingCells(
                    snapshot,
                    safeBuildingClearance);

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            foreach (Vector2Int coordinate in occupiedRoads)
            {
                minX = Mathf.Min(minX, coordinate.x);
                maxX = Mathf.Max(maxX, coordinate.x);
                minY = Mathf.Min(minY, coordinate.y);
                maxY = Mathf.Max(maxY, coordinate.y);
            }

            if (minX == int.MaxValue)
            {
                return 0;
            }

            minX -= safeBoundsExpansion;
            maxX += safeBoundsExpansion;
            minY -= safeBoundsExpansion;
            maxY += safeBoundsExpansion;

            List<RuntimeRoadNode> orderedRoads =
                new List<RuntimeRoadNode>(snapshot.Roads);

            orderedRoads.Sort(delegate(
                RuntimeRoadNode first,
                RuntimeRoadNode second)
            {
                int firstHash = StableHash(
                    seed,
                    first.Coordinate,
                    first.DistrictIndex,
                    17);

                int secondHash = StableHash(
                    seed,
                    second.Coordinate,
                    second.DistrictIndex,
                    17);

                return firstHash.CompareTo(secondHash);
            });

            Dictionary<Vector2Int, int> generatedDistricts =
                new Dictionary<Vector2Int, int>();

            for (int roadIndex = 0;
                 roadIndex < orderedRoads.Count &&
                 generatedDistricts.Count < maximumNewRoadCells;
                 roadIndex++)
            {
                RuntimeRoadNode sourceRoad = orderedRoads[roadIndex];

                if (sourceRoad == null || sourceRoad.ConnectionCount >= 4)
                {
                    continue;
                }

                List<Vector2Int> branchDirections =
                    GetOpenDirections(sourceRoad.Mask);

                for (int directionIndex = 0;
                     directionIndex < branchDirections.Count &&
                     generatedDistricts.Count < maximumNewRoadCells;
                     directionIndex++)
                {
                    Vector2Int initialDirection =
                        branchDirections[directionIndex];

                    float chance = Stable01(
                        seed,
                        sourceRoad.Coordinate,
                        directionIndex * 53 + 101);

                    if (chance > safeBranchChance)
                    {
                        continue;
                    }

                    int lengthRange =
                        safeMaximumLength - safeMinimumLength + 1;

                    int targetLength = safeMinimumLength +
                        Mathf.FloorToInt(
                            Stable01(
                                seed,
                                sourceRoad.Coordinate,
                                directionIndex * 53 + 107) *
                            lengthRange);

                    targetLength = Mathf.Clamp(
                        targetLength,
                        safeMinimumLength,
                        safeMaximumLength);

                    List<Vector2Int> path = BuildBranchPath(
                        sourceRoad.Coordinate,
                        initialDirection,
                        targetLength,
                        seed,
                        directionIndex,
                        safeTurnChance,
                        safeDiagonalChance,
                        minX,
                        maxX,
                        minY,
                        maxY,
                        occupiedRoads,
                        reservedBuildingCells,
                        maximumNewRoadCells - generatedDistricts.Count);

                    if (path.Count < safeMinimumLength)
                    {
                        continue;
                    }

                    for (int pathIndex = 0;
                         pathIndex < path.Count;
                         pathIndex++)
                    {
                        Vector2Int cell = path[pathIndex];

                        if (occupiedRoads.Add(cell))
                        {
                            generatedDistricts[cell] =
                                sourceRoad.DistrictIndex;
                        }
                    }
                }
            }

            if (generatedDistricts.Count == 0)
            {
                return 0;
            }

            return InstantiateAndConnect(
                snapshot,
                gridManager,
                parent,
                generatedDistricts,
                movementAPCost);
        }

        private static List<Vector2Int> BuildBranchPath(
            Vector2Int source,
            Vector2Int initialDirection,
            int targetLength,
            int seed,
            int branchIndex,
            float turnChance,
            float diagonalChance,
            int minX,
            int maxX,
            int minY,
            int maxY,
            HashSet<Vector2Int> occupiedRoads,
            HashSet<Vector2Int> reservedBuildingCells,
            int remainingCapacity)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            HashSet<Vector2Int> local = new HashSet<Vector2Int>();
            Vector2Int current = source;
            Vector2Int direction = initialDirection;

            bool diagonalMode = Stable01(
                seed,
                source,
                branchIndex * 131 + 257) <= diagonalChance;

            Vector2Int diagonalSide = new Vector2Int(
                -initialDirection.y,
                initialDirection.x);

            if (Stable01(
                seed,
                source,
                branchIndex * 131 + 263) >= 0.5f)
            {
                diagonalSide = -diagonalSide;
            }

            for (int step = 0;
                 step < targetLength && step < remainingCapacity;
                 step++)
            {
                Vector2Int stepDirection = direction;

                if (diagonalMode)
                {
                    stepDirection = step % 2 == 0
                        ? initialDirection
                        : diagonalSide;

                    if (!CanContinue(
                        current + stepDirection,
                        minX,
                        maxX,
                        minY,
                        maxY,
                        occupiedRoads,
                        reservedBuildingCells,
                        local))
                    {
                        Vector2Int alternate = stepDirection == initialDirection
                            ? diagonalSide
                            : initialDirection;

                        if (CanContinue(
                            current + alternate,
                            minX,
                            maxX,
                            minY,
                            maxY,
                            occupiedRoads,
                            reservedBuildingCells,
                            local))
                        {
                            stepDirection = alternate;
                        }
                    }
                }

                Vector2Int next = current + stepDirection;

                if (!IsInsideBounds(
                        next,
                        minX,
                        maxX,
                        minY,
                        maxY) ||
                    reservedBuildingCells.Contains(next))
                {
                    break;
                }

                if (occupiedRoads.Contains(next))
                {
                    break;
                }

                if (!local.Add(next))
                {
                    break;
                }

                result.Add(next);
                current = next;
                direction = stepDirection;

                if (step >= 1 &&
                    HasAdjacentRoadOtherThanPrevious(
                        current,
                        direction,
                        occupiedRoads))
                {
                    break;
                }

                if (diagonalMode)
                {
                    continue;
                }

                float turnRoll = Stable01(
                    seed,
                    current,
                    branchIndex * 97 + step * 13 + 211);

                if (turnRoll <= turnChance)
                {
                    Vector2Int left =
                        new Vector2Int(-direction.y, direction.x);

                    Vector2Int right = -left;

                    bool chooseLeft = Stable01(
                        seed,
                        current,
                        branchIndex * 97 + step * 13 + 223) < 0.5f;

                    Vector2Int first = chooseLeft ? left : right;
                    Vector2Int second = chooseLeft ? right : left;

                    if (CanContinue(
                        current + first,
                        minX,
                        maxX,
                        minY,
                        maxY,
                        occupiedRoads,
                        reservedBuildingCells,
                        local))
                    {
                        direction = first;
                    }
                    else if (CanContinue(
                        current + second,
                        minX,
                        maxX,
                        minY,
                        maxY,
                        occupiedRoads,
                        reservedBuildingCells,
                        local))
                    {
                        direction = second;
                    }
                }
            }

            return result;
        }

        private static bool CanContinue(
            Vector2Int coordinate,
            int minX,
            int maxX,
            int minY,
            int maxY,
            HashSet<Vector2Int> occupiedRoads,
            HashSet<Vector2Int> reservedBuildingCells,
            HashSet<Vector2Int> local)
        {
            return IsInsideBounds(
                       coordinate,
                       minX,
                       maxX,
                       minY,
                       maxY) &&
                   !occupiedRoads.Contains(coordinate) &&
                   !reservedBuildingCells.Contains(coordinate) &&
                   !local.Contains(coordinate);
        }

        private static bool HasAdjacentRoadOtherThanPrevious(
            Vector2Int coordinate,
            Vector2Int arrivalDirection,
            HashSet<Vector2Int> occupiedRoads)
        {
            Vector2Int previous = coordinate - arrivalDirection;

            for (int index = 0; index < Directions.Length; index++)
            {
                Vector2Int adjacent = coordinate + Directions[index];

                if (adjacent != previous &&
                    occupiedRoads.Contains(adjacent))
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<Vector2Int> BuildReservedBuildingCells(
            GameBoardRuntimeSnapshot snapshot,
            int clearance)
        {
            HashSet<Vector2Int> result = new HashSet<Vector2Int>();
            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            Vector2 worldOffset = EstimateWorldOffset(snapshot, cell);

            for (int index = 0;
                 index < snapshot.Buildings.Count;
                 index++)
            {
                RuntimeBuildingNode building = snapshot.Buildings[index];

                if (building == null)
                {
                    continue;
                }

                Vector2Int coordinate = new Vector2Int(
                    Mathf.RoundToInt(
                        (building.WorldPosition.x - worldOffset.x) / cell),
                    Mathf.RoundToInt(
                        (building.WorldPosition.z - worldOffset.y) / cell));

                for (int y = -clearance; y <= clearance; y++)
                {
                    for (int x = -clearance; x <= clearance; x++)
                    {
                        result.Add(
                            coordinate + new Vector2Int(x, y));
                    }
                }
            }

            return result;
        }

        private static int InstantiateAndConnect(
            GameBoardRuntimeSnapshot snapshot,
            GridManager gridManager,
            Transform parent,
            Dictionary<Vector2Int, int> generatedDistricts,
            int movementAPCost)
        {
            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            Vector2 worldOffset = EstimateWorldOffset(snapshot, cell);
            float roadY = snapshot.Roads.Count > 0
                ? snapshot.Roads[0].WorldPosition.y
                : 0f;

            Dictionary<Vector2Int, BoardSpacePrefab> allRoadSpaces =
                new Dictionary<Vector2Int, BoardSpacePrefab>();

            for (int index = 0; index < snapshot.Roads.Count; index++)
            {
                RuntimeRoadNode road = snapshot.Roads[index];

                if (road != null && road.Space != null)
                {
                    allRoadSpaces[road.Coordinate] = road.Space;
                }
            }

            int createdCount = 0;

            foreach (KeyValuePair<Vector2Int, int> pair in generatedDistricts)
            {
                Vector2Int coordinate = pair.Key;
                string nodeId =
                    "ROAD_" + coordinate.x + "_" + coordinate.y;

                BoardSpacePrefab existing;

                if (gridManager.TryGetSpace(nodeId, out existing))
                {
                    if (existing != null)
                    {
                        allRoadSpaces[coordinate] = existing;
                    }

                    continue;
                }

                GameObject roadObject = new GameObject(
                    nodeId + "_Supplemental");

                if (parent != null)
                {
                    roadObject.transform.SetParent(parent, true);
                }

                roadObject.transform.position = new Vector3(
                    coordinate.x * cell + worldOffset.x,
                    roadY,
                    coordinate.y * cell + worldOffset.y);

                roadObject.AddComponent<SupplementalRoadNodeMarker>();

                BoardSpacePrefab space =
                    roadObject.AddComponent<BoardSpacePrefab>();

                space.Initialize(
                    nodeId,
                    BoardSpaceType.Road,
                    pair.Value,
                    Mathf.Max(0, movementAPCost));

                if (!gridManager.RegisterSpace(space))
                {
                    DestroyObject(roadObject);
                    continue;
                }

                allRoadSpaces[coordinate] = space;
                createdCount++;
            }

            foreach (KeyValuePair<Vector2Int, int> pair in generatedDistricts)
            {
                BoardSpacePrefab road;

                if (!allRoadSpaces.TryGetValue(pair.Key, out road) ||
                    road == null)
                {
                    continue;
                }

                for (int index = 0; index < Directions.Length; index++)
                {
                    BoardSpacePrefab neighbor;

                    if (allRoadSpaces.TryGetValue(
                            pair.Key + Directions[index],
                            out neighbor) &&
                        neighbor != null)
                    {
                        road.ConnectBidirectional(neighbor);
                    }
                }
            }

            return createdCount;
        }

        private static Vector2 EstimateWorldOffset(
            GameBoardRuntimeSnapshot snapshot,
            float cell)
        {
            if (snapshot.Roads.Count == 0)
            {
                return Vector2.zero;
            }

            RuntimeRoadNode road = snapshot.Roads[0];

            return new Vector2(
                road.WorldPosition.x - road.Coordinate.x * cell,
                road.WorldPosition.z - road.Coordinate.y * cell);
        }

        private static List<Vector2Int> GetOpenDirections(
            RuntimeRoadMask mask)
        {
            List<Vector2Int> result = new List<Vector2Int>();

            for (int index = 0; index < Directions.Length; index++)
            {
                if (!MaskContains(mask, Directions[index]))
                {
                    result.Add(Directions[index]);
                }
            }

            return result;
        }

        private static bool MaskContains(
            RuntimeRoadMask mask,
            Vector2Int direction)
        {
            if (direction == Vector2Int.up)
            {
                return (mask & RuntimeRoadMask.North) != 0;
            }

            if (direction == Vector2Int.right)
            {
                return (mask & RuntimeRoadMask.East) != 0;
            }

            if (direction == Vector2Int.down)
            {
                return (mask & RuntimeRoadMask.South) != 0;
            }

            return (mask & RuntimeRoadMask.West) != 0;
        }

        private static bool IsInsideBounds(
            Vector2Int coordinate,
            int minX,
            int maxX,
            int minY,
            int maxY)
        {
            return coordinate.x >= minX &&
                   coordinate.x <= maxX &&
                   coordinate.y >= minY &&
                   coordinate.y <= maxY;
        }

        private static float Stable01(
            int seed,
            Vector2Int coordinate,
            int salt)
        {
            uint value = (uint)StableHash(seed, coordinate, salt, 0);
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static int StableHash(
            int seed,
            Vector2Int coordinate,
            int firstSalt,
            int secondSalt)
        {
            unchecked
            {
                int hash = seed;
                hash = hash * 397 ^ coordinate.x;
                hash = hash * 397 ^ coordinate.y;
                hash = hash * 397 ^ firstSalt;
                hash = hash * 397 ^ secondSalt;
                hash ^= hash >> 16;
                return hash;
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}

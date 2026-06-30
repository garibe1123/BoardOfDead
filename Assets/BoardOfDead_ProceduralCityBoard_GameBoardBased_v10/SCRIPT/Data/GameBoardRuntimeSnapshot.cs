using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BoardOfDead
{
    [Flags]
    public enum RuntimeRoadMask
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8
    }

    public sealed class RuntimeRoadNode
    {
        public BoardSpacePrefab Space;
        public Vector2Int Coordinate;
        public Vector3 WorldPosition;
        public int DistrictIndex;
        public RuntimeRoadMask Mask;

        public int ConnectionCount
        {
            get
            {
                int count = 0;
                if ((Mask & RuntimeRoadMask.North) != 0) count++;
                if ((Mask & RuntimeRoadMask.East) != 0) count++;
                if ((Mask & RuntimeRoadMask.South) != 0) count++;
                if ((Mask & RuntimeRoadMask.West) != 0) count++;
                return count;
            }
        }
    }

    public sealed class RuntimeBuildingNode
    {
        public BoardSpacePrefab Space;
        public string NodeId;
        public Vector3 WorldPosition;
        public int DistrictIndex;
        public RuntimeRoadNode EntranceRoad;
    }

    public sealed class GameBoardRuntimeSnapshot
    {
        public readonly List<RuntimeRoadNode> Roads =
            new List<RuntimeRoadNode>();

        public readonly List<RuntimeBuildingNode> Buildings =
            new List<RuntimeBuildingNode>();

        public readonly Dictionary<Vector2Int, RuntimeRoadNode> RoadByCoordinate =
            new Dictionary<Vector2Int, RuntimeRoadNode>();

        public readonly Dictionary<int, List<RuntimeBuildingNode>> BuildingsByDistrict =
            new Dictionary<int, List<RuntimeBuildingNode>>();

        public Vector3 BoardCenter;
        public Vector2 BoardSizeXZ = Vector2.one;
        public float CellWorldSize = 1f;
        public int Signature;

        public bool IsValid
        {
            get { return Roads.Count > 0; }
        }
    }

    public static class GameBoardRuntimeSnapshotBuilder
    {
        private const BindingFlags ReflectionFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public static GameBoardRuntimeSnapshot Build(GridManager gridManager)
        {
            GameBoardRuntimeSnapshot snapshot =
                new GameBoardRuntimeSnapshot();

            if (gridManager == null)
            {
                return snapshot;
            }

            List<BoardSpacePrefab> roads =
                new List<BoardSpacePrefab>(gridManager.RoadSpaces);

            List<BoardSpacePrefab> buildings =
                new List<BoardSpacePrefab>(gridManager.BuildingSpaces);

            roads.Sort(CompareByNodeId);
            buildings.Sort(CompareByNodeId);

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            unchecked
            {
                int signature = 17;

                for (int index = 0; index < roads.Count; index++)
                {
                    BoardSpacePrefab space = roads[index];

                    if (space == null)
                    {
                        continue;
                    }

                    Vector2Int coordinate;

                    if (!TryParseRoadCoordinate(space.NodeId, out coordinate))
                    {
                        coordinate = EstimateCoordinateFromWorld(
                            space.transform.position,
                            1f);
                    }

                    RuntimeRoadNode node = new RuntimeRoadNode
                    {
                        Space = space,
                        Coordinate = coordinate,
                        WorldPosition = space.transform.position,
                        DistrictIndex = ReadDistrictIndex(space)
                    };

                    if (!snapshot.RoadByCoordinate.ContainsKey(coordinate))
                    {
                        snapshot.RoadByCoordinate.Add(coordinate, node);
                        snapshot.Roads.Add(node);
                    }

                    EncapsulateXZ(
                        node.WorldPosition,
                        ref minX,
                        ref maxX,
                        ref minZ,
                        ref maxZ);

                    signature = AddSpaceToSignature(signature, space);
                }

                snapshot.CellWorldSize = EstimateCellWorldSize(snapshot);

                for (int index = 0; index < snapshot.Roads.Count; index++)
                {
                    RuntimeRoadNode road = snapshot.Roads[index];
                    road.Mask = BuildRoadMask(snapshot, road.Coordinate);
                }

                for (int index = 0; index < buildings.Count; index++)
                {
                    BoardSpacePrefab space = buildings[index];

                    if (space == null)
                    {
                        continue;
                    }

                    RuntimeBuildingNode node = new RuntimeBuildingNode
                    {
                        Space = space,
                        NodeId = space.NodeId ?? string.Empty,
                        WorldPosition = space.transform.position,
                        DistrictIndex = ReadDistrictIndex(space)
                    };

                    node.EntranceRoad = FindConnectedRoad(snapshot, space);

                    if (node.EntranceRoad == null)
                    {
                        node.EntranceRoad = FindNearestRoad(
                            snapshot,
                            node.WorldPosition,
                            node.DistrictIndex);
                    }

                    snapshot.Buildings.Add(node);

                    List<RuntimeBuildingNode> districtBuildings;

                    if (!snapshot.BuildingsByDistrict.TryGetValue(
                        node.DistrictIndex,
                        out districtBuildings))
                    {
                        districtBuildings = new List<RuntimeBuildingNode>();
                        snapshot.BuildingsByDistrict.Add(
                            node.DistrictIndex,
                            districtBuildings);
                    }

                    districtBuildings.Add(node);

                    EncapsulateXZ(
                        node.WorldPosition,
                        ref minX,
                        ref maxX,
                        ref minZ,
                        ref maxZ);

                    signature = AddSpaceToSignature(signature, space);
                }

                signature = signature * 31 + snapshot.Roads.Count;
                signature = signature * 31 + snapshot.Buildings.Count;
                snapshot.Signature = signature;
            }

            if (!float.IsInfinity(minX) && !float.IsInfinity(minZ))
            {
                snapshot.BoardCenter = new Vector3(
                    (minX + maxX) * 0.5f,
                    0f,
                    (minZ + maxZ) * 0.5f);

                snapshot.BoardSizeXZ = new Vector2(
                    Mathf.Max(snapshot.CellWorldSize, maxX - minX),
                    Mathf.Max(snapshot.CellWorldSize, maxZ - minZ));
            }

            return snapshot;
        }

        private static int CompareByNodeId(
            BoardSpacePrefab first,
            BoardSpacePrefab second)
        {
            string firstId = first != null ? first.NodeId : string.Empty;
            string secondId = second != null ? second.NodeId : string.Empty;
            return string.CompareOrdinal(firstId, secondId);
        }

        private static RuntimeRoadMask BuildRoadMask(
            GameBoardRuntimeSnapshot snapshot,
            Vector2Int coordinate)
        {
            RuntimeRoadMask mask = RuntimeRoadMask.None;

            if (snapshot.RoadByCoordinate.ContainsKey(coordinate + Directions[0]))
            {
                mask |= RuntimeRoadMask.North;
            }

            if (snapshot.RoadByCoordinate.ContainsKey(coordinate + Directions[1]))
            {
                mask |= RuntimeRoadMask.East;
            }

            if (snapshot.RoadByCoordinate.ContainsKey(coordinate + Directions[2]))
            {
                mask |= RuntimeRoadMask.South;
            }

            if (snapshot.RoadByCoordinate.ContainsKey(coordinate + Directions[3]))
            {
                mask |= RuntimeRoadMask.West;
            }

            return mask;
        }

        private static RuntimeRoadNode FindConnectedRoad(
            GameBoardRuntimeSnapshot snapshot,
            BoardSpacePrefab building)
        {
            if (building == null || building.ConnectedSpaces == null)
            {
                return null;
            }

            foreach (BoardSpacePrefab connected in building.ConnectedSpaces)
            {
                if (connected == null ||
                    connected.SpaceType != BoardSpaceType.Road)
                {
                    continue;
                }

                Vector2Int coordinate;

                if (TryParseRoadCoordinate(connected.NodeId, out coordinate))
                {
                    RuntimeRoadNode result;

                    if (snapshot.RoadByCoordinate.TryGetValue(
                        coordinate,
                        out result))
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        private static RuntimeRoadNode FindNearestRoad(
            GameBoardRuntimeSnapshot snapshot,
            Vector3 worldPosition,
            int districtIndex)
        {
            RuntimeRoadNode nearestSameDistrict = null;
            RuntimeRoadNode nearestAny = null;
            float sameDistrictDistance = float.PositiveInfinity;
            float anyDistance = float.PositiveInfinity;

            for (int index = 0; index < snapshot.Roads.Count; index++)
            {
                RuntimeRoadNode road = snapshot.Roads[index];
                float distance = SqrDistanceXZ(
                    road.WorldPosition,
                    worldPosition);

                if (distance < anyDistance)
                {
                    anyDistance = distance;
                    nearestAny = road;
                }

                if (road.DistrictIndex == districtIndex &&
                    distance < sameDistrictDistance)
                {
                    sameDistrictDistance = distance;
                    nearestSameDistrict = road;
                }
            }

            return nearestSameDistrict ?? nearestAny;
        }

        private static float EstimateCellWorldSize(
            GameBoardRuntimeSnapshot snapshot)
        {
            List<float> distances = new List<float>();

            for (int index = 0; index < snapshot.Roads.Count; index++)
            {
                RuntimeRoadNode road = snapshot.Roads[index];

                for (int directionIndex = 0;
                     directionIndex < Directions.Length;
                     directionIndex++)
                {
                    RuntimeRoadNode neighbor;

                    if (!snapshot.RoadByCoordinate.TryGetValue(
                        road.Coordinate + Directions[directionIndex],
                        out neighbor))
                    {
                        continue;
                    }

                    float distance = Mathf.Sqrt(
                        SqrDistanceXZ(
                            road.WorldPosition,
                            neighbor.WorldPosition));

                    if (distance > 0.01f)
                    {
                        distances.Add(distance);
                    }
                }
            }

            if (distances.Count == 0)
            {
                return 1f;
            }

            distances.Sort();
            return Mathf.Max(0.1f, distances[distances.Count / 2]);
        }

        private static bool TryParseRoadCoordinate(
            string nodeId,
            out Vector2Int coordinate)
        {
            coordinate = Vector2Int.zero;

            if (string.IsNullOrWhiteSpace(nodeId) ||
                !nodeId.StartsWith("ROAD_", StringComparison.Ordinal))
            {
                return false;
            }

            string[] parts = nodeId.Split('_');

            if (parts.Length < 3)
            {
                return false;
            }

            int x;
            int y;

            if (!int.TryParse(parts[1], out x) ||
                !int.TryParse(parts[2], out y))
            {
                return false;
            }

            coordinate = new Vector2Int(x, y);
            return true;
        }

        private static Vector2Int EstimateCoordinateFromWorld(
            Vector3 worldPosition,
            float cellWorldSize)
        {
            float size = Mathf.Max(0.1f, cellWorldSize);

            return new Vector2Int(
                Mathf.RoundToInt(worldPosition.x / size),
                Mathf.RoundToInt(worldPosition.z / size));
        }

        private static int ReadDistrictIndex(BoardSpacePrefab space)
        {
            if (space == null)
            {
                return -1;
            }

            Type type = space.GetType();

            PropertyInfo property = type.GetProperty(
                "DistrictIndex",
                ReflectionFlags);

            if (property != null && property.PropertyType == typeof(int))
            {
                object value = property.GetValue(space, null);

                if (value is int)
                {
                    return (int)value;
                }
            }

            FieldInfo field = type.GetField(
                "districtIndex",
                ReflectionFlags);

            if (field != null && field.FieldType == typeof(int))
            {
                object value = field.GetValue(space);

                if (value is int)
                {
                    return (int)value;
                }
            }

            return ParseDistrictFromBuildingId(space.NodeId);
        }

        private static int ParseDistrictFromBuildingId(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return -1;
            }

            int districtMarker = nodeId.IndexOf("_D", StringComparison.Ordinal);

            if (districtMarker < 0)
            {
                return -1;
            }

            int start = districtMarker + 2;
            int end = nodeId.IndexOf('_', start);

            if (end < 0)
            {
                end = nodeId.Length;
            }

            string value = nodeId.Substring(start, end - start);
            int oneBasedDistrict;

            if (!int.TryParse(value, out oneBasedDistrict))
            {
                return -1;
            }

            return Mathf.Max(-1, oneBasedDistrict - 1);
        }

        private static int AddSpaceToSignature(
            int signature,
            BoardSpacePrefab space)
        {
            Vector3 position = space.transform.position;
            signature = signature * 31 + space.GetInstanceID();
            signature = signature * 31 + Mathf.RoundToInt(position.x * 100f);
            signature = signature * 31 + Mathf.RoundToInt(position.z * 100f);
            return signature;
        }

        private static void EncapsulateXZ(
            Vector3 position,
            ref float minX,
            ref float maxX,
            ref float minZ,
            ref float maxZ)
        {
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
            minZ = Mathf.Min(minZ, position.z);
            maxZ = Mathf.Max(maxZ, position.z);
        }

        private static float SqrDistanceXZ(Vector3 first, Vector3 second)
        {
            float x = first.x - second.x;
            float z = first.z - second.z;
            return x * x + z * z;
        }
    }
}

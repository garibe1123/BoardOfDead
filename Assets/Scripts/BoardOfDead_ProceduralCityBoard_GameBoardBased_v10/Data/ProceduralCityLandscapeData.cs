using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public sealed class ProceduralTreePlacement
    {
        public Vector3 WorldPosition;
        public float Radius;
        public float Height;
        public float RotationDegrees;
        public int Variant;
    }

    public sealed class ProceduralIslandLandscapeLayout
    {
        public Vector3 Center;
        public float RadiusX;
        public float RadiusZ;
        public float GroundY;
        public readonly List<Vector3> CoastPoints = new List<Vector3>();
        public readonly List<ProceduralTreePlacement> Trees =
            new List<ProceduralTreePlacement>();
    }

    internal sealed class LandscapeBuildingIndex
    {
        private readonly Dictionary<Vector2Int, List<DenseSmallBuildingPlacement>> buckets =
            new Dictionary<Vector2Int, List<DenseSmallBuildingPlacement>>();

        private readonly float bucketSize;

        public LandscapeBuildingIndex(
            IList<DenseSmallBuildingPlacement> buildings,
            float size)
        {
            bucketSize = Mathf.Max(0.1f, size);

            if (buildings == null)
            {
                return;
            }

            for (int index = 0; index < buildings.Count; index++)
            {
                DenseSmallBuildingPlacement building = buildings[index];

                if (building == null)
                {
                    continue;
                }

                float radius = building.BoundingRadius;
                int minX = Mathf.FloorToInt(
                    (building.WorldPosition.x - radius) / bucketSize);
                int maxX = Mathf.FloorToInt(
                    (building.WorldPosition.x + radius) / bucketSize);
                int minZ = Mathf.FloorToInt(
                    (building.WorldPosition.z - radius) / bucketSize);
                int maxZ = Mathf.FloorToInt(
                    (building.WorldPosition.z + radius) / bucketSize);

                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2Int key = new Vector2Int(x, z);
                        List<DenseSmallBuildingPlacement> list;

                        if (!buckets.TryGetValue(key, out list))
                        {
                            list = new List<DenseSmallBuildingPlacement>();
                            buckets.Add(key, list);
                        }

                        list.Add(building);
                    }
                }
            }
        }

        public bool IsNearBuilding(Vector3 position, float clearance)
        {
            int centerX = Mathf.FloorToInt(position.x / bucketSize);
            int centerZ = Mathf.FloorToInt(position.z / bucketSize);
            int range = Mathf.Max(1, Mathf.CeilToInt(clearance / bucketSize) + 1);
            HashSet<DenseSmallBuildingPlacement> checkedBuildings =
                new HashSet<DenseSmallBuildingPlacement>();

            for (int z = centerZ - range; z <= centerZ + range; z++)
            {
                for (int x = centerX - range; x <= centerX + range; x++)
                {
                    List<DenseSmallBuildingPlacement> list;

                    if (!buckets.TryGetValue(new Vector2Int(x, z), out list))
                    {
                        continue;
                    }

                    for (int index = 0; index < list.Count; index++)
                    {
                        DenseSmallBuildingPlacement building = list[index];

                        if (building == null || !checkedBuildings.Add(building))
                        {
                            continue;
                        }

                        Vector3 delta = position - building.WorldPosition;
                        delta.y = 0f;

                        float localX = Mathf.Abs(Vector3.Dot(delta, building.AxisX));
                        float localZ = Mathf.Abs(Vector3.Dot(delta, building.AxisZ));
                        float halfWidth = Mathf.Max(
                            building.Width,
                            building.CollisionWidth) * 0.5f;
                        float halfDepth = Mathf.Max(
                            building.Depth,
                            building.CollisionDepth) * 0.5f;

                        float outsideX = Mathf.Max(0f, localX - halfWidth);
                        float outsideZ = Mathf.Max(0f, localZ - halfDepth);

                        if (outsideX * outsideX + outsideZ * outsideZ <
                            clearance * clearance)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    public static class ProceduralCityLandscapeBuilder
    {
        public static ProceduralIslandLandscapeLayout Build(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout,
            IList<DenseSmallBuildingPlacement> buildings,
            int seed,
            float coastMarginCells,
            float coastIrregularity,
            int coastSegments,
            float forestMinimumRoadDistanceCells,
            float forestMaximumRoadDistanceCells,
            float forestBuildingClearanceCells,
            float forestSampleStepCells,
            float innerForestChance,
            float outerForestChance,
            int maximumTreeCount,
            float minimumTreeRadiusCells,
            float maximumTreeRadiusCells,
            float minimumTreeHeightCells,
            float maximumTreeHeightCells,
            float groundY)
        {
            ProceduralIslandLandscapeLayout layout =
                new ProceduralIslandLandscapeLayout();

            if (snapshot == null || !snapshot.IsValid)
            {
                return layout;
            }

            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            float margin = Mathf.Max(1.5f, coastMarginCells) * cell;

            layout.Center = new Vector3(
                snapshot.BoardCenter.x,
                groundY,
                snapshot.BoardCenter.z);

            layout.RadiusX = snapshot.BoardSizeXZ.x * 0.5f + margin;
            layout.RadiusZ = snapshot.BoardSizeXZ.y * 0.5f + margin;
            layout.GroundY = groundY;

            BuildCoastline(
                layout,
                seed,
                coastIrregularity,
                coastSegments);

            BuildForest(
                layout,
                snapshot,
                roadLayout,
                buildings,
                seed,
                forestMinimumRoadDistanceCells,
                forestMaximumRoadDistanceCells,
                forestBuildingClearanceCells,
                forestSampleStepCells,
                innerForestChance,
                outerForestChance,
                maximumTreeCount,
                minimumTreeRadiusCells,
                maximumTreeRadiusCells,
                minimumTreeHeightCells,
                maximumTreeHeightCells);

            return layout;
        }

        private static void BuildCoastline(
            ProceduralIslandLandscapeLayout layout,
            int seed,
            float irregularity,
            int segments)
        {
            int safeSegments = Mathf.Clamp(segments, 16, 128);
            float safeIrregularity = Mathf.Clamp(irregularity, 0f, 0.22f);
            const float superellipsePower = 4f;

            for (int index = 0; index < safeSegments; index++)
            {
                float angle = Mathf.PI * 2f * index / safeSegments;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);

                float superX = Mathf.Sign(cosine) *
                    Mathf.Pow(Mathf.Abs(cosine), 2f / superellipsePower);

                float superZ = Mathf.Sign(sine) *
                    Mathf.Pow(Mathf.Abs(sine), 2f / superellipsePower);

                float lowNoise = Mathf.PerlinNoise(
                    index * 0.087f + seed * 0.00091f,
                    seed * 0.00173f + 19.7f) * 2f - 1f;

                float highNoise = Mathf.PerlinNoise(
                    index * 0.213f + seed * 0.00137f,
                    seed * 0.00057f + 71.3f) * 2f - 1f;

                float scale = 1f +
                    (lowNoise * 0.72f + highNoise * 0.28f) *
                    safeIrregularity;

                layout.CoastPoints.Add(
                    new Vector3(
                        layout.Center.x + superX * layout.RadiusX * scale,
                        layout.GroundY,
                        layout.Center.z + superZ * layout.RadiusZ * scale));
            }
        }

        private static void BuildForest(
            ProceduralIslandLandscapeLayout layout,
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout,
            IList<DenseSmallBuildingPlacement> buildings,
            int seed,
            float minimumRoadDistanceCells,
            float maximumRoadDistanceCells,
            float buildingClearanceCells,
            float sampleStepCells,
            float innerChance,
            float outerChance,
            int maximumTreeCount,
            float minimumRadiusCells,
            float maximumRadiusCells,
            float minimumHeightCells,
            float maximumHeightCells)
        {
            if (roadLayout == null || maximumTreeCount <= 0)
            {
                return;
            }

            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            float step = Mathf.Max(0.25f, sampleStepCells) * cell;
            float minimumRoadDistance =
                Mathf.Max(0.05f, minimumRoadDistanceCells) * cell;
            float maximumRoadDistance =
                Mathf.Max(minimumRoadDistanceCells, maximumRoadDistanceCells) * cell;
            float buildingClearance =
                Mathf.Max(0.1f, buildingClearanceCells) * cell;
            float minRadius = Mathf.Max(0.04f, minimumRadiusCells) * cell;
            float maxRadius = Mathf.Max(minimumRadiusCells, maximumRadiusCells) * cell;
            float minHeight = Mathf.Max(0.08f, minimumHeightCells) * cell;
            float maxHeight = Mathf.Max(minimumHeightCells, maximumHeightCells) * cell;
            float treeSpacing = Mathf.Max(minRadius * 1.7f, step * 0.46f);

            LandscapeBuildingIndex buildingIndex =
                new LandscapeBuildingIndex(buildings, cell);

            Dictionary<Vector2Int, List<Vector3>> treeBuckets =
                new Dictionary<Vector2Int, List<Vector3>>();

            float minX = layout.Center.x - layout.RadiusX * 0.94f;
            float maxX = layout.Center.x + layout.RadiusX * 0.94f;
            float minZ = layout.Center.z - layout.RadiusZ * 0.94f;
            float maxZ = layout.Center.z + layout.RadiusZ * 0.94f;
            int xCount = Mathf.CeilToInt((maxX - minX) / step);
            int zCount = Mathf.CeilToInt((maxZ - minZ) / step);
            List<Vector2Int> candidates = new List<Vector2Int>();

            for (int z = 0; z <= zCount; z++)
            {
                for (int x = 0; x <= xCount; x++)
                {
                    candidates.Add(new Vector2Int(x, z));
                }
            }

            candidates.Sort(delegate(Vector2Int first, Vector2Int second)
            {
                return StableHash(seed, first, 1103).CompareTo(
                    StableHash(seed, second, 1103));
            });

            float boardHalfX = snapshot.BoardSizeXZ.x * 0.5f;
            float boardHalfZ = snapshot.BoardSizeXZ.y * 0.5f;

            for (int index = 0;
                 index < candidates.Count && layout.Trees.Count < maximumTreeCount;
                 index++)
            {
                Vector2Int candidate = candidates[index];
                float jitterX = (Stable01(seed, candidate, 1117) * 2f - 1f) * step * 0.42f;
                float jitterZ = (Stable01(seed, candidate, 1123) * 2f - 1f) * step * 0.42f;

                Vector3 position = new Vector3(
                    minX + candidate.x * step + jitterX,
                    layout.GroundY,
                    minZ + candidate.y * step + jitterZ);

                if (!IsInsideIsland(layout, position, 0.92f))
                {
                    continue;
                }

                bool outsideCityRectangle =
                    Mathf.Abs(position.x - snapshot.BoardCenter.x) > boardHalfX + cell * 0.35f ||
                    Mathf.Abs(position.z - snapshot.BoardCenter.z) > boardHalfZ + cell * 0.35f;

                Vector3 nearestRoad;
                Vector3 roadTangent;
                float roadDistance;

                if (!roadLayout.TryGetNearestRoad(
                    position,
                    out nearestRoad,
                    out roadTangent,
                    out roadDistance) ||
                    roadDistance < minimumRoadDistance ||
                    roadDistance > maximumRoadDistance)
                {
                    continue;
                }

                if (buildingIndex.IsNearBuilding(position, buildingClearance))
                {
                    continue;
                }

                float chance = outsideCityRectangle
                    ? Mathf.Clamp01(outerChance)
                    : Mathf.Clamp01(innerChance);

                float patchNoise = Mathf.PerlinNoise(
                    position.x / (cell * 4.7f) + seed * 0.00031f,
                    position.z / (cell * 4.7f) - seed * 0.00047f);

                chance *= Mathf.Lerp(0.30f, 1.20f, patchNoise);

                if (Stable01(seed, candidate, 1151) > Mathf.Clamp01(chance))
                {
                    continue;
                }

                if (IsNearExistingTree(
                    treeBuckets,
                    position,
                    treeSpacing,
                    cell))
                {
                    continue;
                }

                float radius = Mathf.Lerp(
                    minRadius,
                    maxRadius,
                    Stable01(seed, candidate, 1163));

                float height = Mathf.Lerp(
                    minHeight,
                    maxHeight,
                    Stable01(seed, candidate, 1171));

                ProceduralTreePlacement tree = new ProceduralTreePlacement
                {
                    WorldPosition = position,
                    Radius = radius,
                    Height = height,
                    RotationDegrees = Stable01(seed, candidate, 1181) * 360f,
                    Variant = Mathf.Clamp(
                        Mathf.FloorToInt(Stable01(seed, candidate, 1193) * 3f),
                        0,
                        2)
                };

                layout.Trees.Add(tree);
                AddTreeBucket(treeBuckets, position, cell);
            }
        }

        private static bool IsInsideIsland(
            ProceduralIslandLandscapeLayout layout,
            Vector3 position,
            float normalizedRadius)
        {
            float nx = Mathf.Abs(position.x - layout.Center.x) /
                Mathf.Max(0.001f, layout.RadiusX * normalizedRadius);
            float nz = Mathf.Abs(position.z - layout.Center.z) /
                Mathf.Max(0.001f, layout.RadiusZ * normalizedRadius);

            return Mathf.Pow(nx, 4f) + Mathf.Pow(nz, 4f) <= 1f;
        }

        private static bool IsNearExistingTree(
            Dictionary<Vector2Int, List<Vector3>> buckets,
            Vector3 position,
            float spacing,
            float bucketSize)
        {
            Vector2Int center = new Vector2Int(
                Mathf.FloorToInt(position.x / bucketSize),
                Mathf.FloorToInt(position.z / bucketSize));
            float spacingSqr = spacing * spacing;

            for (int z = center.y - 1; z <= center.y + 1; z++)
            {
                for (int x = center.x - 1; x <= center.x + 1; x++)
                {
                    List<Vector3> list;

                    if (!buckets.TryGetValue(new Vector2Int(x, z), out list))
                    {
                        continue;
                    }

                    for (int index = 0; index < list.Count; index++)
                    {
                        Vector3 delta = list[index] - position;
                        delta.y = 0f;

                        if (delta.sqrMagnitude < spacingSqr)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static void AddTreeBucket(
            Dictionary<Vector2Int, List<Vector3>> buckets,
            Vector3 position,
            float bucketSize)
        {
            Vector2Int key = new Vector2Int(
                Mathf.FloorToInt(position.x / bucketSize),
                Mathf.FloorToInt(position.z / bucketSize));
            List<Vector3> list;

            if (!buckets.TryGetValue(key, out list))
            {
                list = new List<Vector3>();
                buckets.Add(key, list);
            }

            list.Add(position);
        }

        private static float Stable01(
            int seed,
            Vector2Int coordinate,
            int salt)
        {
            unchecked
            {
                uint value = (uint)StableHash(seed, coordinate, salt);
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                return (value & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static int StableHash(
            int seed,
            Vector2Int coordinate,
            int salt)
        {
            unchecked
            {
                int hash = seed;
                hash = hash * 397 ^ coordinate.x;
                hash = hash * 397 ^ coordinate.y;
                hash = hash * 397 ^ salt;
                return hash;
            }
        }
    }
}

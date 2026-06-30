using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public sealed class ProceduralRoadSegment
    {
        public Vector3 A;
        public Vector3 B;
        public Vector3 Tangent;
        public float Length;
    }

    public sealed class ProceduralRoadSpline
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        public float Length;

        public void RecalculateLength()
        {
            Length = 0f;

            for (int index = 1; index < Points.Count; index++)
            {
                Length += Vector3.Distance(
                    Points[index - 1],
                    Points[index]);
            }
        }

        public bool TrySample(
            float distance,
            out Vector3 position,
            out Vector3 tangent)
        {
            position = Vector3.zero;
            tangent = Vector3.forward;

            if (Points.Count < 2)
            {
                return false;
            }

            float safeDistance = Mathf.Clamp(distance, 0f, Length);
            float travelled = 0f;

            for (int index = 1; index < Points.Count; index++)
            {
                Vector3 first = Points[index - 1];
                Vector3 second = Points[index];
                Vector3 delta = second - first;
                delta.y = 0f;
                float segmentLength = delta.magnitude;

                if (segmentLength <= 0.0001f)
                {
                    continue;
                }

                if (travelled + segmentLength >= safeDistance ||
                    index == Points.Count - 1)
                {
                    float t = Mathf.Clamp01(
                        (safeDistance - travelled) / segmentLength);

                    position = Vector3.Lerp(first, second, t);
                    tangent = delta / segmentLength;
                    return true;
                }

                travelled += segmentLength;
            }

            Vector3 finalDelta =
                Points[Points.Count - 1] - Points[Points.Count - 2];

            finalDelta.y = 0f;
            tangent = finalDelta.sqrMagnitude > 0.0001f
                ? finalDelta.normalized
                : Vector3.forward;

            position = Points[Points.Count - 1];
            return true;
        }
    }

    public sealed class ProceduralRoadVisualLayout
    {
        private readonly Dictionary<Vector2Int, Vector3> centers =
            new Dictionary<Vector2Int, Vector3>();

        private readonly List<ProceduralRoadSpline> splines =
            new List<ProceduralRoadSpline>();

        private readonly List<ProceduralRoadSegment> segments =
            new List<ProceduralRoadSegment>();

        private readonly List<Vector3> junctionCenters =
            new List<Vector3>();

        public Dictionary<Vector2Int, Vector3> Centers
        {
            get { return centers; }
        }

        public IList<ProceduralRoadSpline> Splines
        {
            get { return splines.AsReadOnly(); }
        }

        public IList<ProceduralRoadSegment> Segments
        {
            get { return segments.AsReadOnly(); }
        }

        public IList<Vector3> JunctionCenters
        {
            get { return junctionCenters.AsReadOnly(); }
        }

        public Vector3 GetCenter(RuntimeRoadNode road)
        {
            if (road == null)
            {
                return Vector3.zero;
            }

            Vector3 center;

            if (centers.TryGetValue(road.Coordinate, out center))
            {
                return center;
            }

            return road.WorldPosition;
        }

        public void AddSpline(ProceduralRoadSpline spline)
        {
            if (spline == null || spline.Points.Count < 2)
            {
                return;
            }

            spline.RecalculateLength();
            splines.Add(spline);

            for (int index = 1; index < spline.Points.Count; index++)
            {
                Vector3 first = spline.Points[index - 1];
                Vector3 second = spline.Points[index];
                Vector3 delta = second - first;
                delta.y = 0f;
                float length = delta.magnitude;

                if (length <= 0.0001f)
                {
                    continue;
                }

                segments.Add(
                    new ProceduralRoadSegment
                    {
                        A = first,
                        B = second,
                        Tangent = delta / length,
                        Length = length
                    });
            }
        }

        public void AddJunction(Vector3 worldPosition)
        {
            junctionCenters.Add(worldPosition);
        }

        public bool TryGetNearestRoad(
            Vector3 worldPosition,
            out Vector3 nearestPoint,
            out Vector3 tangent,
            out float distance)
        {
            nearestPoint = Vector3.zero;
            tangent = Vector3.forward;
            distance = float.PositiveInfinity;

            if (segments.Count == 0)
            {
                return false;
            }

            float bestSqr = float.PositiveInfinity;

            for (int index = 0; index < segments.Count; index++)
            {
                ProceduralRoadSegment segment = segments[index];
                Vector3 point = ClosestPointOnSegmentXZ(
                    worldPosition,
                    segment.A,
                    segment.B);

                float sqr = SqrDistanceXZ(worldPosition, point);

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearestPoint = point;
                    tangent = segment.Tangent;
                }
            }

            distance = Mathf.Sqrt(bestSqr);
            return true;
        }

        public bool IsNearJunction(Vector3 worldPosition, float radius)
        {
            float radiusSqr = radius * radius;

            for (int index = 0; index < junctionCenters.Count; index++)
            {
                if (SqrDistanceXZ(
                        worldPosition,
                        junctionCenters[index]) <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 ClosestPointOnSegmentXZ(
            Vector3 point,
            Vector3 first,
            Vector3 second)
        {
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 a = new Vector2(first.x, first.z);
            Vector2 b = new Vector2(second.x, second.z);
            Vector2 delta = b - a;
            float sqr = delta.sqrMagnitude;

            if (sqr <= 0.000001f)
            {
                return first;
            }

            float t = Mathf.Clamp01(Vector2.Dot(p - a, delta) / sqr);
            Vector2 result = a + delta * t;

            return new Vector3(
                result.x,
                Mathf.Lerp(first.y, second.y, t),
                result.y);
        }

        private static float SqrDistanceXZ(Vector3 first, Vector3 second)
        {
            float x = first.x - second.x;
            float z = first.z - second.z;
            return x * x + z * z;
        }
    }

    public sealed class ProceduralBuildingVariationSettings
    {
        public float HeightRandomness = 0.82f;
        public float TallBuildingChance = 0.18f;
        public float AngleJitterDegrees = 7.5f;
        public float CompositeMassChance = 0.92f;
        public float SteppedSilhouetteChance = 0.58f;
        public float TerraceChance = 0.48f;
        public float RooftopDetailChance = 0.42f;
    }

    public enum ProceduralLandmarkRole
    {
        None = -1,
        Apartment = 0,
        ShoppingMall = 1,
        Hospital = 2,
        Office = 3,
        Industrial = 4,
        Civic = 5
    }

    public sealed class DenseSmallBuildingPlacement
    {
        public Vector3 WorldPosition;
        public Quaternion Rotation;
        public Vector3 AxisX;
        public Vector3 AxisZ;
        public float Width;
        public float Depth;
        public float CollisionWidth;
        public float CollisionDepth;
        public int Floors;
        public int StyleIndex;
        public int MaterialIndex;
        public int ShapeIndex;
        public int TemplateIndex;
        public bool IsLandmark;
        public ProceduralLandmarkRole LandmarkRole;
        public int DetailSeed;
        public bool UseCompositeMass;
        public bool UseSteppedSilhouette;
        public bool HasTerrace;
        public bool HasRooftopDetail;
        public string SourceNodeId;

        public float BoundingRadius
        {
            get
            {
                float halfWidth = Mathf.Max(Width, CollisionWidth) * 0.5f;
                float halfDepth = Mathf.Max(Depth, CollisionDepth) * 0.5f;
                return Mathf.Sqrt(
                    halfWidth * halfWidth +
                    halfDepth * halfDepth);
            }
        }
    }

    internal struct RoadEdgeKey : IEquatable<RoadEdgeKey>
    {
        private readonly Vector2Int first;
        private readonly Vector2Int second;

        public RoadEdgeKey(Vector2Int a, Vector2Int b)
        {
            if (a.x < b.x || (a.x == b.x && a.y <= b.y))
            {
                first = a;
                second = b;
            }
            else
            {
                first = b;
                second = a;
            }
        }

        public bool Equals(RoadEdgeKey other)
        {
            return first == other.first && second == other.second;
        }

        public override bool Equals(object obj)
        {
            return obj is RoadEdgeKey && Equals((RoadEdgeKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return first.GetHashCode() * 397 ^ second.GetHashCode();
            }
        }
    }

    internal sealed class BuildingSpatialIndex
    {
        private readonly Dictionary<Vector2Int, List<DenseSmallBuildingPlacement>> buckets =
            new Dictionary<Vector2Int, List<DenseSmallBuildingPlacement>>();

        private readonly float bucketSize;

        public BuildingSpatialIndex(float size)
        {
            bucketSize = Mathf.Max(0.1f, size);
        }

        public void Add(DenseSmallBuildingPlacement placement)
        {
            int minX;
            int maxX;
            int minY;
            int maxY;
            GetBucketRange(
                placement,
                out minX,
                out maxX,
                out minY,
                out maxY);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2Int key = new Vector2Int(x, y);
                    List<DenseSmallBuildingPlacement> list;

                    if (!buckets.TryGetValue(key, out list))
                    {
                        list = new List<DenseSmallBuildingPlacement>();
                        buckets.Add(key, list);
                    }

                    list.Add(placement);
                }
            }
        }

        public bool Overlaps(DenseSmallBuildingPlacement placement, float gap)
        {
            int minX;
            int maxX;
            int minY;
            int maxY;
            GetBucketRange(
                placement,
                out minX,
                out maxX,
                out minY,
                out maxY);

            HashSet<DenseSmallBuildingPlacement> checkedPlacements =
                new HashSet<DenseSmallBuildingPlacement>();

            for (int y = minY - 1; y <= maxY + 1; y++)
            {
                for (int x = minX - 1; x <= maxX + 1; x++)
                {
                    List<DenseSmallBuildingPlacement> list;

                    if (!buckets.TryGetValue(
                        new Vector2Int(x, y),
                        out list))
                    {
                        continue;
                    }

                    for (int index = 0; index < list.Count; index++)
                    {
                        DenseSmallBuildingPlacement other = list[index];

                        if (other == null || !checkedPlacements.Add(other))
                        {
                            continue;
                        }

                        if (ProceduralCityVisualLayoutBuilder.OverlapsOBB(
                            placement,
                            other,
                            gap))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool HasNearbySameTemplate(
            DenseSmallBuildingPlacement placement,
            float distance)
        {
            if (placement == null || distance <= 0f)
            {
                return false;
            }

            float radius = placement.BoundingRadius + distance;
            int minX = Mathf.FloorToInt(
                (placement.WorldPosition.x - radius) / bucketSize);
            int maxX = Mathf.FloorToInt(
                (placement.WorldPosition.x + radius) / bucketSize);
            int minY = Mathf.FloorToInt(
                (placement.WorldPosition.z - radius) / bucketSize);
            int maxY = Mathf.FloorToInt(
                (placement.WorldPosition.z + radius) / bucketSize);

            HashSet<DenseSmallBuildingPlacement> checkedPlacements =
                new HashSet<DenseSmallBuildingPlacement>();

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    List<DenseSmallBuildingPlacement> list;

                    if (!buckets.TryGetValue(
                        new Vector2Int(x, y),
                        out list))
                    {
                        continue;
                    }

                    for (int index = 0; index < list.Count; index++)
                    {
                        DenseSmallBuildingPlacement other = list[index];

                        if (other == null ||
                            !checkedPlacements.Add(other) ||
                            other.IsLandmark != placement.IsLandmark ||
                            other.TemplateIndex != placement.TemplateIndex)
                        {
                            continue;
                        }

                        float allowed =
                            distance +
                            placement.BoundingRadius +
                            other.BoundingRadius;

                        Vector2 delta = new Vector2(
                            other.WorldPosition.x - placement.WorldPosition.x,
                            other.WorldPosition.z - placement.WorldPosition.z);

                        if (delta.sqrMagnitude < allowed * allowed)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void GetBucketRange(
            DenseSmallBuildingPlacement placement,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY)
        {
            float radius = placement.BoundingRadius;
            minX = Mathf.FloorToInt(
                (placement.WorldPosition.x - radius) / bucketSize);
            maxX = Mathf.FloorToInt(
                (placement.WorldPosition.x + radius) / bucketSize);
            minY = Mathf.FloorToInt(
                (placement.WorldPosition.z - radius) / bucketSize);
            maxY = Mathf.FloorToInt(
                (placement.WorldPosition.z + radius) / bucketSize);
        }
    }

    public static class ProceduralCityVisualLayoutBuilder
    {
        private static readonly Vector2Int[] CoordinateDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public static ProceduralRoadVisualLayout BuildRoadLayout(
            GameBoardRuntimeSnapshot snapshot,
            float crookednessCells,
            float wobbleWavelengthCells,
            int seed,
            float intersectionStability,
            int smoothingIterations,
            int samplesPerCell,
            float edgeBendChance,
            float edgeBendCells,
            float edgeLongitudinalJitterCells)
        {
            ProceduralRoadVisualLayout layout =
                new ProceduralRoadVisualLayout();

            if (snapshot == null || !snapshot.IsValid)
            {
                return layout;
            }

            BuildVisualCenters(
                snapshot,
                layout,
                crookednessCells,
                wobbleWavelengthCells,
                seed,
                intersectionStability);

            List<List<RuntimeRoadNode>> chains = ExtractRoadChains(snapshot);
            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            int safeIterations = Mathf.Clamp(smoothingIterations, 0, 4);
            int safeSamples = Mathf.Clamp(samplesPerCell, 1, 12);

            for (int chainIndex = 0;
                 chainIndex < chains.Count;
                 chainIndex++)
            {
                List<RuntimeRoadNode> chain = chains[chainIndex];

                if (chain == null || chain.Count < 2)
                {
                    continue;
                }

                List<Vector3> points = new List<Vector3>();

                for (int pointIndex = 0;
                     pointIndex < chain.Count;
                     pointIndex++)
                {
                    points.Add(layout.GetCenter(chain[pointIndex]));
                }

                points = BuildOrganicEdgePoints(
                    points,
                    cell,
                    seed,
                    chainIndex,
                    edgeBendChance,
                    edgeBendCells,
                    edgeLongitudinalJitterCells);

                points = ChaikinSmooth(points, safeIterations);
                points = ResamplePolyline(
                    points,
                    cell / safeSamples);

                ProceduralRoadSpline spline =
                    new ProceduralRoadSpline();

                spline.Points.AddRange(points);
                layout.AddSpline(spline);
            }

            for (int index = 0; index < snapshot.Roads.Count; index++)
            {
                RuntimeRoadNode road = snapshot.Roads[index];

                if (road != null && road.ConnectionCount != 2)
                {
                    layout.AddJunction(layout.GetCenter(road));
                }
            }

            return layout;
        }

        public static List<DenseSmallBuildingPlacement> BuildDenseSmallBuildings(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout,
            int seed,
            int maximumDecorativeBuildingCount,
            bool createLandmarkBuildings,
            float placementChance,
            int roadsideRows,
            float minimumSampleSpacingCells,
            float maximumSampleSpacingCells,
            float decorativeMinimumWidthCells,
            float decorativeMaximumWidthCells,
            float decorativeMinimumDepthCells,
            float decorativeMaximumDepthCells,
            int decorativeMinimumFloors,
            int decorativeMaximumFloors,
            float landmarkMinimumWidthCells,
            float landmarkMaximumWidthCells,
            float landmarkMinimumDepthCells,
            float landmarkMaximumDepthCells,
            int landmarkMinimumFloors,
            int landmarkMaximumFloors,
            float decorativeMaximumLandmarkRatio,
            float roadWidthCells,
            float sidewalkWidthCells,
            float setbackCells,
            float rowGapCells,
            float generatedGapCells,
            float intersectionClearanceCells,
            float maximumDecorativeRoadDistanceCells,
            float maximumLandmarkRoadDistanceCells,
            float collisionEnvelopeMultiplier,
            float sameTemplateSeparationCells,
            bool densifyLandmarkNeighborhoods,
            int landmarkNeighborAttemptsPerBuilding,
            float landmarkNeighborPlacementChance,
            float landmarkNeighborhoodRadiusCells,
            bool fillInteriorLots,
            float interiorFillChance,
            float interiorGridStepCells,
            float interiorMaximumRoadDistanceCells,
            ProceduralBuildingVariationSettings variationSettings,
            float buildingBaseYOffset)
        {
            List<DenseSmallBuildingPlacement> result =
                new List<DenseSmallBuildingPlacement>();

            if (snapshot == null ||
                !snapshot.IsValid ||
                roadLayout == null ||
                maximumDecorativeBuildingCount < 0)
            {
                return result;
            }

            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            ProceduralBuildingVariationSettings variation =
                variationSettings ?? new ProceduralBuildingVariationSettings();

            BuildingSpatialIndex spatialIndex =
                new BuildingSpatialIndex(cell);

            float safeLandmarkMaxWidth = Mathf.Max(
                landmarkMinimumWidthCells,
                landmarkMaximumWidthCells);

            float safeLandmarkMaxDepth = Mathf.Max(
                landmarkMinimumDepthCells,
                landmarkMaximumDepthCells);

            float ratio = Mathf.Clamp(
                decorativeMaximumLandmarkRatio,
                0.20f,
                0.50f);

            float safeDecorativeMaxWidth = Mathf.Min(
                Mathf.Max(decorativeMinimumWidthCells, decorativeMaximumWidthCells),
                safeLandmarkMaxWidth * ratio * 0.96f);

            float safeDecorativeMaxDepth = Mathf.Min(
                Mathf.Max(decorativeMinimumDepthCells, decorativeMaximumDepthCells),
                safeLandmarkMaxDepth * ratio * 0.96f);

            if (createLandmarkBuildings)
            {
                AddExistingBuildingLandmarks(
                    snapshot,
                    roadLayout,
                    result,
                    spatialIndex,
                    seed,
                    landmarkMinimumWidthCells,
                    safeLandmarkMaxWidth,
                    landmarkMinimumDepthCells,
                    safeLandmarkMaxDepth,
                    landmarkMinimumFloors,
                    landmarkMaximumFloors,
                    roadWidthCells,
                    sidewalkWidthCells,
                    generatedGapCells,
                    maximumLandmarkRoadDistanceCells,
                    collisionEnvelopeMultiplier,
                    sameTemplateSeparationCells,
                    variation,
                    buildingBaseYOffset);
            }

            int maximumTotalBuildingCount =
                result.Count + Mathf.Max(0, maximumDecorativeBuildingCount);

            if (densifyLandmarkNeighborhoods &&
                createLandmarkBuildings &&
                result.Count < maximumTotalBuildingCount)
            {
                AddLandmarkNeighborhoodClusters(
                    snapshot,
                    roadLayout,
                    result,
                    spatialIndex,
                    seed,
                    maximumTotalBuildingCount,
                    landmarkNeighborAttemptsPerBuilding,
                    landmarkNeighborPlacementChance,
                    landmarkNeighborhoodRadiusCells,
                    decorativeMinimumWidthCells,
                    safeDecorativeMaxWidth,
                    decorativeMinimumDepthCells,
                    safeDecorativeMaxDepth,
                    decorativeMinimumFloors,
                    decorativeMaximumFloors,
                    roadWidthCells,
                    sidewalkWidthCells,
                    generatedGapCells,
                    maximumDecorativeRoadDistanceCells,
                    collisionEnvelopeMultiplier,
                    sameTemplateSeparationCells,
                    variation,
                    buildingBaseYOffset);
            }

            AddRoadsideRows(
                snapshot,
                roadLayout,
                result,
                spatialIndex,
                seed,
                maximumTotalBuildingCount,
                placementChance,
                roadsideRows,
                minimumSampleSpacingCells,
                maximumSampleSpacingCells,
                decorativeMinimumWidthCells,
                safeDecorativeMaxWidth,
                decorativeMinimumDepthCells,
                safeDecorativeMaxDepth,
                decorativeMinimumFloors,
                decorativeMaximumFloors,
                roadWidthCells,
                sidewalkWidthCells,
                setbackCells,
                rowGapCells,
                generatedGapCells,
                intersectionClearanceCells,
                maximumDecorativeRoadDistanceCells,
                collisionEnvelopeMultiplier,
                sameTemplateSeparationCells,
                variation,
                buildingBaseYOffset);

            if (fillInteriorLots && result.Count < maximumTotalBuildingCount)
            {
                AddInteriorLots(
                    snapshot,
                    roadLayout,
                    result,
                    spatialIndex,
                    seed,
                    maximumTotalBuildingCount,
                    interiorFillChance,
                    interiorGridStepCells,
                    interiorMaximumRoadDistanceCells,
                    decorativeMinimumWidthCells,
                    safeDecorativeMaxWidth,
                    decorativeMinimumDepthCells,
                    safeDecorativeMaxDepth,
                    decorativeMinimumFloors,
                    decorativeMaximumFloors,
                    roadWidthCells,
                    sidewalkWidthCells,
                    setbackCells,
                    generatedGapCells,
                    intersectionClearanceCells,
                    collisionEnvelopeMultiplier,
                    sameTemplateSeparationCells,
                    variation,
                    buildingBaseYOffset);
            }

            return result;
        }

        private static void BuildVisualCenters(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout layout,
            float crookednessCells,
            float wobbleWavelengthCells,
            int seed,
            float intersectionStability)
        {
            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            float amplitude = cell * Mathf.Clamp(crookednessCells, 0f, 0.45f);
            float wavelength = Mathf.Max(1.5f, wobbleWavelengthCells);
            float frequency = 1f / wavelength;
            float intersectionFactor = 1f - Mathf.Clamp01(intersectionStability);
            float seedOffset = seed * 0.01731f;

            for (int index = 0; index < snapshot.Roads.Count; index++)
            {
                RuntimeRoadNode road = snapshot.Roads[index];

                if (road == null)
                {
                    continue;
                }

                float noiseA = Mathf.PerlinNoise(
                    road.Coordinate.x * frequency + seedOffset + 17.13f,
                    road.Coordinate.y * frequency - seedOffset + 71.91f) * 2f - 1f;

                float noiseB = Mathf.PerlinNoise(
                    road.Coordinate.x * frequency + seedOffset + 91.37f,
                    road.Coordinate.y * frequency - seedOffset + 29.47f) * 2f - 1f;

                bool northSouth =
                    (road.Mask & RuntimeRoadMask.North) != 0 ||
                    (road.Mask & RuntimeRoadMask.South) != 0;

                bool eastWest =
                    (road.Mask & RuntimeRoadMask.East) != 0 ||
                    (road.Mask & RuntimeRoadMask.West) != 0;

                Vector3 offset = Vector3.zero;

                if (northSouth && !eastWest)
                {
                    offset.x = noiseA * amplitude;
                }
                else if (eastWest && !northSouth)
                {
                    offset.z = noiseB * amplitude;
                }
                else
                {
                    float factor = road.ConnectionCount >= 3
                        ? intersectionFactor
                        : 0.48f;

                    offset.x = noiseA * amplitude * factor;
                    offset.z = noiseB * amplitude * factor;
                }

                if (road.ConnectionCount <= 1)
                {
                    offset *= 0.35f;
                }

                layout.Centers[road.Coordinate] =
                    road.WorldPosition + offset;
            }
        }

        private static List<List<RuntimeRoadNode>> ExtractRoadChains(
            GameBoardRuntimeSnapshot snapshot)
        {
            List<List<RuntimeRoadNode>> result =
                new List<List<RuntimeRoadNode>>();

            HashSet<RoadEdgeKey> visited =
                new HashSet<RoadEdgeKey>();

            List<RuntimeRoadNode> ordered =
                new List<RuntimeRoadNode>(snapshot.Roads);

            ordered.Sort(delegate(
                RuntimeRoadNode first,
                RuntimeRoadNode second)
            {
                int x = first.Coordinate.x.CompareTo(second.Coordinate.x);
                return x != 0
                    ? x
                    : first.Coordinate.y.CompareTo(second.Coordinate.y);
            });

            for (int index = 0; index < ordered.Count; index++)
            {
                RuntimeRoadNode start = ordered[index];

                if (start == null || start.ConnectionCount == 2)
                {
                    continue;
                }

                List<RuntimeRoadNode> neighbors =
                    GetNeighbors(snapshot, start);

                for (int neighborIndex = 0;
                     neighborIndex < neighbors.Count;
                     neighborIndex++)
                {
                    RuntimeRoadNode next = neighbors[neighborIndex];
                    RoadEdgeKey edge = new RoadEdgeKey(
                        start.Coordinate,
                        next.Coordinate);

                    if (visited.Contains(edge))
                    {
                        continue;
                    }

                    List<RuntimeRoadNode> chain = WalkChain(
                        snapshot,
                        start,
                        next,
                        visited);

                    if (chain.Count >= 2)
                    {
                        result.Add(chain);
                    }
                }
            }

            for (int index = 0; index < ordered.Count; index++)
            {
                RuntimeRoadNode start = ordered[index];
                List<RuntimeRoadNode> neighbors =
                    GetNeighbors(snapshot, start);

                for (int neighborIndex = 0;
                     neighborIndex < neighbors.Count;
                     neighborIndex++)
                {
                    RuntimeRoadNode next = neighbors[neighborIndex];
                    RoadEdgeKey edge = new RoadEdgeKey(
                        start.Coordinate,
                        next.Coordinate);

                    if (visited.Contains(edge))
                    {
                        continue;
                    }

                    List<RuntimeRoadNode> chain = WalkChain(
                        snapshot,
                        start,
                        next,
                        visited);

                    if (chain.Count >= 2)
                    {
                        result.Add(chain);
                    }
                }
            }

            return result;
        }

        private static List<RuntimeRoadNode> WalkChain(
            GameBoardRuntimeSnapshot snapshot,
            RuntimeRoadNode start,
            RuntimeRoadNode next,
            HashSet<RoadEdgeKey> visited)
        {
            List<RuntimeRoadNode> chain =
                new List<RuntimeRoadNode>();

            chain.Add(start);
            RuntimeRoadNode previous = start;
            RuntimeRoadNode current = next;
            int safety = Mathf.Max(16, snapshot.Roads.Count * 2);

            while (current != null && safety-- > 0)
            {
                RoadEdgeKey currentEdge = new RoadEdgeKey(
                    previous.Coordinate,
                    current.Coordinate);

                if (!visited.Add(currentEdge))
                {
                    break;
                }

                chain.Add(current);

                if (current == start)
                {
                    break;
                }

                if (current.ConnectionCount != 2)
                {
                    break;
                }

                List<RuntimeRoadNode> neighbors =
                    GetNeighbors(snapshot, current);

                RuntimeRoadNode chosen = null;

                for (int index = 0; index < neighbors.Count; index++)
                {
                    RuntimeRoadNode candidate = neighbors[index];

                    if (candidate == previous)
                    {
                        continue;
                    }

                    RoadEdgeKey candidateEdge = new RoadEdgeKey(
                        current.Coordinate,
                        candidate.Coordinate);

                    if (!visited.Contains(candidateEdge))
                    {
                        chosen = candidate;
                        break;
                    }
                }

                if (chosen == null)
                {
                    break;
                }

                previous = current;
                current = chosen;
            }

            return chain;
        }

        private static List<RuntimeRoadNode> GetNeighbors(
            GameBoardRuntimeSnapshot snapshot,
            RuntimeRoadNode road)
        {
            List<RuntimeRoadNode> result =
                new List<RuntimeRoadNode>();

            for (int index = 0; index < CoordinateDirections.Length; index++)
            {
                RuntimeRoadNode neighbor;

                if (snapshot.RoadByCoordinate.TryGetValue(
                    road.Coordinate + CoordinateDirections[index],
                    out neighbor))
                {
                    result.Add(neighbor);
                }
            }

            return result;
        }

        private static List<Vector3> BuildOrganicEdgePoints(
            List<Vector3> source,
            float cellSize,
            int seed,
            int chainIndex,
            float bendChance,
            float bendCells,
            float longitudinalJitterCells)
        {
            List<Vector3> result = new List<Vector3>();

            if (source == null || source.Count < 2)
            {
                return source != null
                    ? new List<Vector3>(source)
                    : result;
            }

            float safeChance = Mathf.Clamp01(bendChance);
            float maximumBend = Mathf.Max(0f, bendCells) * cellSize;
            float maximumLongitudinal =
                Mathf.Max(0f, longitudinalJitterCells) * cellSize;

            result.Add(source[0]);

            for (int index = 0; index < source.Count - 1; index++)
            {
                Vector3 first = source[index];
                Vector3 second = source[index + 1];
                Vector3 delta = second - first;
                delta.y = 0f;
                float length = delta.magnitude;

                if (length <= 0.0001f)
                {
                    continue;
                }

                Vector3 tangent = delta / length;
                Vector3 perpendicular = new Vector3(
                    -tangent.z,
                    0f,
                    tangent.x);

                Vector2Int stableCoordinate = new Vector2Int(
                    chainIndex,
                    index);

                if (Stable01(seed, stableCoordinate, 1201) <= safeChance)
                {
                    float coherentNoise = Mathf.PerlinNoise(
                        chainIndex * 0.173f + index * 0.31f + seed * 0.00037f,
                        seed * 0.00091f + 47.3f) * 2f - 1f;

                    float localNoise =
                        Stable01(seed, stableCoordinate, 1213) * 2f - 1f;

                    float lateral =
                        (coherentNoise * 0.72f + localNoise * 0.28f) *
                        maximumBend;

                    float longitudinal =
                        (Stable01(seed, stableCoordinate, 1223) * 2f - 1f) *
                        maximumLongitudinal;

                    float t = Mathf.Clamp(
                        0.5f + longitudinal / Mathf.Max(0.001f, length),
                        0.28f,
                        0.72f);

                    Vector3 middle =
                        Vector3.Lerp(first, second, t) +
                        perpendicular * lateral;

                    middle.y = Mathf.Lerp(first.y, second.y, t);
                    result.Add(middle);
                }

                result.Add(second);
            }

            return result;
        }

        private static List<Vector3> ChaikinSmooth(
            List<Vector3> source,
            int iterations)
        {
            if (source == null || source.Count < 3 || iterations <= 0)
            {
                return source != null
                    ? new List<Vector3>(source)
                    : new List<Vector3>();
            }

            List<Vector3> current = new List<Vector3>(source);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                List<Vector3> next = new List<Vector3>();
                next.Add(current[0]);

                for (int index = 0; index < current.Count - 1; index++)
                {
                    Vector3 first = current[index];
                    Vector3 second = current[index + 1];
                    next.Add(Vector3.Lerp(first, second, 0.25f));
                    next.Add(Vector3.Lerp(first, second, 0.75f));
                }

                next.Add(current[current.Count - 1]);
                current = next;
            }

            return current;
        }

        private static List<Vector3> ResamplePolyline(
            List<Vector3> source,
            float spacing)
        {
            List<Vector3> result = new List<Vector3>();

            if (source == null || source.Count == 0)
            {
                return result;
            }

            if (source.Count == 1 || spacing <= 0.001f)
            {
                result.AddRange(source);
                return result;
            }

            result.Add(source[0]);
            Vector3 current = source[0];
            int segmentIndex = 1;
            float remainingToNext = spacing;

            while (segmentIndex < source.Count)
            {
                Vector3 target = source[segmentIndex];
                Vector3 delta = target - current;
                float length = delta.magnitude;

                if (length <= 0.0001f)
                {
                    current = target;
                    segmentIndex++;
                    continue;
                }

                if (length >= remainingToNext)
                {
                    current += delta / length * remainingToNext;
                    result.Add(current);
                    remainingToNext = spacing;
                }
                else
                {
                    remainingToNext -= length;
                    current = target;
                    segmentIndex++;
                }
            }

            Vector3 last = source[source.Count - 1];

            if (Vector3.Distance(result[result.Count - 1], last) > 0.001f)
            {
                result.Add(last);
            }

            return result;
        }

        private static void AddExistingBuildingLandmarks(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout,
            List<DenseSmallBuildingPlacement> result,
            BuildingSpatialIndex spatialIndex,
            int seed,
            float minimumWidthCells,
            float maximumWidthCells,
            float minimumDepthCells,
            float maximumDepthCells,
            int minimumFloors,
            int maximumFloors,
            float roadWidthCells,
            float sidewalkWidthCells,
            float generatedGapCells,
            float maximumRoadDistanceCells,
            float collisionEnvelopeMultiplier,
            float sameTemplateSeparationCells,
            ProceduralBuildingVariationSettings variation,
            float buildingBaseYOffset)
        {
            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            Dictionary<int, int> districtBuildingCounters =
                new Dictionary<int, int>();

            for (int buildingIndex = 0;
                 buildingIndex < snapshot.Buildings.Count;
                 buildingIndex++)
            {
                RuntimeBuildingNode source = snapshot.Buildings[buildingIndex];

                if (source == null)
                {
                    continue;
                }

                int localBuildingIndex = 0;

                if (districtBuildingCounters.TryGetValue(
                    source.DistrictIndex,
                    out localBuildingIndex))
                {
                    districtBuildingCounters[source.DistrictIndex] =
                        localBuildingIndex + 1;
                }
                else
                {
                    districtBuildingCounters.Add(source.DistrictIndex, 1);
                }

                Vector3 nearest;
                Vector3 tangent;
                float roadDistance;

                if (!roadLayout.TryGetNearestRoad(
                    source.WorldPosition,
                    out nearest,
                    out tangent,
                    out roadDistance))
                {
                    tangent = Vector3.right;
                    nearest = source.WorldPosition - Vector3.forward * cell;
                }

                Vector3 outward = source.WorldPosition - nearest;
                outward.y = 0f;

                if (outward.sqrMagnitude <= 0.0001f)
                {
                    outward = new Vector3(-tangent.z, 0f, tangent.x);
                }
                else
                {
                    outward.Normalize();
                }

                tangent.y = 0f;
                tangent = tangent.sqrMagnitude > 0.0001f
                    ? tangent.normalized
                    : Vector3.right;

                Vector2Int stableCoordinate = new Vector2Int(
                    Mathf.Max(0, source.DistrictIndex),
                    localBuildingIndex);

                ProceduralLandmarkRole role = SelectLandmarkRole(
                    seed,
                    source.DistrictIndex,
                    localBuildingIndex,
                    source.NodeId);

                Vector2 footprintFactor = GetLandmarkFootprintFactor(role);

                float widthCells = Mathf.Lerp(
                    Mathf.Max(0.45f, minimumWidthCells),
                    Mathf.Max(minimumWidthCells, maximumWidthCells),
                    Stable01(seed, stableCoordinate, 703));

                float depthCells = Mathf.Lerp(
                    Mathf.Max(0.42f, minimumDepthCells),
                    Mathf.Max(minimumDepthCells, maximumDepthCells),
                    Stable01(seed, stableCoordinate, 709));

                widthCells = Mathf.Clamp(
                    widthCells * footprintFactor.x,
                    minimumWidthCells,
                    maximumWidthCells);

                depthCells = Mathf.Clamp(
                    depthCells * footprintFactor.y,
                    minimumDepthCells,
                    maximumDepthCells);

                int floors = CalculateLandmarkFloors(
                    role,
                    seed,
                    stableCoordinate,
                    minimumFloors,
                    maximumFloors);

                float landmarkRoadLimit =
                    Mathf.Max(0.75f, maximumRoadDistanceCells) * cell;

                float minimumLandmarkRoadDistance =
                    roadWidthCells * cell * 0.5f +
                    sidewalkWidthCells * cell +
                    depthCells * cell * 0.56f +
                    cell * 0.06f;

                float clampedRoadDistance = Mathf.Clamp(
                    roadDistance,
                    minimumLandmarkRoadDistance,
                    Mathf.Max(
                        minimumLandmarkRoadDistance,
                        landmarkRoadLimit));

                Vector3 position =
                    nearest + outward * clampedRoadDistance;

                position.y = buildingBaseYOffset;

                DenseSmallBuildingPlacement placement =
                    CreatePlacement(
                        position,
                        tangent,
                        outward,
                        widthCells * cell,
                        depthCells * cell,
                        floors,
                        ((int)role) % 3,
                        source.NodeId,
                        seed,
                        stableCoordinate,
                        719,
                        variation,
                        true,
                        role,
                        3 + (int)role,
                        GetLandmarkShape(role, seed, stableCoordinate),
                        collisionEnvelopeMultiplier);

                if (!EnsureUniqueTemplate(
                    spatialIndex,
                    placement,
                    Mathf.Max(0.45f, sameTemplateSeparationCells) * cell))
                {
                    continue;
                }

                if (!IsBuildingPlacementValid(
                    snapshot,
                    roadLayout,
                    placement,
                    spatialIndex,
                    roadWidthCells * cell * 0.5f,
                    sidewalkWidthCells * cell,
                    Mathf.Max(0.003f, generatedGapCells) * cell,
                    0f,
                    landmarkRoadLimit,
                    true))
                {
                    placement.Width *= 0.86f;
                    placement.Depth *= 0.86f;
                    placement.CollisionWidth *= 0.86f;
                    placement.CollisionDepth *= 0.86f;

                    if (!IsBuildingPlacementValid(
                        snapshot,
                        roadLayout,
                        placement,
                        spatialIndex,
                        roadWidthCells * cell * 0.5f,
                        sidewalkWidthCells * cell,
                        Mathf.Max(0.003f, generatedGapCells) * cell,
                        0f,
                        landmarkRoadLimit,
                        true))
                    {
                        continue;
                    }
                }

                result.Add(placement);
                spatialIndex.Add(placement);
            }
        }

        private static void AddLandmarkNeighborhoodClusters(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout,
            List<DenseSmallBuildingPlacement> result,
            BuildingSpatialIndex spatialIndex,
            int seed,
            int maximumBuildingCount,
            int attemptsPerLandmark,
            float placementChance,
            float neighborhoodRadiusCells,
            float minimumWidthCells,
            float maximumWidthCells,
            float minimumDepthCells,
            float maximumDepthCells,
            int minimumFloors,
            int maximumFloors,
            float roadWidthCells,
            float sidewalkWidthCells,
            float generatedGapCells,
            float maximumRoadDistanceCells,
            float collisionEnvelopeMultiplier,
            float sameTemplateSeparationCells,
            ProceduralBuildingVariationSettings variation,
            float buildingBaseYOffset)
        {
            if (result == null || result.Count == 0)
            {
                return;
            }

            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            int safeAttempts = Mathf.Clamp(attemptsPerLandmark, 0, 48);
            float safeChance = Mathf.Clamp01(placementChance);
            float roadHalfWidth = Mathf.Max(0.04f, roadWidthCells) * cell * 0.5f;
            float sidewalk = Mathf.Max(0f, sidewalkWidthCells) * cell;
            float generatedGap = Mathf.Max(0.002f, generatedGapCells) * cell;
            float maximumRoadDistance =
                Mathf.Max(0.55f, maximumRoadDistanceCells) * cell;
            float ringExtension = Mathf.Max(0.20f, neighborhoodRadiusCells) * cell;

            float minWidth = Mathf.Max(0.12f, minimumWidthCells) * cell;
            float maxWidth = Mathf.Max(minimumWidthCells, maximumWidthCells) * cell;
            float minDepth = Mathf.Max(0.12f, minimumDepthCells) * cell;
            float maxDepth = Mathf.Max(minimumDepthCells, maximumDepthCells) * cell;

            List<DenseSmallBuildingPlacement> landmarks =
                new List<DenseSmallBuildingPlacement>();

            for (int index = 0; index < result.Count; index++)
            {
                if (result[index] != null && result[index].IsLandmark)
                {
                    landmarks.Add(result[index]);
                }
            }

            for (int landmarkIndex = 0;
                 landmarkIndex < landmarks.Count && result.Count < maximumBuildingCount;
                 landmarkIndex++)
            {
                DenseSmallBuildingPlacement landmark = landmarks[landmarkIndex];

                for (int attempt = 0;
                     attempt < safeAttempts && result.Count < maximumBuildingCount;
                     attempt++)
                {
                    Vector2Int stableCoordinate = new Vector2Int(
                        landmarkIndex,
                        attempt);

                    if (Stable01(seed, stableCoordinate, 1301) > safeChance)
                    {
                        continue;
                    }

                    float width = Mathf.Lerp(
                        minWidth,
                        maxWidth * 0.88f,
                        Stable01(seed, stableCoordinate, 1303));

                    float depth = Mathf.Lerp(
                        minDepth,
                        maxDepth * 0.88f,
                        Stable01(seed, stableCoordinate, 1307));

                    float angle =
                        (attempt / Mathf.Max(1f, safeAttempts)) * Mathf.PI * 2f +
                        (Stable01(seed, stableCoordinate, 1319) * 2f - 1f) * 0.18f;

                    Vector3 radial = new Vector3(
                        Mathf.Cos(angle),
                        0f,
                        Mathf.Sin(angle));

                    float landmarkExtent =
                        Mathf.Max(landmark.CollisionWidth, landmark.CollisionDepth) * 0.52f;

                    float radius = landmarkExtent +
                        Mathf.Lerp(
                            Mathf.Max(width, depth) * 0.55f + generatedGap,
                            ringExtension,
                            Stable01(seed, stableCoordinate, 1321));

                    Vector3 position = landmark.WorldPosition + radial * radius;
                    position.y = buildingBaseYOffset;

                    Vector3 nearestRoad;
                    Vector3 roadTangent;
                    float roadDistance;

                    if (!roadLayout.TryGetNearestRoad(
                        position,
                        out nearestRoad,
                        out roadTangent,
                        out roadDistance) ||
                        roadDistance > maximumRoadDistance)
                    {
                        continue;
                    }

                    Vector3 outward = position - nearestRoad;
                    outward.y = 0f;

                    if (outward.sqrMagnitude <= 0.0001f)
                    {
                        outward = new Vector3(
                            -roadTangent.z,
                            0f,
                            roadTangent.x);
                    }
                    else
                    {
                        outward.Normalize();
                    }

                    int floors = RandomFloor(
                        seed,
                        stableCoordinate,
                        1327,
                        minimumFloors,
                        maximumFloors,
                        variation);

                    DenseSmallBuildingPlacement placement =
                        CreatePlacement(
                            position,
                            roadTangent,
                            outward,
                            width,
                            depth,
                            floors,
                            StableStyle(seed, stableCoordinate, 1329),
                            string.Empty,
                            seed,
                            stableCoordinate,
                            1331,
                            variation,
                            collisionEnvelopeMultiplier:
                                collisionEnvelopeMultiplier);

                    if (!EnsureUniqueTemplate(
                        spatialIndex,
                        placement,
                        Mathf.Max(0.18f, sameTemplateSeparationCells * 0.75f) * cell))
                    {
                        continue;
                    }

                    if (!IsBuildingPlacementValid(
                        snapshot,
                        roadLayout,
                        placement,
                        spatialIndex,
                        roadHalfWidth,
                        sidewalk,
                        generatedGap * 0.35f,
                        0f,
                        maximumRoadDistance,
                        true))
                    {
                        continue;
                    }

                    result.Add(placement);
                    spatialIndex.Add(placement);
                }
            }
        }

        private static void AddRoadsideRows(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout,
            List<DenseSmallBuildingPlacement> result,
            BuildingSpatialIndex spatialIndex,
            int seed,
            int maximumBuildingCount,
            float placementChance,
            int roadsideRows,
            float minimumSampleSpacingCells,
            float maximumSampleSpacingCells,
            float minimumWidthCells,
            float maximumWidthCells,
            float minimumDepthCells,
            float maximumDepthCells,
            int minimumFloors,
            int maximumFloors,
            float roadWidthCells,
            float sidewalkWidthCells,
            float setbackCells,
            float rowGapCells,
            float generatedGapCells,
            float intersectionClearanceCells,
            float maximumRoadDistanceCells,
            float collisionEnvelopeMultiplier,
            float sameTemplateSeparationCells,
            ProceduralBuildingVariationSettings variation,
            float buildingBaseYOffset)
        {
            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            float minSpacing = Mathf.Max(0.14f, minimumSampleSpacingCells) * cell;
            float maxSpacing = Mathf.Max(
                minimumSampleSpacingCells,
                maximumSampleSpacingCells) * cell;

            float minWidth = Mathf.Max(0.15f, minimumWidthCells) * cell;
            float maxWidth = Mathf.Max(minimumWidthCells, maximumWidthCells) * cell;
            float minDepth = Mathf.Max(0.15f, minimumDepthCells) * cell;
            float maxDepth = Mathf.Max(minimumDepthCells, maximumDepthCells) * cell;
            int safeRows = Mathf.Clamp(roadsideRows, 1, 5);
            float roadHalfWidth = Mathf.Max(0.04f, roadWidthCells) * cell * 0.5f;
            float sidewalk = Mathf.Max(0f, sidewalkWidthCells) * cell;
            float setback = Mathf.Max(0f, setbackCells) * cell;
            float rowGap = Mathf.Max(0f, rowGapCells) * cell;
            float generatedGap = Mathf.Max(0.003f, generatedGapCells) * cell;
            float junctionClearance = Mathf.Max(0f, intersectionClearanceCells) * cell;

            for (int splineIndex = 0;
                 splineIndex < roadLayout.Splines.Count &&
                 result.Count < maximumBuildingCount;
                 splineIndex++)
            {
                ProceduralRoadSpline spline = roadLayout.Splines[splineIndex];

                if (spline == null || spline.Length <= 0.1f)
                {
                    continue;
                }

                for (int sideIndex = 0;
                     sideIndex < 2 && result.Count < maximumBuildingCount;
                     sideIndex++)
                {
                    float sideSign = sideIndex == 0 ? -1f : 1f;

                    for (int row = 0;
                         row < safeRows && result.Count < maximumBuildingCount;
                         row++)
                    {
                        float distance = Stable01(
                            seed,
                            new Vector2Int(splineIndex, sideIndex),
                            row * 41 + 401) * maxSpacing;

                        int candidateIndex = 0;

                        while (distance < spline.Length &&
                               result.Count < maximumBuildingCount)
                        {
                            Vector2Int stableCoordinate = new Vector2Int(
                                splineIndex * 10000 + candidateIndex,
                                sideIndex * 100 + row);

                            float width = Mathf.Lerp(
                                minWidth,
                                maxWidth,
                                Stable01(seed, stableCoordinate, 409));

                            float depth = Mathf.Lerp(
                                minDepth,
                                maxDepth,
                                Stable01(seed, stableCoordinate, 419));

                            float spacing = Mathf.Max(
                                Mathf.Lerp(
                                    minSpacing,
                                    maxSpacing,
                                    Stable01(seed, stableCoordinate, 421)),
                                width * 0.70f + generatedGap);

                            Vector3 roadPosition;
                            Vector3 tangent;

                            if (!spline.TrySample(
                                distance,
                                out roadPosition,
                                out tangent))
                            {
                                break;
                            }

                            float chance = Mathf.Clamp01(placementChance) *
                                Mathf.Lerp(1f, 0.68f, row / Mathf.Max(1f, safeRows - 1f));

                            if (Stable01(seed, stableCoordinate, 431) <= chance)
                            {
                                Vector3 left = new Vector3(
                                    -tangent.z,
                                    0f,
                                    tangent.x);

                                Vector3 outward = left * sideSign;
                                float rowPitch = maxDepth * 0.72f + rowGap;
                                float outwardDistance =
                                    roadHalfWidth +
                                    sidewalk +
                                    setback +
                                    depth * 0.5f +
                                    row * rowPitch;

                                float tangentJitter =
                                    (Stable01(seed, stableCoordinate, 433) * 2f - 1f) *
                                    cell * 0.10f;

                                Vector3 position =
                                    roadPosition +
                                    outward * outwardDistance +
                                    tangent * tangentJitter;

                                position.y = buildingBaseYOffset;

                                Vector2Int urbanBlockCoordinate = new Vector2Int(
                                    splineIndex * 4096 + candidateIndex / 5,
                                    sideIndex * 64 + row);

                                int blockFloors = RandomFloor(
                                    seed,
                                    urbanBlockCoordinate,
                                    439,
                                    minimumFloors,
                                    maximumFloors,
                                    variation);

                                if (Stable01(seed, stableCoordinate, 441) < 0.24f)
                                {
                                    blockFloors += Stable01(
                                        seed,
                                        stableCoordinate,
                                        442) < 0.5f ? -1 : 1;
                                }

                                DenseSmallBuildingPlacement placement =
                                    CreatePlacement(
                                        position,
                                        tangent,
                                        outward,
                                        width,
                                        depth,
                                        Mathf.Clamp(
                                            blockFloors,
                                            minimumFloors,
                                            maximumFloors),
                                        StableStyle(
                                            seed,
                                            urbanBlockCoordinate,
                                            443),
                                        string.Empty,
                                        seed,
                                        stableCoordinate,
                                        457,
                                        variation,
                                        collisionEnvelopeMultiplier:
                                            collisionEnvelopeMultiplier);

                                if (!EnsureUniqueTemplate(
                                    spatialIndex,
                                    placement,
                                    Mathf.Max(0.45f, sameTemplateSeparationCells) * cell))
                                {
                                    distance += spacing;
                                    candidateIndex++;
                                    continue;
                                }

                                if (IsBuildingPlacementValid(
                                    snapshot,
                                    roadLayout,
                                    placement,
                                    spatialIndex,
                                    roadHalfWidth,
                                    sidewalk,
                                    generatedGap,
                                    junctionClearance,
                                    Mathf.Max(0.45f, maximumRoadDistanceCells) * cell,
                                    true))
                                {
                                    result.Add(placement);
                                    spatialIndex.Add(placement);
                                }
                            }

                            distance += spacing;
                            candidateIndex++;
                        }
                    }
                }
            }
        }

        private static void AddInteriorLots(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout,
            List<DenseSmallBuildingPlacement> result,
            BuildingSpatialIndex spatialIndex,
            int seed,
            int maximumBuildingCount,
            float fillChance,
            float gridStepCells,
            float maximumRoadDistanceCells,
            float minimumWidthCells,
            float maximumWidthCells,
            float minimumDepthCells,
            float maximumDepthCells,
            int minimumFloors,
            int maximumFloors,
            float roadWidthCells,
            float sidewalkWidthCells,
            float setbackCells,
            float generatedGapCells,
            float intersectionClearanceCells,
            float collisionEnvelopeMultiplier,
            float sameTemplateSeparationCells,
            ProceduralBuildingVariationSettings variation,
            float buildingBaseYOffset)
        {
            float cell = Mathf.Max(0.1f, snapshot.CellWorldSize);
            float step = Mathf.Max(0.25f, gridStepCells) * cell;
            float maxRoadDistance = Mathf.Max(0.5f, maximumRoadDistanceCells) * cell;
            float roadHalfWidth = Mathf.Max(0.04f, roadWidthCells) * cell * 0.5f;
            float sidewalk = Mathf.Max(0f, sidewalkWidthCells) * cell;
            float setback = Mathf.Max(0f, setbackCells) * cell;
            float generatedGap = Mathf.Max(0.003f, generatedGapCells) * cell;
            float junctionClearance = Mathf.Max(0f, intersectionClearanceCells) * cell;
            float minimumRoadDistance = roadHalfWidth + sidewalk + setback;

            float minX = snapshot.BoardCenter.x - snapshot.BoardSizeXZ.x * 0.5f - cell;
            float maxX = snapshot.BoardCenter.x + snapshot.BoardSizeXZ.x * 0.5f + cell;
            float minZ = snapshot.BoardCenter.z - snapshot.BoardSizeXZ.y * 0.5f - cell;
            float maxZ = snapshot.BoardCenter.z + snapshot.BoardSizeXZ.y * 0.5f + cell;

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
                int firstHash = StableHash(seed, first, 503);
                int secondHash = StableHash(seed, second, 503);
                return firstHash.CompareTo(secondHash);
            });

            for (int index = 0;
                 index < candidates.Count &&
                 result.Count < maximumBuildingCount;
                 index++)
            {
                Vector2Int candidate = candidates[index];

                if (Stable01(seed, candidate, 509) > Mathf.Clamp01(fillChance))
                {
                    continue;
                }

                float jitterX =
                    (Stable01(seed, candidate, 521) * 2f - 1f) *
                    step * 0.42f;

                float jitterZ =
                    (Stable01(seed, candidate, 523) * 2f - 1f) *
                    step * 0.42f;

                Vector3 position = new Vector3(
                    minX + candidate.x * step + jitterX,
                    buildingBaseYOffset,
                    minZ + candidate.y * step + jitterZ);

                Vector3 nearest;
                Vector3 tangent;
                float roadDistance;

                if (!roadLayout.TryGetNearestRoad(
                    position,
                    out nearest,
                    out tangent,
                    out roadDistance) ||
                    roadDistance < minimumRoadDistance ||
                    roadDistance > maxRoadDistance)
                {
                    continue;
                }

                Vector3 outward = position - nearest;
                outward.y = 0f;

                if (outward.sqrMagnitude <= 0.0001f)
                {
                    outward = new Vector3(-tangent.z, 0f, tangent.x);
                }
                else
                {
                    outward.Normalize();
                }

                float width = Mathf.Lerp(
                    minimumWidthCells,
                    maximumWidthCells,
                    Stable01(seed, candidate, 541)) * cell;

                float depth = Mathf.Lerp(
                    minimumDepthCells,
                    maximumDepthCells,
                    Stable01(seed, candidate, 547)) * cell;

                DenseSmallBuildingPlacement placement =
                    CreatePlacement(
                        position,
                        tangent,
                        outward,
                        width,
                        depth,
                        RandomFloor(
                            seed,
                            candidate,
                            557,
                            minimumFloors,
                            maximumFloors,
                            variation),
                        StableStyle(seed, candidate, 563),
                        string.Empty,
                        seed,
                        candidate,
                        571,
                        variation,
                        collisionEnvelopeMultiplier:
                            collisionEnvelopeMultiplier);

                if (!EnsureUniqueTemplate(
                    spatialIndex,
                    placement,
                    Mathf.Max(0.45f, sameTemplateSeparationCells) * cell))
                {
                    continue;
                }

                if (!IsBuildingPlacementValid(
                    snapshot,
                    roadLayout,
                    placement,
                    spatialIndex,
                    roadHalfWidth,
                    sidewalk,
                    generatedGap,
                    junctionClearance,
                    maxRoadDistance,
                    true))
                {
                    continue;
                }

                result.Add(placement);
                spatialIndex.Add(placement);
            }
        }

        private static DenseSmallBuildingPlacement CreatePlacement(
            Vector3 position,
            Vector3 tangent,
            Vector3 outward,
            float width,
            float depth,
            int floors,
            int styleIndex,
            string sourceNodeId,
            int seed,
            Vector2Int variationCoordinate,
            int variationSalt,
            ProceduralBuildingVariationSettings variation,
            bool isLandmark = false,
            ProceduralLandmarkRole landmarkRole = ProceduralLandmarkRole.None,
            int materialIndex = -1,
            int forcedShapeIndex = -1,
            float collisionEnvelopeMultiplier = 1.12f)
        {
            tangent.y = 0f;
            tangent = tangent.sqrMagnitude > 0.0001f
                ? tangent.normalized
                : Vector3.right;

            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f
                ? outward.normalized
                : new Vector3(-tangent.z, 0f, tangent.x);

            ProceduralBuildingVariationSettings safeVariation =
                variation ?? new ProceduralBuildingVariationSettings();

            float angleJitter =
                (Stable01(seed, variationCoordinate, variationSalt) * 2f - 1f) *
                Mathf.Max(0f, safeVariation.AngleJitterDegrees);

            Quaternion rotation =
                Quaternion.AngleAxis(angleJitter, Vector3.up) *
                Quaternion.LookRotation(outward, Vector3.up);

            Vector3 rotatedAxisX = rotation * Vector3.right;
            Vector3 rotatedAxisZ = rotation * Vector3.forward;
            int detailSeed = StableHash(
                seed,
                variationCoordinate,
                variationSalt + 17);

            bool composite;
            bool stepped;
            bool terrace;
            bool rooftop;
            int shapeIndex;

            if (isLandmark)
            {
                composite = true;
                stepped = Stable01(
                    seed,
                    variationCoordinate,
                    variationSalt + 29) <=
                    Mathf.Clamp01(safeVariation.SteppedSilhouetteChance);

                terrace = Stable01(
                    seed,
                    variationCoordinate,
                    variationSalt + 31) <=
                    Mathf.Clamp01(safeVariation.TerraceChance);

                rooftop = Stable01(
                    seed,
                    variationCoordinate,
                    variationSalt + 37) <=
                    Mathf.Clamp01(safeVariation.RooftopDetailChance);

                shapeIndex = forcedShapeIndex >= 0
                    ? Mathf.Clamp(forcedShapeIndex, 0, 6)
                    : 5;

                ApplyLandmarkDetailRules(
                    landmarkRole,
                    ref stepped,
                    ref terrace,
                    ref rooftop);
            }
            else
            {
                composite = Stable01(
                    seed,
                    variationCoordinate,
                    variationSalt + 23) <=
                    Mathf.Clamp01(safeVariation.CompositeMassChance);

                stepped = false;
                terrace = false;
                rooftop = Stable01(
                    seed,
                    variationCoordinate,
                    variationSalt + 37) <=
                    Mathf.Clamp01(safeVariation.RooftopDetailChance);

                float shapeRoll = Stable01(
                    seed,
                    variationCoordinate,
                    variationSalt + 41);

                if (!composite || shapeRoll < 0.58f)
                {
                    shapeIndex = 0;
                    composite = false;
                }
                else if (shapeRoll < 0.82f)
                {
                    shapeIndex = 1;
                }
                else
                {
                    shapeIndex = 4;
                }
            }

            return new DenseSmallBuildingPlacement
            {
                WorldPosition = position,
                Rotation = rotation,
                AxisX = rotatedAxisX,
                AxisZ = rotatedAxisZ,
                Width = Mathf.Max(0.08f, width),
                Depth = Mathf.Max(0.08f, depth),
                CollisionWidth = Mathf.Max(
                    0.08f,
                    width * Mathf.Max(1.0f, collisionEnvelopeMultiplier)),
                CollisionDepth = Mathf.Max(
                    0.08f,
                    depth * Mathf.Max(1.0f, collisionEnvelopeMultiplier)),
                Floors = Mathf.Max(1, floors),
                StyleIndex = Mathf.Clamp(styleIndex, 0, 2),
                MaterialIndex = materialIndex >= 0
                    ? materialIndex
                    : Mathf.Clamp(styleIndex, 0, 2),
                ShapeIndex = shapeIndex,
                TemplateIndex = shapeIndex,
                IsLandmark = isLandmark,
                LandmarkRole = landmarkRole,
                DetailSeed = detailSeed,
                UseCompositeMass = composite,
                UseSteppedSilhouette = stepped,
                HasTerrace = terrace,
                HasRooftopDetail = rooftop,
                SourceNodeId = sourceNodeId ?? string.Empty
            };
        }

        private static bool EnsureUniqueTemplate(
            BuildingSpatialIndex spatialIndex,
            DenseSmallBuildingPlacement placement,
            float separation)
        {
            if (spatialIndex == null || placement == null)
            {
                return false;
            }

            int[] allowedShapes = placement.IsLandmark
                ? new[] { 0, 1, 2, 3, 4, 5, 6 }
                : new[] { 0, 1, 4 };

            int startIndex = 0;

            for (int index = 0; index < allowedShapes.Length; index++)
            {
                if (allowedShapes[index] == placement.ShapeIndex)
                {
                    startIndex = index;
                    break;
                }
            }

            for (int attempt = 0; attempt < allowedShapes.Length; attempt++)
            {
                int candidateShape =
                    allowedShapes[(startIndex + attempt) % allowedShapes.Length];

                placement.ShapeIndex = candidateShape;
                placement.TemplateIndex = candidateShape;
                placement.UseCompositeMass = candidateShape != 0;

                if (!spatialIndex.HasNearbySameTemplate(
                    placement,
                    Mathf.Max(0f, separation)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBuildingPlacementValid(
            GameBoardRuntimeSnapshot snapshot,
            ProceduralRoadVisualLayout roadLayout,
            DenseSmallBuildingPlacement placement,
            BuildingSpatialIndex spatialIndex,
            float roadHalfWidth,
            float sidewalkWidth,
            float generatedGap,
            float intersectionClearance,
            float maximumRoadDistance,
            bool checkRoadDistance)
        {
            if (!IsInsideBoardBounds(snapshot, placement, snapshot.CellWorldSize))
            {
                return false;
            }

            if (intersectionClearance > 0f &&
                roadLayout.IsNearJunction(
                    placement.WorldPosition,
                    intersectionClearance))
            {
                return false;
            }

            if (checkRoadDistance)
            {
                Vector3 nearest;
                Vector3 tangent;
                float distance;

                if (!roadLayout.TryGetNearestRoad(
                    placement.WorldPosition,
                    out nearest,
                    out tangent,
                    out distance))
                {
                    return false;
                }

                float inwardHalfDepth =
                    Mathf.Max(
                        placement.Depth,
                        placement.CollisionDepth) * 0.42f;

                float required =
                    roadHalfWidth +
                    sidewalkWidth +
                    inwardHalfDepth;

                if (distance < required)
                {
                    return false;
                }

                if (maximumRoadDistance > 0f &&
                    distance > maximumRoadDistance)
                {
                    return false;
                }
            }

            return !spatialIndex.Overlaps(placement, generatedGap);
        }

        private static bool IsInsideBoardBounds(
            GameBoardRuntimeSnapshot snapshot,
            DenseSmallBuildingPlacement placement,
            float margin)
        {
            float radius = placement.BoundingRadius;
            float halfX = snapshot.BoardSizeXZ.x * 0.5f + margin;
            float halfZ = snapshot.BoardSizeXZ.y * 0.5f + margin;

            return placement.WorldPosition.x - radius >=
                       snapshot.BoardCenter.x - halfX &&
                   placement.WorldPosition.x + radius <=
                       snapshot.BoardCenter.x + halfX &&
                   placement.WorldPosition.z - radius >=
                       snapshot.BoardCenter.z - halfZ &&
                   placement.WorldPosition.z + radius <=
                       snapshot.BoardCenter.z + halfZ;
        }

        internal static bool OverlapsOBB(
            DenseSmallBuildingPlacement first,
            DenseSmallBuildingPlacement second,
            float gap)
        {
            Vector2 firstCenter = new Vector2(
                first.WorldPosition.x,
                first.WorldPosition.z);

            Vector2 secondCenter = new Vector2(
                second.WorldPosition.x,
                second.WorldPosition.z);

            Vector2 delta = secondCenter - firstCenter;
            Vector2 firstX = new Vector2(first.AxisX.x, first.AxisX.z).normalized;
            Vector2 firstZ = new Vector2(first.AxisZ.x, first.AxisZ.z).normalized;
            Vector2 secondX = new Vector2(second.AxisX.x, second.AxisX.z).normalized;
            Vector2 secondZ = new Vector2(second.AxisZ.x, second.AxisZ.z).normalized;

            float firstHalfWidth =
                Mathf.Max(first.Width, first.CollisionWidth) * 0.5f +
                gap * 0.5f;
            float firstHalfDepth =
                Mathf.Max(first.Depth, first.CollisionDepth) * 0.5f +
                gap * 0.5f;
            float secondHalfWidth =
                Mathf.Max(second.Width, second.CollisionWidth) * 0.5f +
                gap * 0.5f;
            float secondHalfDepth =
                Mathf.Max(second.Depth, second.CollisionDepth) * 0.5f +
                gap * 0.5f;

            Vector2[] axes =
            {
                firstX,
                firstZ,
                secondX,
                secondZ
            };

            for (int index = 0; index < axes.Length; index++)
            {
                Vector2 axis = axes[index];
                float centerDistance = Mathf.Abs(Vector2.Dot(delta, axis));

                float firstRadius =
                    firstHalfWidth * Mathf.Abs(Vector2.Dot(firstX, axis)) +
                    firstHalfDepth * Mathf.Abs(Vector2.Dot(firstZ, axis));

                float secondRadius =
                    secondHalfWidth * Mathf.Abs(Vector2.Dot(secondX, axis)) +
                    secondHalfDepth * Mathf.Abs(Vector2.Dot(secondZ, axis));

                if (centerDistance > firstRadius + secondRadius)
                {
                    return false;
                }
            }

            return true;
        }

        private static ProceduralLandmarkRole SelectLandmarkRole(
            int seed,
            int districtIndex,
            int localBuildingIndex,
            string nodeId)
        {
            Vector2Int districtCoordinate = new Vector2Int(
                Mathf.Max(0, districtIndex),
                0);

            int districtOffset =
                (StableHash(seed, districtCoordinate, 661) & int.MaxValue) % 6;

            int roleIndex = (districtOffset + Mathf.Max(0, localBuildingIndex)) % 6;
            return (ProceduralLandmarkRole)roleIndex;
        }

        private static Vector2 GetLandmarkFootprintFactor(
            ProceduralLandmarkRole role)
        {
            switch (role)
            {
                case ProceduralLandmarkRole.Apartment:
                    return new Vector2(0.82f, 0.92f);

                case ProceduralLandmarkRole.ShoppingMall:
                    return new Vector2(1.00f, 0.96f);

                case ProceduralLandmarkRole.Hospital:
                    return new Vector2(0.96f, 0.92f);

                case ProceduralLandmarkRole.Office:
                    return new Vector2(0.78f, 0.80f);

                case ProceduralLandmarkRole.Industrial:
                    return new Vector2(1.00f, 0.84f);

                case ProceduralLandmarkRole.Civic:
                    return new Vector2(0.90f, 0.90f);

                default:
                    return Vector2.one;
            }
        }

        private static int CalculateLandmarkFloors(
            ProceduralLandmarkRole role,
            int seed,
            Vector2Int coordinate,
            int minimumFloors,
            int maximumFloors)
        {
            int minFloors = Mathf.Max(1, minimumFloors);
            int maxFloors = Mathf.Max(minFloors, maximumFloors);
            float minimumRatio;
            float maximumRatio;

            switch (role)
            {
                case ProceduralLandmarkRole.Apartment:
                    minimumRatio = 0.58f;
                    maximumRatio = 1.00f;
                    break;

                case ProceduralLandmarkRole.ShoppingMall:
                    minimumRatio = 0.05f;
                    maximumRatio = 0.34f;
                    break;

                case ProceduralLandmarkRole.Hospital:
                    minimumRatio = 0.30f;
                    maximumRatio = 0.58f;
                    break;

                case ProceduralLandmarkRole.Office:
                    minimumRatio = 0.58f;
                    maximumRatio = 1.00f;
                    break;

                case ProceduralLandmarkRole.Industrial:
                    minimumRatio = 0.08f;
                    maximumRatio = 0.42f;
                    break;

                case ProceduralLandmarkRole.Civic:
                    minimumRatio = 0.24f;
                    maximumRatio = 0.62f;
                    break;

                default:
                    minimumRatio = 0f;
                    maximumRatio = 1f;
                    break;
            }

            float value = Mathf.Lerp(
                minimumRatio,
                maximumRatio,
                Stable01(seed, coordinate, 677));

            return Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(minFloors, maxFloors, value)),
                minFloors,
                maxFloors);
        }

        private static int GetLandmarkShape(
            ProceduralLandmarkRole role,
            int seed,
            Vector2Int coordinate)
        {
            float variant = Stable01(seed, coordinate, 683);

            switch (role)
            {
                case ProceduralLandmarkRole.Apartment:
                    return variant < 0.55f ? 4 : 2;

                case ProceduralLandmarkRole.ShoppingMall:
                    return variant < 0.68f ? 3 : 5;

                case ProceduralLandmarkRole.Hospital:
                    return variant < 0.72f ? 1 : 6;

                case ProceduralLandmarkRole.Office:
                    return variant < 0.78f ? 5 : 2;

                case ProceduralLandmarkRole.Industrial:
                    return variant < 0.64f ? 6 : 0;

                case ProceduralLandmarkRole.Civic:
                    return variant < 0.60f ? 3 : 1;

                default:
                    return 0;
            }
        }

        private static void ApplyLandmarkDetailRules(
            ProceduralLandmarkRole role,
            ref bool stepped,
            ref bool terrace,
            ref bool rooftop)
        {
            switch (role)
            {
                case ProceduralLandmarkRole.Apartment:
                    stepped = true;
                    rooftop = true;
                    break;

                case ProceduralLandmarkRole.ShoppingMall:
                    terrace = true;
                    stepped = true;
                    break;

                case ProceduralLandmarkRole.Hospital:
                    rooftop = true;
                    terrace = true;
                    break;

                case ProceduralLandmarkRole.Office:
                    stepped = true;
                    rooftop = true;
                    break;

                case ProceduralLandmarkRole.Industrial:
                    rooftop = true;
                    break;

                case ProceduralLandmarkRole.Civic:
                    terrace = true;
                    break;
            }
        }

        private static int RandomFloor(
            int seed,
            Vector2Int coordinate,
            int salt,
            int minimumFloors,
            int maximumFloors,
            ProceduralBuildingVariationSettings variation)
        {
            int minFloors = Mathf.Max(1, minimumFloors);
            int maxFloors = Mathf.Max(minFloors, maximumFloors);

            if (minFloors == maxFloors)
            {
                return minFloors;
            }

            ProceduralBuildingVariationSettings safeVariation =
                variation ?? new ProceduralBuildingVariationSettings();

            float individual = Stable01(seed, coordinate, salt);
            float districtNoise = Mathf.PerlinNoise(
                coordinate.x * 0.071f + seed * 0.00037f,
                coordinate.y * 0.071f - seed * 0.00029f);

            float randomness = Mathf.Clamp01(
                safeVariation.HeightRandomness);

            float value = Mathf.Lerp(
                districtNoise,
                individual,
                randomness);

            value = Mathf.Pow(Mathf.Clamp01(value), 1.18f);

            if (Stable01(seed, coordinate, salt + 7) <=
                Mathf.Clamp01(safeVariation.TallBuildingChance))
            {
                value = Mathf.Lerp(
                    0.72f,
                    1f,
                    Stable01(seed, coordinate, salt + 11));
            }

            return Mathf.Clamp(
                minFloors + Mathf.FloorToInt(
                    value * (maxFloors - minFloors + 1)),
                minFloors,
                maxFloors);
        }

        private static int StableStyle(
            int seed,
            Vector2Int coordinate,
            int salt)
        {
            return Mathf.Clamp(
                Mathf.FloorToInt(Stable01(seed, coordinate, salt) * 3f),
                0,
                2);
        }

        private static float Stable01(
            int seed,
            Vector2Int coordinate,
            int salt)
        {
            uint value = (uint)StableHash(seed, coordinate, salt);
            return (value & 0x00FFFFFFu) / 16777215f;
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
                hash ^= hash >> 16;
                return hash;
            }
        }
    }
}

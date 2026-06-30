using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoardOfDead
{
    public static class ProceduralCityMeshFactory
    {
        public static Mesh CreateRoadMesh(
            ProceduralRoadVisualLayout layout,
            Transform root,
            float width,
            float height,
            float surfaceOffset,
            float junctionRadiusMultiplier)
        {
            return CreateRibbonNetworkMesh(
                "GameBoard_ContinuousCurvedRoadMesh",
                layout,
                root,
                width,
                height,
                surfaceOffset,
                junctionRadiusMultiplier);
        }

        public static Mesh CreateSidewalkMesh(
            ProceduralRoadVisualLayout layout,
            Transform root,
            float roadWidth,
            float sidewalkWidth,
            float height,
            float surfaceOffset,
            float junctionRadiusMultiplier)
        {
            float totalWidth =
                Mathf.Max(0.05f, roadWidth) +
                Mathf.Max(0f, sidewalkWidth) * 2f;

            return CreateRibbonNetworkMesh(
                "GameBoard_ContinuousCurvedSidewalkMesh",
                layout,
                root,
                totalWidth,
                height,
                surfaceOffset,
                junctionRadiusMultiplier);
        }

        public static Mesh CreateDenseSmallBuildingMesh(
            IList<DenseSmallBuildingPlacement> placements,
            Transform root,
            float floorHeight,
            int materialIndex)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            if (placements != null)
            {
                Quaternion inverseRootRotation =
                    Quaternion.Inverse(root.rotation);

                for (int index = 0; index < placements.Count; index++)
                {
                    DenseSmallBuildingPlacement placement =
                        placements[index];

                    if (placement == null ||
                        placement.MaterialIndex != materialIndex)
                    {
                        continue;
                    }

                    Vector3 localPosition = root.InverseTransformPoint(
                        placement.WorldPosition);

                    Quaternion localRotation =
                        inverseRootRotation * placement.Rotation;

                    AppendCompositeBuilding(
                        vertices,
                        normals,
                        uvs,
                        triangles,
                        localPosition,
                        localRotation,
                        placement,
                        floorHeight);
                }
            }

            return BuildMesh(
                "GameBoard_Buildings_Material" + materialIndex,
                vertices,
                normals,
                uvs,
                triangles);
        }

        public static Mesh CreateBoardTileMesh(
            IList<ProceduralBoardTilePlacement> placements,
            Transform root,
            float tileSize,
            float surfaceOffset,
            int kindFilter)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            if (placements == null || root == null || tileSize <= 0f)
            {
                return BuildMesh(
                    "GameBoard_LogicalMovementTiles_Empty",
                    vertices,
                    normals,
                    uvs,
                    triangles);
            }

            float half = tileSize * 0.5f;

            for (int index = 0; index < placements.Count; index++)
            {
                ProceduralBoardTilePlacement placement = placements[index];

                if (placement == null ||
                    (kindFilter >= 0 && (int)placement.Kind != kindFilter))
                {
                    continue;
                }

                Vector3 center = root.InverseTransformPoint(
                    placement.WorldPosition + Vector3.up * surfaceOffset);

                Vector3 first = center + new Vector3(-half, 0f, -half);
                Vector3 second = center + new Vector3(-half, 0f, half);
                Vector3 third = center + new Vector3(half, 0f, half);
                Vector3 fourth = center + new Vector3(half, 0f, -half);

                AppendQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    first,
                    second,
                    third,
                    fourth,
                    Vector3.up);
            }

            return BuildMesh(
                "GameBoard_LogicalMovementTiles_" + kindFilter,
                vertices,
                normals,
                uvs,
                triangles);
        }

        public static Mesh CreateIslandGroundMesh(
            ProceduralIslandLandscapeLayout layout,
            Transform root)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            if (layout == null ||
                root == null ||
                layout.CoastPoints.Count < 3)
            {
                return BuildMesh(
                    "GameBoard_IslandGround",
                    vertices,
                    normals,
                    uvs,
                    triangles);
            }

            Vector3 localCenter = root.InverseTransformPoint(layout.Center);
            int topCenterIndex = vertices.Count;
            vertices.Add(localCenter);
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int coastStart = vertices.Count;

            for (int index = 0; index < layout.CoastPoints.Count; index++)
            {
                Vector3 local = root.InverseTransformPoint(
                    layout.CoastPoints[index]);

                vertices.Add(local);
                normals.Add(Vector3.up);
                uvs.Add(
                    new Vector2(
                        (local.x - localCenter.x) /
                            Mathf.Max(0.001f, layout.RadiusX * 2f) + 0.5f,
                        (local.z - localCenter.z) /
                            Mathf.Max(0.001f, layout.RadiusZ * 2f) + 0.5f));
            }

            for (int index = 0; index < layout.CoastPoints.Count; index++)
            {
                int next = (index + 1) % layout.CoastPoints.Count;
                triangles.Add(topCenterIndex);
                triangles.Add(coastStart + next);
                triangles.Add(coastStart + index);
            }

            float sideDepth = Mathf.Max(
                0.06f,
                Mathf.Min(layout.RadiusX, layout.RadiusZ) * 0.015f);

            for (int index = 0; index < layout.CoastPoints.Count; index++)
            {
                int next = (index + 1) % layout.CoastPoints.Count;
                Vector3 topFirst = root.InverseTransformPoint(
                    layout.CoastPoints[index]);
                Vector3 topSecond = root.InverseTransformPoint(
                    layout.CoastPoints[next]);
                Vector3 bottomFirst = topFirst - Vector3.up * sideDepth;
                Vector3 bottomSecond = topSecond - Vector3.up * sideDepth;
                Vector3 edge = topSecond - topFirst;
                Vector3 normal = Vector3.Cross(Vector3.up, edge).normalized;

                AppendQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    topFirst,
                    topSecond,
                    bottomSecond,
                    bottomFirst,
                    normal);
            }

            return BuildMesh(
                "GameBoard_IslandGround",
                vertices,
                normals,
                uvs,
                triangles);
        }

        public static Mesh CreateWaterMesh(
            ProceduralIslandLandscapeLayout layout,
            Transform root,
            float padding,
            float worldY)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            if (layout == null || root == null)
            {
                return BuildMesh(
                    "GameBoard_IslandWater",
                    vertices,
                    normals,
                    uvs,
                    triangles);
            }

            Vector3 worldCenter = layout.Center;
            worldCenter.y = worldY;
            Vector3 localCenter = root.InverseTransformPoint(worldCenter);
            float width = (layout.RadiusX + Mathf.Max(0f, padding)) * 2f;
            float depth = (layout.RadiusZ + Mathf.Max(0f, padding)) * 2f;

            AppendOrientedBox(
                vertices,
                normals,
                uvs,
                triangles,
                localCenter,
                new Vector3(width, 0.025f, depth),
                Quaternion.identity);

            return BuildMesh(
                "GameBoard_IslandWater",
                vertices,
                normals,
                uvs,
                triangles);
        }

        public static Mesh CreateForestTrunkMesh(
            IList<ProceduralTreePlacement> trees,
            Transform root)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            if (trees != null && root != null)
            {
                for (int index = 0; index < trees.Count; index++)
                {
                    ProceduralTreePlacement tree = trees[index];

                    if (tree == null)
                    {
                        continue;
                    }

                    float trunkHeight = tree.Height * 0.34f;
                    Vector3 worldCenter =
                        tree.WorldPosition +
                        Vector3.up * (trunkHeight * 0.5f);

                    AppendCylinder(
                        vertices,
                        normals,
                        uvs,
                        triangles,
                        root.InverseTransformPoint(worldCenter),
                        Mathf.Max(0.012f, tree.Radius * 0.18f),
                        trunkHeight,
                        7);
                }
            }

            return BuildMesh(
                "GameBoard_ForestTrunks",
                vertices,
                normals,
                uvs,
                triangles);
        }

        public static Mesh CreateForestCanopyMesh(
            IList<ProceduralTreePlacement> trees,
            Transform root)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            if (trees != null && root != null)
            {
                for (int index = 0; index < trees.Count; index++)
                {
                    ProceduralTreePlacement tree = trees[index];

                    if (tree == null)
                    {
                        continue;
                    }

                    float trunkHeight = tree.Height * 0.30f;
                    float lowerHeight = tree.Height * 0.48f;
                    float upperHeight = tree.Height * 0.38f;
                    Vector3 lowerWorld =
                        tree.WorldPosition +
                        Vector3.up * (trunkHeight + lowerHeight * 0.42f);
                    Vector3 upperWorld =
                        tree.WorldPosition +
                        Vector3.up * (trunkHeight + lowerHeight * 0.70f);

                    float lowerRadius = tree.Radius *
                        (tree.Variant == 0 ? 1.10f : 0.96f);

                    AppendFrustum(
                        vertices,
                        normals,
                        uvs,
                        triangles,
                        root.InverseTransformPoint(lowerWorld),
                        lowerRadius,
                        lowerRadius * 0.16f,
                        lowerHeight,
                        8);

                    if (tree.Variant != 2)
                    {
                        AppendFrustum(
                            vertices,
                            normals,
                            uvs,
                            triangles,
                            root.InverseTransformPoint(upperWorld),
                            tree.Radius * 0.76f,
                            tree.Radius * 0.06f,
                            upperHeight,
                            8);
                    }
                }
            }

            return BuildMesh(
                "GameBoard_ForestCanopies",
                vertices,
                normals,
                uvs,
                triangles);
        }

        private static Mesh CreateRibbonNetworkMesh(
            string meshName,
            ProceduralRoadVisualLayout layout,
            Transform root,
            float width,
            float height,
            float surfaceOffset,
            float junctionRadiusMultiplier)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            if (layout == null || root == null)
            {
                return BuildMesh(
                    meshName,
                    vertices,
                    normals,
                    uvs,
                    triangles);
            }

            float safeWidth = Mathf.Max(0.03f, width);
            float safeHeight = Mathf.Max(0.005f, height);
            Quaternion inverseRootRotation =
                Quaternion.Inverse(root.rotation);

            for (int splineIndex = 0;
                 splineIndex < layout.Splines.Count;
                 splineIndex++)
            {
                ProceduralRoadSpline spline = layout.Splines[splineIndex];

                if (spline == null || spline.Points.Count < 2)
                {
                    continue;
                }

                for (int pointIndex = 1;
                     pointIndex < spline.Points.Count;
                     pointIndex++)
                {
                    Vector3 worldFirst = spline.Points[pointIndex - 1];
                    Vector3 worldSecond = spline.Points[pointIndex];
                    worldFirst.y += surfaceOffset - safeHeight * 0.5f;
                    worldSecond.y += surfaceOffset - safeHeight * 0.5f;

                    Vector3 localFirst = root.InverseTransformPoint(worldFirst);
                    Vector3 localSecond = root.InverseTransformPoint(worldSecond);
                    Vector3 delta = localSecond - localFirst;
                    delta.y = 0f;
                    float length = delta.magnitude;

                    if (length <= 0.0001f)
                    {
                        continue;
                    }

                    Vector3 localCenter =
                        (localFirst + localSecond) * 0.5f;

                    Quaternion localRotation =
                        inverseRootRotation *
                        Quaternion.LookRotation(
                            (worldSecond - worldFirst).normalized,
                            Vector3.up);

                    AppendOrientedBox(
                        vertices,
                        normals,
                        uvs,
                        triangles,
                        localCenter,
                        new Vector3(
                            safeWidth,
                            safeHeight,
                            length + safeWidth * 0.38f),
                        localRotation);
                }
            }

            float junctionRadius =
                safeWidth * 0.56f *
                Mathf.Max(0.9f, junctionRadiusMultiplier);

            for (int index = 0;
                 index < layout.JunctionCenters.Count;
                 index++)
            {
                Vector3 worldCenter = layout.JunctionCenters[index];
                worldCenter.y += surfaceOffset - safeHeight * 0.5f;
                Vector3 localCenter = root.InverseTransformPoint(worldCenter);

                AppendCylinder(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    localCenter,
                    junctionRadius,
                    safeHeight,
                    12);
            }

            return BuildMesh(
                meshName,
                vertices,
                normals,
                uvs,
                triangles);
        }

        private static void AppendCompositeBuilding(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            DenseSmallBuildingPlacement placement,
            float floorHeight)
        {
            float safeWidth = Mathf.Max(0.08f, placement.Width);
            float safeDepth = Mathf.Max(0.08f, placement.Depth);
            int safeFloors = Mathf.Max(1, placement.Floors);
            float safeFloorHeight = Mathf.Max(0.08f, floorHeight);
            float bodyHeight = safeFloors * safeFloorHeight;
            float plinthHeight = Mathf.Min(safeFloorHeight * 0.18f, 0.08f);
            int styleIndex = Mathf.Clamp(placement.StyleIndex, 0, 2);
            int shapeIndex = Mathf.Clamp(placement.ShapeIndex, 0, 6);

            float edgeScale = styleIndex == 0
                ? 1f
                : styleIndex == 1
                    ? 0.97f
                    : 0.93f;

            AppendLocalBox(
                vertices,
                normals,
                uvs,
                triangles,
                basePosition,
                rotation,
                new Vector3(0f, plinthHeight * 0.5f, 0f),
                new Vector3(
                    safeWidth * 1.035f,
                    plinthHeight,
                    safeDepth * 1.035f));

            if (!placement.UseCompositeMass)
            {
                AppendSimpleMass(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    basePosition,
                    rotation,
                    safeWidth,
                    safeDepth,
                    bodyHeight,
                    plinthHeight,
                    edgeScale,
                    placement);

                return;
            }

            switch (shapeIndex)
            {
                case 1:
                    AppendLShapeMass(
                        vertices, normals, uvs, triangles,
                        basePosition, rotation,
                        safeWidth, safeDepth, bodyHeight,
                        plinthHeight, edgeScale, placement);
                    break;

                case 2:
                    AppendSteppedMass(
                        vertices, normals, uvs, triangles,
                        basePosition, rotation,
                        safeWidth, safeDepth, bodyHeight,
                        plinthHeight, edgeScale, placement);
                    break;

                case 3:
                    AppendTerracedMass(
                        vertices, normals, uvs, triangles,
                        basePosition, rotation,
                        safeWidth, safeDepth, bodyHeight,
                        plinthHeight, edgeScale, placement);
                    break;

                case 4:
                    AppendTwinMass(
                        vertices, normals, uvs, triangles,
                        basePosition, rotation,
                        safeWidth, safeDepth, bodyHeight,
                        plinthHeight, edgeScale, placement);
                    break;

                case 5:
                    AppendPodiumTowerMass(
                        vertices, normals, uvs, triangles,
                        basePosition, rotation,
                        safeWidth, safeDepth, bodyHeight,
                        plinthHeight, edgeScale, placement);
                    break;

                case 6:
                    AppendStaggeredMass(
                        vertices, normals, uvs, triangles,
                        basePosition, rotation,
                        safeWidth, safeDepth, bodyHeight,
                        plinthHeight, edgeScale, placement);
                    break;

                default:
                    AppendSimpleMass(
                        vertices, normals, uvs, triangles,
                        basePosition, rotation,
                        safeWidth, safeDepth, bodyHeight,
                        plinthHeight, edgeScale, placement);
                    break;
            }
        }

        private static void AppendSimpleMass(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            float width,
            float depth,
            float height,
            float baseHeight,
            float scale,
            DenseSmallBuildingPlacement placement)
        {
            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(0f, baseHeight + height * 0.5f, 0f),
                new Vector3(width * scale, height, depth * scale));

            AppendRoofDetail(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                width * scale, depth * scale,
                baseHeight + height,
                placement);
        }

        private static void AppendLShapeMass(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            float width,
            float depth,
            float height,
            float baseHeight,
            float scale,
            DenseSmallBuildingPlacement placement)
        {
            float firstWidth = width * 0.66f * scale;
            float secondWidth = width * 0.50f * scale;
            float secondDepth = depth * Mathf.Lerp(
                0.48f, 0.70f, Hash01(placement.DetailSeed, 3));
            float secondHeight = height * Mathf.Lerp(
                0.55f, 0.88f, Hash01(placement.DetailSeed, 5));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(-width * 0.16f, baseHeight + height * 0.5f, 0f),
                new Vector3(firstWidth, height, depth * scale));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(width * 0.23f, baseHeight + secondHeight * 0.5f, -depth * 0.17f),
                new Vector3(secondWidth, secondHeight, secondDepth));

            if (placement.HasTerrace)
            {
                AppendTerraceDeck(
                    vertices, normals, uvs, triangles,
                    basePosition, rotation,
                    width * 0.42f, depth * 0.42f,
                    new Vector3(width * 0.20f, baseHeight + secondHeight, depth * 0.20f));
            }

            AppendRoofDetail(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                firstWidth, depth * scale,
                baseHeight + height,
                placement);
        }

        private static void AppendSteppedMass(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            float width,
            float depth,
            float height,
            float baseHeight,
            float scale,
            DenseSmallBuildingPlacement placement)
        {
            float lowerHeight = height * Mathf.Lerp(
                0.34f, 0.52f, Hash01(placement.DetailSeed, 7));
            float upperHeight = Mathf.Max(0.08f, height - lowerHeight);
            float offsetX = (Hash01(placement.DetailSeed, 11) * 2f - 1f) * width * 0.10f;
            float offsetZ = (Hash01(placement.DetailSeed, 13) * 2f - 1f) * depth * 0.10f;

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(0f, baseHeight + lowerHeight * 0.5f, 0f),
                new Vector3(width * scale, lowerHeight, depth * scale));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(offsetX, baseHeight + lowerHeight + upperHeight * 0.5f, offsetZ),
                new Vector3(width * 0.68f * scale, upperHeight, depth * 0.66f * scale));

            if (placement.HasTerrace)
            {
                AppendTerraceDeck(
                    vertices, normals, uvs, triangles,
                    basePosition, rotation,
                    width * 0.92f, depth * 0.90f,
                    new Vector3(0f, baseHeight + lowerHeight, 0f));
            }

            AppendRoofDetail(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                width * 0.68f, depth * 0.66f,
                baseHeight + height,
                placement);
        }

        private static void AppendTerracedMass(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            float width,
            float depth,
            float height,
            float baseHeight,
            float scale,
            DenseSmallBuildingPlacement placement)
        {
            float lowerHeight = height * 0.48f;
            float upperHeight = Mathf.Max(0.08f, height - lowerHeight);
            float upperDepth = depth * 0.56f;
            float upperOffsetZ = depth * 0.17f;

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(0f, baseHeight + lowerHeight * 0.5f, 0f),
                new Vector3(width * scale, lowerHeight, depth * scale));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(0f, baseHeight + lowerHeight + upperHeight * 0.5f, upperOffsetZ),
                new Vector3(width * 0.72f * scale, upperHeight, upperDepth));

            AppendTerraceDeck(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                width * 0.88f, depth * 0.33f,
                new Vector3(0f, baseHeight + lowerHeight, -depth * 0.27f));

            AppendRoofDetail(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                width * 0.72f, upperDepth,
                baseHeight + height,
                placement);
        }

        private static void AppendTwinMass(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            float width,
            float depth,
            float height,
            float baseHeight,
            float scale,
            DenseSmallBuildingPlacement placement)
        {
            float gap = width * 0.035f;
            float towerWidth = width * 0.47f * scale;
            float firstHeight = height;
            float secondHeight = height * Mathf.Lerp(
                0.56f, 0.90f, Hash01(placement.DetailSeed, 17));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(-width * 0.25f, baseHeight + firstHeight * 0.5f, 0f),
                new Vector3(towerWidth, firstHeight, depth * 0.92f * scale));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(width * 0.25f + gap, baseHeight + secondHeight * 0.5f, depth * 0.05f),
                new Vector3(towerWidth, secondHeight, depth * 0.76f * scale));

            if (placement.HasTerrace)
            {
                AppendTerraceDeck(
                    vertices, normals, uvs, triangles,
                    basePosition, rotation,
                    width * 0.34f, depth * 0.50f,
                    new Vector3(width * 0.25f, baseHeight + secondHeight, -depth * 0.14f));
            }

            AppendRoofDetail(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                towerWidth, depth * 0.92f,
                baseHeight + firstHeight,
                placement);
        }

        private static void AppendPodiumTowerMass(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            float width,
            float depth,
            float height,
            float baseHeight,
            float scale,
            DenseSmallBuildingPlacement placement)
        {
            float podiumHeight = Mathf.Min(
                height * 0.38f,
                Mathf.Max(0.12f, height * 0.52f));
            float towerHeight = Mathf.Max(0.08f, height - podiumHeight);
            float towerWidth = width * Mathf.Lerp(
                0.46f, 0.64f, Hash01(placement.DetailSeed, 19));
            float towerDepth = depth * Mathf.Lerp(
                0.44f, 0.62f, Hash01(placement.DetailSeed, 23));
            float offsetX = (Hash01(placement.DetailSeed, 29) * 2f - 1f) * width * 0.12f;
            float offsetZ = (Hash01(placement.DetailSeed, 31) * 2f - 1f) * depth * 0.10f;

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(0f, baseHeight + podiumHeight * 0.5f, 0f),
                new Vector3(width * scale, podiumHeight, depth * scale));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(offsetX, baseHeight + podiumHeight + towerHeight * 0.5f, offsetZ),
                new Vector3(towerWidth, towerHeight, towerDepth));

            if (placement.HasTerrace)
            {
                AppendTerraceDeck(
                    vertices, normals, uvs, triangles,
                    basePosition, rotation,
                    width * 0.90f, depth * 0.88f,
                    new Vector3(0f, baseHeight + podiumHeight, 0f));
            }

            AppendRoofDetail(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                towerWidth, towerDepth,
                baseHeight + height,
                placement);
        }

        private static void AppendStaggeredMass(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            float width,
            float depth,
            float height,
            float baseHeight,
            float scale,
            DenseSmallBuildingPlacement placement)
        {
            float leftHeight = height * Mathf.Lerp(
                0.56f, 0.86f, Hash01(placement.DetailSeed, 37));
            float rightHeight = height * Mathf.Lerp(
                0.44f, 0.78f, Hash01(placement.DetailSeed, 41));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(0f, baseHeight + height * 0.5f, depth * 0.10f),
                new Vector3(width * 0.54f * scale, height, depth * 0.76f * scale));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(-width * 0.27f, baseHeight + leftHeight * 0.5f, -depth * 0.16f),
                new Vector3(width * 0.48f, leftHeight, depth * 0.54f));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(width * 0.27f, baseHeight + rightHeight * 0.5f, -depth * 0.12f),
                new Vector3(width * 0.44f, rightHeight, depth * 0.50f));

            if (placement.HasTerrace)
            {
                AppendTerraceDeck(
                    vertices, normals, uvs, triangles,
                    basePosition, rotation,
                    width * 0.38f, depth * 0.32f,
                    new Vector3(width * 0.25f, baseHeight + rightHeight, depth * 0.08f));
            }

            AppendRoofDetail(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                width * 0.54f, depth * 0.76f,
                baseHeight + height,
                placement);
        }

        private static void AppendTerraceDeck(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            float width,
            float depth,
            Vector3 localCenter)
        {
            float slabHeight = Mathf.Max(0.012f, Mathf.Min(width, depth) * 0.045f);
            float railHeight = slabHeight * 2.2f;
            float railThickness = Mathf.Max(0.012f, Mathf.Min(width, depth) * 0.035f);

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                localCenter + Vector3.up * (slabHeight * 0.5f),
                new Vector3(width, slabHeight, depth));

            float railY = localCenter.y + slabHeight + railHeight * 0.5f;

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(localCenter.x, railY, localCenter.z - depth * 0.5f),
                new Vector3(width, railHeight, railThickness));

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(localCenter.x - width * 0.5f, railY, localCenter.z),
                new Vector3(railThickness, railHeight, depth));
        }

        private static void AppendRoofDetail(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            float roofWidth,
            float roofDepth,
            float roofY,
            DenseSmallBuildingPlacement placement)
        {
            float roofHeight = Mathf.Max(0.025f, Mathf.Min(roofWidth, roofDepth) * 0.075f);

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(0f, roofY + roofHeight * 0.5f, 0f),
                new Vector3(
                    roofWidth * 1.015f,
                    roofHeight,
                    roofDepth * 1.015f));

            if (!placement.HasRooftopDetail)
            {
                return;
            }

            float detailWidth = roofWidth * Mathf.Lerp(
                0.16f, 0.30f, Hash01(placement.DetailSeed, 47));
            float detailDepth = roofDepth * Mathf.Lerp(
                0.14f, 0.28f, Hash01(placement.DetailSeed, 53));
            float detailHeight = roofHeight * Mathf.Lerp(
                1.8f, 4.2f, Hash01(placement.DetailSeed, 59));
            float offsetX = (Hash01(placement.DetailSeed, 61) * 2f - 1f) * roofWidth * 0.18f;
            float offsetZ = (Hash01(placement.DetailSeed, 67) * 2f - 1f) * roofDepth * 0.18f;

            AppendLocalBox(
                vertices, normals, uvs, triangles,
                basePosition, rotation,
                new Vector3(
                    offsetX,
                    roofY + roofHeight + detailHeight * 0.5f,
                    offsetZ),
                new Vector3(detailWidth, detailHeight, detailDepth));
        }

        private static void AppendLocalBox(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 basePosition,
            Quaternion rotation,
            Vector3 localCenter,
            Vector3 size)
        {
            AppendOrientedBox(
                vertices,
                normals,
                uvs,
                triangles,
                basePosition + rotation * localCenter,
                size,
                rotation);
        }

        private static float Hash01(int seed, int salt)
        {
            unchecked
            {
                uint value = (uint)(seed * 397 ^ salt * 486187739);
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                return (value & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static void AppendFrustum(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 center,
            float bottomRadius,
            float topRadius,
            float height,
            int sides)
        {
            int safeSides = Mathf.Max(6, sides);
            float halfHeight = height * 0.5f;
            int bottomCenter = vertices.Count;

            vertices.Add(center - Vector3.up * halfHeight);
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int topCenter = vertices.Count;
            vertices.Add(center + Vector3.up * halfHeight);
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int ringStart = vertices.Count;

            for (int index = 0; index < safeSides; index++)
            {
                float angle = Mathf.PI * 2f * index / safeSides;
                Vector3 radial = new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle));

                vertices.Add(
                    center + radial * bottomRadius -
                    Vector3.up * halfHeight);
                normals.Add(Vector3.down);
                uvs.Add(new Vector2(
                    radial.x * 0.5f + 0.5f,
                    radial.z * 0.5f + 0.5f));

                vertices.Add(
                    center + radial * topRadius +
                    Vector3.up * halfHeight);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(
                    radial.x * 0.5f + 0.5f,
                    radial.z * 0.5f + 0.5f));
            }

            for (int index = 0; index < safeSides; index++)
            {
                int next = (index + 1) % safeSides;
                int bottomCurrent = ringStart + index * 2;
                int topCurrent = bottomCurrent + 1;
                int bottomNext = ringStart + next * 2;
                int topNext = bottomNext + 1;

                triangles.Add(bottomCenter);
                triangles.Add(bottomNext);
                triangles.Add(bottomCurrent);

                triangles.Add(topCenter);
                triangles.Add(topCurrent);
                triangles.Add(topNext);

                int sideBase = vertices.Count;
                Vector3 firstBottom = vertices[bottomCurrent];
                Vector3 firstTop = vertices[topCurrent];
                Vector3 secondBottom = vertices[bottomNext];
                Vector3 secondTop = vertices[topNext];
                Vector3 edgeA = firstTop - firstBottom;
                Vector3 edgeB = secondBottom - firstBottom;
                Vector3 sideNormal = Vector3.Cross(edgeA, edgeB).normalized;

                vertices.Add(firstBottom);
                vertices.Add(firstTop);
                vertices.Add(secondBottom);
                vertices.Add(secondTop);

                normals.Add(sideNormal);
                normals.Add(sideNormal);
                normals.Add(sideNormal);
                normals.Add(sideNormal);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));

                triangles.Add(sideBase);
                triangles.Add(sideBase + 1);
                triangles.Add(sideBase + 2);
                triangles.Add(sideBase + 2);
                triangles.Add(sideBase + 1);
                triangles.Add(sideBase + 3);
            }
        }

        private static void AppendCylinder(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 center,
            float radius,
            float height,
            int sides)
        {
            int safeSides = Mathf.Max(6, sides);
            int baseIndex = vertices.Count;
            float halfHeight = height * 0.5f;

            vertices.Add(center + Vector3.up * halfHeight);
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            vertices.Add(center - Vector3.up * halfHeight);
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int index = 0; index < safeSides; index++)
            {
                float angle = Mathf.PI * 2f * index / safeSides;
                Vector3 radial = new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle));

                Vector3 top = center + radial * radius + Vector3.up * halfHeight;
                Vector3 bottom = center + radial * radius - Vector3.up * halfHeight;

                vertices.Add(top);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(
                    radial.x * 0.5f + 0.5f,
                    radial.z * 0.5f + 0.5f));

                vertices.Add(bottom);
                normals.Add(Vector3.down);
                uvs.Add(new Vector2(
                    radial.x * 0.5f + 0.5f,
                    radial.z * 0.5f + 0.5f));
            }

            for (int index = 0; index < safeSides; index++)
            {
                int next = (index + 1) % safeSides;
                int topCurrent = baseIndex + 2 + index * 2;
                int bottomCurrent = topCurrent + 1;
                int topNext = baseIndex + 2 + next * 2;
                int bottomNext = topNext + 1;

                triangles.Add(baseIndex);
                triangles.Add(topCurrent);
                triangles.Add(topNext);

                triangles.Add(baseIndex + 1);
                triangles.Add(bottomNext);
                triangles.Add(bottomCurrent);

                int sideBase = vertices.Count;
                Vector3 radialCurrent =
                    (vertices[topCurrent] - center).normalized;
                Vector3 radialNext =
                    (vertices[topNext] - center).normalized;

                vertices.Add(vertices[topCurrent]);
                vertices.Add(vertices[bottomCurrent]);
                vertices.Add(vertices[topNext]);
                vertices.Add(vertices[bottomNext]);

                normals.Add(radialCurrent);
                normals.Add(radialCurrent);
                normals.Add(radialNext);
                normals.Add(radialNext);

                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));

                triangles.Add(sideBase);
                triangles.Add(sideBase + 1);
                triangles.Add(sideBase + 2);
                triangles.Add(sideBase + 2);
                triangles.Add(sideBase + 1);
                triangles.Add(sideBase + 3);
            }
        }

        private static void AppendOrientedBox(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 center,
            Vector3 size,
            Quaternion rotation)
        {
            Vector3 half = size * 0.5f;

            Vector3[] corners =
            {
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y, -half.z),
                new Vector3( half.x, -half.y,  half.z),
                new Vector3(-half.x, -half.y,  half.z),
                new Vector3(-half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y, -half.z),
                new Vector3( half.x,  half.y,  half.z),
                new Vector3(-half.x,  half.y,  half.z)
            };

            for (int index = 0; index < corners.Length; index++)
            {
                corners[index] = center + rotation * corners[index];
            }

            AppendQuad(
                vertices,
                normals,
                uvs,
                triangles,
                corners[4],
                corners[7],
                corners[6],
                corners[5],
                rotation * Vector3.up);

            AppendQuad(
                vertices,
                normals,
                uvs,
                triangles,
                corners[0],
                corners[1],
                corners[2],
                corners[3],
                rotation * Vector3.down);

            AppendQuad(
                vertices,
                normals,
                uvs,
                triangles,
                corners[3],
                corners[2],
                corners[6],
                corners[7],
                rotation * Vector3.forward);

            AppendQuad(
                vertices,
                normals,
                uvs,
                triangles,
                corners[1],
                corners[0],
                corners[4],
                corners[5],
                rotation * Vector3.back);

            AppendQuad(
                vertices,
                normals,
                uvs,
                triangles,
                corners[2],
                corners[1],
                corners[5],
                corners[6],
                rotation * Vector3.right);

            AppendQuad(
                vertices,
                normals,
                uvs,
                triangles,
                corners[0],
                corners[3],
                corners[7],
                corners[4],
                rotation * Vector3.left);
        }

        private static void AppendQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 first,
            Vector3 second,
            Vector3 third,
            Vector3 fourth,
            Vector3 normal)
        {
            int baseIndex = vertices.Count;
            vertices.Add(first);
            vertices.Add(second);
            vertices.Add(third);
            vertices.Add(fourth);

            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }

        private static Mesh BuildMesh(
            string name,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;

            if (vertices.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public enum ProceduralBoardTileKind
    {
        Road = 0,
        NeutralBuilding = 1,
        Apartment = 2,
        ShoppingMall = 3,
        Hospital = 4,
        Office = 5,
        Industrial = 6,
        Civic = 7
    }

    public sealed class ProceduralBoardTilePlacement
    {
        public Vector3 WorldPosition;
        public ProceduralBoardTileKind Kind;
        public string SourceNodeId;
    }

    public static class ProceduralBoardTileLayoutBuilder
    {
        public static List<ProceduralBoardTilePlacement> Build(
            GameBoardRuntimeSnapshot snapshot,
            IList<DenseSmallBuildingPlacement> buildingPlacements)
        {
            List<ProceduralBoardTilePlacement> result =
                new List<ProceduralBoardTilePlacement>();

            if (snapshot == null)
            {
                return result;
            }

            Dictionary<string, ProceduralBoardTileKind> landmarkKinds =
                BuildLandmarkKindLookup(buildingPlacements);

            for (int index = 0; index < snapshot.Roads.Count; index++)
            {
                RuntimeRoadNode road = snapshot.Roads[index];

                if (road == null || road.Space == null)
                {
                    continue;
                }

                result.Add(
                    new ProceduralBoardTilePlacement
                    {
                        WorldPosition = road.WorldPosition,
                        Kind = ProceduralBoardTileKind.Road,
                        SourceNodeId = road.Space.NodeId ?? string.Empty
                    });
            }

            for (int index = 0; index < snapshot.Buildings.Count; index++)
            {
                RuntimeBuildingNode building = snapshot.Buildings[index];

                if (building == null || building.Space == null)
                {
                    continue;
                }

                ProceduralBoardTileKind kind =
                    ProceduralBoardTileKind.NeutralBuilding;

                ProceduralBoardTileKind mappedKind;

                if (!string.IsNullOrEmpty(building.NodeId) &&
                    landmarkKinds.TryGetValue(building.NodeId, out mappedKind))
                {
                    kind = mappedKind;
                }

                result.Add(
                    new ProceduralBoardTilePlacement
                    {
                        WorldPosition = building.WorldPosition,
                        Kind = kind,
                        SourceNodeId = building.NodeId ?? string.Empty
                    });
            }

            return result;
        }

        private static Dictionary<string, ProceduralBoardTileKind>
            BuildLandmarkKindLookup(
                IList<DenseSmallBuildingPlacement> buildingPlacements)
        {
            Dictionary<string, ProceduralBoardTileKind> result =
                new Dictionary<string, ProceduralBoardTileKind>();

            if (buildingPlacements == null)
            {
                return result;
            }

            for (int index = 0; index < buildingPlacements.Count; index++)
            {
                DenseSmallBuildingPlacement placement =
                    buildingPlacements[index];

                if (placement == null ||
                    !placement.IsLandmark ||
                    string.IsNullOrEmpty(placement.SourceNodeId))
                {
                    continue;
                }

                ProceduralBoardTileKind kind =
                    ConvertRole(placement.LandmarkRole);

                if (!result.ContainsKey(placement.SourceNodeId))
                {
                    result.Add(placement.SourceNodeId, kind);
                }
            }

            return result;
        }

        private static ProceduralBoardTileKind ConvertRole(
            ProceduralLandmarkRole role)
        {
            switch (role)
            {
                case ProceduralLandmarkRole.Apartment:
                    return ProceduralBoardTileKind.Apartment;

                case ProceduralLandmarkRole.ShoppingMall:
                    return ProceduralBoardTileKind.ShoppingMall;

                case ProceduralLandmarkRole.Hospital:
                    return ProceduralBoardTileKind.Hospital;

                case ProceduralLandmarkRole.Office:
                    return ProceduralBoardTileKind.Office;

                case ProceduralLandmarkRole.Industrial:
                    return ProceduralBoardTileKind.Industrial;

                case ProceduralLandmarkRole.Civic:
                    return ProceduralBoardTileKind.Civic;

                default:
                    return ProceduralBoardTileKind.NeutralBuilding;
            }
        }
    }
}

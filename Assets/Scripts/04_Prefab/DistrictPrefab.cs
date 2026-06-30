using UnityEngine;

namespace BoardOfDead
{
    public class DistrictPrefab : MonoBehaviour
    {
        [Header("District")]
        [SerializeField] private string districtName = "District";
        [SerializeField] private DistrictType districtType = DistrictType.Mixed;
        [Tooltip("보드 중앙 지구 마커에 표시할 아이콘입니다. 비어 있으면 지구 종류의 한글 약칭을 사용합니다.")]
        [SerializeField] private Sprite districtIcon;
        [Tooltip("비워두면 District Type을 한국어 표시명으로 변환합니다.")]
        [SerializeField] private string districtShortName;

        [Header("2x1 Building Prefabs")]
        [SerializeField] private GameObject[] buildingPrefabs;

        [Header("Road Prefabs")]
        [Tooltip("기본 연결 방향: 북")]
        [SerializeField] private GameObject roadDeadEndPrefab;

        [Tooltip("기본 연결 방향: 남북")]
        [SerializeField] private GameObject roadStraightPrefab;

        [Tooltip("기본 연결 방향: 북동")]
        [SerializeField] private GameObject roadCornerPrefab;

        [Tooltip("기본 연결 방향: 북동서")]
        [SerializeField] private GameObject roadTJunctionPrefab;

        [SerializeField] private GameObject roadCrossPrefab;

        public string DistrictName
        {
            get
            {
                return string.IsNullOrWhiteSpace(districtName)
                    ? gameObject.name
                    : districtName;
            }
        }

        public DistrictType DistrictType
        {
            get { return ResolveDistrictType(); }
        }

        public Sprite DistrictIcon
        {
            get { return districtIcon; }
        }

        public string DistrictShortName
        {
            get
            {
                return string.IsNullOrWhiteSpace(districtShortName)
                    ? GetDistrictTypeDisplayName(DistrictType)
                    : districtShortName.Trim();
            }
        }

        private DistrictType ResolveDistrictType()
        {
            if (!string.Equals(
                    districtType.ToString(),
                    "Mixed",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return districtType;
            }

            string normalized =
                DistrictName.ToLowerInvariant();

            string candidateName = null;

            if (normalized.Contains("주거") ||
                normalized.Contains("residential") ||
                normalized.Contains("housing") ||
                normalized.Contains("apartment"))
            {
                candidateName = "Residential";
            }
            else if (normalized.Contains("상업") ||
                     normalized.Contains("commercial") ||
                     normalized.Contains("shopping") ||
                     normalized.Contains("mall"))
            {
                candidateName = "Commercial";
            }
            else if (normalized.Contains("의료") ||
                     normalized.Contains("medical") ||
                     normalized.Contains("hospital"))
            {
                candidateName = "Medical";
            }
            else if (normalized.Contains("산업") ||
                     normalized.Contains("industrial") ||
                     normalized.Contains("factory"))
            {
                candidateName = "Industrial";
            }
            else if (normalized.Contains("공공") ||
                     normalized.Contains("civic") ||
                     normalized.Contains("public"))
            {
                candidateName = "Civic";
            }

            DistrictType parsed;

            if (!string.IsNullOrWhiteSpace(candidateName) &&
                System.Enum.TryParse(
                    candidateName,
                    true,
                    out parsed))
            {
                return parsed;
            }

            return districtType;
        }

        public static string GetDistrictTypeDisplayName(DistrictType type)
        {
            string normalized = type.ToString().ToLowerInvariant();

            if (normalized.Contains("residential") || normalized.Contains("housing"))
                return "주거 지구";
            if (normalized.Contains("commercial") || normalized.Contains("shopping"))
                return "상업 지구";
            if (normalized.Contains("medical") || normalized.Contains("hospital"))
                return "의료 지구";
            if (normalized.Contains("industrial") || normalized.Contains("factory"))
                return "산업 지구";
            if (normalized.Contains("civic") || normalized.Contains("public"))
                return "공공 지구";
            if (normalized.Contains("mixed"))
                return "복합 지구";

            return type.ToString();
        }

        public GameObject GetRandomBuildingPrefab(System.Random random)
        {
            if (buildingPrefabs == null || buildingPrefabs.Length == 0)
            {
                return null;
            }

            int startIndex = random.Next(0, buildingPrefabs.Length);

            for (int offset = 0; offset < buildingPrefabs.Length; offset++)
            {
                int index = (startIndex + offset) % buildingPrefabs.Length;
                GameObject candidate = buildingPrefabs[index];

                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        public GameObject GetRoadPrefab(
            RoadConnectionMask mask,
            out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            int directionCount = CountDirections(mask);

            if (directionCount >= 4)
            {
                return roadCrossPrefab;
            }

            if (directionCount == 3)
            {
                rotation = RotationForTJunction(mask);
                return roadTJunctionPrefab;
            }

            if (directionCount == 2)
            {
                bool northSouth =
                    Has(mask, RoadConnectionMask.North) &&
                    Has(mask, RoadConnectionMask.South);

                bool eastWest =
                    Has(mask, RoadConnectionMask.East) &&
                    Has(mask, RoadConnectionMask.West);

                if (northSouth || eastWest)
                {
                    rotation =
                        eastWest
                            ? Quaternion.Euler(0f, 90f, 0f)
                            : Quaternion.identity;

                    return roadStraightPrefab;
                }

                rotation = RotationForCorner(mask);
                return roadCornerPrefab;
            }

            if (directionCount == 1)
            {
                rotation = RotationForDeadEnd(mask);
                return roadDeadEndPrefab;
            }

            return roadStraightPrefab;
        }

        public static int CountDirections(RoadConnectionMask mask)
        {
            int count = 0;

            if (Has(mask, RoadConnectionMask.North)) count++;
            if (Has(mask, RoadConnectionMask.East)) count++;
            if (Has(mask, RoadConnectionMask.South)) count++;
            if (Has(mask, RoadConnectionMask.West)) count++;

            return count;
        }

        public static bool Has(
            RoadConnectionMask mask,
            RoadConnectionMask value)
        {
            return (mask & value) == value;
        }

        public static Quaternion RotationForDeadEnd(
            RoadConnectionMask mask)
        {
            if (Has(mask, RoadConnectionMask.East))
            {
                return Quaternion.Euler(0f, 90f, 0f);
            }

            if (Has(mask, RoadConnectionMask.South))
            {
                return Quaternion.Euler(0f, 180f, 0f);
            }

            if (Has(mask, RoadConnectionMask.West))
            {
                return Quaternion.Euler(0f, 270f, 0f);
            }

            return Quaternion.identity;
        }

        public static Quaternion RotationForCorner(
            RoadConnectionMask mask)
        {
            if (Has(mask, RoadConnectionMask.North) &&
                Has(mask, RoadConnectionMask.East))
            {
                return Quaternion.identity;
            }

            if (Has(mask, RoadConnectionMask.East) &&
                Has(mask, RoadConnectionMask.South))
            {
                return Quaternion.Euler(0f, 90f, 0f);
            }

            if (Has(mask, RoadConnectionMask.South) &&
                Has(mask, RoadConnectionMask.West))
            {
                return Quaternion.Euler(0f, 180f, 0f);
            }

            return Quaternion.Euler(0f, 270f, 0f);
        }

        public static Quaternion RotationForTJunction(
            RoadConnectionMask mask)
        {
            if (!Has(mask, RoadConnectionMask.South))
            {
                return Quaternion.identity;
            }

            if (!Has(mask, RoadConnectionMask.West))
            {
                return Quaternion.Euler(0f, 90f, 0f);
            }

            if (!Has(mask, RoadConnectionMask.North))
            {
                return Quaternion.Euler(0f, 180f, 0f);
            }

            return Quaternion.Euler(0f, 270f, 0f);
        }
    }
}

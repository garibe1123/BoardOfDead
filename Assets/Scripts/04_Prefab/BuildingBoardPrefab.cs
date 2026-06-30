using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 2x1 건물 모델의 루트 컴포넌트입니다.
    /// 정적 건물 분류 데이터와 자동 생성 보드에서 부여되는 런타임 위치 ID를 함께 제공합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingBoardPrefab : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Transform playerPlacementRoot;

        [Header("Building Event Metadata")]
        [Tooltip("약국, 경찰서처럼 개별 건물을 식별하는 안정적인 ID입니다.")]
        [SerializeField] private string buildingDefinitionId;

        [SerializeField] private string buildingDisplayName;

        [Tooltip("Medical, Police, Garage처럼 사건 Variant 필터에 사용하는 유형 ID입니다.")]
        [SerializeField] private string buildingTypeId;

        [Tooltip("Supply, Shelter, Objective처럼 게임 내 역할을 나타내는 ID입니다.")]
        [SerializeField] private string buildingRoleId;

        [SerializeField] private List<string> buildingTags =
            new List<string>();

        [SerializeField] private Sprite defaultEventIllustration;

        [Tooltip("이 건물에서만 후보가 되는 고유 이벤트 ID입니다.")]
        [SerializeField] private List<string> exclusiveEventIds =
            new List<string>();

        private string runtimeNodeId;
        private string runtimeDistrictId;
        private string runtimeDistrictName;
        private DistrictType runtimeDistrictType;

        public Transform PlayerPlacementRoot =>
            playerPlacementRoot != null ? playerPlacementRoot : transform;

        public string BuildingDefinitionId => buildingDefinitionId;
        public string BuildingDisplayName =>
            string.IsNullOrWhiteSpace(buildingDisplayName)
                ? gameObject.name
                : buildingDisplayName;
        public string BuildingTypeId => buildingTypeId;
        public string BuildingRoleId => buildingRoleId;
        public IReadOnlyList<string> BuildingTags => buildingTags;
        public Sprite DefaultEventIllustration => defaultEventIllustration;
        public IReadOnlyList<string> ExclusiveEventIds => exclusiveEventIds;

        public string RuntimeNodeId => runtimeNodeId;
        public string RuntimeDistrictId => runtimeDistrictId;
        public string RuntimeDistrictName => runtimeDistrictName;
        public DistrictType RuntimeDistrictType => runtimeDistrictType;

        /// <summary>
        /// 자동 보드 생성기가 런타임 Node와 District 정보를 연결합니다.
        /// </summary>
        public void BindRuntime(
            string nodeId,
            string districtId,
            string districtName,
            DistrictType districtType,
            string fallbackDefinitionId)
        {
            runtimeNodeId = nodeId ?? string.Empty;
            runtimeDistrictId = districtId ?? string.Empty;
            runtimeDistrictName = string.IsNullOrWhiteSpace(districtName)
                ? runtimeDistrictId
                : districtName;
            runtimeDistrictType = districtType;

            if (string.IsNullOrWhiteSpace(buildingDefinitionId))
            {
                buildingDefinitionId = SanitizeId(
                    string.IsNullOrWhiteSpace(fallbackDefinitionId)
                        ? gameObject.name
                        : fallbackDefinitionId);
            }

            if (string.IsNullOrWhiteSpace(buildingDisplayName))
            {
                buildingDisplayName = gameObject.name.Replace("(Clone)", string.Empty);
            }

            if (string.IsNullOrWhiteSpace(buildingTypeId))
            {
                buildingTypeId = buildingDefinitionId;
            }
        }

        public bool HasTag(string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId))
            {
                return false;
            }

            for (int index = 0; index < buildingTags.Count; index++)
            {
                if (string.Equals(
                    buildingTags[index],
                    tagId,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasExclusiveEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return false;
            }

            for (int index = 0; index < exclusiveEventIds.Count; index++)
            {
                if (exclusiveEventIds[index] == eventId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "BUILDING";
            }

            return value
                .Replace("(Clone)", string.Empty)
                .Trim()
                .Replace(" ", "_")
                .ToUpperInvariant();
        }
    }
}

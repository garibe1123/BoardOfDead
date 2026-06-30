using UnityEngine;

namespace BoardOfDead
{
    [CreateAssetMenu(fileName = "District_", menuName = "Board Of Dead/SOBJ/District")]
    public class DistrictSOBJ : ScriptableObject
    {
        [SerializeField] private string districtId;
        [SerializeField] private string displayName;
        [SerializeField] private DistrictType districtType;

        public string DistrictId => districtId;
        public string DisplayName => displayName;
        public DistrictType DistrictType => districtType;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(districtId))
            {
                districtId = name;
            }
        }
#endif
    }
}

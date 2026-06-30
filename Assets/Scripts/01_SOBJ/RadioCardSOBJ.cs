using UnityEngine;

namespace BoardOfDead
{
    [CreateAssetMenu(fileName = "RadioCard_", menuName = "Board Of Dead/SOBJ/Radio Card")]
    public class RadioCardSOBJ : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string radioCardId;
        [SerializeField] private string displayName = "무전 신호";
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        public string RadioCardId => string.IsNullOrWhiteSpace(radioCardId) ? name : radioCardId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;

        public void ConfigureRuntime(string id, string title)
        {
            radioCardId = id;
            displayName = title;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(radioCardId))
            {
                radioCardId = name;
            }
        }
#endif
    }
}

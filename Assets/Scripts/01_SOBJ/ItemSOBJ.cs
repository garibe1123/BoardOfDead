using UnityEngine;

namespace BoardOfDead
{
    [CreateAssetMenu(fileName = "Item_", menuName = "Board Of Dead/SOBJ/Item")]
    public class ItemSOBJ : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [TextArea] [SerializeField] private string description;

        [Header("Rule")]
        [SerializeField] private ItemType itemType = ItemType.General;
        [SerializeField, Min(0f)] private float weight = 1f;
        [SerializeField] private bool stackable = true;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public ItemType ItemType => itemType;
        public float Weight => Mathf.Max(0f, weight);
        public bool Stackable => stackable;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = name;
            }
        }
#endif
    }
}

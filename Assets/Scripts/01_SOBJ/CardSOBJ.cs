using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 탐색 카드와 필드 변환 카드(차량/탈출 루트)를 함께 지원합니다.
    /// 기존 VehicleManager/EscapeManager와 신규 SearchManager가 모두 참조할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Card_",
        menuName = "Board Of Dead/SOBJ/Card")]
    public class CardSOBJ : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string cardId = "CARD";
        [SerializeField] private string displayName = "테스트 카드";

        [TextArea]
        [SerializeField] private string description = "테스트용 카드입니다.";

        [SerializeField] private CardType cardType = CardType.Supply;
        [SerializeField] private Sprite cardImage;

        [Header("Field Conversion - Optional")]
        [SerializeField] private VehicleSOBJ vehicleSOBJ;
        [SerializeField] private EscapeRouteSOBJ escapeRouteSOBJ;

        public string CardId
        {
            get
            {
                return string.IsNullOrWhiteSpace(cardId)
                    ? name
                    : cardId;
            }
        }

        public string DisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(displayName)
                    ? name
                    : displayName;
            }
        }

        public string Description
        {
            get { return description; }
        }

        public CardType CardType
        {
            get { return cardType; }
        }

        public Sprite CardImage
        {
            get { return cardImage; }
        }

        public VehicleSOBJ VehicleSOBJ
        {
            get { return vehicleSOBJ; }
        }

        public EscapeRouteSOBJ EscapeRouteSOBJ
        {
            get { return escapeRouteSOBJ; }
        }

        public void ConfigureRuntime(
            string id,
            string title,
            CardType type)
        {
            cardId = id;
            displayName = title;
            cardType = type;
        }

        public void SetRuntimeFallback(
            string id,
            string title,
            string body)
        {
            cardId = id;
            displayName = title;
            description = body;
            cardType = CardType.Supply;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                cardId = name;
            }
        }
#endif
    }
}

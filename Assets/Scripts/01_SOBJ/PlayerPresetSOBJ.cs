using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 플레이어 캐릭터의 원본 프리셋입니다.
    /// 스탯과 실제로 생성할 PlayerPrefab을 함께 보관합니다.
    /// 시작 노드는 한 판의 배치 정보이므로 PlayerSpawnSettingData가 담당합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerPreset_", menuName = "Board Of Dead/SOBJ/Player Preset")]
    public class PlayerPresetSOBJ : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string presetId;
        [SerializeField] private string displayName = "Player";

        [Header("Prefab")]
        [SerializeField] private PlayerPrefab playerPrefab;
        [SerializeField] private Sprite portrait;

        [Header("Stat")]
        [SerializeField, Min(1)] private int maxHP = 10;
        [SerializeField, Min(1)] private int maxSAN = 50;
        [SerializeField, Min(1)] private int speed = 3;
        [SerializeField, Range(1, 99)] private int resistance = 50;
        [SerializeField, Range(1, 99)] private int strength = 50;
        [SerializeField, Range(1, 99)] private int intelligence = 50;
        [SerializeField, Range(1, 99)] private int charisma = 50;
        [SerializeField, Range(1, 99)] private int body = 50;

        public string PresetId => presetId;
        public string DisplayName => displayName;
        public PlayerPrefab PlayerPrefab => playerPrefab;
        public Sprite Portrait => portrait;
        public int MaxHP => Mathf.Max(1, maxHP);
        public int MaxSAN => Mathf.Max(1, maxSAN);
        public int Speed => Mathf.Max(1, speed);
        public int Resistance => Mathf.Clamp(resistance, 1, 99);
        public int Strength => Mathf.Clamp(strength, 1, 99);
        public int Intelligence => Mathf.Clamp(intelligence, 1, 99);
        public int Charisma => Mathf.Clamp(charisma, 1, 99);
        public int Body => Mathf.Clamp(body, 1, 99);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                presetId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }
        }
#endif
    }
}

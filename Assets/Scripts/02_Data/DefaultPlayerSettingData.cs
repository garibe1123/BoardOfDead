using System;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 테스트 단계에서 PlayerPresetSOBJ 없이 기본 플레이어를 생성하기 위한 설정입니다.
    /// 기존 코드와 신규 코드 양쪽에서 사용할 수 있도록 호환 프로퍼티를 함께 제공합니다.
    /// </summary>
    [Serializable]
    public class DefaultPlayerSettingData
    {
        [Header("Identity")]
        [SerializeField] private string playerNamePrefix = "테스트 요원";
        [SerializeField] private string presetId = "DEFAULT_TEST_AGENT";

        [Header("Prefab - Optional")]
        [SerializeField] private PlayerPrefab playerPrefab;
        [SerializeField, Range(0.05f, 1f)] private float boardScaleMultiplier = 0.2f;

        [Header("Default Stat")]
        [SerializeField, Min(1)] private int maxHP = 10;
        [SerializeField, Min(1)] private int maxSAN = 50;
        [SerializeField, Min(1)] private int speed = 6;
        [SerializeField, Range(1, 99)] private int resistance = 50;
        [SerializeField, Range(1, 99)] private int strength = 50;
        [SerializeField, Range(1, 99)] private int intelligence = 50;
        [SerializeField, Range(1, 99)] private int charisma = 50;
        [SerializeField, Range(1, 99)] private int body = 50;

        public string PlayerNamePrefix
        {
            get
            {
                return string.IsNullOrWhiteSpace(playerNamePrefix)
                    ? "테스트 요원"
                    : playerNamePrefix;
            }
        }

        public string PresetId
        {
            get
            {
                return string.IsNullOrWhiteSpace(presetId)
                    ? "DEFAULT_TEST_AGENT"
                    : presetId;
            }
        }

        public PlayerPrefab PlayerPrefab
        {
            get { return playerPrefab; }
        }

        public GameObject PlayerPrefabObject
        {
            get
            {
                return playerPrefab != null
                    ? playerPrefab.gameObject
                    : null;
            }
        }

        public float BoardScaleMultiplier
        {
            get { return Mathf.Clamp(boardScaleMultiplier, 0.05f, 1f); }
        }

        // 신규 코드 호환 별칭
        public float BoardScale
        {
            get { return BoardScaleMultiplier; }
        }

        public int MaxHP { get { return Mathf.Max(1, maxHP); } }
        public int MaxSAN { get { return Mathf.Max(1, maxSAN); } }
        public int Speed { get { return Mathf.Max(1, speed); } }
        public int Resistance { get { return Mathf.Clamp(resistance, 1, 99); } }
        public int Strength { get { return Mathf.Clamp(strength, 1, 99); } }
        public int Intelligence { get { return Mathf.Clamp(intelligence, 1, 99); } }
        public int Charisma { get { return Mathf.Clamp(charisma, 1, 99); } }
        public int Body { get { return Mathf.Clamp(body, 1, 99); } }

        public string GetPlayerName(int oneBasedIndex)
        {
            return PlayerNamePrefix + " " + Mathf.Max(1, oneBasedIndex);
        }
    }
}

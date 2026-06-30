using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 기존 차량/탈출/프리셋 시스템과 신규 자동 테스트 플레이어 시스템을
    /// 동시에 지원하는 호환 PlayerData입니다.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        [Header("Identity")]
        [SerializeField] private string playerId;
        [SerializeField] private string playerPresetId;
        [SerializeField] private string playerName;

        [Header("Location")]
        [SerializeField] private string currentNodeId;
        [SerializeField] private string currentVehicleInstanceId;

        [Header("State")]
        [SerializeField] private PlayerLifeState lifeState =
            PlayerLifeState.Survivor;

        [SerializeField] private int currentHP;
        [SerializeField] private int maxHP = 10;
        [SerializeField] private int currentSAN;
        [SerializeField] private int maxSAN = 50;
        [SerializeField] private float currentAP;

        [Header("Stat")]
        [SerializeField] private int speed = 6;
        [SerializeField] private int resistance = 50;
        [SerializeField] private int strength = 50;
        [SerializeField] private int intelligence = 50;
        [SerializeField] private int charisma = 50;
        [SerializeField] private int body = 50;

        [Header("Inventory")]
        [SerializeField] private List<ItemAmountData> inventory =
            new List<ItemAmountData>();

        public string PlayerId
        {
            get { return playerId; }
            set { playerId = value ?? string.Empty; }
        }

        public string PlayerPresetId
        {
            get { return playerPresetId; }
            set
            {
                playerPresetId =
                    string.IsNullOrWhiteSpace(value)
                        ? "DEFAULT_TEST_AGENT"
                        : value;
            }
        }

        public string PlayerName
        {
            get { return playerName; }
            set
            {
                playerName =
                    string.IsNullOrWhiteSpace(value)
                        ? playerId
                        : value;
            }
        }

        /// <summary>
        /// 신규 PlayerManager가 사용하는 이름 별칭입니다.
        /// </summary>
        public string DisplayName
        {
            get { return PlayerName; }
            set { PlayerName = value; }
        }

        public string CurrentNodeId
        {
            get { return currentNodeId; }
            set { currentNodeId = value ?? string.Empty; }
        }

        public string CurrentVehicleInstanceId
        {
            get { return currentVehicleInstanceId; }
            set { currentVehicleInstanceId = value ?? string.Empty; }
        }

        public PlayerLifeState LifeState
        {
            get { return lifeState; }
            set { lifeState = value; }
        }

        public int CurrentHP
        {
            get { return currentHP; }
            set { currentHP = Mathf.Clamp(value, 0, Mathf.Max(1, maxHP)); }
        }

        public int MaxHP
        {
            get { return maxHP; }
            set
            {
                maxHP = Mathf.Max(1, value);
                currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            }
        }

        public int CurrentSAN
        {
            get { return currentSAN; }
            set { currentSAN = Mathf.Clamp(value, 0, Mathf.Max(1, maxSAN)); }
        }

        public int MaxSAN
        {
            get { return maxSAN; }
            set
            {
                maxSAN = Mathf.Max(1, value);
                currentSAN = Mathf.Clamp(currentSAN, 0, maxSAN);
            }
        }

        public float CurrentAP
        {
            get { return currentAP; }
            set { currentAP = Mathf.Max(0f, value); }
        }

        public int Speed
        {
            get { return speed; }
            set { speed = Mathf.Max(1, value); }
        }

        public int Resistance
        {
            get { return resistance; }
            set { resistance = Mathf.Clamp(value, 1, 99); }
        }

        public int Strength
        {
            get { return strength; }
            set { strength = Mathf.Clamp(value, 1, 99); }
        }

        public int Intelligence
        {
            get { return intelligence; }
            set { intelligence = Mathf.Clamp(value, 1, 99); }
        }

        public int Charisma
        {
            get { return charisma; }
            set { charisma = Mathf.Clamp(value, 1, 99); }
        }

        public int Body
        {
            get { return body; }
            set { body = Mathf.Clamp(value, 1, 99); }
        }

        public IReadOnlyList<ItemAmountData> Inventory
        {
            get { return inventory; }
        }

        public bool CanTakeTurn
        {
            get
            {
                return
                    lifeState != PlayerLifeState.Dead &&
                    lifeState != PlayerLifeState.Escaped;
            }
        }

        public PlayerData()
        {
            playerId = string.Empty;
            playerPresetId = "DEFAULT_TEST_AGENT";
            playerName = string.Empty;
            currentNodeId = string.Empty;
            currentVehicleInstanceId = string.Empty;

            maxHP = 10;
            currentHP = maxHP;
            maxSAN = 50;
            currentSAN = maxSAN;
        }

        public PlayerData(
            string id,
            PlayerSpawnSettingData spawnSetting,
            string sharedStartNodeId)
        {
            if (spawnSetting == null ||
                spawnSetting.PlayerPresetSOBJ == null)
            {
                throw new ArgumentException(
                    "PlayerSpawnSettingData 또는 PlayerPresetSOBJ가 없습니다.");
            }

            PlayerPresetSOBJ preset =
                spawnSetting.PlayerPresetSOBJ;

            Initialize(
                id,
                preset.PresetId,
                spawnSetting.ResolvedPlayerName,
                sharedStartNodeId,
                preset.MaxHP,
                preset.MaxSAN,
                preset.Speed,
                preset.Resistance,
                preset.Strength,
                preset.Intelligence,
                preset.Charisma,
                preset.Body);
        }

        public PlayerData(
            string id,
            DefaultPlayerSettingData defaultSetting,
            int playerNumber,
            string sharedStartNodeId)
        {
            if (defaultSetting == null)
            {
                throw new ArgumentNullException("defaultSetting");
            }

            Initialize(
                id,
                defaultSetting.PresetId,
                defaultSetting.GetPlayerName(playerNumber),
                sharedStartNodeId,
                defaultSetting.MaxHP,
                defaultSetting.MaxSAN,
                defaultSetting.Speed,
                defaultSetting.Resistance,
                defaultSetting.Strength,
                defaultSetting.Intelligence,
                defaultSetting.Charisma,
                defaultSetting.Body);
        }

        private void Initialize(
            string id,
            string presetId,
            string displayName,
            string startNodeId,
            int hp,
            int san,
            int spd,
            int res,
            int str,
            int intel,
            int cha,
            int bod)
        {
            PlayerId = id;
            PlayerPresetId = presetId;
            PlayerName = displayName;
            CurrentNodeId = startNodeId;
            CurrentVehicleInstanceId = string.Empty;
            LifeState = PlayerLifeState.Survivor;

            MaxHP = hp;
            CurrentHP = MaxHP;
            MaxSAN = san;
            CurrentSAN = MaxSAN;

            Speed = spd;
            Resistance = res;
            Strength = str;
            Intelligence = intel;
            Charisma = cha;
            Body = bod;
            CurrentAP = 0f;
        }

        public void ResetAP()
        {
            CurrentAP = Speed;
        }

        public bool TrySpendAP(float amount)
        {
            float safeAmount = Mathf.Max(0f, amount);

            if (CurrentAP + 0.0001f < safeAmount)
            {
                return false;
            }

            CurrentAP -= safeAmount;
            return true;
        }

        public void SetCurrentNode(string nodeId)
        {
            CurrentNodeId = nodeId;
        }

        public void SetVehicle(string vehicleInstanceId)
        {
            CurrentVehicleInstanceId = vehicleInstanceId;
        }

        public int GetItemAmount(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return 0;
            }

            ItemAmountData entry =
                inventory.Find(
                    delegate(ItemAmountData item)
                    {
                        return
                            item != null &&
                            item.ItemId == itemId;
                    });

            return entry != null
                ? entry.Amount
                : 0;
        }

        public void AddItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) ||
                amount <= 0)
            {
                return;
            }

            ItemAmountData entry =
                inventory.Find(
                    delegate(ItemAmountData item)
                    {
                        return
                            item != null &&
                            item.ItemId == itemId;
                    });

            if (entry == null)
            {
                inventory.Add(
                    new ItemAmountData(
                        itemId,
                        amount));

                return;
            }

            entry.Add(amount);
        }

        public bool RemoveItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) ||
                amount <= 0)
            {
                return false;
            }

            ItemAmountData entry =
                inventory.Find(
                    delegate(ItemAmountData item)
                    {
                        return
                            item != null &&
                            item.ItemId == itemId;
                    });

            if (entry == null ||
                entry.Amount < amount)
            {
                return false;
            }

            entry.Add(-amount);
            return true;
        }
    }
}

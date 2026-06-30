using System;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 향후 캐릭터 선택 UI 결과를 임시로 연결하기 위한 선택 데이터입니다.
    /// 현재 버전에서는 모든 플레이어가 GameBoardSettingManager의 공통 시작 노드를 사용합니다.
    /// Preset이 비어 있으면 해당 순번은 DefaultPlayerSettingData로 자동 생성됩니다.
    /// </summary>
    [Serializable]
    public class PlayerSpawnSettingData
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private PlayerPresetSOBJ playerPresetSOBJ;
        [SerializeField] private string playerNameOverride;

        public bool Enabled => enabled;
        public PlayerPresetSOBJ PlayerPresetSOBJ => playerPresetSOBJ;

        public string ResolvedPlayerName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(playerNameOverride))
                {
                    return playerNameOverride;
                }

                return playerPresetSOBJ != null ? playerPresetSOBJ.DisplayName : "Player";
            }
        }
    }
}

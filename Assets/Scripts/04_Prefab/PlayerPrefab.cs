using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 보드 위 플레이어 말입니다. 데이터는 PlayerData가 보유하고 이 컴포넌트는 표현과 위치만 담당합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerPrefab : MonoBehaviour
    {
        [SerializeField] private string playerId;
        [SerializeField] private string playerPresetId;
        [SerializeField] private Vector3 originalLocalScale = Vector3.one;
        [SerializeField] private bool originalScaleCaptured;

        public string PlayerId => playerId;
        public string PlayerPresetId => playerPresetId;

        private void Awake()
        {
            CaptureOriginalScale();
        }

        public void Bind(PlayerData playerData)
        {
            playerId = playerData != null ? playerData.PlayerId : string.Empty;
            playerPresetId = playerData != null ? playerData.PlayerPresetId : string.Empty;

            gameObject.name = playerData != null
                ? $"Player_{playerData.PlayerId}_{playerData.PlayerName}"
                : "Player_Unbound";
        }

        /// <summary>
        /// 원본 프리팹 비율을 유지한 채 보드 말 스케일을 적용합니다. 0.2는 원본의 1/5입니다.
        /// </summary>
        public void ApplyBoardScale(float multiplier)
        {
            CaptureOriginalScale();
            transform.localScale = originalLocalScale * Mathf.Clamp(multiplier, 0.01f, 10f);
        }

        public void PlaceAtPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        private void CaptureOriginalScale()
        {
            if (originalScaleCaptured)
            {
                return;
            }

            originalLocalScale = transform.localScale;
            originalScaleCaptured = true;
        }
    }
}

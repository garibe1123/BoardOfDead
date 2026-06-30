using UnityEngine;

namespace BoardOfDead
{
    [DisallowMultipleComponent]
    public class RadioCardPrefab : MonoBehaviour
    {
        [SerializeField] private string radioCardInstanceId;
        [SerializeField] private string nodeId;

        public string RadioCardInstanceId => radioCardInstanceId;

        public void Bind(RadioCardData data, int stackIndex)
        {
            radioCardInstanceId = data != null ? data.RadioCardInstanceId : string.Empty;
            nodeId = data != null ? data.NodeId : string.Empty;
            gameObject.name = data != null
                ? $"Radio_{data.RadioCardInstanceId}"
                : "Radio_Unbound";

            transform.localPosition = new Vector3(0f, 0.2f + stackIndex * 0.12f, 0f);
        }
    }
}

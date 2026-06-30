using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 고정 보드 안에서 두 노드를 연결하는 연결 프리팹입니다.
    /// 고가도로는 시각적으로 3칸 길이지만 중간 정지 없이 한 번에 통과하며 기본 AP 2를 소모합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardConnectionPrefab : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string connectionId;
        [SerializeField] private BoardNodePrefab fromNode;
        [SerializeField] private BoardNodePrefab toNode;

        [Header("Rule")]
        [SerializeField] private BoardConnectionType connectionType = BoardConnectionType.NormalRoad;
        [SerializeField] private bool bidirectional = true;
        [SerializeField, Min(0.1f)] private float movementAPCost = 1f;
        [SerializeField, Min(1)] private int visualTileLength = 1;

        public string ConnectionId => string.IsNullOrWhiteSpace(connectionId) ? gameObject.name : connectionId;
        public BoardNodePrefab FromNode => fromNode;
        public BoardNodePrefab ToNode => toNode;
        public string FromNodeId => fromNode != null ? fromNode.NodeId : string.Empty;
        public string ToNodeId => toNode != null ? toNode.NodeId : string.Empty;
        public BoardConnectionType ConnectionType => connectionType;
        public bool Bidirectional => bidirectional;
        public float MovementAPCost => Mathf.Max(0.1f, movementAPCost);
        public int VisualTileLength => Mathf.Max(1, visualTileLength);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                connectionId = gameObject.name;
            }

            if (connectionType == BoardConnectionType.ElevatedRoad)
            {
                movementAPCost = 2f;
                visualTileLength = 3;
            }
            else
            {
                movementAPCost = Mathf.Max(0.1f, movementAPCost);
                visualTileLength = Mathf.Max(1, visualTileLength);
            }
        }
#endif
    }
}

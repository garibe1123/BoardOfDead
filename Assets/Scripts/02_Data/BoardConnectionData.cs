using System;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 고정 보드 프리팹에서 읽어 온 노드 간 연결의 런타임 데이터입니다.
    /// </summary>
    [Serializable]
    public class BoardConnectionData
    {
        [SerializeField] private string connectionId;
        [SerializeField] private string fromNodeId;
        [SerializeField] private string toNodeId;
        [SerializeField] private BoardConnectionType connectionType;
        [SerializeField] private bool bidirectional;
        [SerializeField] private float movementAPCost;
        [SerializeField] private int visualTileLength;

        public string ConnectionId => connectionId;
        public string FromNodeId => fromNodeId;
        public string ToNodeId => toNodeId;
        public BoardConnectionType ConnectionType => connectionType;
        public bool Bidirectional => bidirectional;
        public float MovementAPCost => Mathf.Max(0.1f, movementAPCost);
        public int VisualTileLength => Mathf.Max(1, visualTileLength);

        public BoardConnectionData(BoardConnectionPrefab source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            connectionId = source.ConnectionId;
            fromNodeId = source.FromNodeId;
            toNodeId = source.ToNodeId;
            connectionType = source.ConnectionType;
            bidirectional = source.Bidirectional;
            movementAPCost = source.MovementAPCost;
            visualTileLength = source.VisualTileLength;
        }

        public bool Connects(string startNodeId, string destinationNodeId)
        {
            if (fromNodeId == startNodeId && toNodeId == destinationNodeId)
            {
                return true;
            }

            return bidirectional && fromNodeId == destinationNodeId && toNodeId == startNodeId;
        }

        public string GetOtherNodeId(string nodeId)
        {
            if (fromNodeId == nodeId)
            {
                return toNodeId;
            }

            if (bidirectional && toNodeId == nodeId)
            {
                return fromNodeId;
            }

            return string.Empty;
        }
    }
}

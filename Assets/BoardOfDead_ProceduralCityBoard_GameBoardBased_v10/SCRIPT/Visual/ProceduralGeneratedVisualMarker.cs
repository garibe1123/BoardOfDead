using UnityEngine;

namespace BoardOfDead
{
    public enum ProceduralGeneratedVisualType
    {
        Root,
        Road,
        Sidewalk,
        DenseSmallBuilding,
        IslandGround,
        Water,
        Forest,
        MovementTile
    }

    [DisallowMultipleComponent]
    public sealed class ProceduralGeneratedVisualMarker : MonoBehaviour
    {
        [SerializeField]
        private ProceduralGeneratedVisualType visualType;

        [SerializeField]
        private string sourceNodeId;

        public ProceduralGeneratedVisualType VisualType
        {
            get { return visualType; }
        }

        public string SourceNodeId
        {
            get { return sourceNodeId; }
        }

        public void Initialize(
            ProceduralGeneratedVisualType type,
            string nodeId)
        {
            visualType = type;
            sourceNodeId = nodeId ?? string.Empty;
        }
    }
}

using System;
using UnityEngine;

namespace BoardOfDead
{
    [Serializable]
    public class CardData
    {
        [SerializeField] private string cardInstanceId;
        [SerializeField] private string cardSOBJId;
        [SerializeField] private string nodeId;
        [SerializeField] private string ownerPlayerId;
        [SerializeField] private bool revealed;
        [SerializeField] private bool resolved;

        public string CardInstanceId => cardInstanceId;
        public string CardSOBJId => cardSOBJId;
        public string NodeId => nodeId;
        public string OwnerPlayerId => ownerPlayerId;
        public bool Revealed => revealed;
        public bool Resolved => resolved;

        public CardData(string instanceId, string sobjId, string targetNodeId)
            : this(instanceId, sobjId, targetNodeId, string.Empty, false)
        {
        }

        public CardData(
            string instanceId,
            string sobjId,
            string targetNodeId,
            string playerId,
            bool isRevealed)
        {
            cardInstanceId = instanceId;
            cardSOBJId = sobjId;
            nodeId = targetNodeId;
            ownerPlayerId = playerId ?? string.Empty;
            revealed = isRevealed;
            resolved = false;
        }

        public void Reveal()
        {
            revealed = true;
        }

        public void Resolve()
        {
            resolved = true;
        }
    }
}

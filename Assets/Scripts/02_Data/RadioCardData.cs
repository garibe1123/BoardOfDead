using System;
using UnityEngine;

namespace BoardOfDead
{
    [Serializable]
    public class RadioCardData
    {
        [SerializeField] private string radioCardInstanceId;
        [SerializeField] private string radioCardSOBJId;
        [SerializeField] private string nodeId;
        [SerializeField] private int remainingRounds;

        public string RadioCardInstanceId => radioCardInstanceId;
        public string RadioCardSOBJId => radioCardSOBJId;
        public string NodeId => nodeId;
        public int RemainingRounds => remainingRounds;
        public bool IsExpired => remainingRounds <= 0;

        public RadioCardData(string instanceId, string sobjId, string targetNodeId, int durationRounds)
        {
            radioCardInstanceId = instanceId;
            radioCardSOBJId = sobjId;
            nodeId = targetNodeId;
            remainingRounds = Mathf.Max(1, durationRounds);
        }

        public void TickRound()
        {
            remainingRounds = Mathf.Max(0, remainingRounds - 1);
        }
    }
}

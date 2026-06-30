using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [Serializable]
    public class TurnQueueData
    {
        [SerializeField] private List<string> playerIds = new List<string>();
        [SerializeField] private int currentIndex = -1;

        public IReadOnlyList<string> PlayerIds => playerIds;
        public int CurrentIndex => currentIndex;
        public bool HasCurrent => currentIndex >= 0 && currentIndex < playerIds.Count;
        public string CurrentPlayerId => HasCurrent ? playerIds[currentIndex] : string.Empty;

        public void Build(IEnumerable<string> ids)
        {
            playerIds.Clear();
            playerIds.AddRange(ids);
            currentIndex = playerIds.Count > 0 ? 0 : -1;
        }

        public bool MoveNext()
        {
            if (currentIndex < 0)
            {
                return false;
            }

            currentIndex++;
            return currentIndex < playerIds.Count;
        }
    }
}

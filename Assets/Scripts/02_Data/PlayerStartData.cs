using System;
using UnityEngine;

namespace BoardOfDead
{
    [Serializable]
    public class PlayerStartData
    {
        [SerializeField] private string playerName = "Player";
        [SerializeField] private string startNodeId;

        [Header("Stat")]
        [SerializeField, Min(1)] private int maxHP = 10;
        [SerializeField, Min(1)] private int maxSAN = 50;
        [SerializeField, Min(1)] private int speed = 3;
        [SerializeField, Range(1, 99)] private int resistance = 50;
        [SerializeField, Range(1, 99)] private int strength = 50;
        [SerializeField, Range(1, 99)] private int intelligence = 50;
        [SerializeField, Range(1, 99)] private int charisma = 50;
        [SerializeField, Range(1, 99)] private int body = 50;

        public string PlayerName => playerName;
        public string StartNodeId => startNodeId;
        public int MaxHP => Mathf.Max(1, maxHP);
        public int MaxSAN => Mathf.Max(1, maxSAN);
        public int Speed => Mathf.Max(1, speed);
        public int Resistance => Mathf.Clamp(resistance, 1, 99);
        public int Strength => Mathf.Clamp(strength, 1, 99);
        public int Intelligence => Mathf.Clamp(intelligence, 1, 99);
        public int Charisma => Mathf.Clamp(charisma, 1, 99);
        public int Body => Mathf.Clamp(body, 1, 99);
    }
}

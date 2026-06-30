using System;
using UnityEngine;

namespace BoardOfDead
{
    public class GameLogManager : MonoBehaviour
    {
        public event Action<LogCategory, string> OnLogAdded;

        public void AddLog(LogCategory category, string message)
        {
            string formatted = $"[{category}] {message}";
            Debug.Log(formatted);
            OnLogAdded?.Invoke(category, message);
        }

        public void AddWarning(LogCategory category, string message)
        {
            string formatted = $"[{category}] {message}";
            Debug.LogWarning(formatted);
            OnLogAdded?.Invoke(category, message);
        }

        public void AddError(string message)
        {
            Debug.LogError($"[BoardOfDead] {message}");
            OnLogAdded?.Invoke(LogCategory.System, message);
        }
    }
}

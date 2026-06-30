using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardOfDead
{
    [DisallowMultipleComponent]
    public class BuildingEventChoicePrefab : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text detailText;

        private Action clickAction;

        public void Bind(
            string title,
            string details,
            bool interactable,
            Action onClick)
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            clickAction = onClick;

            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
            }

            if (detailText != null)
            {
                detailText.text = details ?? string.Empty;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = interactable;
                button.onClick.AddListener(HandleClick);
            }
        }

        private void HandleClick()
        {
            clickAction?.Invoke();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardOfDead
{
    [DisallowMultipleComponent]
    public class BuildingEventUIPrefab : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup inputBlockCanvasGroup;

        [Header("Card")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Image illustrationImage;
        [SerializeField] private bool hideIllustrationWhenMissing = true;
        [SerializeField] private Sprite fallbackIllustration;

        [Header("Choices")]
        [SerializeField] private Transform choiceContainer;
        [SerializeField] private BuildingEventChoicePrefab choiceButtonPrefab;

        [Header("Roll")]
        [SerializeField] private GameObject rollPanel;
        [SerializeField] private TMP_Text rollResultText;

        [Header("Push")]
        [SerializeField] private GameObject pushPanel;
        [SerializeField] private TMP_Text pushDescriptionText;
        [SerializeField] private Button pushButton;
        [SerializeField] private Button acceptFailureButton;

        [Header("Result")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button confirmResultButton;

        private readonly List<BuildingEventChoicePrefab> spawnedChoices =
            new List<BuildingEventChoicePrefab>();

        private Action pushAction;
        private Action acceptFailureAction;
        private Action confirmAction;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (pushButton != null)
            {
                pushButton.onClick.AddListener(HandlePush);
            }

            if (acceptFailureButton != null)
            {
                acceptFailureButton.onClick.AddListener(HandleAcceptFailure);
            }

            if (confirmResultButton != null)
            {
                confirmResultButton.onClick.AddListener(HandleConfirm);
            }

            CloseImmediate();
        }

        public bool CanOpen(out string failureReason)
        {
            failureReason = string.Empty;

            if (panelRoot == null)
            {
                failureReason = "BuildingEventUIPrefab Panel Root가 연결되지 않았습니다.";
                return false;
            }

            if (choiceContainer == null || choiceButtonPrefab == null)
            {
                failureReason = "선택지 Container 또는 Prefab이 연결되지 않았습니다.";
                return false;
            }

            return true;
        }

        public void Open(
            PreparedBuildingEvent prepared,
            Action<int> onChoiceSelected)
        {
            ClearChoices();
            SetInputBlocked(true);

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (titleText != null)
            {
                titleText.text = prepared != null ? prepared.Title : string.Empty;
            }

            if (bodyText != null)
            {
                bodyText.text = prepared != null ? prepared.Body : string.Empty;
            }

            Sprite sprite = prepared != null ? prepared.Illustration : null;

            if (sprite == null)
            {
                sprite = fallbackIllustration;
            }

            if (illustrationImage != null)
            {
                illustrationImage.sprite = sprite;
                illustrationImage.gameObject.SetActive(
                    sprite != null || !hideIllustrationWhenMissing);
            }

            SetPanelActive(rollPanel, false);
            SetPanelActive(pushPanel, false);
            SetPanelActive(resultPanel, false);

            if (prepared == null)
            {
                return;
            }

            for (int index = 0; index < prepared.Choices.Count; index++)
            {
                ResolvedBuildingEventChoice choice = prepared.Choices[index];

                if (choice == null ||
                    (choice.Source != null &&
                     !choice.Available &&
                     choice.Source.unavailableMode ==
                     BuildingEventUnavailableChoiceMode.Hide))
                {
                    continue;
                }

                BuildingEventChoicePrefab instance = Instantiate(
                    choiceButtonPrefab,
                    choiceContainer);
                spawnedChoices.Add(instance);

                int capturedIndex = index;
                instance.Bind(
                    choice.DisplayText,
                    BuildChoiceDetail(choice),
                    choice.Available,
                    delegate
                    {
                        onChoiceSelected?.Invoke(capturedIndex);
                    });
            }
        }

        public void SetChoicesInteractable(bool interactable)
        {
            for (int index = 0; index < spawnedChoices.Count; index++)
            {
                BuildingEventChoicePrefab item = spawnedChoices[index];

                if (item != null)
                {
                    Button button = item.GetComponent<Button>();

                    if (button != null)
                    {
                        button.interactable = interactable && button.interactable;
                    }
                }
            }
        }

        public void ShowRoll(BuildingEventCheckResult result, bool isPush)
        {
            SetPanelActive(rollPanel, true);

            if (rollResultText != null && result != null)
            {
                string prefix = isPush ? "밀어붙이기 재판정" : "판정";
                rollResultText.text =
                    prefix + "\n" +
                    "D100: " + result.Roll +
                    " / 기준 " + result.FinalAbility +
                    "\n" + GetSuccessLevelText(result.SuccessLevel);
            }
        }

        public void ShowPush(
            float apCost,
            bool canPush,
            Action onPush,
            Action onAcceptFailure)
        {
            pushAction = onPush;
            acceptFailureAction = onAcceptFailure;
            SetPanelActive(pushPanel, true);

            if (pushDescriptionText != null)
            {
                pushDescriptionText.text =
                    "AP " + apCost.ToString("0.##") +
                    "을 사용해 한 번 재판정합니다.\n" +
                    "재실패 시 기본 실패와 추가 불이익이 함께 적용됩니다.";
            }

            if (pushButton != null)
            {
                pushButton.interactable = canPush;
            }
        }

        public void ShowResult(string text, Action onConfirm)
        {
            pushAction = null;
            acceptFailureAction = null;
            confirmAction = onConfirm;
            SetPanelActive(pushPanel, false);
            SetPanelActive(resultPanel, true);

            if (resultText != null)
            {
                resultText.text = string.IsNullOrWhiteSpace(text)
                    ? "사건 처리가 완료되었습니다."
                    : text;
            }

            if (confirmResultButton != null)
            {
                confirmResultButton.interactable = true;
            }
        }

        public void CloseImmediate()
        {
            ClearChoices();
            pushAction = null;
            acceptFailureAction = null;
            confirmAction = null;

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            SetInputBlocked(false);
        }

        private string BuildChoiceDetail(ResolvedBuildingEventChoice choice)
        {
            if (choice == null || choice.Source == null)
            {
                return string.Empty;
            }

            BuildingEventChoiceData source = choice.Source;
            List<string> lines = new List<string>();

            if (!choice.Available)
            {
                lines.Add(string.IsNullOrWhiteSpace(choice.UnavailableReason)
                    ? source.conditionDescription
                    : choice.UnavailableReason);
                return string.Join("\n", lines.ToArray());
            }

            if (source.useCheck)
            {
                lines.Add(source.abilityType + " · " + source.difficulty);

                if (source.showSuccessProbability)
                {
                    lines.Add("성공률 " + choice.SuccessProbability + "%");
                }
            }

            if (source.apCost > 0f)
            {
                lines.Add("필요 AP " + source.apCost.ToString("0.##"));
            }

            if (source.suppliesCost > 0)
            {
                lines.Add("필요 물자 " + source.suppliesCost);
            }

            if (!string.IsNullOrWhiteSpace(source.conditionDescription))
            {
                lines.Add(source.conditionDescription);
            }

            return string.Join("\n", lines.ToArray());
        }

        private static string GetSuccessLevelText(
            BuildingEventSuccessLevel level)
        {
            switch (level)
            {
                case BuildingEventSuccessLevel.ExtremeSuccess:
                    return "극단적 성공";
                case BuildingEventSuccessLevel.HardSuccess:
                    return "어려운 성공";
                case BuildingEventSuccessLevel.NormalSuccess:
                    return "일반 성공";
                case BuildingEventSuccessLevel.CriticalFailure:
                    return "대실패";
                default:
                    return "실패";
            }
        }

        private void HandlePush()
        {
            if (pushButton != null)
            {
                pushButton.interactable = false;
            }

            pushAction?.Invoke();
        }

        private void HandleAcceptFailure()
        {
            if (acceptFailureButton != null)
            {
                acceptFailureButton.interactable = false;
            }

            acceptFailureAction?.Invoke();
        }

        private void HandleConfirm()
        {
            if (confirmResultButton != null)
            {
                confirmResultButton.interactable = false;
            }

            confirmAction?.Invoke();
        }

        private void ClearChoices()
        {
            for (int index = 0; index < spawnedChoices.Count; index++)
            {
                if (spawnedChoices[index] != null)
                {
                    Destroy(spawnedChoices[index].gameObject);
                }
            }

            spawnedChoices.Clear();
        }

        private void SetInputBlocked(bool blocked)
        {
            if (inputBlockCanvasGroup == null)
            {
                return;
            }

            inputBlockCanvasGroup.alpha = blocked ? 1f : 0f;
            inputBlockCanvasGroup.blocksRaycasts = blocked;
            inputBlockCanvasGroup.interactable = blocked;
        }

        private static void SetPanelActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}

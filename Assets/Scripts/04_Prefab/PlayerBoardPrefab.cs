using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BoardOfDead
{
    public class PlayerBoardPrefab : MonoBehaviour
    {
        public event Action<PlayerBoardPrefab> Clicked;

        [Header("Move Presentation")]
        [SerializeField, Min(0f)] private float moveDuration = 0.22f;
        [SerializeField] private AnimationCurve moveCurve =
            new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 3f),
                new Keyframe(1f, 1f, 0f, 0f));

        [Header("Turn Highlight")]
        [SerializeField, Min(1f)] private float highlightedScaleMultiplier = 1.18f;

        [Header("Click")]
        [SerializeField] private bool ensureClickCollider = true;
        [SerializeField, Min(0.01f)] private float minimumClickColliderSize = 0.35f;

        private PlayerData playerData;
        private Vector3 prefabOriginalScale = Vector3.one;
        private Vector3 boundBaseScale = Vector3.one;
        private Coroutine moveRoutine;
        private bool highlighted;

        public PlayerData PlayerData => playerData;
        public float MoveDuration => Mathf.Max(0f, moveDuration);

        private void Awake()
        {
            prefabOriginalScale = transform.localScale;
            boundBaseScale = prefabOriginalScale;
        }

        public void Bind(PlayerData data, float scaleMultiplier)
        {
            playerData = data;

            gameObject.name =
                data != null
                    ? "Player_" + data.PlayerId + "_" + data.DisplayName
                    : "Player_Unbound";

            boundBaseScale =
                prefabOriginalScale *
                Mathf.Max(0.05f, scaleMultiplier);

            ApplyHighlightScale();

            if (ensureClickCollider)
            {
                EnsureColliderForClick();
            }
        }

        public void SetBoardPosition(
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            if (!Application.isPlaying ||
                moveDuration <= 0f ||
                !isActiveAndEnabled)
            {
                transform.SetPositionAndRotation(
                    worldPosition,
                    worldRotation);

                return;
            }

            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
            }

            moveRoutine =
                StartCoroutine(
                    MoveRoutine(
                        worldPosition,
                        worldRotation));
        }

        public void SetTurnHighlighted(bool value)
        {
            highlighted = value;
            ApplyHighlightScale();
        }

        private void OnMouseDown()
        {
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Clicked?.Invoke(this);
        }

        private IEnumerator MoveRoutine(
            Vector3 targetPosition,
            Quaternion targetRotation)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float normalized =
                    Mathf.Clamp01(
                        elapsed /
                        Mathf.Max(0.0001f, moveDuration));

                float eased =
                    moveCurve != null
                        ? moveCurve.Evaluate(normalized)
                        : normalized;

                transform.position =
                    Vector3.LerpUnclamped(
                        startPosition,
                        targetPosition,
                        eased);

                transform.rotation =
                    Quaternion.SlerpUnclamped(
                        startRotation,
                        targetRotation,
                        eased);

                yield return null;
            }

            transform.SetPositionAndRotation(
                targetPosition,
                targetRotation);

            moveRoutine = null;
        }

        private void ApplyHighlightScale()
        {
            transform.localScale =
                highlighted
                    ? boundBaseScale * highlightedScaleMultiplier
                    : boundBaseScale;
        }

        private void EnsureColliderForClick()
        {
            if (GetComponentInChildren<Collider>(true) != null ||
                GetComponentInChildren<Collider2D>(true) != null)
            {
                return;
            }

            BoxCollider clickCollider = gameObject.AddComponent<BoxCollider>();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers == null || renderers.Length == 0)
            {
                clickCollider.size = Vector3.one * minimumClickColliderSize;
                return;
            }

            Bounds bounds = renderers[0].bounds;

            for (int index = 1; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }

            Vector3 scale = transform.lossyScale;
            Vector3 safeScale = new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                Mathf.Max(0.0001f, Mathf.Abs(scale.z)));

            clickCollider.center = transform.InverseTransformPoint(bounds.center);
            clickCollider.size = new Vector3(
                Mathf.Max(minimumClickColliderSize, bounds.size.x / safeScale.x),
                Mathf.Max(minimumClickColliderSize, bounds.size.y / safeScale.y),
                Mathf.Max(minimumClickColliderSize, bounds.size.z / safeScale.z));
        }
    }
}

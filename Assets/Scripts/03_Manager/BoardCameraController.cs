using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BoardOfDead
{
    /// <summary>
    /// 1 Unity Unit 크기의 보드 오브젝트를 기준으로 설계된 카메라입니다.
    ///
    /// 조작:
    /// - WASD / 방향키: 화면 기준 이동
    /// - 우클릭 드래그: 현재 포커스 지점 중심 회전
    /// - Shift + 우클릭 드래그: 평면 이동
    /// - 마우스 휠: 줌
    ///
    /// 줌:
    /// - 보드 Bounds를 받으면 최소/최대/시작 거리를 자동 계산
    /// - 멀리 있을수록 휠 한 칸에 크게 이동
    /// - 가까이 있을수록 휠 한 칸에 작게 이동
    /// - 거리 전체 구간에 따라 FOV가 자연스럽게 변화
    /// - 최대 줌 아웃 근처에서는 자동으로 내려보기 각도로 변화
    /// </summary>
    [DisallowMultipleComponent]
    public class BoardCameraController : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera targetCamera;

        [SerializeField] private bool autoConfigurePerspective = true;

        [SerializeField]
        private Vector3 defaultCameraEuler =
            new Vector3(45f, 45f, 0f);

        [Header("Board Scale Auto Zoom")]
        [Tooltip(
            "켜면 SetBoardBounds로 전달받은 보드 크기를 기준으로 " +
            "카메라 줌 범위를 자동 계산합니다.")]
        [SerializeField] private bool autoCalculateZoomFromBoard = true;

        [Tooltip(
            "풀 줌 아웃 상태에서 보드가 화면을 차지할 비율입니다. " +
            "0.75면 보드가 화면 가로/세로 중 제한되는 축의 약 75%를 채웁니다.")]
        [SerializeField, Range(0.5f, 0.95f)]
        private float fullZoomOutBoardFill = 0.75f;

        [Tooltip(
            "게임 시작 시 보드가 화면을 차지할 비율입니다. " +
            "풀 줌 아웃보다 약간 더 가까운 상태로 시작합니다.")]
        [SerializeField, Range(0.55f, 1f)]
        private float initialBoardFill = 0.88f;

        [Tooltip(
            "보드 전체 보기 거리 대비 최대 줌 인 거리 비율입니다. " +
            "값이 작을수록 더 가까이 줌 인할 수 있습니다.")]
        [SerializeField, Range(0.015f, 0.3f)]
        private float closestZoomDistanceRatio = 0.055f;

        [Tooltip(
            "최대 줌 인 거리가 이 값보다 작아지지 않도록 제한합니다.")]
        [SerializeField, Min(0.1f)]
        private float absoluteMinimumZoomDistance = 0.8f;

        [Tooltip("보드 Bounds 외곽에 추가할 월드 단위 여백입니다.")]
        [SerializeField, Min(0f)]
        private float fitPadding = 0.75f;

        [Header("Fallback Zoom Range")]
        [Tooltip("보드 Bounds가 아직 없을 때만 사용하는 시작 거리입니다.")]
        [SerializeField, Min(0.1f)]
        private float fallbackInitialDistance = 24f;

        [Tooltip("보드 Bounds가 아직 없을 때만 사용하는 최소 거리입니다.")]
        [SerializeField, Min(0.1f)]
        private float fallbackMinimumDistance = 3f;

        [Tooltip("보드 Bounds가 아직 없을 때만 사용하는 최대 거리입니다.")]
        [SerializeField, Min(0.1f)]
        private float fallbackMaximumDistance = 60f;

        [Header("Wheel Zoom")]
        [Tooltip(
            "휠 입력에 직접 곱하는 전체 줌 배율입니다. " +
            "기본 4배이며, 값이 클수록 휠 한 번에 훨씬 크게 이동합니다.")]
        [SerializeField, Range(0.1f, 20f)]
        private float zoomInputMultiplier = 4f;

        [Tooltip(
            "가까운 상태에서 휠 한 칸당 현재 거리의 몇 퍼센트를 이동할지 정합니다. " +
            "0.06은 약 6%입니다.")]
        [SerializeField, Range(0.005f, 0.5f)]
        private float nearZoomPercentPerNotch = 0.06f;

        [Tooltip(
            "먼 상태에서 휠 한 칸당 현재 거리의 몇 퍼센트를 이동할지 정합니다. " +
            "0.24는 약 24%입니다.")]
        [SerializeField, Range(0.01f, 0.8f)]
        private float farZoomPercentPerNotch = 0.24f;

        [Tooltip(
            "1보다 작으면 중간 거리부터 빠르게 민감해집니다. " +
            "권장값은 0.5입니다.")]
        [SerializeField, Range(0.1f, 3f)]
        private float zoomSensitivityCurve = 0.5f;

        [Tooltip("휠 줌 Tween 지속시간입니다. 0이면 즉시 이동합니다.")]
        [SerializeField, Range(0f, 1f)]
        private float zoomTweenDuration = 0.16f;

        [Tooltip(
            "줌 Tween 곡선입니다. 기본은 빠르게 출발하고 부드럽게 멈추는 Ease Out입니다.")]
        [SerializeField]
        private AnimationCurve zoomTweenCurve =
            new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 3f),
                new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip(
            "한 프레임에 적용할 수 있는 최종 휠 입력의 최대값입니다. " +
            "고해상도 휠의 과도한 순간 입력만 제한합니다.")]
        [SerializeField, Range(1f, 30f)]
        private float maximumEffectiveWheelInput = 16f;

        [Header("Automatic FOV")]
        [Tooltip("최대 줌 아웃 상태의 FOV입니다.")]
        [SerializeField, Range(10f, 90f)]
        private float farFOV = 42f;

        [Tooltip("최대 줌 인 상태의 FOV입니다.")]
        [SerializeField, Range(20f, 120f)]
        private float nearFOV = 68f;

        [Tooltip(
            "1은 기본입니다. 높이면 가까워질수록 광각 변화가 더 강해집니다.")]
        [SerializeField, Range(0.1f, 3f)]
        private float fovStrength = 1f;

        [SerializeField, Range(0f, 0.5f)]
        private float fovSmoothTime = 0.1f;

        [Header("Automatic Top Down")]
        [SerializeField] private bool enableAutomaticTopDown = true;

        [Tooltip(
            "전체 줌 범위에서 이 비율 이상 멀어지면 내려보기 전환을 시작합니다.")]
        [SerializeField, Range(0f, 1f)]
        private float topDownStartNormalized = 0.72f;

        [Tooltip(
            "전체 줌 범위에서 이 비율에 도달하면 내려보기 각도가 완전히 적용됩니다.")]
        [SerializeField, Range(0f, 1f)]
        private float topDownFullNormalized = 0.96f;

        [SerializeField, Range(1f, 89f)]
        private float topDownPitch = 78f;

        [SerializeField, Range(0f, 0.5f)]
        private float angleSmoothTime = 0.12f;

        [Header("Move")]
        [SerializeField, Min(0f)]
        private float keyboardMoveSpeed = 8f;

        [SerializeField, Min(0f)]
        private float farMoveSpeedMultiplier = 1.5f;

        [Header("Right Mouse Orbit")]
        [SerializeField] private bool enableRightMouseOrbit = true;

        [SerializeField, Min(0f)]
        private float horizontalOrbitSensitivity = 0.22f;

        [SerializeField, Min(0f)]
        private float verticalOrbitSensitivity = 0.18f;

        [SerializeField, Range(1f, 89f)]
        private float minimumManualPitch = 20f;

        [SerializeField, Range(1f, 89f)]
        private float maximumManualPitch = 70f;

        [SerializeField] private bool invertHorizontalOrbit;
        [SerializeField] private bool invertVerticalOrbit;

        [Header("Shift + Right Mouse Pan")]
        [SerializeField] private bool enableShiftRightMousePan = true;

        [SerializeField, Min(0f)]
        private float rightDragPanSensitivity = 1f;

        [Header("Board Clamp")]
        [SerializeField] private bool clampToBoard = true;

        [SerializeField, Min(0f)]
        private float boundsPadding = 2f;

        [Header("Runtime Debug")]
        [SerializeField] private float runtimeMinimumDistance;
        [SerializeField] private float runtimeInitialDistance;
        [SerializeField] private float runtimeMaximumDistance;
        [SerializeField] private float runtimeCurrentDistance;

        private bool hasBoardBounds;
        private Bounds boardBounds;
        private Vector3 focusPoint;

        private float manualYaw;
        private float manualPitch;
        private float appliedPitch;

        private float targetDistance;
        private float currentDistance;

        private float zoomTweenStartDistance;
        private float zoomTweenElapsed;
        private bool isZoomTweening;

        private float fovVelocity;
        private float pitchVelocity;

        private bool isRightDragging;
        private Vector2 previousMousePosition;

        public Camera TargetCamera
        {
            get
            {
                if (targetCamera == null)
                {
                    ResolveCamera();
                }

                return targetCamera;
            }
        }

        /// <summary>
        /// 현재 줌 거리를 반환합니다.
        /// </summary>
        public float CurrentZoomDistance
        {
            get { return currentDistance; }
        }

        /// <summary>
        /// 0은 최대 줌 인, 1은 최대 줌 아웃입니다.
        /// 보드 위 Screen Space UI의 크기 보정에 사용합니다.
        /// </summary>
        public float CurrentZoomNormalized
        {
            get
            {
                return Mathf.InverseLerp(
                    runtimeMinimumDistance,
                    runtimeMaximumDistance,
                    currentDistance);
            }
        }

        private Transform CameraTransform
        {
            get
            {
                return targetCamera != null
                    ? targetCamera.transform
                    : transform;
            }
        }

        private void Awake()
        {
            ResolveCamera();
            ValidateValues();

            runtimeMinimumDistance = fallbackMinimumDistance;
            runtimeInitialDistance = fallbackInitialDistance;
            runtimeMaximumDistance = fallbackMaximumDistance;

            targetDistance =
                Mathf.Clamp(
                    runtimeInitialDistance,
                    runtimeMinimumDistance,
                    runtimeMaximumDistance);

            currentDistance = targetDistance;
            runtimeCurrentDistance = currentDistance;

            manualPitch =
                Mathf.Clamp(
                    NormalizeAngle(defaultCameraEuler.x),
                    minimumManualPitch,
                    maximumManualPitch);

            manualYaw =
                NormalizeAngle(defaultCameraEuler.y);

            appliedPitch = manualPitch;

            SyncFocusPointFromCurrentCamera();
        }

        private void OnValidate()
        {
            ValidateValues();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                ResolveCamera();

                if (targetCamera == null)
                {
                    return;
                }
            }

            HandleKeyboardMove();
            HandleRightMouseInput();
            HandleZoomInput();

            if (clampToBoard && hasBoardBounds)
            {
                ClampFocusPoint();
            }

            UpdateDistance();
            UpdateFOV();
            UpdatePitch();
            ApplyCameraTransform();

            runtimeCurrentDistance = currentDistance;
        }

        public void SetBoardBounds(
            Bounds bounds,
            bool focusImmediately)
        {
            boardBounds = bounds;
            hasBoardBounds = true;

            RecalculateRuntimeZoomRange();

            if (focusImmediately)
            {
                FocusBoard();
            }
        }

        [ContextMenu("Focus Board")]
        public void FocusBoard()
        {
            if (targetCamera == null)
            {
                ResolveCamera();
            }

            if (targetCamera == null)
            {
                return;
            }

            if (hasBoardBounds)
            {
                focusPoint = boardBounds.center;
                RecalculateRuntimeZoomRange();
            }

            if (autoConfigurePerspective)
            {
                targetCamera.orthographic = false;

                manualPitch =
                    Mathf.Clamp(
                        NormalizeAngle(defaultCameraEuler.x),
                        minimumManualPitch,
                        maximumManualPitch);

                manualYaw =
                    NormalizeAngle(defaultCameraEuler.y);
            }

            targetDistance =
                Mathf.Clamp(
                    runtimeInitialDistance,
                    runtimeMinimumDistance,
                    runtimeMaximumDistance);

            currentDistance = targetDistance;
            zoomTweenStartDistance = currentDistance;
            zoomTweenElapsed = 0f;
            isZoomTweening = false;

            appliedPitch = GetTargetPitch();
            targetCamera.fieldOfView = GetTargetFOV();

            ApplyCameraTransform();
        }

        /// <summary>
        /// UI에서 플레이어 말, 카드 슬롯, 건물 등의 Transform을 선택했을 때
        /// 해당 위치를 카메라 중심으로 맞춥니다.
        /// </summary>
        public void FocusTransform(
            Transform target,
            bool keepCurrentZoom = true)
        {
            if (target == null)
            {
                return;
            }

            FocusWorldPoint(
                target.position,
                keepCurrentZoom);
        }

        /// <summary>
        /// 지정 월드 좌표를 현재 카메라의 포커스 지점으로 설정합니다.
        /// keepCurrentZoom이 false면 보드 초기 거리로 함께 복귀합니다.
        /// </summary>
        public void FocusWorldPoint(
            Vector3 worldPoint,
            bool keepCurrentZoom = true)
        {
            if (targetCamera == null)
            {
                ResolveCamera();
            }

            if (targetCamera == null)
            {
                return;
            }

            focusPoint = worldPoint;

            if (hasBoardBounds)
            {
                focusPoint.y = boardBounds.center.y;
            }

            if (!keepCurrentZoom)
            {
                targetDistance =
                    Mathf.Clamp(
                        runtimeInitialDistance,
                        runtimeMinimumDistance,
                        runtimeMaximumDistance);

                currentDistance = targetDistance;
                zoomTweenStartDistance = currentDistance;
                zoomTweenElapsed = 0f;
                isZoomTweening = false;
            }

            if (clampToBoard && hasBoardBounds)
            {
                ClampFocusPoint();
            }

            ApplyCameraTransform();
        }

        private void ValidateValues()
        {
            fallbackMinimumDistance =
                Mathf.Max(0.1f, fallbackMinimumDistance);

            fallbackMaximumDistance =
                Mathf.Max(
                    fallbackMinimumDistance + 0.1f,
                    fallbackMaximumDistance);

            fallbackInitialDistance =
                Mathf.Clamp(
                    fallbackInitialDistance,
                    fallbackMinimumDistance,
                    fallbackMaximumDistance);

            maximumManualPitch =
                Mathf.Max(
                    minimumManualPitch,
                    maximumManualPitch);

            initialBoardFill =
                Mathf.Max(
                    fullZoomOutBoardFill,
                    initialBoardFill);

            closestZoomDistanceRatio =
                Mathf.Clamp(
                    closestZoomDistanceRatio,
                    0.015f,
                    0.3f);

            absoluteMinimumZoomDistance =
                Mathf.Max(
                    0.1f,
                    absoluteMinimumZoomDistance);

            farZoomPercentPerNotch =
                Mathf.Max(
                    nearZoomPercentPerNotch,
                    farZoomPercentPerNotch);

            nearFOV =
                Mathf.Max(
                    farFOV,
                    nearFOV);

            topDownFullNormalized =
                Mathf.Max(
                    topDownStartNormalized,
                    topDownFullNormalized);
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void RecalculateRuntimeZoomRange()
        {
            if (!autoCalculateZoomFromBoard ||
                !hasBoardBounds ||
                targetCamera == null)
            {
                runtimeMinimumDistance =
                    fallbackMinimumDistance;

                runtimeInitialDistance =
                    fallbackInitialDistance;

                runtimeMaximumDistance =
                    fallbackMaximumDistance;

                return;
            }

            float defaultPitch =
                Mathf.Clamp(
                    NormalizeAngle(defaultCameraEuler.x),
                    minimumManualPitch,
                    maximumManualPitch);

            float defaultYaw =
                NormalizeAngle(defaultCameraEuler.y);

            float farPitch =
                enableAutomaticTopDown
                    ? topDownPitch
                    : defaultPitch;

            // 최대 줌 아웃은 실제 최대 거리에서 적용될 내려보기 각도와
            // 망원 FOV를 기준으로 계산합니다.
            runtimeMaximumDistance =
                CalculateDistanceForBoardFill(
                    fullZoomOutBoardFill,
                    farPitch,
                    defaultYaw,
                    farFOV);

            // 시작 거리는 기본 아이소메트릭 각도에서 화면을 조금 더 채우도록 계산합니다.
            runtimeInitialDistance =
                CalculateDistanceForBoardFill(
                    initialBoardFill,
                    defaultPitch,
                    defaultYaw,
                    farFOV);

            runtimeInitialDistance =
                Mathf.Min(
                    runtimeInitialDistance,
                    runtimeMaximumDistance);

            // 최대 줌 인은 전체 보기 거리의 비율로 계산합니다.
            // 기존 0.12보다 낮은 0.055가 기본이라 훨씬 가까이 들어갑니다.
            runtimeMinimumDistance =
                Mathf.Max(
                    absoluteMinimumZoomDistance,
                    runtimeMaximumDistance *
                    closestZoomDistanceRatio);

            runtimeInitialDistance =
                Mathf.Clamp(
                    runtimeInitialDistance,
                    runtimeMinimumDistance,
                    runtimeMaximumDistance);

            targetDistance =
                Mathf.Clamp(
                    targetDistance,
                    runtimeMinimumDistance,
                    runtimeMaximumDistance);

            currentDistance =
                Mathf.Clamp(
                    currentDistance,
                    runtimeMinimumDistance,
                    runtimeMaximumDistance);
        }

        private float CalculateDistanceForBoardFill(
            float boardFill,
            float pitch,
            float yaw,
            float verticalFOV)
        {
            float safeFill =
                Mathf.Clamp(
                    boardFill,
                    0.05f,
                    1f);

            float safeVerticalFOV =
                Mathf.Clamp(
                    verticalFOV,
                    1f,
                    179f);

            float aspect =
                Mathf.Max(
                    0.1f,
                    targetCamera.aspect);

            float tanVertical =
                Mathf.Tan(
                    safeVerticalFOV *
                    0.5f *
                    Mathf.Deg2Rad);

            float tanHorizontal =
                tanVertical *
                aspect;

            Quaternion rotation =
                Quaternion.Euler(
                    pitch,
                    yaw,
                    0f);

            Quaternion inverseRotation =
                Quaternion.Inverse(rotation);

            Vector3 center =
                boardBounds.center;

            Vector3 extents =
                boardBounds.extents +
                new Vector3(
                    fitPadding,
                    fitPadding,
                    fitPadding);

            float requiredDistance = 0.1f;

            for (int xIndex = -1;
                 xIndex <= 1;
                 xIndex += 2)
            {
                for (int yIndex = -1;
                     yIndex <= 1;
                     yIndex += 2)
                {
                    for (int zIndex = -1;
                         zIndex <= 1;
                         zIndex += 2)
                    {
                        Vector3 corner =
                            center +
                            new Vector3(
                                extents.x * xIndex,
                                extents.y * yIndex,
                                extents.z * zIndex);

                        Vector3 localCorner =
                            inverseRotation *
                            (corner - center);

                        float horizontalDistance =
                            Mathf.Abs(localCorner.x) /
                            Mathf.Max(
                                0.01f,
                                tanHorizontal *
                                safeFill) -
                            localCorner.z;

                        float verticalDistance =
                            Mathf.Abs(localCorner.y) /
                            Mathf.Max(
                                0.01f,
                                tanVertical *
                                safeFill) -
                            localCorner.z;

                        requiredDistance =
                            Mathf.Max(
                                requiredDistance,
                                horizontalDistance,
                                verticalDistance);
                    }
                }
            }

            return Mathf.Max(
                absoluteMinimumZoomDistance,
                requiredDistance);
        }

        private void SyncFocusPointFromCurrentCamera()
        {
            if (targetCamera == null)
            {
                focusPoint = Vector3.zero;
                return;
            }

            Plane boardPlane =
                new Plane(
                    Vector3.up,
                    Vector3.zero);

            Ray centerRay =
                targetCamera.ViewportPointToRay(
                    new Vector3(0.5f, 0.5f, 0f));

            float enter;

            if (boardPlane.Raycast(centerRay, out enter))
            {
                focusPoint = centerRay.GetPoint(enter);
            }
            else
            {
                focusPoint =
                    CameraTransform.position +
                    CameraTransform.forward *
                    fallbackInitialDistance;
            }
        }

        private void HandleKeyboardMove()
        {
            Vector2 input = ReadMoveInput();

            if (input.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 right = CameraTransform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 forward = CameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 movement =
                right * input.x +
                forward * input.y;

            if (movement.sqrMagnitude > 1f)
            {
                movement.Normalize();
            }

            float far01 =
                GetDistanceNormalized(currentDistance);

            float moveMultiplier =
                Mathf.Lerp(
                    1f,
                    farMoveSpeedMultiplier,
                    far01);

            focusPoint +=
                movement *
                keyboardMoveSpeed *
                moveMultiplier *
                Time.unscaledDeltaTime;
        }

        private void HandleRightMouseInput()
        {
            Vector2 mousePosition =
                ReadMousePosition();

            if (ReadRightMouseDown())
            {
                isRightDragging = true;
                previousMousePosition =
                    mousePosition;
            }

            if (isRightDragging &&
                ReadRightMouseHeld())
            {
                Vector2 delta =
                    mousePosition -
                    previousMousePosition;

                previousMousePosition =
                    mousePosition;

                if (enableShiftRightMousePan &&
                    ReadShiftHeld())
                {
                    PanByMouseDelta(delta);
                }
                else if (enableRightMouseOrbit)
                {
                    OrbitByMouseDelta(delta);
                }
            }

            if (ReadRightMouseUp())
            {
                isRightDragging = false;
            }
        }

        private void OrbitByMouseDelta(
            Vector2 delta)
        {
            float horizontalSign =
                invertHorizontalOrbit
                    ? -1f
                    : 1f;

            float verticalSign =
                invertVerticalOrbit
                    ? -1f
                    : 1f;

            manualYaw +=
                delta.x *
                horizontalOrbitSensitivity *
                horizontalSign;

            manualPitch -=
                delta.y *
                verticalOrbitSensitivity *
                verticalSign;

            manualPitch =
                Mathf.Clamp(
                    manualPitch,
                    minimumManualPitch,
                    maximumManualPitch);
        }

        private void PanByMouseDelta(
            Vector2 delta)
        {
            float worldPerPixel =
                2f *
                currentDistance *
                Mathf.Tan(
                    targetCamera.fieldOfView *
                    0.5f *
                    Mathf.Deg2Rad) /
                Mathf.Max(
                    1f,
                    Screen.height);

            Vector3 right = CameraTransform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 forward = CameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            focusPoint +=
                (-right * delta.x -
                 forward * delta.y) *
                worldPerPixel *
                rightDragPanSensitivity;
        }

        private void HandleZoomInput()
        {
            float rawScroll =
                ReadMouseScroll();

            if (Mathf.Abs(rawScroll) <= 0.0001f)
            {
                return;
            }

            // 휠 원본 입력에 전체 배율을 먼저 곱합니다.
            // 기본값 4이면 일반 휠 한 칸이 4칸 상당의 입력으로 처리됩니다.
            float effectiveScroll =
                Mathf.Clamp(
                    rawScroll * zoomInputMultiplier,
                    -maximumEffectiveWheelInput,
                    maximumEffectiveWheelInput);

            float far01 =
                GetDistanceNormalized(targetDistance);

            float curvedFar01 =
                Mathf.Pow(
                    Mathf.Clamp01(far01),
                    zoomSensitivityCurve);

            float percentPerNotch =
                Mathf.Lerp(
                    nearZoomPercentPerNotch,
                    farZoomPercentPerNotch,
                    curvedFar01);

            // 현재 목표 거리에서 비율식으로 누적합니다.
            // 휠을 연속으로 굴리면 Tween 도중에도 목표 거리가 계속 크게 갱신됩니다.
            float distanceMultiplier =
                Mathf.Exp(
                    -effectiveScroll *
                    percentPerNotch);

            float newTargetDistance =
                Mathf.Clamp(
                    targetDistance * distanceMultiplier,
                    runtimeMinimumDistance,
                    runtimeMaximumDistance);

            if (Mathf.Approximately(
                newTargetDistance,
                targetDistance))
            {
                return;
            }

            targetDistance =
                newTargetDistance;

            // 현재 화면 위치에서 새 목표까지 Tween을 다시 시작합니다.
            zoomTweenStartDistance =
                currentDistance;

            zoomTweenElapsed = 0f;
            isZoomTweening = true;
        }

        private void UpdateDistance()
        {
            if (!isZoomTweening)
            {
                currentDistance =
                    targetDistance;

                return;
            }

            if (zoomTweenDuration <= 0f)
            {
                currentDistance =
                    targetDistance;

                isZoomTweening = false;
                return;
            }

            zoomTweenElapsed +=
                Time.unscaledDeltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    zoomTweenElapsed /
                    zoomTweenDuration);

            float easedTime =
                zoomTweenCurve != null
                    ? zoomTweenCurve.Evaluate(normalizedTime)
                    : normalizedTime;

            currentDistance =
                Mathf.LerpUnclamped(
                    zoomTweenStartDistance,
                    targetDistance,
                    easedTime);

            if (normalizedTime >= 1f)
            {
                currentDistance =
                    targetDistance;

                isZoomTweening = false;
            }
        }

        private void UpdateFOV()
        {
            float targetFOV =
                GetTargetFOV();

            if (fovSmoothTime <= 0f)
            {
                targetCamera.fieldOfView =
                    targetFOV;

                return;
            }

            targetCamera.fieldOfView =
                Mathf.SmoothDamp(
                    targetCamera.fieldOfView,
                    targetFOV,
                    ref fovVelocity,
                    fovSmoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
        }

        private float GetTargetFOV()
        {
            float far01 =
                GetDistanceNormalized(currentDistance);

            float near01 =
                1f -
                far01;

            float smoothNear01 =
                SmoothStep01(near01);

            float strengthenedNear01 =
                Mathf.Clamp01(
                    smoothNear01 *
                    fovStrength);

            return Mathf.Lerp(
                farFOV,
                nearFOV,
                strengthenedNear01);
        }

        private void UpdatePitch()
        {
            float targetPitch =
                GetTargetPitch();

            if (angleSmoothTime <= 0f)
            {
                appliedPitch =
                    targetPitch;

                return;
            }

            appliedPitch =
                Mathf.SmoothDamp(
                    appliedPitch,
                    targetPitch,
                    ref pitchVelocity,
                    angleSmoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
        }

        private float GetTargetPitch()
        {
            if (!enableAutomaticTopDown)
            {
                return manualPitch;
            }

            float far01 =
                GetDistanceNormalized(currentDistance);

            float topDown01 =
                Mathf.InverseLerp(
                    topDownStartNormalized,
                    topDownFullNormalized,
                    far01);

            topDown01 =
                SmoothStep01(topDown01);

            return Mathf.Lerp(
                manualPitch,
                topDownPitch,
                topDown01);
        }

        private float GetDistanceNormalized(
            float distance)
        {
            return Mathf.InverseLerp(
                runtimeMinimumDistance,
                runtimeMaximumDistance,
                distance);
        }

        private void ClampFocusPoint()
        {
            focusPoint.x =
                Mathf.Clamp(
                    focusPoint.x,
                    boardBounds.min.x -
                    boundsPadding,
                    boardBounds.max.x +
                    boundsPadding);

            focusPoint.z =
                Mathf.Clamp(
                    focusPoint.z,
                    boardBounds.min.z -
                    boundsPadding,
                    boardBounds.max.z +
                    boundsPadding);

            focusPoint.y =
                boardBounds.center.y;
        }

        private void ApplyCameraTransform()
        {
            Quaternion rotation =
                Quaternion.Euler(
                    appliedPitch,
                    manualYaw,
                    0f);

            CameraTransform.rotation =
                rotation;

            CameraTransform.position =
                focusPoint -
                CameraTransform.forward *
                currentDistance;
        }

        private static float SmoothStep01(
            float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static float NormalizeAngle(
            float angle)
        {
            while (angle > 180f)
            {
                angle -= 360f;
            }

            while (angle < -180f)
            {
                angle += 360f;
            }

            return angle;
        }

        private static Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                float x = 0f;
                float y = 0f;

                if (Keyboard.current.aKey.isPressed ||
                    Keyboard.current.leftArrowKey.isPressed)
                {
                    x -= 1f;
                }

                if (Keyboard.current.dKey.isPressed ||
                    Keyboard.current.rightArrowKey.isPressed)
                {
                    x += 1f;
                }

                if (Keyboard.current.sKey.isPressed ||
                    Keyboard.current.downArrowKey.isPressed)
                {
                    y -= 1f;
                }

                if (Keyboard.current.wKey.isPressed ||
                    Keyboard.current.upArrowKey.isPressed)
                {
                    y += 1f;
                }

                return Vector2.ClampMagnitude(
                    new Vector2(x, y),
                    1f);
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Vector2.ClampMagnitude(
                new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical")),
                1f);
#else
            return Vector2.zero;
#endif
        }

        private static Vector2 ReadMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return
                    Mouse.current.position.ReadValue();
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        private static float ReadMouseScroll()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return
                    Mouse.current.scroll.ReadValue().y /
                    120f;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        private static bool ReadRightMouseDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return
                    Mouse.current.rightButton
                        .wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        private static bool ReadRightMouseHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return
                    Mouse.current.rightButton
                        .isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        private static bool ReadRightMouseUp()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return
                    Mouse.current.rightButton
                        .wasReleasedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(1);
#else
            return false;
#endif
        }

        private static bool ReadShiftHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return
                    Keyboard.current.leftShiftKey
                        .isPressed ||
                    Keyboard.current.rightShiftKey
                        .isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);
#else
            return false;
#endif
        }
    }
}

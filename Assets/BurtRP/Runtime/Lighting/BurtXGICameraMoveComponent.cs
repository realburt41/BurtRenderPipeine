using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Burt.RenderPipeline
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/BurtRP/XGI Camera Move Component")]
    [MovedFrom(true, "XRender.Pipeline.Modules.XGI", "FunPlus.WorldX.XRender.Runtime", "XGICameraMoveComponent")]
    public sealed class BurtXGICameraMoveComponent : MonoBehaviour
    {
        public enum AutoMoveType
        {
            MoveForwardBackward = 0,
            MoveRightLeft = 1,
            MoveUpDown = 2,
            MoveRotateX = 3,
            MoveRotateY = 4,
            MoveRotateZ = 5
        }

        public enum ViewType
        {
            Scene = 0,
            Game = 1
        }

        public ViewType viewType = ViewType.Scene;
        public bool resetToMainCameraPosition;
        public AutoMoveType autoMoveType = AutoMoveType.MoveForwardBackward;
        [Min(0f)] public float moveStep = 0.1f;
        public float moveAngle = 5f;
        [Min(1)] public int maxMoveCount = 20;

        private float moveDirection = 1f;
        private int moveCount;
        private bool resetApplied;
        private Vector3 originalPosition;
        private Quaternion originalRotation = Quaternion.identity;

        private void OnEnable()
        {
            moveDirection = 1f;
            moveCount = 0;
            resetApplied = false;
            CaptureOriginalCameraPose();
        }

        private void Update()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            ApplyResetToMainCameraOnce();
            UpdateCameraPose();
            moveCount++;
            if (moveCount > Mathf.Max(1, maxMoveCount))
            {
                moveCount = 0;
                moveDirection *= -1f;
            }
        }

        private void OnDisable()
        {
            ResetCameraToOriginalPose();
        }

        private void OnDestroy()
        {
            ResetCameraToOriginalPose();
        }

        private void CaptureOriginalCameraPose()
        {
            if (TryGetTargetTransform(out var targetTransform))
            {
                originalPosition = targetTransform.position;
                originalRotation = targetTransform.rotation;
                return;
            }

#if UNITY_EDITOR
            if (viewType == ViewType.Scene && SceneView.lastActiveSceneView != null)
            {
                originalPosition = SceneView.lastActiveSceneView.pivot;
                originalRotation = SceneView.lastActiveSceneView.rotation;
                return;
            }
#endif

            originalPosition = transform.position;
            originalRotation = transform.rotation;
        }

        private void ApplyResetToMainCameraOnce()
        {
            if (!resetToMainCameraPosition || resetApplied)
            {
                return;
            }

            resetApplied = true;
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            SetCameraPose(mainCamera.transform.position, mainCamera.transform.rotation);
        }

        private void UpdateCameraPose()
        {
            if (!TryGetCurrentPose(out var position, out var rotation, out var forward, out var right, out var up))
            {
                return;
            }

            switch (autoMoveType)
            {
                case AutoMoveType.MoveForwardBackward:
                    position += forward * moveStep * moveDirection;
                    break;
                case AutoMoveType.MoveRightLeft:
                    position += right * moveStep * moveDirection;
                    break;
                case AutoMoveType.MoveUpDown:
                    position += up * moveStep * moveDirection;
                    break;
                case AutoMoveType.MoveRotateX:
                    rotation *= Quaternion.Euler(moveAngle * moveDirection, 0f, 0f);
                    break;
                case AutoMoveType.MoveRotateY:
                    rotation *= Quaternion.Euler(0f, moveAngle * moveDirection, 0f);
                    break;
                case AutoMoveType.MoveRotateZ:
                    rotation *= Quaternion.Euler(0f, 0f, moveAngle * moveDirection);
                    break;
            }

            SetCameraPose(position, rotation);
        }

        private bool TryGetCurrentPose(out Vector3 position, out Quaternion rotation, out Vector3 forward, out Vector3 right, out Vector3 up)
        {
            if (TryGetTargetTransform(out var targetTransform))
            {
                position = targetTransform.position;
                rotation = targetTransform.rotation;
                forward = targetTransform.forward;
                right = targetTransform.right;
                up = targetTransform.up;
                return true;
            }

#if UNITY_EDITOR
            if (viewType == ViewType.Scene && SceneView.lastActiveSceneView != null)
            {
                var sceneView = SceneView.lastActiveSceneView;
                position = sceneView.pivot;
                rotation = sceneView.rotation;
                forward = rotation * Vector3.forward;
                right = rotation * Vector3.right;
                up = rotation * Vector3.up;
                return true;
            }
#endif

            position = default;
            rotation = default;
            forward = default;
            right = default;
            up = default;
            return false;
        }

        private bool TryGetTargetTransform(out Transform targetTransform)
        {
            if (viewType == ViewType.Game)
            {
                var ownCamera = GetComponent<Camera>();
                var camera = ownCamera != null ? ownCamera : Camera.main;
                if (camera != null)
                {
                    targetTransform = camera.transform;
                    return true;
                }
            }

            targetTransform = null;
            return false;
        }

        private void SetCameraPose(Vector3 position, Quaternion rotation)
        {
            if (TryGetTargetTransform(out var targetTransform))
            {
                targetTransform.SetPositionAndRotation(position, rotation);
                return;
            }

#if UNITY_EDITOR
            if (viewType == ViewType.Scene && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAtDirect(position, rotation);
                SceneView.lastActiveSceneView.Repaint();
            }
#endif
        }

        private void ResetCameraToOriginalPose()
        {
            SetCameraPose(originalPosition, originalRotation);
            moveDirection = 1f;
            moveCount = 0;
            resetApplied = false;
        }
    }
}

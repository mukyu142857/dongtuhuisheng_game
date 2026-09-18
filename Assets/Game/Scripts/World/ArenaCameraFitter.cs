using ShapeCastle.Characters;
using ShapeCastle.Gameplay;
using UnityEngine;

namespace ShapeCastle.World
{
    /// <summary>
    /// Keeps an orthographic camera directly above the followed player.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraFitter : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.5f)] private float orthographicSize = 6.5f;
        [SerializeField] private float cameraZ = -10f;
        [SerializeField] private Color backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);

        private Camera targetCamera;

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            ResolveTarget();
            ApplyNow();

            if (Application.isPlaying)
            {
                MatchController.EnsureExists();
            }
        }

        private void OnValidate()
        {
            orthographicSize = Mathf.Max(0.5f, orthographicSize);
            targetCamera = GetComponent<Camera>();
            ApplyNow();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                ResolveTarget();
            }

            ApplyNow();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            ApplyNow();
        }

        public void ApplyNow()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = orthographicSize;
            targetCamera.backgroundColor = backgroundColor;

            Vector3 targetPosition = target != null ? target.position : Vector3.zero;
            transform.position = new Vector3(targetPosition.x, targetPosition.y, cameraZ);
            transform.rotation = Quaternion.identity;
        }

        private void ResolveTarget()
        {
            PlayerMovementController player = FindObjectOfType<PlayerMovementController>();
            if (player != null)
            {
                target = player.transform;
            }
        }
    }
}

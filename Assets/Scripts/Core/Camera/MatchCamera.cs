using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>
    /// Scrolling, zooming match camera (ARCHITECTURE 3.1, D-017). Presentation only: never affects combat.
    /// Put this on the camera rig root; the Camera itself may sit on a child so Juice can shake
    /// the child's local offset without fighting this component.
    /// World frame: the arena backdrop's top-left corner is the world origin; canvas (x px, row px)
    /// maps to world (x / 60, -row / 60).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchCamera : MonoBehaviour
    {
        public const float PixelsPerUnit = 60f;

        [Header("Camera")]
        [Tooltip("Orthographic camera driven by this rig. Defaults to a Camera on this object or a child.")]
        [SerializeField] private Camera targetCamera;

        [Header("Arena (world units)")]
        [Tooltip("Left edge of the arena art (canvas x 0).")]
        [SerializeField] private float arenaLeft = 0f;
        [Tooltip("Right edge of the arena art (canvas x 1600).")]
        [SerializeField] private float arenaRight = 1600f / PixelsPerUnit;
        [Tooltip("Top edge of the arena art (canvas row 0).")]
        [SerializeField] private float arenaTop = 0f;
        [Tooltip("Bottom edge of the arena art (canvas row 900).")]
        [SerializeField] private float arenaBottom = -900f / PixelsPerUnit;
        [Tooltip("Vertical center of the default view: canvas rows 300-840, center row 570.")]
        [SerializeField] private float defaultCenterY = -570f / PixelsPerUnit;

        [Header("Zoom")]
        [Tooltip("Fraction of the arena width visible at the default zoom (D-017: 0.6 = 960 native px).")]
        [Range(0.1f, 1f)]
        [SerializeField] private float defaultVisibleWidthFraction = 0.6f;
        [Tooltip("Fraction of the arena width visible at full zoom-out (1.0 = whole arena).")]
        [Range(0.1f, 1f)]
        [SerializeField] private float maxVisibleWidthFraction = 1f;
        [Tooltip("World units kept between each target and the screen edge before zooming out.")]
        [Min(0f)]
        [SerializeField] private float edgeMargin = 2f;
        [Tooltip("Seconds to ease the zoom toward its target size.")]
        [Min(0f)]
        [SerializeField] private float zoomSmoothTime = 0.35f;

        [Header("Follow")]
        [Tooltip("Seconds to ease the horizontal position toward the targets' midpoint.")]
        [Min(0f)]
        [SerializeField] private float followSmoothTime = 0.2f;

        private Transform targetA;
        private Transform targetB;
        private float zoomVelocity;
        private float followVelocityX;
        private float followVelocityY;
        private float currentX;
        private float currentY;
        private bool initialized;

        public Camera TargetCamera => targetCamera;
        public float DefaultOrthographicSize => SizeForWidthFraction(defaultVisibleWidthFraction);
        public float MaxOrthographicSize => SizeForWidthFraction(Mathf.Max(maxVisibleWidthFraction, defaultVisibleWidthFraction));

        /// <summary>Assign the two wrestler roots (or placeholders) at runtime.</summary>
        public void SetTargets(Transform a, Transform b)
        {
            targetA = a;
            targetB = b;
            SnapToTargets();
        }

        private void Awake()
        {
            if (targetCamera == null)
                targetCamera = GetComponentInChildren<Camera>();
            if (targetCamera != null)
                targetCamera.orthographic = true;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                return;
            if (!initialized)
                SnapToTargets();

            float dt = Time.unscaledDeltaTime;
            float aspect = Mathf.Max(0.01f, targetCamera.aspect);
            float defaultSize = DefaultOrthographicSize;
            float maxSize = MaxOrthographicSize;

            float desiredSize = Mathf.Clamp(RequiredSize(aspect), defaultSize, maxSize);
            float size = Mathf.SmoothDamp(targetCamera.orthographicSize, desiredSize, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, dt);
            if (Mathf.Abs(size - defaultSize) < 0.0005f && desiredSize <= defaultSize)
            {
                size = defaultSize;
                zoomVelocity = 0f;
            }
            targetCamera.orthographicSize = size;

            Vector2 desired = ClampedCenter(TargetMidX(), defaultCenterY, size, aspect);
            currentX = Mathf.SmoothDamp(currentX, desired.x, ref followVelocityX, followSmoothTime, Mathf.Infinity, dt);
            currentY = Mathf.SmoothDamp(currentY, desired.y, ref followVelocityY, zoomSmoothTime, Mathf.Infinity, dt);
            // Keep the view inside the arena even while easing.
            Vector2 clamped = ClampedCenter(currentX, currentY, size, aspect);
            currentX = clamped.x;
            currentY = clamped.y;

            ApplyPosition(size == defaultSize);
        }

        private void SnapToTargets()
        {
            if (targetCamera == null)
                return;
            float aspect = Mathf.Max(0.01f, targetCamera.aspect);
            float size = Mathf.Clamp(RequiredSize(aspect), DefaultOrthographicSize, MaxOrthographicSize);
            targetCamera.orthographicSize = size;
            Vector2 center = ClampedCenter(TargetMidX(), defaultCenterY, size, aspect);
            currentX = center.x;
            currentY = center.y;
            zoomVelocity = followVelocityX = followVelocityY = 0f;
            initialized = true;
            ApplyPosition(Mathf.Approximately(size, DefaultOrthographicSize));
        }

        private void ApplyPosition(bool pixelSnap)
        {
            float x = currentX;
            float y = currentY;
            if (pixelSnap)
            {
                x = Mathf.Round(x * PixelsPerUnit) / PixelsPerUnit;
                y = Mathf.Round(y * PixelsPerUnit) / PixelsPerUnit;
            }
            transform.position = new Vector3(x, y, transform.position.z);
        }

        private float SizeForWidthFraction(float fraction)
        {
            float aspect = targetCamera != null ? Mathf.Max(0.01f, targetCamera.aspect) : 16f / 9f;
            return fraction * (arenaRight - arenaLeft) * 0.5f / aspect;
        }

        private float RequiredSize(float aspect)
        {
            if (targetA == null || targetB == null)
                return 0f;
            float halfWidth = Mathf.Abs(targetA.position.x - targetB.position.x) * 0.5f + edgeMargin;
            return halfWidth / aspect;
        }

        private float TargetMidX()
        {
            if (targetA != null && targetB != null)
                return (targetA.position.x + targetB.position.x) * 0.5f;
            if (targetA != null)
                return targetA.position.x;
            if (targetB != null)
                return targetB.position.x;
            return initialized ? currentX : (arenaLeft + arenaRight) * 0.5f;
        }

        private Vector2 ClampedCenter(float x, float y, float size, float aspect)
        {
            float halfW = size * aspect;
            float minX = arenaLeft + halfW;
            float maxX = arenaRight - halfW;
            x = minX <= maxX ? Mathf.Clamp(x, minX, maxX) : (arenaLeft + arenaRight) * 0.5f;

            float minY = arenaBottom + size;
            float maxY = arenaTop - size;
            y = minY <= maxY ? Mathf.Clamp(y, minY, maxY) : (arenaTop + arenaBottom) * 0.5f;
            return new Vector2(x, y);
        }
    }
}

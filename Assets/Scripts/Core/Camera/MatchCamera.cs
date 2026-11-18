using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>
    /// Scrolling, zooming match camera (ARCHITECTURE 3.1, D-017 behaviour, D-048 numbers). Presentation only: never
    /// affects combat. Scrolls horizontally and vertically over the arena art and never shows past its edges.
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

        [Header("Arena art (world units, D-048: 2160x1080 native px)")]
        [Tooltip("Left edge of the arena art (canvas x 0).")]
        [SerializeField] private float arenaLeft = 0f;
        [Tooltip("Right edge of the arena art (canvas x 2160).")]
        [SerializeField] private float arenaRight = 2160f / PixelsPerUnit;
        [Tooltip("Top edge of the arena art (canvas row 0).")]
        [SerializeField] private float arenaTop = 0f;
        [Tooltip("Bottom edge of the arena art (canvas row 1080).")]
        [SerializeField] private float arenaBottom = -1080f / PixelsPerUnit;

        [Header("Zoom")]
        [Tooltip("World units visible across the screen at the default zoom (D-013/D-048: 16 = 960 native px, x2 at 1080p).")]
        [Min(1f)]
        [SerializeField] private float defaultVisibleWidth = 960f / PixelsPerUnit;
        [Tooltip("Largest zoom-out, as a multiple of the default orthographic size (D-048 Starting point 1.45; " +
                 "1.45 shows 1392x783 native px). Capped well short of the whole arena so the wrestlers stay readable.")]
        [Range(1f, 3f)]
        [SerializeField] private float maxZoomOut = 1.45f;
        [Tooltip("World units kept between each target and the left/right screen edge before zooming out.")]
        [Min(0f)]
        [SerializeField] private float edgeMargin = 2f;
        [Tooltip("World units kept in view above the higher target's feet (Barry is 4.0 units tall).")]
        [Min(0f)]
        [SerializeField] private float headroom = 4.6f;
        [Tooltip("World units kept in view below the lower target's feet.")]
        [Min(0f)]
        [SerializeField] private float footroom = 0.6f;
        [Tooltip("When the wrestlers are farther apart than the largest zoom-out can show, keep the first target " +
                 "(the player) on screen instead of centering on the midpoint.")]
        [SerializeField] private bool keepFirstTargetInView = true;
        [Tooltip("Seconds to ease the zoom toward its target size.")]
        [Min(0f)]
        [SerializeField] private float zoomSmoothTime = 0.35f;

        [Header("Follow")]
        [Tooltip("Seconds to ease the horizontal position toward the targets.")]
        [Min(0f)]
        [SerializeField] private float followSmoothTime = 0.2f;
        [Tooltip("Seconds to ease the vertical position toward the targets (D-048 vertical scrolling).")]
        [Min(0f)]
        [SerializeField] private float verticalFollowSmoothTime = 0.3f;

        [Header("Punch-in (D-046, requested by Juice)")]
        [Tooltip("Seconds to ease into and out of a punch-in (zoom and position). Real time.")]
        [Min(0f)]
        [SerializeField] private float punchInSmoothTime = 0.12f;
        [Tooltip("Smallest orthographic size a punch-in may request, as a fraction of the default size.")]
        [Range(0.2f, 1f)]
        [SerializeField] private float minPunchInSizeFraction = 0.5f;

        private Transform targetA;
        private Transform targetB;
        private float zoomVelocity;
        private float followVelocityX;
        private float followVelocityY;
        private float currentX;
        private float currentY;
        private bool initialized;
        private float punchInRemaining;
        private float punchInSizeFraction = 1f;
        private Vector2 punchInPoint;
        private Transform punchInTarget;
        private Vector2 punchInTargetOffset;

        public Camera TargetCamera => targetCamera;
        /// <summary>Wrestler roots passed to <see cref="SetTargets"/> (read-only; scene lighting follows them).</summary>
        public Transform TargetA => targetA;
        public Transform TargetB => targetB;
        public float DefaultOrthographicSize => defaultVisibleWidth * 0.5f / CurrentAspect;
        public float MaxOrthographicSize => DefaultOrthographicSize * Mathf.Max(1f, maxZoomOut);
        /// <summary>Arena art edges in world units (the view never shows past them).</summary>
        public float ArenaLeft => arenaLeft;
        public float ArenaRight => arenaRight;
        public float ArenaTop => arenaTop;
        public float ArenaBottom => arenaBottom;
        /// <summary>True while a punch-in holds the framing.</summary>
        public bool IsPunchedIn => punchInRemaining > 0f;

        /// <summary>
        /// D-046 KO camera punch-in. For <paramref name="holdSeconds"/> of real time the rig eases (punchInSmoothTime)
        /// to an orthographic size of DefaultOrthographicSize × <paramref name="sizeFraction"/> (clamped to
        /// minPunchInSizeFraction..1; e.g. 0.75 = 33% closer) centered on <paramref name="worldPoint"/> (clamped so the
        /// view stays on the arena art), then eases back to the normal follow/zoom. A new call replaces the current one.
        /// The view is not pixel-snapped while punched in. Camera shake stays on the child camera's local offset.
        /// </summary>
        public void PunchIn(Vector2 worldPoint, float sizeFraction, float holdSeconds)
        {
            punchInTarget = null;
            punchInPoint = worldPoint;
            StartPunchIn(sizeFraction, holdSeconds);
        }

        /// <summary>Like <see cref="PunchIn(Vector2,float,float)"/>, but keeps centering on
        /// <paramref name="target"/>.position + <paramref name="offset"/> (e.g. a sliding knocked-out wrestler's root plus
        /// about (0, 1) so the body, not the feet, is centered).</summary>
        public void PunchIn(Transform target, Vector2 offset, float sizeFraction, float holdSeconds)
        {
            if (target == null)
                return;
            punchInTarget = target;
            punchInTargetOffset = offset;
            punchInPoint = (Vector2)target.position + offset;
            StartPunchIn(sizeFraction, holdSeconds);
        }

        /// <summary>Ends a punch-in now; the rig eases back to the normal framing.</summary>
        public void ReleasePunchIn() => punchInRemaining = 0f;

        private void StartPunchIn(float sizeFraction, float holdSeconds)
        {
            if (!(holdSeconds > 0f) || float.IsNaN(sizeFraction))
                return;
            punchInSizeFraction = Mathf.Clamp(sizeFraction, minPunchInSizeFraction, 1f);
            punchInRemaining = holdSeconds;
        }

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

            bool punchedIn = punchInRemaining > 0f;
            float zoomTime = zoomSmoothTime, followTimeX = followSmoothTime, followTimeY = verticalFollowSmoothTime;
            float desiredSize;
            Vector2 desiredCenter;
            if (punchedIn)
            {
                punchInRemaining = Mathf.Max(0f, punchInRemaining - dt);
                if (punchInTarget != null)
                    punchInPoint = (Vector2)punchInTarget.position + punchInTargetOffset;
                desiredSize = defaultSize * punchInSizeFraction;
                desiredCenter = punchInPoint;
                zoomTime = followTimeX = followTimeY = punchInSmoothTime;
            }
            else
            {
                Framing(aspect, out desiredSize, out desiredCenter);
            }

            float size = Mathf.SmoothDamp(targetCamera.orthographicSize, desiredSize, ref zoomVelocity, zoomTime, Mathf.Infinity, dt);
            if (!punchedIn && Mathf.Abs(size - defaultSize) < 0.0005f && desiredSize <= defaultSize)
            {
                size = defaultSize;
                zoomVelocity = 0f;
            }
            targetCamera.orthographicSize = size;

            Vector2 desired = ClampedCenter(desiredCenter.x, desiredCenter.y, size, aspect);
            currentX = Mathf.SmoothDamp(currentX, desired.x, ref followVelocityX, followTimeX, Mathf.Infinity, dt);
            currentY = Mathf.SmoothDamp(currentY, desired.y, ref followVelocityY, followTimeY, Mathf.Infinity, dt);
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
            Framing(aspect, out float size, out Vector2 framed);
            targetCamera.orthographicSize = size;
            Vector2 center = ClampedCenter(framed.x, framed.y, size, aspect);
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

        private float CurrentAspect => targetCamera != null ? Mathf.Max(0.01f, targetCamera.aspect) : 16f / 9f;

        /// <summary>
        /// Normal follow framing (D-017 behaviour, D-048 numbers): the size that keeps both targets' feet within
        /// edgeMargin of the side edges and their bodies (headroom/footroom) within the top/bottom, clamped to
        /// default..max; centered on the targets. Past the max zoom-out the first target (player) is kept in view.
        /// </summary>
        private void Framing(float aspect, out float size, out Vector2 center)
        {
            float defaultSize = DefaultOrthographicSize;
            float required = 0f;
            float cx = TargetMidX();
            float cy = initialized ? currentY : (arenaTop + arenaBottom) * 0.5f;
            if (targetA != null || targetB != null)
            {
                Vector2 pa = targetA != null ? (Vector2)targetA.position : (Vector2)targetB.position;
                Vector2 pb = targetB != null ? (Vector2)targetB.position : pa;
                float top = Mathf.Max(pa.y, pb.y) + headroom;
                float bottom = Mathf.Min(pa.y, pb.y) - footroom;
                cy = (top + bottom) * 0.5f;
                float requiredW = (Mathf.Abs(pa.x - pb.x) * 0.5f + edgeMargin) / aspect;
                float requiredH = (top - bottom) * 0.5f;
                required = Mathf.Max(requiredW, requiredH);
            }
            size = Mathf.Clamp(required, defaultSize, MaxOrthographicSize);
            if (keepFirstTargetInView && targetA != null && targetB != null)
            {
                float halfW = size * aspect;
                float slack = halfW - edgeMargin;
                float ax = targetA.position.x;
                if (slack > 0f)
                    cx = Mathf.Clamp(cx, ax - slack, ax + slack);
                else
                    cx = ax;
            }
            center = new Vector2(cx, cy);
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

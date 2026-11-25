using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>
    /// Start screen backdrop camera (ARCHITECTURE 3.1, D-051): a slow, eased ping-pong across the arena art's horizontal
    /// range at a fixed zoom and height, never showing past the art's edges. Presentation only; unscaled time.
    /// Put this on the rig root; the Camera sits on a child so Juice can shake the child's local position (same
    /// layout as the Match rig). This component sets the root's position and the camera's orthographic size.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartBackdropDrift : MonoBehaviour
    {
        public const float PixelsPerUnit = 60f;

        [Tooltip("Orthographic camera on a child of this rig. Defaults to a Camera on a child.")]
        [SerializeField] private Camera targetCamera;

        [Header("Arena art (world units, D-048: 2160x1080 native px)")]
        [SerializeField] private float arenaLeft = 0f;
        [SerializeField] private float arenaRight = 2160f / PixelsPerUnit;
        [SerializeField] private float arenaTop = 0f;
        [SerializeField] private float arenaBottom = -1080f / PixelsPerUnit;

        [Header("View (Starting points)")]
        [Tooltip("Orthographic size. 6 shows 1280x720 art px (trailer, truck, porch, pool across the drift); 4.5 is the match default (960x540, x2 at 1080p).")]
        [Min(0.5f)]
        [SerializeField] private float orthographicSize = 6f;
        [Tooltip("World y of the view's center; clamped so the view stays on the art.")]
        [SerializeField] private float viewCenterY = -8f;
        [Tooltip("World units kept between the view and the art's left/right edges at the ends of the drift.")]
        [Min(0f)]
        [SerializeField] private float edgeMargin = 0.5f;

        [Header("Drift (Starting points)")]
        [Tooltip("Seconds for one full left-right-left cycle. Real time.")]
        [Min(1f)]
        [SerializeField] private float period = 26f;
        [Tooltip("Where in the cycle the drift starts (0 = left end, 0.5 = right end).")]
        [Range(0f, 1f)]
        [SerializeField] private float startPhase = 0.25f;
        [Tooltip("Round the rig position to the 1/60 art-pixel grid.")]
        [SerializeField] private bool snapToPixels = true;

        private float elapsed;

        public float Period { get => period; set => period = Mathf.Max(1f, value); }

        /// <summary>Eased ping-pong 0..1..0 over <paramref name="period"/> seconds (cosine ease, so it slows at each end).</summary>
        public static float PingPong01(float time, float period, float phase01)
        {
            if (period <= 0f) return 0f;
            float cycles = time / period + phase01;
            return 0.5f - 0.5f * Mathf.Cos(cycles * Mathf.PI * 2f);
        }

        /// <summary>Center x range [min, max] that keeps a view of <paramref name="halfWidth"/> on the art; a view wider than the art centers.</summary>
        public static Vector2 CenterRange(float left, float right, float halfWidth, float margin)
        {
            float min = left + halfWidth + margin;
            float max = right - halfWidth - margin;
            if (min > max)
            {
                float mid = (left + right) * 0.5f;
                return new Vector2(mid, mid);
            }
            return new Vector2(min, max);
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();
        }

        private void OnEnable()
        {
            elapsed = 0f;
            Apply();
        }

        private void LateUpdate()
        {
            elapsed += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            float aspect = targetCamera != null ? targetCamera.aspect : 16f / 9f;
            float halfHeight = orthographicSize;
            float halfWidth = orthographicSize * aspect;
            if (targetCamera != null)
            {
                targetCamera.orthographic = true;
                targetCamera.orthographicSize = orthographicSize;
            }

            Vector2 xRange = CenterRange(arenaLeft, arenaRight, halfWidth, edgeMargin);
            float x = Mathf.Lerp(xRange.x, xRange.y, PingPong01(elapsed, period, startPhase));

            float yMin = arenaBottom + halfHeight;
            float yMax = arenaTop - halfHeight;
            float y = yMin > yMax ? (arenaTop + arenaBottom) * 0.5f : Mathf.Clamp(viewCenterY, yMin, yMax);

            if (snapToPixels)
            {
                x = Mathf.Round(x * PixelsPerUnit) / PixelsPerUnit;
                y = Mathf.Round(y * PixelsPerUnit) / PixelsPerUnit;
            }

            Vector3 p = transform.position;
            transform.position = new Vector3(x, y, p.z);
        }
    }
}

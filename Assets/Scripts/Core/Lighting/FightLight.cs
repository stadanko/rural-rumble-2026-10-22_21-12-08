using RuralRumble.Core;
using UnityEngine;

namespace RuralRumble.Lighting
{
    /// <summary>
    /// Scene lighting only (D-046): keeps a soft light on the fight by following the midpoint of the
    /// two wrestler roots the match camera tracks (<see cref="MatchCamera.TargetA"/>/<see cref="MatchCamera.TargetB"/>).
    /// Presentation; reads transforms only, never combat state. Unscaled time.
    /// </summary>
    public sealed class FightLight : MonoBehaviour
    {
        [Tooltip("Camera rig whose two targets (the wrestler roots) this light follows.")]
        [SerializeField] private MatchCamera matchCamera;

        [Tooltip("Offset from the feet midpoint, world units (y up lights the bodies, not the feet). Starting point.")]
        [SerializeField] private Vector2 offset = new Vector2(0f, 1.6f);

        [Tooltip("Smoothing time in seconds. Starting point.")]
        [SerializeField] private float smoothTime = 0.25f;

        private Vector2 velocity;
        private bool placed;

        private void LateUpdate()
        {
            if (matchCamera == null) return;
            Transform a = matchCamera.TargetA, b = matchCamera.TargetB;
            if (a == null && b == null) return;
            Vector2 mid = a != null && b != null
                ? ((Vector2)a.position + (Vector2)b.position) * 0.5f
                : (Vector2)(a != null ? a.position : b.position);
            Vector2 goal = mid + offset;
            Vector2 pos = placed
                ? Vector2.SmoothDamp(transform.position, goal, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime)
                : goal;
            placed = true;
            transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        }
    }
}

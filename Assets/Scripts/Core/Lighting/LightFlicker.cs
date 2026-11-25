using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RuralRumble.Lighting
{
    /// <summary>
    /// Scene lighting only (D-046): a gentle real-time flicker on a 2D light, e.g. the porch lantern.
    /// Presentation; never reads or changes combat state. Runs on unscaled time, so hitstop and the
    /// knockout slow motion do not freeze it.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class LightFlicker : MonoBehaviour
    {
        [Tooltip("Intensity the flicker varies around. Starting point.")]
        [SerializeField] private float baseIntensity = 1.6f;

        [Tooltip("Volume (haze) intensity the flicker varies around; 0 leaves the light's volume alone. Starting point.")]
        [SerializeField] private float baseVolumeIntensity = 0f;

        [Tooltip("Steady flicker depth as a fraction of the base (0.1 = +/-10%). Starting point.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float flickerAmount = 0.08f;

        [Tooltip("Flicker speed (noise cycles per second). Starting point.")]
        [SerializeField] private float flickerSpeed = 5f;

        [Tooltip("Average number of brief dips per second (a guttering flame). Starting point.")]
        [SerializeField] private float dipsPerSecond = 0.25f;

        [Tooltip("How far a dip drops the light, as a fraction of the base. Starting point.")]
        [Range(0f, 1f)]
        [SerializeField] private float dipDepth = 0.3f;

        [Tooltip("Dip length in seconds. Starting point.")]
        [SerializeField] private float dipDuration = 0.08f;

        [Tooltip("Noise seed; lights with the same seed flicker together.")]
        [SerializeField] private float seed = 17.3f;

        private Light2D target;
        private float dipRemaining;

        public float BaseIntensity { get => baseIntensity; set => baseIntensity = value; }

        /// <summary>
        /// Runtime multiplier on both the intensity and the volume the flicker writes (1 = as authored, 0 = dark). Not
        /// serialized. A fade such as the Start screen light reveal sets this instead of the light's intensity, which
        /// this component overwrites every frame.
        /// </summary>
        public float Brightness { get => brightness; set => brightness = Mathf.Max(0f, value); }

        private float brightness = 1f;

        private void Awake() => target = GetComponent<Light2D>();

        private void Update()
        {
            float t = Time.unscaledTime * flickerSpeed;
            // Two octaves of noise in -1..1.
            float n = (Mathf.PerlinNoise(seed, t) - 0.5f) * 1.4f + (Mathf.PerlinNoise(seed + 41.7f, t * 2.7f) - 0.5f) * 0.6f;
            float k = 1f + n * flickerAmount;

            if (dipRemaining > 0f)
            {
                dipRemaining -= Time.unscaledDeltaTime;
                k -= dipDepth;
            }
            else if (dipsPerSecond > 0f && Random.value < dipsPerSecond * Time.unscaledDeltaTime)
            {
                dipRemaining = dipDuration;
            }

            k = Mathf.Max(0f, k) * brightness;
            target.intensity = baseIntensity * k;
            if (baseVolumeIntensity > 0f) target.volumeIntensity = baseVolumeIntensity * k;
        }

        private void OnDisable()
        {
            if (target == null) return;
            target.intensity = baseIntensity * brightness;
            if (baseVolumeIntensity > 0f) target.volumeIntensity = baseVolumeIntensity * brightness;
        }
    }
}

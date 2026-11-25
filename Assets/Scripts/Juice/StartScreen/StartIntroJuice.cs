using System.Collections.Generic;
using RuralRumble.Core;
using RuralRumble.Lighting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Start screen intro juice (ARCHITECTURE 4.6 "Start screen intro"). The yard opens dim; when the falling logo lands
    /// (<see cref="StartScreenEvents.LogoLanded"/>, not skipped) the lights ramp to their authored values, the camera
    /// shakes and a pixel dust burst blows sideways from the logo's bottom edge. A skip snaps the lights to full with no
    /// shake or dust. The thud is <see cref="UiSound.LogoSlam"/>, raised by UI and played by <see cref="MenuAudioPlayer"/>.
    /// Presentation only: unscaled time, never touches Time.timeScale. Lives on prefab
    /// <c>Assets/Prefabs/Effects/StartIntroJuice.prefab</c> in Start.unity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartIntroJuice : MonoBehaviour
    {
        private const float PixelsPerUnit = 60f;

        private struct LitLight
        {
            public Light2D Light;
            public LightFlicker Flicker;
            public float Intensity;
            public float Volume;
        }

        [Header("Scene references (wire in Start.unity)")]
        [Tooltip("The 'Lighting' root that holds every Light2D in the scene. Empty = the root GameObject named 'Lighting'.")]
        [SerializeField] private Transform lightingRoot;
        [Tooltip("The child camera to shake (StartCamera/Main Camera), never the rig. Empty = Camera.main.")]
        [SerializeField] private Camera shakeCamera;
        [Tooltip("The Start screen UIDocument with the logo. Empty = the first UIDocument in the scene.")]
        [SerializeField] private UIDocument titleDocument;
        [Tooltip("Name of the logo element whose bottom edge the dust bursts from.")]
        [SerializeField] private string titleElementName = "title-block";

        [Header("Light reveal (Starting points)")]
        [Tooltip("Light level before the logo lands, as a fraction of each light's authored value (0.25 = 25%, a silhouette yard).")]
        [Range(0f, 1f)] [SerializeField] private float dimLevel = 0.25f;
        [Tooltip("Seconds from dim to full after the logo lands (ease-out, real time).")]
        [Range(0f, 3f)] [SerializeField] private float rampSeconds = 0.5f;
        [Tooltip("If the logo never lands (e.g. UI missing), ramp up anyway after this many seconds. 0 = never.")]
        [Range(0f, 30f)] [SerializeField] private float failsafeSeconds = 8f;

        [Header("Shake (Starting points)")]
        [Tooltip("Shake strength in world units at landing. A match bodyslam is 0.2 at zoom 4.5; Start is zoomed out to 6, so this is a bit bigger.")]
        [Range(0f, 1f)] [SerializeField] private float shakeAmplitude = 0.32f;
        [Tooltip("Seconds the shake lasts (decays quadratically, real time).")]
        [Range(0f, 2f)] [SerializeField] private float shakeSeconds = 0.45f;
        [Tooltip("How many times per second the shake picks a new direction.")]
        [Range(5f, 120f)] [SerializeField] private float shakeFrequency = 40f;
        [Tooltip("Move the shake in whole art pixels (retro look).")]
        [SerializeField] private bool shakeSnapToPixels = true;

        [Header("Dust burst (Starting points)")]
        [Tooltip("Lit sprite material (JuiceEffectLit), so the yard lights tint the dust like the match dust.")]
        [SerializeField] private Material dustMaterial;
        [Tooltip("Number of dust puffs at landing.")]
        [Range(0, 120)] [SerializeField] private int dustPuffs = 36;
        [Tooltip("Dust colour (match dust: 0.8, 0.68, 0.52, 0.6; a bit more opaque here so it reads at the screen edges).")]
        [SerializeField] private Color dustColor = new Color(0.8f, 0.68f, 0.52f, 0.75f);
        [Tooltip("Smallest puff at birth, art pixels.")]
        [Range(2, 60)] [SerializeField] private int dustMinPixels = 12;
        [Tooltip("Largest puff at birth, art pixels.")]
        [Range(2, 80)] [SerializeField] private int dustMaxPixels = 22;
        [Tooltip("Puff size at the end of its life, times its birth size.")]
        [Range(0.5f, 5f)] [SerializeField] private float dustEndScale = 2.4f;
        [Tooltip("Puff lifetime, seconds (each puff 75% to 115% of this).")]
        [Range(0.1f, 3f)] [SerializeField] private float dustLifetime = 0.9f;
        [Tooltip("Sideways launch speed at the logo's ends, world units per second (puffs nearer the centre go slower).")]
        [Range(0f, 30f)] [SerializeField] private float dustSpeed = 11f;
        [Tooltip("Upward launch speed, world units per second.")]
        [Range(0f, 10f)] [SerializeField] private float dustRise = 1.4f;
        [Tooltip("Fraction of speed lost per second. Distance travelled is about speed / drag.")]
        [Range(0f, 10f)] [SerializeField] private float dustDrag = 3f;
        [Tooltip("Puffs start between this fraction of the logo's half width and its ends (0 = anywhere along the edge, 1 = only at the ends).")]
        [Range(0f, 1f)] [SerializeField] private float dustInnerStart = 0.35f;
        [Tooltip("Spawn width as a multiple of the logo's width (above 1 starts the outermost puffs past the logo, where the UI doesn't cover them).")]
        [Range(0.2f, 2f)] [SerializeField] private float dustWidthScale = 1.05f;
        [Tooltip("World units added to the logo's bottom edge height (negative = lower).")]
        [Range(-3f, 3f)] [SerializeField] private float dustYOffset = 0f;
        [Tooltip("Fallback when the logo element can't be found: dust centre height in world units (x follows the camera).")]
        [SerializeField] private float fallbackDustY = -8.7f;
        [Tooltip("Fallback half width of the dust line, world units.")]
        [Range(0.5f, 15f)] [SerializeField] private float fallbackHalfWidth = 6f;
        [Tooltip("Dust sorting order (match dust uses 6; must be above the backdrop sprite).")]
        [SerializeField] private int dustSortingOrder = 6;
        [Tooltip("Draw dust on the art pixel grid.")]
        [SerializeField] private bool snapDustToPixels = true;

        private readonly List<LitLight> lights = new List<LitLight>();
        private readonly ScreenShake shake = new ScreenShake();
        private StartReveal reveal;
        private float appliedBrightness = -1f;

        private Transform shakeTarget;
        private Vector3 shakeBase;
        private Vector2 shakeOffset;
        private float shakeRetarget;

        private FxParticleList dust;
        private ParticleBatch dustBatch;
        private Texture2D puffTex;
        private bool dustDrawn;

        private void Awake()
        {
            reveal = new StartReveal(dimLevel, rampSeconds, failsafeSeconds);
            CollectLights();
            ApplyBrightness(reveal.Brightness);

            if (shakeCamera == null)
                shakeCamera = Camera.main;
            if (shakeCamera != null)
            {
                shakeTarget = shakeCamera.transform;
                shakeBase = shakeTarget.localPosition;
            }

            dust = new FxParticleList(Mathf.Max(8, dustPuffs * 2));
            puffTex = PixelTextures.Puff("StartIntroPuff");
            dustBatch = new ParticleBatch(transform, "StartIntroDust", dustMaterial, puffTex, dustSortingOrder, Mathf.Max(8, dustPuffs * 2));
        }

        private void OnEnable() => StartScreenEvents.LogoLanded += OnLogoLanded;

        private void OnDisable()
        {
            StartScreenEvents.LogoLanded -= OnLogoLanded;
            ApplyBrightness(1f);
            ResetShake();
        }

        private void OnDestroy()
        {
            StartScreenEvents.LogoLanded -= OnLogoLanded;
            dustBatch?.Destroy();
            if (puffTex != null)
                Destroy(puffTex);
        }

        private void OnLogoLanded(bool skipped)
        {
            if (reveal == null)
                return;
            if (!reveal.Land(skipped))
            {
                ApplyBrightness(reveal.Brightness);
                if (skipped)
                {
                    shake.Stop();
                    ResetShake();
                    dust.Clear();
                }
                return;
            }
            ApplyBrightness(reveal.Brightness);
            // Dust point first: it reads the camera before the shake moves it.
            SpawnDust();
            shake.Kick(shakeAmplitude, shakeSeconds);
            shakeRetarget = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            reveal.Advance(dt);
            ApplyBrightness(reveal.Brightness);
            UpdateDust(dt);
        }

        private void LateUpdate() => UpdateShake(Time.unscaledDeltaTime);

        private void CollectLights()
        {
            lights.Clear();
            Transform root = lightingRoot;
            if (root == null)
            {
                GameObject found = GameObject.Find("Lighting");
                root = found != null ? found.transform : null;
            }
            if (root == null)
            {
                Debug.LogWarning("StartIntroJuice: no 'Lighting' root; the yard light reveal is off.", this);
                return;
            }
            foreach (Light2D light in root.GetComponentsInChildren<Light2D>(true))
            {
                lights.Add(new LitLight
                {
                    Light = light,
                    Flicker = light.GetComponent<LightFlicker>(),
                    Intensity = light.intensity,
                    Volume = light.volumeIntensity,
                });
            }
        }

        private void ApplyBrightness(float b)
        {
            if (Mathf.Approximately(b, appliedBrightness))
                return;
            appliedBrightness = b;
            foreach (LitLight l in lights)
            {
                if (l.Light == null)
                    continue;
                if (l.Flicker != null)
                {
                    // LightFlicker rewrites the intensity every frame; it scales by this multiplier.
                    l.Flicker.Brightness = b;
                    continue;
                }
                l.Light.intensity = l.Intensity * b;
                if (l.Volume > 0f)
                    l.Light.volumeIntensity = l.Volume * b;
            }
        }

        private void SpawnDust()
        {
            if (dustPuffs <= 0 || dust == null)
                return;
            if (!TryLogoBottom(out Vector2 centre, out float halfWidth))
            {
                float x = shakeTarget != null ? shakeTarget.position.x : 0f;
                centre = new Vector2(x, fallbackDustY);
                halfWidth = fallbackHalfWidth;
            }
            centre.y += dustYOffset;
            halfWidth *= dustWidthScale;

            int minPx = Mathf.Max(1, Mathf.Min(dustMinPixels, dustMaxPixels));
            int maxPx = Mathf.Max(minPx, dustMaxPixels);
            for (int i = 0; i < dustPuffs; i++)
            {
                // Alternate sides so the burst is even.
                float side01 = i % 2 == 0 ? 0f : 1f;
                float f = StartIntroRules.SpawnFraction(side01, Random.value, dustInnerStart);
                float size = Random.Range(minPx, maxPx + 1) / PixelsPerUnit;
                dust.Add(new FxParticle
                {
                    Position = centre + new Vector2(f * halfWidth, Random.Range(0f, 0.15f)),
                    Velocity = StartIntroRules.DustVelocity(f, dustSpeed, dustRise, Random.value, Random.value),
                    Drag = dustDrag,
                    Lifetime = dustLifetime * Random.Range(0.75f, 1.15f),
                    StartSize = size,
                    EndSize = size * dustEndScale,
                    Color = dustColor,
                    FadeFrom = 0.35f,
                });
            }
        }

        /// <summary>World bottom-centre and half width of the logo element, from its on-screen bounds.</summary>
        private bool TryLogoBottom(out Vector2 centre, out float halfWidth)
        {
            centre = Vector2.zero;
            halfWidth = 0f;
            Camera cam = shakeCamera != null ? shakeCamera : Camera.main;
            if (cam == null)
                return false;
            UIDocument doc = titleDocument != null ? titleDocument : FindAnyObjectByType<UIDocument>();
            VisualElement root = doc != null ? doc.rootVisualElement : null;
            VisualElement title = root != null && !string.IsNullOrEmpty(titleElementName) ? root.Q(titleElementName) : null;
            if (title == null || title.panel == null)
                return false;
            Rect bounds = title.worldBound;
            Vector2 panelSize = title.panel.visualTree.layout.size;
            if (!(bounds.width > 0f) || !(panelSize.x > 0f) || !(panelSize.y > 0f) || float.IsNaN(bounds.yMax))
                return false;

            var screen = new Vector2(Screen.width, Screen.height);
            Vector2 left = StartIntroRules.PanelToScreen(new Vector2(bounds.xMin, bounds.yMax), panelSize, screen);
            Vector2 right = StartIntroRules.PanelToScreen(new Vector2(bounds.xMax, bounds.yMax), panelSize, screen);
            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 wl = cam.ScreenToWorldPoint(new Vector3(left.x, left.y, depth));
            Vector3 wr = cam.ScreenToWorldPoint(new Vector3(right.x, right.y, depth));
            centre = new Vector2((wl.x + wr.x) * 0.5f, (wl.y + wr.y) * 0.5f);
            halfWidth = Mathf.Abs(wr.x - wl.x) * 0.5f;
            return halfWidth > 0f;
        }

        private void UpdateDust(float dt)
        {
            if (dust == null || dustBatch == null)
                return;
            dust.Step(dt, null);
            if (dust.Count == 0 && !dustDrawn)
                return;
            float snap = snapDustToPixels ? PixelsPerUnit : 0f;
            dustBatch.Begin();
            for (int i = 0; i < dust.Count; i++)
            {
                FxParticle p = dust[i];
                Color32 c = p.Color;
                c.a = (byte)Mathf.RoundToInt(c.a * p.Alpha01);
                float size = p.Size;
                dustBatch.Add(p.Position, size, size, c, snap);
            }
            dustBatch.End();
            dustDrawn = dust.Count > 0;
        }

        private void UpdateShake(float realDt)
        {
            if (shakeTarget == null)
                return;
            shake.Advance(realDt);
            float strength = shake.Strength;
            if (strength <= 0f)
            {
                if (shakeOffset != Vector2.zero)
                    ResetShake();
                return;
            }
            shakeRetarget -= realDt;
            if (shakeRetarget <= 0f)
            {
                shakeRetarget = 1f / Mathf.Max(1f, shakeFrequency);
                shakeOffset = ScreenShake.Offset(1f, Random.value, Random.value);
            }
            Vector2 offset = shakeOffset * strength;
            if (shakeSnapToPixels)
                offset = ScreenShake.SnapToPixels(offset, PixelsPerUnit);
            shakeTarget.localPosition = shakeBase + new Vector3(offset.x, offset.y, 0f);
        }

        private void ResetShake()
        {
            shakeOffset = Vector2.zero;
            if (shakeTarget != null)
                shakeTarget.localPosition = shakeBase;
        }
    }
}

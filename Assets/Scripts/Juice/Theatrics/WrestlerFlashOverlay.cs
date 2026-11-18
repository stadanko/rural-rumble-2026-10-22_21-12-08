using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// The D-046 red flash for one wrestler. At runtime it adds a child "JuiceFlash" SpriteRenderer under the wrestler's
    /// animated sprite (the runtime instance only; the prefab is untouched) that copies the current sprite, flip and
    /// sorting every LateUpdate and draws it as a solid silhouette with the flash material. Because it is a separate
    /// renderer with its own material, the wrestler clips' SpriteRenderer colour curves can't fight it.
    /// It sits <see cref="SortNudge"/> world units lower so the custom-axis (y) sort draws it just in front of its own
    /// wrestler and nothing else changes order (0.001 units = 0.06 art pixels, invisible).
    /// </summary>
    public sealed class WrestlerFlashOverlay
    {
        public const float SortNudge = 0.001f;

        private readonly SpriteRenderer source;
        private readonly SpriteRenderer overlay;
        public readonly FlashEnvelope Envelope = new FlashEnvelope();

        public SpriteRenderer Source => source;
        public SpriteRenderer Overlay => overlay;

        public WrestlerFlashOverlay(SpriteRenderer source, Material flashMaterial)
        {
            this.source = source;
            if (source == null)
                return;
            var go = new GameObject("JuiceFlash");
            go.transform.SetParent(source.transform, false);
            go.transform.localPosition = new Vector3(0f, -SortNudge, 0f);
            overlay = go.AddComponent<SpriteRenderer>();
            overlay.sharedMaterial = flashMaterial;
            overlay.spriteSortPoint = source.spriteSortPoint;
            overlay.enabled = false;
        }

        public void Trigger(float peak, float hold, float fade) => Envelope.Trigger(peak, hold, fade);

        /// <summary>Advance (real seconds) and mirror the wrestler's sprite. Call in LateUpdate, after the Animator.</summary>
        public void Update(float realSeconds)
        {
            Envelope.Advance(realSeconds);
            if (overlay == null || source == null)
                return;
            float a = Envelope.Alpha;
            bool show = a > 0f && source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
            overlay.enabled = show;
            if (!show)
                return;
            overlay.sprite = source.sprite;
            overlay.flipX = source.flipX;
            overlay.flipY = source.flipY;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder;
            overlay.color = new Color(1f, 1f, 1f, a);
        }

        public void Destroy()
        {
            if (overlay != null)
                Object.Destroy(overlay.gameObject);
        }
    }
}

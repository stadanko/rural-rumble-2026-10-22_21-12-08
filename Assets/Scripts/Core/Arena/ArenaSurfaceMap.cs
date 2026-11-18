using UnityEngine;

namespace RuralRumble.Core
{
    /// <summary>
    /// Footstep surface map of the arena (D-049, ARCHITECTURE 4.8), on the root of Arena.prefab. Holds the colour-coded
    /// mask (Assets/Data/Arena/ArenaSurfaceMap.png, built by Tools/SurfaceMap/build_surface_map.py) and answers
    /// <see cref="SurfaceAt"/>. Presentation data only: no combat rule reads it. Reach it as
    /// MatchBootstrap.Current.SurfaceMap. With no mask (or an unreadable one) every point is Dirt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaSurfaceMap : MonoBehaviour
    {
        [Tooltip("Colour-coded mask aligned to the backdrop: black = Dirt, green = Grass, blue = Puddle. " +
                 "Import with Read/Write on, Point filter, no mipmaps, Non-Power of 2 None, uncompressed.")]
        [SerializeField] private Texture2D mask;

        [Tooltip("World position of the mask's top-left corner, relative to this object (the backdrop's top-left).")]
        [SerializeField] private Vector2 localTopLeft = Vector2.zero;

        [Tooltip("Native px per mask texel (D-049 map: 4). World units per texel = this / pixelsPerUnit.")]
        [SerializeField] private float nativePxPerTexel = 4f;

        [Tooltip("Art pixels per world unit (D-013: 60).")]
        [SerializeField] private float pixelsPerUnit = 60f;

        private SurfaceMap map;
        private bool built;

        /// <summary>The lookup, built from the mask on first use; null when there is no readable mask.</summary>
        public SurfaceMap Map
        {
            get
            {
                if (!built)
                    Build();
                return map;
            }
        }

        public Texture2D Mask => mask;

        /// <summary>Surface at a world point. Off the map, or with no mask, returns Dirt.</summary>
        public SurfaceType SurfaceAt(Vector2 world)
        {
            SurfaceMap m = Map;
            return m == null ? SurfaceType.Dirt : m.SurfaceAt(world);
        }

        /// <summary>Rebuilds the lookup from the mask (after replacing the mask or moving the arena in a test).</summary>
        public void Rebuild()
        {
            built = false;
            map = null;
        }

        private void Awake() => Build();

        private void Build()
        {
            built = true;
            map = null;
            if (mask == null)
                return;
            if (!mask.isReadable)
            {
                Debug.LogError($"ArenaSurfaceMap: mask '{mask.name}' is not Read/Write enabled; footsteps fall back to Dirt.", this);
                return;
            }
            Vector3 origin = transform.TransformPoint(new Vector3(localTopLeft.x, localTopLeft.y, 0f));
            map = SurfaceMap.FromPixels(mask.GetPixels32(), mask.width, mask.height,
                new Vector2(origin.x, origin.y), nativePxPerTexel / pixelsPerUnit);
        }
    }
}

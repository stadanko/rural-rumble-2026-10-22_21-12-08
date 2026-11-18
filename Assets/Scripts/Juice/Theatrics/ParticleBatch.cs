using UnityEngine;

namespace RuralRumble.Juice
{
    /// <summary>
    /// Draws a list of simulated effect particles through one Unity <see cref="ParticleSystem"/> (D-046: effects come from
    /// particle systems). The system does no simulation of its own (no emission, no velocity, very long lifetimes): Juice
    /// moves the particles in plain C# (<see cref="FxParticleList"/>, <see cref="StainBook"/>) and hands the result over
    /// every frame, so landing on the ground line, stains and combat-time following stay testable.
    /// </summary>
    public sealed class ParticleBatch
    {
        private const float KeepAlive = 1000f;

        private readonly ParticleSystem system;
        private readonly ParticleSystemRenderer renderer;
        private readonly Material material;
        private ParticleSystem.Particle[] buffer;
        private int count;

        public GameObject GameObject => system.gameObject;
        public Material Material => material;

        public ParticleBatch(Transform parent, string name, Material source, Texture2D texture, int sortingOrder, int capacity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.maxParticles = Mathf.Max(1, capacity);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = KeepAlive;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startSize3D = true;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.OldestInFront;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 10f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;

            if (source != null)
            {
                material = new Material(source) { name = name + " (runtime)", hideFlags = HideFlags.DontSave };
                if (texture != null)
                    material.mainTexture = texture;
                renderer.sharedMaterial = material;
            }

            buffer = new ParticleSystem.Particle[Mathf.Max(1, capacity)];
            go.SetActive(true);
            system.Play(true);
        }

        public int SortingOrder
        {
            get => renderer.sortingOrder;
            set => renderer.sortingOrder = value;
        }

        public void Begin() => count = 0;

        /// <summary>Queues one particle for this frame. <paramref name="pixelsPerUnit"/> &gt; 0 snaps position and size to the art pixel grid.</summary>
        public void Add(Vector2 position, float width, float height, Color32 color, float pixelsPerUnit)
        {
            if (count >= buffer.Length)
                System.Array.Resize(ref buffer, buffer.Length * 2);
            if (pixelsPerUnit > 0f)
            {
                position = new Vector2(Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit, Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit);
                width = Mathf.Max(1f, Mathf.Round(width * pixelsPerUnit)) / pixelsPerUnit;
                height = Mathf.Max(1f, Mathf.Round(height * pixelsPerUnit)) / pixelsPerUnit;
            }
            buffer[count++] = new ParticleSystem.Particle
            {
                position = new Vector3(position.x, position.y, 0f),
                velocity = Vector3.zero,
                startSize3D = new Vector3(width, height, 1f),
                startColor = color,
                startLifetime = KeepAlive,
                remainingLifetime = KeepAlive,
                rotation3D = Vector3.zero,
                randomSeed = (uint)count,
            };
        }

        public void End()
        {
            if (system == null)
                return;
            ParticleSystem.MainModule main = system.main;
            if (main.maxParticles < count)
                main.maxParticles = count;
            if (!system.isPlaying)
                system.Play(true);
            system.SetParticles(buffer, count);
        }

        public void Destroy()
        {
            if (material != null)
                Object.Destroy(material);
            if (system != null)
                Object.Destroy(system.gameObject);
        }
    }
}

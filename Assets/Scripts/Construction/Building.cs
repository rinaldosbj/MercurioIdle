using System;
using System.Collections.Generic;
using UnityEngine;

namespace MercurioIdle.Construction
{
    public abstract class Building : MonoBehaviour
    {
        private sealed class Particle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 Start;
            public Vector3 Delta;
            public float Rotation;
            public float Duration;
            public float Age;
        }

        private static readonly float[] KeyTimes = { 0f, 0.62f, 0.74f, 0.86f, 1f };
        private static readonly float[] KeyScaleX = { 1f, 1f, 1.14f, 0.95f, 1f };
        private static readonly float[] KeyScaleY = { 1f, 1f, 0.8f, 1.06f, 1f };
        private static readonly float[] KeyLift = { 0f, 0f, 0f, 0.3f, 0f };

        public static readonly List<Building> Instances = new List<Building>();

        private readonly List<Particle> particles = new List<Particle>();
        private Transform art;
        private float fallHeight;
        private float landDuration;
        private float landTime;
        private bool landing;
        private bool dustSpawned;

        public static event Action<Building> Built;

        public float Width { get; private set; }
        public float Height { get; private set; }
        public bool Landed => !landing;
        public float Left => transform.position.x - Width * 0.5f;
        public float Right => transform.position.x + Width * 0.5f;
        public float Bottom => transform.position.y;
        public float Top => transform.position.y + Height;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instances.Clear();
            Built = null;
        }

        protected static T Spawn<T>(string objectName, Sprite sprite, float width, float height, Vector3 basePosition, float fall) where T : Building
        {
            var go = new GameObject(objectName);
            go.transform.position = basePosition;
            var building = go.AddComponent<T>();
            building.Initialize(sprite, width, height, fall);
            return building;
        }

        protected virtual void OnEnable()
        {
            if (!Instances.Contains(this)) Instances.Add(this);
        }

        protected virtual void OnDisable() => Instances.Remove(this);

        protected virtual void OnBuilt()
        {
        }

        protected virtual void Update()
        {
            if (landing)
            {
                landTime += Time.deltaTime;
                float t = Mathf.Clamp01(landTime / landDuration);
                ApplyLanding(t);

                if (!dustSpawned && t >= KeyTimes[1])
                {
                    dustSpawned = true;
                    SpawnDust();
                }

                if (t >= 1f) landing = false;
            }

            UpdateParticles();
        }

        protected void AddParticle(Vector3 localStart, float size, Color color, Vector3 delta, float rotation, float duration)
        {
            var go = new GameObject("Particle");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localStart;
            go.transform.localScale = new Vector3(size, size, 1f);

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = ShapeRaster.GetSquareSprite();
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 6;

            particles.Add(new Particle
            {
                Transform = go.transform,
                Renderer = spriteRenderer,
                Start = localStart,
                Delta = delta,
                Rotation = rotation,
                Duration = duration
            });
        }

        private void Initialize(Sprite sprite, float width, float height, float fall)
        {
            Width = width;
            Height = height;

            var artObject = new GameObject("Art");
            art = artObject.transform;
            art.SetParent(transform, false);

            var spriteRenderer = artObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 4;

            fallHeight = Mathf.Max(0.3f, fall);
            landDuration = Mathf.Clamp(0.3f + Mathf.Sqrt(fallHeight * 20f) / 38f, 0.35f, 0.9f);
            landTime = 0f;
            landing = true;
            ApplyLanding(0f);

            OnBuilt();
            Built?.Invoke(this);
        }

        private void ApplyLanding(float t)
        {
            int segment = 0;
            while (segment < KeyTimes.Length - 2 && t > KeyTimes[segment + 1]) segment++;

            float k = Mathf.InverseLerp(KeyTimes[segment], KeyTimes[segment + 1], t);
            float eased = segment == 0 ? k * k : Mathf.SmoothStep(0f, 1f, k);

            float offset = segment == 0
                ? Mathf.Lerp(fallHeight, 0f, eased)
                : Mathf.Lerp(KeyLift[segment], KeyLift[segment + 1], eased);

            art.localPosition = new Vector3(0f, offset, 0f);
            art.localScale = new Vector3(
                Mathf.Lerp(KeyScaleX[segment], KeyScaleX[segment + 1], eased),
                Mathf.Lerp(KeyScaleY[segment], KeyScaleY[segment + 1], eased),
                1f);
        }

        private void SpawnDust()
        {
            float half = Width * 0.5f;
            Vector3 left = new Vector3(-1.3f, 0.6f, 0f);
            Vector3 right = new Vector3(1.3f, 0.6f, 0f);

            AddParticle(new Vector3(-half - 0.15f, 0.15f, -0.05f), 0.3f, ShapeRaster.Gray(181), left, -90f, 0.55f);
            AddParticle(new Vector3(-half + 0.1f, 0.4f, -0.05f), 0.2f, ShapeRaster.Gray(122), left, -90f, 0.45f);
            AddParticle(new Vector3(half + 0.15f, 0.15f, -0.05f), 0.3f, ShapeRaster.Gray(181), right, 90f, 0.55f);
            AddParticle(new Vector3(half - 0.1f, 0.4f, -0.05f), 0.2f, ShapeRaster.Gray(122), right, 90f, 0.45f);
        }

        private void UpdateParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var particle = particles[i];
                particle.Age += Time.deltaTime;
                float t = Mathf.Clamp01(particle.Age / particle.Duration);
                float eased = 1f - (1f - t) * (1f - t);

                particle.Transform.localPosition = particle.Start + particle.Delta * eased;
                particle.Transform.localRotation = Quaternion.Euler(0f, 0f, particle.Rotation * eased);

                Color color = particle.Renderer.color;
                color.a = 1f - t;
                particle.Renderer.color = color;

                if (t >= 1f)
                {
                    Destroy(particle.Transform.gameObject);
                    particles.RemoveAt(i);
                }
            }
        }
    }
}

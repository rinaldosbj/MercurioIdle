using System.Collections.Generic;
using UnityEngine;

namespace MercurioIdle.Construction
{
    public sealed class RobotFactory : MonoBehaviour
    {
        private sealed class Dust
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

        public static readonly List<RobotFactory> All = new List<RobotFactory>();

        [SerializeField] private int robotsProvided = 3;

        private readonly List<Dust> dust = new List<Dust>();
        private Transform art;
        private float fallHeight;
        private float landDuration;
        private float landTime;
        private bool landing;
        private bool dustSpawned;

        public int RobotsProvided => robotsProvided;
        public float Width => FactoryArt.WorldWidth;
        public float Height => FactoryArt.WorldHeight;
        public float Left => transform.position.x - Width * 0.5f;
        public float Right => transform.position.x + Width * 0.5f;
        public float Bottom => transform.position.y;
        public float Top => transform.position.y + Height;
        public Vector3 DoorPoint => new Vector3(Left + Width * FactoryArt.DoorCenter, Bottom + 0.8f, transform.position.z);
        public Vector3 HomePoint => new Vector3(transform.position.x, Top + 1.5f, transform.position.z);

        public static int TotalRobotCapacity
        {
            get
            {
                int total = 0;
                foreach (var factory in All) total += factory.robotsProvided;
                return total;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        public static RobotFactory Create(Vector3 basePosition, float fall)
        {
            var go = new GameObject("Fábrica");
            go.transform.position = basePosition;
            var factory = go.AddComponent<RobotFactory>();
            factory.Build(fall);
            return factory;
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        private void OnDisable() => All.Remove(this);

        private void Build(float fall)
        {
            var artObject = new GameObject("Art");
            art = artObject.transform;
            art.SetParent(transform, false);

            var spriteRenderer = artObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = FactoryArt.GetFactorySprite();
            spriteRenderer.sortingOrder = 4;

            fallHeight = Mathf.Max(0.3f, fall);
            landDuration = Mathf.Clamp(0.3f + Mathf.Sqrt(fallHeight * 20f) / 38f, 0.35f, 0.9f);
            landTime = 0f;
            landing = true;
            ApplyLanding(0f);
        }

        private void Update()
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

            UpdateDust();
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

            AddDust(new Vector3(-half - 0.15f, 0.15f, -0.05f), 0.3f, Gray(181), left, -90f, 0.55f);
            AddDust(new Vector3(-half + 0.1f, 0.4f, -0.05f), 0.2f, Gray(122), left, -90f, 0.45f);
            AddDust(new Vector3(half + 0.15f, 0.15f, -0.05f), 0.3f, Gray(181), right, 90f, 0.55f);
            AddDust(new Vector3(half - 0.1f, 0.4f, -0.05f), 0.2f, Gray(122), right, 90f, 0.45f);
        }

        private void AddDust(Vector3 localStart, float size, Color color, Vector3 delta, float rotation, float duration)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localStart;
            go.transform.localScale = new Vector3(size, size, 1f);

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = FactoryArt.GetSquareSprite();
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 6;

            dust.Add(new Dust
            {
                Transform = go.transform,
                Renderer = spriteRenderer,
                Start = localStart,
                Delta = delta,
                Rotation = rotation,
                Duration = duration
            });
        }

        private void UpdateDust()
        {
            for (int i = dust.Count - 1; i >= 0; i--)
            {
                var piece = dust[i];
                piece.Age += Time.deltaTime;
                float t = Mathf.Clamp01(piece.Age / piece.Duration);
                float eased = 1f - (1f - t) * (1f - t);

                piece.Transform.localPosition = piece.Start + piece.Delta * eased;
                piece.Transform.localRotation = Quaternion.Euler(0f, 0f, piece.Rotation * eased);

                Color color = piece.Renderer.color;
                color.a = 1f - t;
                piece.Renderer.color = color;

                if (t >= 1f)
                {
                    Destroy(piece.Transform.gameObject);
                    dust.RemoveAt(i);
                }
            }
        }

        private static Color Gray(int value)
        {
            float v = value / 255f;
            return new Color(v, v, v, 1f);
        }
    }
}

using UnityEngine;
using MercurioIdle.Planet;

namespace MercurioIdle.Robots
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Minion : MonoBehaviour
    {
        private enum State { Pause, Wander, GoToBlock, Mining }

        private const int TextureSize = 96;
        private const float SpriteWorldSize = 0.6f;
        private const float TurnSeconds = 0.07f;
        private const float PulsePeriod = 0.3f;
        private const float PulseAmount = 0.35f;
        private const float SpawnSeconds = 0.5f;

        [Header("Movimento")]
        [SerializeField] private float flySpeed = 3.5f;
        [SerializeField] private float wanderSpeed = 2f;
        [SerializeField] private float arriveDistance = 0.08f;
        [SerializeField] private float searchSeconds = 0.25f;

        [Header("Passeio")]
        [SerializeField] private float wanderRadius = 4f;
        [SerializeField] private float minWanderDistance = 1f;
        [SerializeField] private Vector2 pauseSeconds = new Vector2(0.4f, 1.6f);
        [SerializeField] private float screenMargin = 1f;

        [Header("Escavação")]
        [SerializeField] private float digSeconds = 1.2f;

        [Header("Caminho")]
        [SerializeField] private bool showPath = true;
        [SerializeField] private float pathWidth = 0.08f;
        [SerializeField] private Color wanderPathColor = new Color(0.71f, 0.71f, 0.71f, 0.85f);
        [SerializeField] private Color digPathColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        [Header("Visual")]
        [SerializeField] private bool useDefaultTriangle = true;
        [SerializeField] private float size = 0.6f;

        private static Sprite triangleSprite;
        private static Texture2D dashTexture;
        private static Sprite dotSprite;

        private SpriteRenderer spriteRenderer;
        private Camera cam;
        private Vector3 baseScale = Vector3.one;
        private State state = State.Pause;

        private Vector3 home;
        private bool homeAssigned;
        private Vector3 wanderTarget;
        private float pauseTimer;

        private IPlanetBlock job;
        private IPlanetLevel jobLevel;
        private Vector3 jobWorld;
        private float digProgress;
        private float searchTimer;
        private int lastMarksVersion;

        private float currentAngle = -180f;
        private float desiredAngle;
        private float pulseTime;
        private float bornTime;

        private GameObject pathRoot;
        private LineRenderer pathLine;
        private SpriteRenderer pathMarker;
        private Material pathMaterial;
        private bool pathVisible;

        public void SetHome(Vector3 position)
        {
            home = position;
            homeAssigned = true;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            cam = Camera.main;

            if (useDefaultTriangle || spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = GetTriangleSprite();
                spriteRenderer.color = Color.white;
                baseScale = Vector3.one * (size / SpriteWorldSize);
            }
            else
            {
                baseScale = transform.localScale;
            }

            spriteRenderer.enabled = true;
            spriteRenderer.sortingOrder = 10;
            bornTime = Time.time;
            transform.localScale = Vector3.zero;

            BuildPathVisuals();
        }

        private void Start()
        {
            var designations = DigDesignations.Instance;
            if (designations != null && designations.Terrain != null)
            {
                float frontZ = designations.Terrain.transform.position.z - 0.5f;
                Vector3 current = transform.position;
                if (current.z > frontZ)
                {
                    current.z = frontZ;
                    transform.position = current;
                }
            }

            if (!homeAssigned) home = transform.position;
            pauseTimer = Random.Range(0f, pauseSeconds.y);
        }

        private void OnDisable() => CancelJob();

        private void OnDestroy()
        {
            if (pathRoot != null) Destroy(pathRoot);
            if (pathMaterial != null) Destroy(pathMaterial);
        }

        private void Update()
        {
            var designations = DigDesignations.Instance;
            var level = designations != null && designations.Terrain != null ? designations.Terrain.Level : null;

            pathVisible = false;

            if (job != null && (level == null || level != jobLevel || job.IsRemoved || !designations.IsMarked(job)))
            {
                CancelJob();
                state = State.Pause;
                pauseTimer = 0f;
            }

            if (job == null && level != null)
            {
                searchTimer -= Time.deltaTime;
                if (designations.Version != lastMarksVersion)
                {
                    lastMarksVersion = designations.Version;
                    searchTimer = 0f;
                }

                if (searchTimer <= 0f)
                {
                    searchTimer = searchSeconds;
                    if (designations.TryClaimNearest(transform.position, out var block, out var world))
                    {
                        job = block;
                        jobLevel = level;
                        jobWorld = world;
                        digProgress = 0f;
                        state = State.GoToBlock;
                    }
                }
            }

            bool digging = false;

            switch (state)
            {
                case State.Pause:
                    pauseTimer -= Time.deltaTime;
                    if (pauseTimer <= 0f)
                    {
                        if (PickWanderTarget()) state = State.Wander;
                        else pauseTimer = Random.Range(pauseSeconds.x, pauseSeconds.y);
                    }
                    break;

                case State.Wander:
                    if (Reached(wanderTarget))
                    {
                        state = State.Pause;
                        pauseTimer = Random.Range(pauseSeconds.x, pauseSeconds.y);
                    }
                    else
                    {
                        Fly(wanderTarget, wanderSpeed);
                        ShowPath(transform.position, wanderTarget, wanderPathColor);
                    }
                    break;

                case State.GoToBlock:
                    if (job == null)
                    {
                        state = State.Pause;
                        pauseTimer = 0f;
                        break;
                    }

                    if (Reached(jobWorld))
                    {
                        state = State.Mining;
                        digProgress = 0f;
                    }
                    else
                    {
                        Fly(jobWorld, flySpeed);
                        ShowPath(transform.position, jobWorld, digPathColor);
                    }
                    break;

                case State.Mining:
                    if (job == null)
                    {
                        state = State.Pause;
                        pauseTimer = 0f;
                        break;
                    }

                    digging = true;
                    desiredAngle = 180f;
                    digProgress += Time.deltaTime;

                    if (digProgress >= digSeconds)
                    {
                        jobLevel.RemoveBlock(job);
                        CancelJob();
                        searchTimer = 0f;
                        state = State.Pause;
                        pauseTimer = 0f;
                    }
                    break;
            }

            if (!pathVisible) SetPathActive(false);
            ApplyVisuals(digging);
        }

        private bool Reached(Vector3 target)
        {
            Vector3 delta = target - transform.position;
            return new Vector2(delta.x, delta.y).magnitude <= arriveDistance;
        }

        private void Fly(Vector3 target, float speed)
        {
            Vector3 position = transform.position;
            target.z = position.z;
            Vector3 delta = target - position;
            desiredAngle = Vector2.SignedAngle(Vector2.up, new Vector2(delta.x, delta.y));
            transform.position = Vector3.MoveTowards(position, target, speed * Time.deltaTime);
        }

        private bool PickWanderTarget()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                if (offset.magnitude < minWanderDistance) continue;

                Vector3 candidate = home + new Vector3(offset.x, offset.y, 0f);
                candidate.z = transform.position.z;
                candidate = ClampToScreen(candidate);

                if (!IsSolid(candidate))
                {
                    wanderTarget = candidate;
                    return true;
                }
            }

            if (IsSolid(home)) home = FindFreePointAbove(home);
            return false;
        }

        private static Vector3 FindFreePointAbove(Vector3 point)
        {
            for (int step = 0; step < 160; step++)
            {
                if (!IsSolid(point)) return point;
                point.y += 0.5f;
            }

            return point;
        }

        private Vector3 ClampToScreen(Vector3 point)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return point;

            float depth = Mathf.Abs(point.z - cam.transform.position.z);
            Vector3 min = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 max = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));

            Vector3 here = transform.position;
            if (here.x < min.x || here.x > max.x || here.y < min.y || here.y > max.y) return point;

            point.x = Mathf.Clamp(point.x, min.x + screenMargin, max.x - screenMargin);
            point.y = Mathf.Clamp(point.y, min.y + screenMargin, max.y - screenMargin);
            return point;
        }

        private static bool IsSolid(Vector3 world)
        {
            var designations = DigDesignations.Instance;
            if (designations == null || designations.Terrain == null) return false;

            var terrain = designations.Terrain;
            var level = terrain.Level;
            var projection = terrain.Projection as PlanetProjection;
            if (level == null || projection == null) return false;

            try
            {
                Vector3 local = terrain.transform.InverseTransformPoint(world);
                var position = projection.FromLocal(new Vector3(local.x, local.y, 0f));
                return level.TryGetBlock(position, out _);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private void ApplyVisuals(bool digging)
        {
            float age = Time.time - bornTime;
            if (age < SpawnSeconds)
            {
                float t = age / SpawnSeconds;
                float scale;
                float angle;
                if (t < 0.6f)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
                    scale = Mathf.Lerp(0f, 1.7f, k);
                    angle = Mathf.Lerp(-180f, 0f, k);
                }
                else
                {
                    scale = Mathf.Lerp(1.7f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
                    angle = 0f;
                }

                currentAngle = angle;
                transform.localScale = baseScale * scale;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
                return;
            }

            currentAngle = Mathf.LerpAngle(currentAngle, desiredAngle, 1f - Mathf.Exp(-Time.deltaTime / TurnSeconds));
            transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);

            float pulse = 1f;
            if (digging)
            {
                pulseTime += Time.deltaTime;
                pulse = 1f + PulseAmount * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * pulseTime / PulsePeriod));
            }
            else
            {
                pulseTime = 0f;
            }

            transform.localScale = baseScale * pulse;
        }

        private void CancelJob()
        {
            if (job != null && DigDesignations.Instance != null) DigDesignations.Instance.Release(job);
            job = null;
            jobLevel = null;
            digProgress = 0f;
        }

        private void BuildPathVisuals()
        {
            if (!showPath) return;

            if (dashTexture == null)
            {
                dashTexture = new Texture2D(4, 1, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };
                dashTexture.SetPixels(new[] { Color.white, Color.white, Color.clear, Color.clear });
                dashTexture.Apply();
            }

            if (dotSprite == null)
            {
                const int dotSize = 16;
                var texture = new Texture2D(dotSize, dotSize, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color[dotSize * dotSize];
                float center = (dotSize - 1) * 0.5f;
                for (int y = 0; y < dotSize; y++)
                {
                    for (int x = 0; x < dotSize; x++)
                    {
                        float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                        pixels[y * dotSize + x] = distance <= dotSize * 0.5f - 0.5f ? Color.white : Color.clear;
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                dotSprite = Sprite.Create(texture, new Rect(0, 0, dotSize, dotSize), new Vector2(0.5f, 0.5f), dotSize);
            }

            pathRoot = new GameObject("Minion path");

            pathMaterial = new Material(spriteRenderer.sharedMaterial) { mainTexture = dashTexture };
            pathLine = pathRoot.AddComponent<LineRenderer>();
            pathLine.useWorldSpace = true;
            pathLine.positionCount = 2;
            pathLine.widthMultiplier = pathWidth;
            pathLine.textureMode = LineTextureMode.Tile;
            pathLine.numCapVertices = 0;
            pathLine.sortingOrder = 8;
            pathLine.sharedMaterial = pathMaterial;

            var markerObject = new GameObject("Destination");
            markerObject.transform.SetParent(pathRoot.transform, false);
            markerObject.transform.localScale = Vector3.one * 0.25f;
            pathMarker = markerObject.AddComponent<SpriteRenderer>();
            pathMarker.sprite = dotSprite;
            pathMarker.sortingOrder = 9;

            pathRoot.SetActive(false);
        }

        private void ShowPath(Vector3 from, Vector3 to, Color color)
        {
            if (pathRoot == null) return;

            pathVisible = true;
            SetPathActive(true);

            from.z = transform.position.z;
            to.z = transform.position.z;
            pathLine.SetPosition(0, from);
            pathLine.SetPosition(1, to);
            pathLine.startColor = color;
            pathLine.endColor = color;
            pathMarker.transform.position = to;
            pathMarker.color = color;
        }

        private void SetPathActive(bool active)
        {
            if (pathRoot != null && pathRoot.activeSelf != active) pathRoot.SetActive(active);
        }

        private static Sprite GetTriangleSprite()
        {
            if (triangleSprite != null) return triangleSprite;

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            const int samples = 4;
            var pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / samples) / TextureSize;
                            float v = (y + (sy + 0.5f) / samples) / TextureSize;
                            if (v <= 1f - Mathf.Abs(2f * u - 1f)) covered++;
                        }
                    }

                    pixels[y * TextureSize + x] = new Color(0.949f, 0.949f, 0.949f, covered / (float)(samples * samples));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            triangleSprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize / SpriteWorldSize);
            return triangleSprite;
        }
    }
}

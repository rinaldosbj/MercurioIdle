using UnityEngine;
using MercurioIdle.Planet;

namespace MercurioIdle.Robots
{
    /// <summary>
    /// Minion: anda pela superfície com gravidade (cai em direção ao centro do planeta),
    /// passeia sem sair da tela e, quando há blocos marcados, voa até eles e escava.
    /// Mostra uma linha tracejada com um ponto indicando para onde está indo.
    /// Use este script no prefab do minion (precisa de um SpriteRenderer).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Minion : MonoBehaviour
    {
        private enum State { Pause, Wander, GoToBlock, Mining }

        [Header("Andar na superfície")]
        [SerializeField] private float wanderSpeed = 1.5f;
        [SerializeField, Tooltip("Distância máxima (m) de cada passeio a partir do ponto de referência.")]
        private float wanderRadius = 3f;
        [SerializeField, Tooltip("Tempo parado entre um passeio e outro: mínimo e máximo (s).")]
        private Vector2 pauseSeconds = new Vector2(0.5f, 2f);
        [SerializeField, Tooltip("Folga (m) entre o minion e a borda da tela.")]
        private float screenMargin = 1f;

        [Header("Gravidade")]
        [SerializeField, Tooltip("Aceleração da queda (m/s²).")]
        private float gravity = 20f;
        [SerializeField, Tooltip("Velocidade máxima de queda (m/s).")]
        private float maxFallSpeed = 20f;

        [Header("Escavar")]
        [SerializeField, Tooltip("Velocidade de voo até o bloco marcado.")]
        private float digSpeed = 4f;
        [SerializeField, Tooltip("Segundos para quebrar um bloco.")]
        private float mineSeconds = 1f;
        [SerializeField, Tooltip("Distância (m) do centro do bloco para começar a minerar.")]
        private float reach = 0.8f;
        [SerializeField, Tooltip("Intervalo (s) entre buscas por blocos marcados.")]
        private float searchInterval = 0.5f;

        [Header("Caminho")]
        [SerializeField, Tooltip("Mostra uma linha tracejada e um ponto no destino.")]
        private bool showPath = true;
        [SerializeField] private float pathWidth = 0.08f;
        [SerializeField] private Color walkPathColor = new Color(0.4f, 0.9f, 1f, 0.9f);
        [SerializeField] private Color digPathColor = new Color(1f, 0.8f, 0.2f, 0.95f);

        [Header("Visual")]
        [SerializeField, Tooltip("Marque se o desenho do sprite olha para a direita. Se ele andar de costas, desmarque.")]
        private bool spriteFacesRight = true;

        private SpriteRenderer spriteRenderer;
        private Camera cam;
        private State state = State.Pause;

        // Terreno (atualizado a cada frame)
        private PlanetExcavationPreview terrain;
        private IPlanetLevel level;
        private PlanetProjection projection;
        private Vector3 up = Vector3.up;       // "para cima" = contra o centro do planeta
        private Vector3 right = Vector3.right; // ao longo da superfície

        // Corpo
        private float feetOffset;  // do centro do sprite até os pés
        private float halfWidth;
        private float fallSpeed;

        // Passeio
        private Vector3 anchor;
        private Vector3 wanderTarget;
        private float pauseTimer;

        // Trabalho
        private IPlanetBlock job;
        private IPlanetLevel jobLevel;
        private Vector3 jobWorld;
        private float progress;
        private float searchTimer;
        private int lastMarksVersion;

        // Indicador de caminho
        private GameObject pathRoot;
        private LineRenderer pathLine;
        private SpriteRenderer pathMarker;
        private Material pathMaterial;
        private bool pathVisible;
        private static Texture2D dashTexture;
        private static Sprite dotSprite;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 10;
            cam = Camera.main;
            BuildPathVisuals();
        }

        private void Start()
        {
            anchor = transform.position;
            pauseTimer = Random.Range(0f, pauseSeconds.y);

            // Tamanho do corpo tirado do sprite (já considera a escala do prefab)
            Bounds bounds = spriteRenderer.bounds;
            feetOffset = Mathf.Max(0f, transform.position.y - bounds.min.y);
            halfWidth = bounds.extents.x;
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
            terrain = designations != null ? designations.Terrain : null;
            level = terrain != null ? terrain.Level : null;
            projection = terrain != null ? terrain.Projection as PlanetProjection : null;
            ComputeFrame();

            pathVisible = false;

            // O trabalho acabou: bloco removido, desmarcado, ou o terreno foi regenerado.
            if (job != null && (level == null || level != jobLevel || job.IsRemoved || !designations.IsMarked(job)))
                CancelJob();

            // Sem trabalho: ver se há algum bloco marcado e disponível
            if (job == null && level != null)
            {
                searchTimer -= Time.deltaTime;
                if (designations.Version != lastMarksVersion)
                {
                    lastMarksVersion = designations.Version; // bloco novo marcado: reage na hora
                    searchTimer = 0f;
                }
                if (searchTimer <= 0f)
                {
                    searchTimer = searchInterval;
                    if (designations.TryClaimNearest(transform.position, out var block, out var world))
                    {
                        job = block;
                        jobLevel = level;
                        jobWorld = world;
                        progress = 0f;
                        fallSpeed = 0f;
                        state = State.GoToBlock;
                    }
                }
            }

            switch (state)
            {
                case State.Pause:
                    ApplyGravity();
                    pauseTimer -= Time.deltaTime;
                    if (pauseTimer <= 0f)
                    {
                        PickWanderTarget();
                        state = State.Wander;
                    }
                    break;

                case State.Wander:
                    ApplyGravity();
                    Walk();
                    break;

                case State.GoToBlock:
                    if (job == null) { state = State.Pause; pauseTimer = 0f; break; }
                    if (Reached(jobWorld, reach)) { state = State.Mining; progress = 0f; }
                    else
                    {
                        MoveToward(jobWorld, digSpeed);
                        ShowPath(transform.position, jobWorld, digPathColor);
                    }
                    break;

                case State.Mining:
                    if (job == null) { state = State.Pause; pauseTimer = 0f; break; }
                    Face(jobWorld.x - transform.position.x);
                    progress += Time.deltaTime;
                    if (progress >= mineSeconds)
                    {
                        jobLevel.RemoveBlock(job);
                        CancelJob();
                        anchor = transform.position; // próximos passeios começam aqui
                        fallSpeed = 0f;
                        state = State.Pause;
                        pauseTimer = 0f;
                        searchTimer = 0f;            // procura o próximo bloco já
                    }
                    break;
            }

            if (!pathVisible) SetPathActive(false);
        }

        // ---------- Gravidade e andar ----------

        // "Para cima" e "para o lado" seguindo a curvatura do planeta na posição do minion.
        private void ComputeFrame()
        {
            up = Vector3.up;
            right = Vector3.right;
            if (terrain == null || projection == null) return;

            Vector3 local = terrain.transform.InverseTransformPoint(transform.position);
            PlanetPosition p = projection.FromLocal(new Vector3(local.x, local.y, 0f));
            float c = (float)System.Math.Cos(p.AngleRadians);
            float s = (float)System.Math.Sin(p.AngleRadians);
            up = terrain.transform.TransformDirection(new Vector3(c, s, 0f));
            right = terrain.transform.TransformDirection(new Vector3(s, -c, 0f));
        }

        // Existe bloco sólido neste ponto do mundo?
        private bool IsSolid(Vector3 world)
        {
            if (level == null || projection == null) return false;
            try
            {
                Vector3 local = terrain.transform.InverseTransformPoint(world);
                PlanetPosition position = projection.FromLocal(new Vector3(local.x, local.y, 0f));
                return level.TryGetBlock(position, out _);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private void ApplyGravity()
        {
            if (level == null || projection == null) return;

            Vector3 feet = transform.position - up * feetOffset;

            if (IsSolid(feet - up * 0.05f)) // chão logo abaixo dos pés
            {
                fallSpeed = 0f;
                // se afundou um pouco no chão, empurra para cima
                int guard = 0;
                while (IsSolid(transform.position - up * feetOffset) && guard++ < 20)
                    transform.position += up * 0.05f;
                return;
            }

            fallSpeed = Mathf.Min(fallSpeed + gravity * Time.deltaTime, maxFallSpeed);
            transform.position -= up * (fallSpeed * Time.deltaTime);
        }

        private void Walk()
        {
            float remaining = Vector3.Dot(wanderTarget - transform.position, right);
            if (Mathf.Abs(remaining) <= 0.05f) { StartPause(); return; }

            float direction = Mathf.Sign(remaining);
            Face(right.x * direction);

            // Parede à frente: para e escolhe outro rumo depois
            Vector3 feet = transform.position - up * feetOffset;
            Vector3 probe = feet + up * 0.3f + right * (direction * (halfWidth + 0.05f));
            if (IsSolid(probe)) { StartPause(); return; }

            float step = Mathf.Min(wanderSpeed * Time.deltaTime, Mathf.Abs(remaining));
            transform.position += right * (direction * step);

            remaining -= direction * step;
            Vector3 from = transform.position - up * (feetOffset - 0.05f);
            ShowPath(from, from + right * remaining, walkPathColor);
        }

        private void StartPause()
        {
            state = State.Pause;
            pauseTimer = Random.Range(pauseSeconds.x, pauseSeconds.y);
        }

        private void PickWanderTarget()
        {
            float distance = Random.Range(wanderRadius * 0.25f, wanderRadius);
            float side = Random.value < 0.5f ? -1f : 1f;
            wanderTarget = ClampToScreen(anchor + right * (distance * side));
        }

        // ---------- Voo até o bloco ----------

        private bool Reached(Vector3 target, float distance) =>
            Vector2.Distance(transform.position, target) <= distance;

        private void MoveToward(Vector3 target, float speed)
        {
            Vector3 position = transform.position;
            Vector3 goal = new Vector3(target.x, target.y, position.z); // mantém o Z (fica na frente do terreno)
            Face(goal.x - position.x);
            transform.position = Vector3.MoveTowards(position, goal, speed * Time.deltaTime);
        }

        // Vira o sprite para o lado em que está indo
        private void Face(float deltaX)
        {
            if (Mathf.Abs(deltaX) < 0.02f) return;
            bool goingRight = deltaX > 0f;
            spriteRenderer.flipX = spriteFacesRight ? !goingRight : goingRight;
        }

        private Vector3 ClampToScreen(Vector3 point)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return point;

            float depth = Mathf.Abs(point.z - cam.transform.position.z);
            Vector3 min = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 max = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            point.x = Mathf.Clamp(point.x, min.x + screenMargin, max.x - screenMargin);
            point.y = Mathf.Clamp(point.y, min.y + screenMargin, max.y - screenMargin);
            return point;
        }

        // ---------- Trabalho ----------

        private void CancelJob()
        {
            if (job != null && DigDesignations.Instance != null) DigDesignations.Instance.Release(job);
            job = null;
            jobLevel = null;
            progress = 0f;
        }

        // ---------- Indicador de caminho ----------

        private void BuildPathVisuals()
        {
            if (!showPath) return;

            if (dashTexture == null)
            {
                // 4x1: metade preenchida, metade vazia => tracejado de 0,5 m
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
                const int size = 16;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color[size * size];
                float center = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                        pixels[y * size + x] = distance <= size * 0.5f - 0.5f ? Color.white : Color.clear;
                    }
                texture.SetPixels(pixels);
                texture.Apply();
                dotSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
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
            markerObject.transform.localScale = Vector3.one * 0.35f;
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
    }
}

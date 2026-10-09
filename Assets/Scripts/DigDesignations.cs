using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using MercurioIdle.Planet;

namespace MercurioIdle.Robots
{
    /// <summary>
    /// Guarda os blocos que o jogador marcou para escavar, desenha a marcação
    /// (amarela = pronto para escavar, vermelha = esperando ficar exposto)
    /// e distribui os blocos para os minions (cada bloco só vai para um minion por vez).
    /// Só existe um na cena. É adicionado automaticamente junto com o DigSelectionController.
    /// </summary>
    public sealed class DigDesignations : MonoBehaviour
    {
        public static DigDesignations Instance { get; private set; }

        [Header("Terreno")]
        [SerializeField, Tooltip("Objeto com o PlanetExcavationPreview. Se vazio, procura na cena.")]
        private PlanetExcavationPreview terrain;
        [SerializeField, Tooltip("Nível das folhas = DetailLevels - 1 (nas configurações do terreno). Padrão: 8 níveis => 7.")]
        private int leafDepth = 7;

        [Header("Visual da marcação")]
        [FormerlySerializedAs("markColor")]
        [SerializeField, Tooltip("Amarelo: o minion já pode escavar este bloco.")]
        private Color readyColor = new Color(1f, 0.85f, 0.1f, 0.45f);
        [SerializeField, Tooltip("Vermelho: bloco marcado, mas ainda coberto por outro bloco. Os minions esperam ele ficar exposto.")]
        private Color waitingColor = new Color(0.9f, 0.25f, 0.2f, 0.35f);

        [Header("Ajustes")]
        [SerializeField, Tooltip("Quantas marcações têm a cor reavaliada por frame.")]
        private int refreshPerFrame = 100;
        [SerializeField, Tooltip("Depois deste tempo (s) uma reserva esquecida é liberada para outro minion.")]
        private float claimTimeoutSeconds = 30f;

        public PlanetExcavationPreview Terrain => terrain;
        public int LeafDepth => leafDepth;

        /// <summary>Aumenta a cada bloco marcado. Os minions usam isso para reagir na hora.</summary>
        public int Version { get; private set; }

        private readonly Dictionary<PlanetBlockAddress, IPlanetBlock> marked = new Dictionary<PlanetBlockAddress, IPlanetBlock>();
        private readonly Dictionary<PlanetBlockAddress, SpriteRenderer> marks = new Dictionary<PlanetBlockAddress, SpriteRenderer>();
        private readonly Dictionary<PlanetBlockAddress, float> claimedAt = new Dictionary<PlanetBlockAddress, float>();
        private readonly List<PlanetBlockAddress> refreshQueue = new List<PlanetBlockAddress>();
        private int refreshIndex;
        private bool refreshRequested;
        private float refreshTimer;

        private IPlanetLevel level;
        private Sprite readySprite;
        private Sprite waitingSprite;

        private PlanetProjection Projection => terrain != null ? terrain.Projection as PlanetProjection : null;

        private void Awake()
        {
            Instance = this;
            if (terrain == null) terrain = FindFirstObjectByType<PlanetExcavationPreview>();
            readySprite = CreateMarkSprite(readyColor);
            waitingSprite = CreateMarkSprite(waitingColor);
        }

        private void Update()
        {
            // Se o terreno foi regenerado, as marcações antigas deixam de valer.
            var current = terrain != null ? terrain.Level : null;
            if (current != level) Rebind(current);

            // Reavalia as cores de tempos em tempos (a exposição muda quando blocos são removidos).
            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 1f;
                refreshRequested = true;
            }
            RefreshMarkSprites();
        }

        private void OnDestroy()
        {
            if (level != null) level.BlockRemoved -= OnBlockRemoved;
            if (Instance == this) Instance = null;
        }

        private void Rebind(IPlanetLevel newLevel)
        {
            if (level != null) level.BlockRemoved -= OnBlockRemoved;
            ClearAll();
            level = newLevel;
            if (level != null) level.BlockRemoved += OnBlockRemoved;
        }

        private void OnBlockRemoved(IPlanetBlock block)
        {
            Unmark(block.Address);
            refreshRequested = true; // blocos abaixo podem ter ficado expostos
        }

        // ---------- Marcar / desmarcar ----------

        public bool IsMarked(IPlanetBlock block) => block != null && marked.ContainsKey(block.Address);

        public void Mark(IPlanetBlock block)
        {
            if (level == null || block == null || block.IsRemoved) return;
            var address = block.Address;
            if (marked.ContainsKey(address)) return;

            marked[address] = block;
            marks[address] = CreateMark(block);
            Version++;
            refreshRequested = true;
        }

        public void Unmark(PlanetBlockAddress address)
        {
            marked.Remove(address);
            claimedAt.Remove(address);
            if (marks.TryGetValue(address, out var renderer))
            {
                if (renderer != null) Destroy(renderer.gameObject);
                marks.Remove(address);
            }
        }

        public void Toggle(IPlanetBlock block)
        {
            if (IsMarked(block)) Unmark(block.Address);
            else Mark(block);
        }

        private void ClearAll()
        {
            foreach (var renderer in marks.Values)
                if (renderer != null) Destroy(renderer.gameObject);
            marks.Clear();
            marked.Clear();
            claimedAt.Clear();
            refreshQueue.Clear();
            refreshIndex = 0;
            Version++;
        }

        // ---------- Cor da marcação ----------

        // Amarelo = pode escavar agora; vermelho = ainda coberto. Processa em partes para não pesar.
        private void RefreshMarkSprites()
        {
            if (level == null) return;

            if (refreshIndex >= refreshQueue.Count)
            {
                if (!refreshRequested) return;
                refreshRequested = false;
                refreshQueue.Clear();
                refreshQueue.AddRange(marked.Keys);
                refreshIndex = 0;
            }

            int processed = 0;
            while (refreshIndex < refreshQueue.Count && processed < refreshPerFrame)
            {
                var address = refreshQueue[refreshIndex++];
                processed++;

                if (!marked.TryGetValue(address, out var block)) continue;
                if (!marks.TryGetValue(address, out var renderer) || renderer == null) continue;

                renderer.sprite = level.CanRemoveBlock(block) ? readySprite : waitingSprite;
            }
        }

        // ---------- Trabalho para os minions ----------

        /// <summary>
        /// Reserva o bloco marcado mais próximo que já pode ser escavado (exposto).
        /// Blocos marcados que ainda estão cobertos esperam até ficarem expostos.
        /// </summary>
        public bool TryClaimNearest(Vector3 fromWorld, out IPlanetBlock best, out Vector3 bestWorld)
        {
            best = null;
            bestWorld = default;
            if (level == null || Projection == null) return false;

            float bestDistance = float.MaxValue;
            foreach (var pair in marked)
            {
                // Reservado por outro minion (reservas esquecidas expiram sozinhas)
                if (claimedAt.TryGetValue(pair.Key, out float since) && Time.time - since < claimTimeoutSeconds)
                    continue;

                var block = pair.Value;
                Vector3 world = GetWorldCenter(block);
                float distance = Vector2.Distance(world, fromWorld);
                if (distance >= bestDistance) continue;
                if (!level.CanRemoveBlock(block)) continue;

                best = block;
                bestWorld = world;
                bestDistance = distance;
            }

            if (best == null) return false;
            claimedAt[best.Address] = Time.time;
            return true;
        }

        public void Release(IPlanetBlock block)
        {
            if (block != null) claimedAt.Remove(block.Address);
        }

        // ---------- Geometria ----------

        /// <summary>Centro do bloco em coordenadas polares do planeta.</summary>
        public static PlanetPosition CenterOf(IPlanetBlock block)
        {
            var b = block.Bounds;
            return new PlanetPosition(
                (b.InnerRadiusMeters + b.OuterRadiusMeters) * 0.5,
                (b.StartAngleRadians + b.EndAngleRadians) * 0.5);
        }

        /// <summary>Centro do bloco no mundo (plano do terreno).</summary>
        public Vector3 GetWorldCenter(IPlanetBlock block)
        {
            Vector3 local = Projection.ToLocal(CenterOf(block));
            return terrain.transform.TransformPoint(local);
        }

        private SpriteRenderer CreateMark(IPlanetBlock block)
        {
            var b = block.Bounds;
            double midRadius = (b.InnerRadiusMeters + b.OuterRadiusMeters) * 0.5;

            Vector3 local = Projection.ToLocal(CenterOf(block));
            local.z = -0.2f; // um pouco na frente do terreno

            var go = new GameObject("DigMark");
            go.transform.SetParent(transform, false);
            go.transform.position = terrain.transform.TransformPoint(local);
            go.transform.localScale = new Vector3(
                (float)(midRadius * (b.EndAngleRadians - b.StartAngleRadians)),
                (float)(b.OuterRadiusMeters - b.InnerRadiusMeters), 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = waitingSprite; // a cor certa é definida logo em seguida pelo refresh
            renderer.sortingOrder = 5;
            return renderer;
        }

        private static Sprite CreateMarkSprite(Color fill)
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };

            var border = new Color(fill.r, fill.g, fill.b, 1f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                    pixels[y * size + x] = edge ? border : fill;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}

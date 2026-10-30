using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using MercurioIdle.Construction;
using MercurioIdle.Planet;

namespace MercurioIdle.Robots
{
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

        public int Version { get; private set; }

        private const float ProtectionMargin = 0.25f;

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
            Building.Built += OnBuildingBuilt;
        }

        private void Update()
        {
            var current = terrain != null ? terrain.Level : null;
            if (current != level) Rebind(current);

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
            Building.Built -= OnBuildingBuilt;
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
            refreshRequested = true;
        }

        public bool IsMarked(IPlanetBlock block) => block != null && marked.ContainsKey(block.Address);

        public void Mark(IPlanetBlock block)
        {
            if (level == null || block == null || block.IsRemoved) return;
            var address = block.Address;
            if (marked.ContainsKey(address)) return;
            if (IsProtected(block)) return;

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

        public bool TryClaimNearest(Vector3 fromWorld, out IPlanetBlock best, out Vector3 bestWorld)
        {
            best = null;
            bestWorld = default;
            if (level == null || Projection == null) return false;

            float bestDistance = float.MaxValue;
            foreach (var pair in marked)
            {
                if (claimedAt.TryGetValue(pair.Key, out float since) && Time.time - since < claimTimeoutSeconds)
                    continue;

                var block = pair.Value;
                Vector3 world = GetWorldCenter(block);
                if (IsProtectedAt(world)) continue;
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

        public static PlanetPosition CenterOf(IPlanetBlock block)
        {
            var b = block.Bounds;
            return new PlanetPosition(
                (b.InnerRadiusMeters + b.OuterRadiusMeters) * 0.5,
                (b.StartAngleRadians + b.EndAngleRadians) * 0.5);
        }

        public bool IsProtected(IPlanetBlock block)
        {
            if (block == null || terrain == null || Projection == null) return false;
            return IsProtectedAt(GetWorldCenter(block));
        }

        private static bool IsProtectedAt(Vector3 world)
        {
            foreach (var building in Building.Instances)
            {
                if (world.x < building.Left - ProtectionMargin || world.x > building.Right + ProtectionMargin) continue;
                if (world.y < building.Bottom) return true;
            }

            return false;
        }

        private void OnBuildingBuilt(Building building)
        {
            var protectedAddresses = new List<PlanetBlockAddress>();
            foreach (var pair in marked)
                if (IsProtected(pair.Value)) protectedAddresses.Add(pair.Key);

            foreach (var address in protectedAddresses) Unmark(address);
        }

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
            local.z = -0.2f;

            var go = new GameObject("DigMark");
            go.transform.SetParent(transform, false);
            go.transform.position = terrain.transform.TransformPoint(local);
            go.transform.localScale = new Vector3(
                (float)(midRadius * (b.EndAngleRadians - b.StartAngleRadians)),
                (float)(b.OuterRadiusMeters - b.InnerRadiusMeters), 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = waitingSprite;
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

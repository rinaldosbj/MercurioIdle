using System.Collections.Generic;
using UnityEngine;
using MercurioIdle.Planet;

namespace MercurioIdle.Chunks
{
    /// <summary>
    /// Carrega e descarrega o terreno em chunks conforme a câmera se move.
    /// Substitui a janela fixa de 30 m do PlanetExcavationPreview: cada chunk é uma malha própria,
    /// com o mesmo desenho (mesmo shader e mesmos quadriláteros) do terreno original.
    ///
    /// O terreno em si (blocos, escavação) vive no IPlanetLevel do preview. Os chunks decidem o que é
    /// DESENHADO e, com "Unload Far Terrain", também pedem ao level que libere da memória os blocos
    /// longe da câmera. Tudo volta idêntico (inclusive as escavações) quando a câmera retorna.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlanetChunkStreamer : MonoBehaviour
    {
        private sealed class Chunk
        {
            public Vector2Int Key;
            public GameObject Object;
            public Mesh Mesh;
        }

        [Header("Terreno")]
        [SerializeField, Tooltip("Objeto com o PlanetExcavationPreview. Se vazio, procura na cena.")]
        private PlanetExcavationPreview terrain;
        [SerializeField, Tooltip("Câmera que define o que carregar. Se vazia, usa a Main Camera.")]
        private Camera viewCamera;
        [SerializeField, Tooltip("Shader dos blocos. Se vazio, usa MercurioIdle/Blocks.")]
        private Shader blockShader;
        [SerializeField, Tooltip("Nível das folhas = DetailLevels - 1 (nas configurações do terreno). Padrão: 8 níveis => 7.")]
        private int leafDepth = 7;

        [Header("Chunks")]
        [SerializeField, Min(4f), Tooltip("Tamanho aproximado de cada chunk em metros.")]
        private float chunkSizeMeters = 16f;
        [SerializeField, Min(0), Tooltip("Quantos chunks extras carregar além da borda da tela.")]
        private int loadMarginChunks = 1;
        [SerializeField, Min(1), Tooltip("Chunks mais longe que isso da tela são descarregados (precisa ser maior que a margem de carga).")]
        private int unloadMarginChunks = 2;
        [SerializeField, Min(1), Tooltip("Limite de chunks carregados ao mesmo tempo (útil se a câmera estiver muito afastada).")]
        private int maxLoadedChunks = 120;
        [SerializeField, Min(1f), Tooltip("Tempo máximo por frame (ms) gastando em construir chunks novos. Evita travadas.")]
        private float buildBudgetMilliseconds = 8f;
        [SerializeField, Min(100), Tooltip("Segurança: máximo de blocos por chunk.")]
        private int maxBlocksPerChunk = 4000;

        [Header("Memória")]
        [SerializeField, Tooltip("Libera da memória o terreno (não só as malhas) que está longe da câmera. A escavação é preservada.")]
        private bool unloadFarTerrain = true;
        [SerializeField, Min(2), Tooltip("Margem (em chunks) além da tela que continua na memória.")]
        private int keepMarginChunks = 4;
        [SerializeField, Min(0.5f), Tooltip("De quanto em quanto tempo (s) a limpeza pode rodar.")]
        private float unloadIntervalSeconds = 3f;

        private readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        private readonly HashSet<Vector2Int> dirtyKeys = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> wanted = new List<Vector2Int>();
        private readonly List<Vector2Int> removeList = new List<Vector2Int>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> indices = new List<int>();

        private IPlanetLevel level;
        private PlanetProjection projection;
        private Transform container;
        private Material material;

        // Grade de chunks em coordenadas polares, medida a partir da origem do plano local.
        private double originRadius;
        private double originAngle;
        private double chunkAngle;

        private bool hasRange;
        private int rangeIMin, rangeIMax, rangeJMin, rangeJMax;
        private bool unloadNeeded;
        private float unloadTimer;

        private void Awake()
        {
            if (terrain == null) terrain = FindFirstObjectByType<PlanetExcavationPreview>();
            if (viewCamera == null) viewCamera = Camera.main;
            if (terrain == null || viewCamera == null)
            {
                Debug.LogError("PlanetChunkStreamer precisa do PlanetExcavationPreview e de uma câmera na cena.", this);
                enabled = false;
                return;
            }

            var shader = blockShader != null ? blockShader : Shader.Find("MercurioIdle/Blocks");
            if (shader == null)
            {
                Debug.LogError("Shader MercurioIdle/Blocks não encontrado.", this);
                enabled = false;
                return;
            }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            container = new GameObject("Terrain chunks").transform;
            container.SetParent(terrain.transform, false);

            // Os chunks substituem a janela fixa de 30 m do preview (só durante o Play).
            terrain.DrawLocalWindow = false;
        }

        private void OnDestroy()
        {
            if (level != null) level.BlockRemoved -= OnBlockRemoved;
            DestroyAllChunks();
            if (container != null) Destroy(container.gameObject);
            if (material != null) Destroy(material);
        }

        private void Update()
        {
            if (terrain == null) return;

            // O preview recria o level ao regenerar: nesse caso recomeça do zero.
            var currentLevel = terrain.Level;
            var currentProjection = terrain.Projection as PlanetProjection;
            if (currentLevel != level || currentProjection != projection) Rebind(currentLevel, currentProjection);
            if (level == null) return;

            UpdateWantedChunks();
            BuildPendingChunks();
            UnloadFarTerrain();
        }

        private void Rebind(IPlanetLevel newLevel, PlanetProjection newProjection)
        {
            if (level != null) level.BlockRemoved -= OnBlockRemoved;
            DestroyAllChunks();

            level = newLevel;
            projection = newProjection;
            hasRange = false;
            unloadNeeded = false;
            if (level == null || projection == null) return;

            PlanetPosition origin = projection.FromLocal(Vector3.zero);
            originRadius = origin.RadiusMeters;
            originAngle = origin.AngleRadians;
            chunkAngle = chunkSizeMeters / originRadius;

            level.BlockRemoved += OnBlockRemoved;
        }

        // ---------- Quais chunks carregar ----------

        // Descobre os chunks que cobrem a tela da câmera (+ margem) e descarrega os distantes.
        private void UpdateWantedChunks()
        {
            if (!TryGetViewRange(out int iMin, out int iMax, out int jMin, out int jMax)) return;
            if (hasRange && iMin == rangeIMin && iMax == rangeIMax && jMin == rangeJMin && jMax == rangeJMax) return;

            hasRange = true;
            rangeIMin = iMin; rangeIMax = iMax; rangeJMin = jMin; rangeJMax = jMax;
            unloadNeeded = true;

            // Chunks desejados, do mais próximo do centro da tela para o mais distante
            wanted.Clear();
            for (int i = iMin - loadMarginChunks; i <= iMax + loadMarginChunks; i++)
                for (int j = jMin - loadMarginChunks; j <= jMax + loadMarginChunks; j++)
                    wanted.Add(new Vector2Int(i, j));

            float centerI = (iMin + iMax) * 0.5f;
            float centerJ = (jMin + jMax) * 0.5f;
            wanted.Sort((a, b) =>
            {
                float da = (a.x - centerI) * (a.x - centerI) + (a.y - centerJ) * (a.y - centerJ);
                float db = (b.x - centerI) * (b.x - centerI) + (b.y - centerJ) * (b.y - centerJ);
                return da.CompareTo(db);
            });
            if (wanted.Count > maxLoadedChunks) wanted.RemoveRange(maxLoadedChunks, wanted.Count - maxLoadedChunks);

            // Descarrega o que ficou longe da tela
            int unload = Mathf.Max(unloadMarginChunks, loadMarginChunks + 1);
            removeList.Clear();
            foreach (var key in chunks.Keys)
            {
                if (key.x < iMin - unload || key.x > iMax + unload || key.y < jMin - unload || key.y > jMax + unload)
                    removeList.Add(key);
            }
            foreach (var key in removeList) DestroyChunk(key);
        }

        // Faixa de índices de chunk que a tela da câmera ocupa.
        private bool TryGetViewRange(out int iMin, out int iMax, out int jMin, out int jMax)
        {
            iMin = iMax = jMin = jMax = 0;
            if (viewCamera == null) return false;

            float depth = Mathf.Abs(terrain.transform.position.z - viewCamera.transform.position.z);
            bool first = true;
            for (int cx = 0; cx < 2; cx++)
            {
                for (int cy = 0; cy < 2; cy++)
                {
                    Vector3 world = viewCamera.ViewportToWorldPoint(new Vector3(cx, cy, depth));
                    Vector3 local = terrain.transform.InverseTransformPoint(world);
                    Vector2Int key = KeyOf(projection.FromLocal(new Vector3(local.x, local.y, 0f)));

                    if (first)
                    {
                        iMin = iMax = key.x;
                        jMin = jMax = key.y;
                        first = false;
                    }
                    else
                    {
                        iMin = Mathf.Min(iMin, key.x); iMax = Mathf.Max(iMax, key.x);
                        jMin = Mathf.Min(jMin, key.y); jMax = Mathf.Max(jMax, key.y);
                    }
                }
            }
            return true;
        }

        // ---------- Memória ----------

        // Libera da memória o terreno (os dados, não só as malhas) que está longe da câmera.
        // A escavação é preservada e os blocos voltam idênticos quando a câmera chegar perto de novo.
        private void UnloadFarTerrain()
        {
            if (!unloadFarTerrain || !unloadNeeded || !hasRange) return;

            unloadTimer -= Time.deltaTime;
            if (unloadTimer > 0f) return;
            unloadTimer = unloadIntervalSeconds;
            unloadNeeded = false;

            // A região mantida cobre os chunks desenhados (com a margem de descarga) e ainda sobra folga.
            int keep = Mathf.Max(keepMarginChunks, Mathf.Max(unloadMarginChunks, loadMarginChunks + 1) + 2);
            double inner = System.Math.Max(0.0, originRadius + (rangeJMin - keep) * (double)chunkSizeMeters);
            double outer = originRadius + (rangeJMax + 1 + keep) * (double)chunkSizeMeters;
            double start = originAngle + (rangeIMin - keep) * chunkAngle;
            double width = System.Math.Min(2 * System.Math.PI, (rangeIMax - rangeIMin + 1 + 2 * keep) * chunkAngle);

            try
            {
                level.UnloadOutside(new PlanetRegion(inner, outer, start, width));
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                unloadFarTerrain = false; // não tenta de novo
            }
        }

        // ---------- Construção ----------

        private void BuildPendingChunks()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // 1) Chunks em que algum bloco foi removido
            if (dirtyKeys.Count > 0)
            {
                foreach (var key in dirtyKeys)
                    if (chunks.TryGetValue(key, out var dirty)) BuildChunk(dirty);
                dirtyKeys.Clear();
            }

            // 2) Chunks novos, do mais próximo para o mais distante, respeitando o orçamento de tempo
            for (int n = 0; n < wanted.Count; n++)
            {
                var key = wanted[n];
                if (chunks.ContainsKey(key)) continue;

                var chunk = new Chunk { Key = key };
                chunks.Add(key, chunk);
                BuildChunk(chunk);

                if (watch.Elapsed.TotalMilliseconds > buildBudgetMilliseconds) break;
            }
        }

        private void BuildChunk(Chunk chunk)
        {
            vertices.Clear();
            indices.Clear();

            try
            {
                int count = 0;
                foreach (var block in level.EnumerateBlocks(RegionOf(chunk.Key), leafDepth))
                {
                    // O bloco pertence ao chunk onde está o seu centro (evita desenhar duas vezes).
                    if (KeyOf(CenterOf(block)) != chunk.Key) continue;
                    if (++count > maxBlocksPerChunk)
                    {
                        Debug.LogWarning($"Chunk {chunk.Key} passou de {maxBlocksPerChunk} blocos; o restante não foi desenhado.", this);
                        break;
                    }

                    // Mesmos quatro cantos que o terreno original usa
                    var bounds = block.Bounds;
                    var a = projection.ToLocal(new PlanetPosition(bounds.InnerRadiusMeters, bounds.StartAngleRadians));
                    var b = projection.ToLocal(new PlanetPosition(bounds.OuterRadiusMeters, bounds.StartAngleRadians));
                    var c = projection.ToLocal(new PlanetPosition(bounds.OuterRadiusMeters, bounds.EndAngleRadians));
                    var d = projection.ToLocal(new PlanetPosition(bounds.InnerRadiusMeters, bounds.EndAngleRadians));
                    int start = vertices.Count;
                    vertices.Add(a);
                    vertices.Add(b);
                    vertices.Add(c);
                    vertices.Add(d);
                    indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                    indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                vertices.Clear();
                indices.Clear();
            }

            // Chunk sem blocos (céu ou tudo escavado): não precisa de objeto
            if (vertices.Count == 0)
            {
                if (chunk.Object != null) { Destroy(chunk.Object); chunk.Object = null; }
                if (chunk.Mesh != null) { Destroy(chunk.Mesh); chunk.Mesh = null; }
                return;
            }

            if (chunk.Mesh == null)
                chunk.Mesh = new Mesh { name = $"Chunk {chunk.Key.x},{chunk.Key.y}", hideFlags = HideFlags.HideAndDontSave };

            if (chunk.Object == null)
            {
                chunk.Object = new GameObject($"Chunk {chunk.Key.x},{chunk.Key.y}");
                chunk.Object.transform.SetParent(container, false);
                chunk.Object.AddComponent<MeshFilter>().sharedMesh = chunk.Mesh;
                var meshRenderer = chunk.Object.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }

            chunk.Mesh.Clear();
            chunk.Mesh.SetVertices(vertices);
            chunk.Mesh.SetTriangles(indices, 0);
            chunk.Mesh.RecalculateBounds();
        }

        private void OnBlockRemoved(IPlanetBlock block)
        {
            // Só o chunk que continha o bloco precisa ser redesenhado.
            var key = KeyOf(CenterOf(block));
            if (chunks.ContainsKey(key)) dirtyKeys.Add(key);
        }

        private void DestroyChunk(Vector2Int key)
        {
            if (chunks.TryGetValue(key, out var chunk))
            {
                if (chunk.Object != null) Destroy(chunk.Object);
                if (chunk.Mesh != null) Destroy(chunk.Mesh);
                chunks.Remove(key);
            }
            dirtyKeys.Remove(key);
        }

        private void DestroyAllChunks()
        {
            removeList.Clear();
            removeList.AddRange(chunks.Keys);
            foreach (var key in removeList) DestroyChunk(key);
            dirtyKeys.Clear();
            wanted.Clear();
        }

        // ---------- Geometria dos chunks ----------

        private Vector2Int KeyOf(PlanetPosition position)
        {
            double deltaAngle = WrapAngle(position.AngleRadians - originAngle);
            return new Vector2Int(
                (int)System.Math.Floor(deltaAngle / chunkAngle),
                (int)System.Math.Floor((position.RadiusMeters - originRadius) / chunkSizeMeters));
        }

        // Região polar do chunk, com folga para pegar blocos que cruzam a borda.
        private PlanetRegion RegionOf(Vector2Int key)
        {
            const double padMeters = 2.5;
            double inner = System.Math.Max(0.0, originRadius + key.y * (double)chunkSizeMeters - padMeters);
            double outer = originRadius + (key.y + 1) * (double)chunkSizeMeters + padMeters;
            double padAngle = padMeters / originRadius;
            return new PlanetRegion(inner, outer,
                originAngle + key.x * chunkAngle - padAngle,
                chunkAngle + 2.0 * padAngle);
        }

        private static PlanetPosition CenterOf(IPlanetBlock block)
        {
            var b = block.Bounds;
            return new PlanetPosition(
                (b.InnerRadiusMeters + b.OuterRadiusMeters) * 0.5,
                (b.StartAngleRadians + b.EndAngleRadians) * 0.5);
        }

        private static double WrapAngle(double angle)
        {
            while (angle > System.Math.PI) angle -= 2 * System.Math.PI;
            while (angle <= -System.Math.PI) angle += 2 * System.Math.PI;
            return angle;
        }
    }
}

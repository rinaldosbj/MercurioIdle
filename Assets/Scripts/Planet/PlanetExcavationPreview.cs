using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Gera e desenha o terreno no Editor antes do Play; no jogo permite remover com clique esquerdo.
    /// Desenha uma região local fixa, com origem próxima da superfície para manter precisão.
    /// Parâmetros ficam no Inspector; não contém save, simulação de robôs ou durabilidade.
    /// </summary>
    [ExecuteAlways]
    public sealed class PlanetExcavationPreview : MonoBehaviour
    {
        [SerializeField] private PlanetBlockSettings settings = new PlanetBlockSettings();
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Shader blockShader;
        [SerializeField, Tooltip("Ângulo da região visitada em graus.")] private double angleDegrees = 90;
        [SerializeField, Min(0), Tooltip("Distância da origem abaixo da superfície, em metros.")] private float depthMeters = 5;
        [SerializeField, Range(2, 30), Tooltip("Meia extensão da região de teste em metros.")] private float extentMeters = 15;
        private PlanetLevel level;
        private PlanetProjection projection;
        private Mesh mesh;
        private Material material;
        private GameObject surface;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> indices = new List<int>();
        private bool dirty;
        private bool regenerationRequested;
        private bool generatedForPlay;
        private PlanetRegion region;
        private int leafDepth;

        public IPlanetLevel Level => level;
        public IPlanetProjection Projection => projection;

        private void OnEnable() => regenerationRequested = true;

        // OnValidate pode rodar durante desserialização: apenas sinaliza, sem criar objetos.
        private void OnValidate() => regenerationRequested = true;

        /// <summary>
        /// Solicita uma nova geração na próxima atualização do Editor ou jogo.
        /// Recriar o level restaura o terreno escavado desta sessão.
        /// </summary>
        [ContextMenu("Generate level")]
        public void GenerateLevel() => regenerationRequested = true;

        private void Generate()
        {
            regenerationRequested = false;
            ReleaseGeneratedObjects();
            generatedForPlay = Application.IsPlaying(gameObject);
            try
            {
                var parameters = settings.Copy();
                double radius = parameters.PlanetRadiusMeters - depthMeters;
                if (radius <= parameters.CoreRadius * parameters.PlanetRadiusMeters)
                    throw new ArgumentException("A origem deve ficar acima do núcleo.");
                level = new PlanetLevel(parameters);
                leafDepth = parameters.DetailLevels - 1;
                if (viewCamera == null)
                    throw new InvalidOperationException("Associe uma câmera da cena ao campo View Camera.");
                projection = new PlanetProjection(viewCamera, transform, new PlanetPosition(radius, angleDegrees * Math.PI / 180));
                double halfAngle = Math.Min(Math.PI, extentMeters / radius);
                region = new PlanetRegion(Math.Max(0, radius - extentMeters),
                    radius + extentMeters, angleDegrees * Math.PI / 180 - halfAngle, 2 * halfAngle);
                surface = new GameObject("Local planet blocks");
                surface.hideFlags = HideFlags.HideAndDontSave;
                surface.transform.SetParent(transform, false);
                mesh = new Mesh { name = "Local excavation blocks", hideFlags = HideFlags.HideAndDontSave,
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                var shader = blockShader != null ? blockShader : Shader.Find("MercurioIdle/Blocks");
                if (shader == null) throw new InvalidOperationException("Shader MercurioIdle/Blocks não encontrado.");
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                surface.AddComponent<MeshRenderer>().sharedMaterial = material;
                level.BlockRemoved += OnBlockRemoved;
                dirty = true;
                RebuildMesh();
#if UNITY_EDITOR
                UnityEditor.SceneView.RepaintAll();
#endif
            }
            catch (Exception exception) { ReleaseGeneratedObjects(); Debug.LogException(exception, this); }
        }

        private void OnBlockRemoved(IPlanetBlock block) => dirty = true;

        private void Update()
        {
            if (regenerationRequested || generatedForPlay != Application.IsPlaying(gameObject)) Generate();
            if (level == null) return;
            try
            {
                // Cliques de seleção no Editor não escavam o terreno.
                var mouse = Application.IsPlaying(gameObject) ? Mouse.current : null;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                    projection.TryScreenToPlanet(mouse.position.ReadValue(), out var position) &&
                    level.TryGetBlock(position, out var block)) level.RemoveBlock(block);
                if (dirty) RebuildMesh();
            }
            catch (Exception exception) { ReleaseGeneratedObjects(); Debug.LogException(exception, this); }
        }

        /// <summary>Desenha folhas vivas da janela local; limita a prévia a 10000 blocos.</summary>
        private void RebuildMesh()
        {
            vertices.Clear();
            indices.Clear();
            int count = 0;
            foreach (var block in level.EnumerateBlocks(region, leafDepth))
            {
                if (++count > 10000) throw new InvalidOperationException("Região de teste grande demais; reduza a extensão.");
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
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            dirty = false;
        }

        private void OnDisable() => ReleaseGeneratedObjects();

        private void OnDestroy() => ReleaseGeneratedObjects();

        /// <summary>Libera a prévia transitória, preservando a câmera explícita da cena.</summary>
        private void ReleaseGeneratedObjects()
        {
            if (level != null) level.BlockRemoved -= OnBlockRemoved;
            level = null;
            projection = null;
            dirty = false;
            Release(surface);
            Release(mesh);
            Release(material);
            surface = null;
            mesh = null;
            material = null;
        }

        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}

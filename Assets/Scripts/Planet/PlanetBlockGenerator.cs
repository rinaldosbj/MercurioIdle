using System;
using System.Collections.Generic;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Gera raízes e materializa apenas os caminhos consultados até folhas de gameplay.
    /// Coordenadas de entrada são polares físicas; a geometria interna é normalizada.
    /// Não contém câmera, renderização ou regras de escavação.
    /// </summary>
    public sealed class PlanetBlockGenerator
    {
        private readonly PlanetBlockSettings settings;
        private readonly List<PlanetBlock> roots = new List<PlanetBlock>();
        private readonly Dictionary<PlanetBlock, Division> divisions = new Dictionary<PlanetBlock, Division>();
        private readonly Dictionary<PlanetBlock, PlanetBlockAddress> addresses = new Dictionary<PlanetBlock, PlanetBlockAddress>();
        private int nextId;

        public int MaterializedBlockCount => nextId;
        internal int LeafDepth => settings.DetailLevels - 1;

        /// <summary>
        /// Copia as configurações e busca coroas com filhos de área e formato válidos.
        /// Rejeita configurações incompatíveis em vez de ignorar suas restrições.
        /// </summary>
        public PlanetBlockGenerator(PlanetBlockSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            this.settings = settings.Copy();
            ValidateSettings();
            BuildRoots();
        }

        /// <summary>
        /// Obtém uma folha por posição, gerando somente seu caminho e irmãos imediatos.
        /// Retorna false fora do terreno. Uma subdivisão inviável lança InvalidOperationException.
        /// </summary>
        public bool TryGetLeaf(PlanetPosition position, out PlanetBlock block) => TryGetLeaf(position, out block, null);

        internal bool TryGetLeaf(PlanetPosition position, out PlanetBlock block, Func<PlanetBlock, bool> excluded)
        {
            double radius = position.RadiusMeters / settings.PlanetRadiusMeters;
            block = null;
            if (radius < settings.CoreRadius || radius >= 1) return false;
            foreach (var root in roots)
            {
                if (!Contains(root, radius, position.AngleRadians)) continue;
                block = root;
                if (excluded != null && excluded(block)) { block = null; return false; }
                while (block.Depth < settings.DetailLevels - 1)
                {
                    EnsureChildren(block);
                    PlanetBlock found = null;
                    foreach (var child in block.Children)
                        if (Contains(child, radius, position.AngleRadians)) { found = child; break; }
                    if (found == null) throw new InvalidOperationException("Subdivisão não cobre a posição consultada.");
                    block = found;
                    if (excluded != null && excluded(block)) { block = null; return false; }
                }
                return true;
            }
            return false;
        }

        /// <summary>Resolve um endereço, materializando o caminho; false indica índice inexistente.</summary>
        public bool TryGetBlock(PlanetBlockAddress address, out PlanetBlock block) => TryGetBlock(address, out block, null);

        internal bool TryGetBlock(PlanetBlockAddress address, out PlanetBlock block, Func<PlanetBlock, bool> excluded)
        {
            block = null;
            if (address.RootIndex >= roots.Count || address.Depth >= settings.DetailLevels) return false;
            var node = roots[address.RootIndex];
            if (excluded != null && excluded(node)) return false;
            for (int depth = 0; depth < address.Depth; depth++)
            {
                EnsureChildren(node);
                var division = divisions[node];
                var index = address[depth];
                if (index.Radial >= division.Radial || index.Angular >= division.Angular) return false;
                node = node.Children[index.Radial * division.Angular + index.Angular];
                if (excluded != null && excluded(node)) return false;
            }
            block = node;
            return true;
        }

        /// <summary>Retorna a identidade de um bloco deste gerador; rejeita referências externas.</summary>
        public PlanetBlockAddress GetAddress(PlanetBlock block) => addresses.TryGetValue(block, out var address)
            ? address : throw new ArgumentException("Bloco pertence a outro gerador.", nameof(block));

        internal void GetDivision(PlanetBlock node, out int radial, out int angular)
        {
            EnsureChildren(node);
            var division = divisions[node];
            radial = division.Radial;
            angular = division.Angular;
        }

        /// <summary>
        /// Verifica cobertura radial em todas as coroas, sem amostrar somente o centro.
        /// Regiões intactas são verificadas como setores grandes; só ramos parciais são percorridos.
        /// </summary>
        internal bool HasSolidAbove(PlanetBlock target, Func<PlanetBlock, bool> removed, Func<PlanetBlock, bool> partial)
        {
            foreach (var root in roots)
                if (CoversAbove(root, target, removed, partial)) return true;
            return false;
        }

        private bool CoversAbove(PlanetBlock node, PlanetBlock target,
            Func<PlanetBlock, bool> removed, Func<PlanetBlock, bool> partial)
        {
            // Tolerância de arredondamento dos limites compartilhados, não uma espessura mínima.
            const double roundoff = 8 * 2.220446049250313e-16;
            double angularOverlap = Math.Min(node.EndAngle, target.EndAngle) - Math.Max(node.StartAngle, target.StartAngle);
            if (node.OuterRadius <= target.OuterRadius + roundoff ||
                angularOverlap <= roundoff * Math.Max(1, Math.Max(node.EndAngle, target.EndAngle)) || removed(node)) return false;
            if (!partial(node) && node.InnerRadius >= target.OuterRadius - roundoff) return true;
            // O nó contém o alvo ou uma fronteira parcial: precisamos conferir cada faixa relevante.
            if (node.Depth == settings.DetailLevels - 1) return true;
            EnsureChildren(node);
            foreach (var child in node.Children)
                if (CoversAbove(child, target, removed, partial)) return true;
            return false;
        }

        /// <summary>Enumera somente ramos que intersectam a região, podando regiões removidas antes de gerar filhos.</summary>
        internal IEnumerable<PlanetBlock> EnumerateBlocks(PlanetRegion region, int depth, Func<PlanetBlock, bool> excluded)
        {
            if (depth < 0 || depth >= settings.DetailLevels) throw new ArgumentOutOfRangeException(nameof(depth));
            if (region.OuterRadiusMeters <= region.InnerRadiusMeters || region.AngularWidthRadians <= 0)
                throw new ArgumentException("Região vazia ou não inicializada.", nameof(region));
            foreach (var root in roots)
                foreach (var node in Collect(root, region, depth, excluded)) yield return node;
        }

        private IEnumerable<PlanetBlock> Collect(PlanetBlock node, PlanetRegion region, int depth, Func<PlanetBlock, bool> excluded)
        {
            if ((excluded != null && excluded(node)) || !Intersects(node, region)) yield break;
            if (node.Depth == depth) { yield return node; yield break; }
            EnsureChildren(node);
            foreach (var child in node.Children)
                foreach (var result in Collect(child, region, depth, excluded)) yield return result;
        }

        private bool Intersects(PlanetBlock node, PlanetRegion region)
        {
            if (node.OuterRadius * settings.PlanetRadiusMeters <= region.InnerRadiusMeters ||
                node.InnerRadius * settings.PlanetRadiusMeters >= region.OuterRadiusMeters) return false;
            double end = region.StartAngleRadians + region.AngularWidthRadians;
            bool first = node.EndAngle > region.StartAngleRadians && node.StartAngle < Math.Min(2 * Math.PI, end);
            return first || (end > 2 * Math.PI && node.StartAngle < end - 2 * Math.PI);
        }

        private void ValidateSettings()
        {
            if (settings.DetailLevels < 2 || settings.DetailLevels > 32 ||
                !double.IsFinite(settings.CoreRadius) || settings.CoreRadius <= 0 || settings.CoreRadius >= 1 ||
                !double.IsFinite(settings.MaxRadialRatio) || settings.MaxRadialRatio <= 1 ||
                !double.IsFinite(settings.MaxAspectRatio) || settings.MaxAspectRatio <= 1 ||
                !double.IsFinite(settings.MaxAreaRatio) || settings.MaxAreaRatio < 1 ||
                settings.MinDivisionsPerAxis < 1 || settings.MaxDivisionsPerAxis < 2 ||
                settings.MinDivisionsPerAxis > settings.MaxDivisionsPerAxis || settings.MaxDivisionsPerAxis > byte.MaxValue)
                throw new ArgumentException("Parâmetros de geometria ou subdivisão inválidos.");
            double area = settings.GetTargetAreaSquareMeters(1);
            if (!double.IsFinite(area) || area <= 0) throw new ArgumentException("Área alvo inválida.");
        }

        private void BuildRoots()
        {
            double size = Math.Sqrt(settings.GetTargetAreaSquareMeters(1)) / settings.PlanetRadiusMeters;
            int idealRings = Math.Max(1, (int)Math.Round((1 - settings.CoreRadius) / size));
            // Busca layouts completos, evitando uma faixa residual fina na superfície.
            for (int delta = 0; delta <= 20; delta++)
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int rings = idealRings + delta * sign;
                if (rings < 1 || rings > 4096) continue;
                var layout = new List<PlanetBlock>();
                bool valid = true;
                for (int ring = 0; ring < rings && valid; ring++)
                {
                    double r0 = settings.CoreRadius + (1 - settings.CoreRadius) * ring / rings;
                    double r1 = ring == rings - 1 ? 1 : settings.CoreRadius + (1 - settings.CoreRadius) * (ring + 1) / rings;
                    int groups = Math.Max(1, (int)Math.Round(Math.PI * (r1 - r0) * (r1 + r0) / (size * size) / settings.MaxDivisionsPerAxis));
                    for (int group = 0; group < groups; group++)
                    {
                        var root = new PlanetBlock(0, r0, r1,
                            PlanetBlockGeometry.AngularBoundary(0, 2 * Math.PI, group, groups),
                            PlanetBlockGeometry.AngularBoundary(0, 2 * Math.PI, group + 1, groups));
                        if (!FindDivision(root, out _)) { valid = false; break; }
                        layout.Add(root);
                    }
                }
                if (!valid) continue;
                foreach (var candidate in layout)
                {
                    var root = new PlanetBlock(nextId++, candidate.InnerRadius, candidate.OuterRadius, candidate.StartAngle, candidate.EndAngle);
                    addresses.Add(root, new PlanetBlockAddress(roots.Count));
                    roots.Add(root);
                }
                return;
            }
            throw new InvalidOperationException("Nenhum layout raiz atende às áreas e formas configuradas.");
        }

        private bool FindDivision(PlanetBlock node, out Division result)
        {
            result = default;
            double best = double.PositiveInfinity;
            double target = settings.GetTargetAreaSquareMeters(node.Depth + 1);
            for (int m = settings.MinDivisionsPerAxis; m <= settings.MaxDivisionsPerAxis; m++)
            for (int n = settings.MinDivisionsPerAxis; n <= settings.MaxDivisionsPerAxis; n++)
            {
                if (!PlanetBlockGeometry.TryEvaluateSubdivision(node, settings, m, n, out var evaluation)) continue;
                double score = Math.Abs(Math.Log(Math.Sqrt(evaluation.MinChildAreaSquareMeters * evaluation.MaxChildAreaSquareMeters) / target));
                if (score < best) { best = score; result = new Division(m, n); }
            }
            return double.IsFinite(best);
        }

        private void EnsureChildren(PlanetBlock node)
        {
            if (node.Children.Count > 0) return;
            if (!FindDivision(node, out var division))
                throw new InvalidOperationException($"Sem divisão viável no bloco {node.Id}, profundidade {node.Depth}. Ajuste áreas, níveis ou limites M/N.");
            divisions.Add(node, division);
            for (int m = 0; m < division.Radial; m++)
            for (int n = 0; n < division.Angular; n++)
            {
                var child = new PlanetBlock(nextId++,
                    PlanetBlockGeometry.RadialBoundary(node.InnerRadius, node.OuterRadius, m, division.Radial),
                    PlanetBlockGeometry.RadialBoundary(node.InnerRadius, node.OuterRadius, m + 1, division.Radial),
                    PlanetBlockGeometry.AngularBoundary(node.StartAngle, node.EndAngle, n, division.Angular),
                    PlanetBlockGeometry.AngularBoundary(node.StartAngle, node.EndAngle, n + 1, division.Angular), node);
                node.Children.Add(child);
                addresses.Add(child, addresses[node].Append(new PlanetChildIndex(m, n)));
            }
        }

        private static bool Contains(PlanetBlock node, double radius, double angle) =>
            radius >= node.InnerRadius && radius < node.OuterRadius && angle >= node.StartAngle && angle < node.EndAngle;

        private readonly struct Division
        {
            public int Radial { get; }
            public int Angular { get; }
            public Division(int radial, int angular) { Radial = radial; Angular = angular; }
        }
    }
}

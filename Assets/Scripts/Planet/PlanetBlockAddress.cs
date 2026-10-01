using System;

namespace MercurioIdle.Planet
{
    /// <summary>Índices inteiros de um filho na divisão radial e angular do pai.</summary>
    public readonly struct PlanetChildIndex : IEquatable<PlanetChildIndex>
    {
        public int Radial { get; }
        public int Angular { get; }

        public PlanetChildIndex(int radial, int angular)
        {
            if (radial < 0) throw new ArgumentOutOfRangeException(nameof(radial));
            if (angular < 0) throw new ArgumentOutOfRangeException(nameof(angular));
            Radial = radial;
            Angular = angular;
        }

        public bool Equals(PlanetChildIndex other) => Radial == other.Radial && Angular == other.Angular;
        public override bool Equals(object obj) => obj is PlanetChildIndex other && Equals(other);
        public override int GetHashCode() => unchecked(Radial * 397 ^ Angular);
    }

    /// <summary>
    /// Identidade discreta de um bloco: índice da raiz e caminho de filhos.
    /// Estável somente para a mesma configuração e versão do gerador.
    /// </summary>
    /// <remarks>
    /// O caminho é copiado e não pode ser modificado externamente.
    /// O endereço padrão representa a raiz zero. Igualdade compara o caminho completo.
    /// </remarks>
    public readonly struct PlanetBlockAddress : IEquatable<PlanetBlockAddress>
    {
        private readonly PlanetChildIndex[] path;
        public int RootIndex { get; }
        public int Depth => path?.Length ?? 0;
        public PlanetChildIndex this[int depth] => depth >= 0 && depth < Depth
            ? path[depth] : throw new ArgumentOutOfRangeException(nameof(depth));

        public PlanetBlockAddress(int rootIndex, params PlanetChildIndex[] path)
        {
            if (rootIndex < 0) throw new ArgumentOutOfRangeException(nameof(rootIndex));
            if (path == null) throw new ArgumentNullException(nameof(path));
            RootIndex = rootIndex;
            this.path = path.Length == 0 ? null : (PlanetChildIndex[])path.Clone();
        }

        /// <summary>Retorna o endereço de um filho sem modificar este endereço.</summary>
        public PlanetBlockAddress Append(PlanetChildIndex child)
        {
            var next = new PlanetChildIndex[Depth + 1];
            if (Depth > 0) Array.Copy(path, next, Depth);
            next[Depth] = child;
            return new PlanetBlockAddress(RootIndex, next);
        }

        public bool Equals(PlanetBlockAddress other)
        {
            if (RootIndex != other.RootIndex || Depth != other.Depth) return false;
            for (int i = 0; i < Depth; i++)
                if (!path[i].Equals(other.path[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is PlanetBlockAddress other && Equals(other);
        public override int GetHashCode()
        {
            int hash = RootIndex;
            for (int i = 0; i < Depth; i++) hash = unchecked(hash * 397 ^ path[i].GetHashCode());
            return hash;
        }
        public static bool operator ==(PlanetBlockAddress left, PlanetBlockAddress right) => left.Equals(right);
        public static bool operator !=(PlanetBlockAddress left, PlanetBlockAddress right) => !left.Equals(right);
    }
}

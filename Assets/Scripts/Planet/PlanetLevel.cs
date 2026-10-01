using System;
using System.Collections.Generic;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Terreno consultável e escavável, com geração sob demanda escondida pela API.
    /// Mantém identidade, dados e remoções durante a sessão. Não é thread-safe e não salva em disco.
    /// </summary>
    public sealed class PlanetLevel : IPlanetLevel
    {
        private readonly PlanetBlockGenerator generator;
        private readonly double radiusMeters;
        private readonly Dictionary<PlanetBlock, LevelBlock> blocks = new Dictionary<PlanetBlock, LevelBlock>();
        private readonly PlanetExcavationState excavation;
        private int version;

        public event Action<IPlanetBlock> BlockRemoved;

        /// <summary>Materializa a divisão imediata e oferece coordenadas locais iguais em todos os níveis.</summary>
        public bool TryGetGridContext(PlanetBlockAddress sectorAddress, out PlanetGridContext context)
        {
            context = null;
            if (sectorAddress.Depth >= generator.LeafDepth ||
                !generator.TryGetBlock(sectorAddress, out var node, IsRemoved)) return false;
            generator.GetDivision(node, out int radial, out int angular);
            context = new PlanetGridContext(this, sectorAddress, angular, radial, node, radiusMeters);
            return true;
        }

        /// <summary>Cria o level com uma cópia dos parâmetros; folhas só são geradas quando consultadas.</summary>
        public PlanetLevel(PlanetBlockSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            radiusMeters = settings.PlanetRadiusMeters;
            generator = new PlanetBlockGenerator(settings);
            excavation = new PlanetExcavationState(generator);
        }

        /// <summary>Obtém uma folha viva por posição; não gera descendentes de regiões escavadas.</summary>
        public bool TryGetBlock(PlanetPosition position, out IPlanetBlock block)
        {
            block = null;
            if (!generator.TryGetLeaf(position, out var node, IsRemoved)) return false;
            block = Wrap(node);
            return true;
        }

        /// <summary>Resolve um endereço vivo, preservando a instância e os dados entre consultas.</summary>
        public bool TryGetBlock(PlanetBlockAddress address, out IPlanetBlock block)
        {
            block = null;
            if (!generator.TryGetBlock(address, out var node, IsRemoved)) return false;
            block = Wrap(node);
            return true;
        }

        /// <summary>
        /// Enumera blocos vivos na região e profundidade; a travessia é preguiçosa.
        /// Uma remoção durante a iteração invalida o enumerador e lança InvalidOperationException.
        /// </summary>
        public IEnumerable<IPlanetBlock> EnumerateBlocks(PlanetRegion region, int depth)
        {
            int expectedVersion = version;
            using (var iterator = generator.EnumerateBlocks(region, depth, IsRemoved).GetEnumerator())
            {
                while (true)
                {
                    if (version != expectedVersion)
                        throw new InvalidOperationException("O level foi modificado durante a enumeração.");
                    if (!iterator.MoveNext()) yield break;
                    yield return Wrap(iterator.Current);
                }
            }
        }

        /// <summary>
        /// Verifica exposição de toda a borda externa, inclusive entre setores e coroas diferentes.
        /// </summary>
        public bool CanRemoveBlock(IPlanetBlock block) =>
            block is LevelBlock owned && owned.Owner == this && !IsRemoved(owned.Node) &&
            !generator.HasSolidAbove(owned.Node, IsRemoved, excavation.IsPartial);

        /// <summary>
        /// Remove somente regiões expostas, atualizando a fronteira recursiva.
        /// Preserva dados existentes e dispara um evento após a remoção bem-sucedida.
        /// </summary>
        public bool RemoveBlock(IPlanetBlock block)
        {
            if (!CanRemoveBlock(block)) return false;
            var owned = (LevelBlock)block;
            excavation.Remove(owned.Node);
            version++;
            BlockRemoved?.Invoke(owned);
            return true;
        }

        private bool IsRemoved(PlanetBlock node) => excavation.IsRemoved(node);

        private LevelBlock Wrap(PlanetBlock node)
        {
            if (!blocks.TryGetValue(node, out var block))
            {
                block = new LevelBlock(this, node);
                blocks.Add(node, block);
            }
            return block;
        }

        private sealed class LevelBlock : IPlanetBlock
        {
            internal PlanetLevel Owner { get; }
            internal PlanetBlock Node { get; }
            private Dictionary<Type, object> data;
            public PlanetBlockAddress Address { get; }
            public PlanetBlockBounds Bounds { get; }
            public bool IsRemoved => Owner.IsRemoved(Node);

            internal LevelBlock(PlanetLevel owner, PlanetBlock node)
            {
                Owner = owner;
                Node = node;
                Address = owner.generator.GetAddress(node);
                Bounds = new PlanetBlockBounds(node.InnerRadius * owner.radiusMeters,
                    node.OuterRadius * owner.radiusMeters, node.StartAngle, node.EndAngle);
            }

            public T GetOrAddData<T>(Func<T> create) where T : class
            {
                if (TryGetData<T>(out var existing)) return existing;
                if (create == null) throw new ArgumentNullException(nameof(create));
                var value = create() ?? throw new InvalidOperationException("A fábrica de dados retornou null.");
                // A fábrica pode ter associado dados reentrando neste método.
                if (TryGetData<T>(out existing)) return existing;
                if (data == null) data = new Dictionary<Type, object>();
                data.Add(typeof(T), value);
                return value;
            }

            public bool TryGetData<T>(out T value) where T : class
            {
                value = null;
                if (data == null || !data.TryGetValue(typeof(T), out var entry)) return false;
                value = (T)entry;
                return true;
            }
        }
    }
}

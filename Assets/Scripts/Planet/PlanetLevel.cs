using System;
using System.Collections.Generic;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Terreno consultável e escavável, com geração sob demanda escondida pela API.
    /// Mantém dados e remoções durante a sessão. Não é thread-safe e não salva em disco.
    /// Depois de UnloadOutside, objetos de bloco sem dados podem ser recriados: compare blocos pelo Address.
    /// </summary>
    public sealed class PlanetLevel : IPlanetLevel
    {
        private readonly PlanetBlockGenerator generator;
        private readonly double radiusMeters;
        private readonly Dictionary<PlanetBlockAddress, LevelBlock> blocks = new Dictionary<PlanetBlockAddress, LevelBlock>();
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

        public int LoadedBlockCount => generator.LoadedBlockCount;

        public int UnloadOutside(PlanetRegion keep)
        {
            int freed = generator.Prune(keep);
            if (freed == 0) return 0;

            // Descarta os objetos de bloco cujo nó foi liberado e que não têm dados.
            // Quem ainda os referencia continua funcionando: o nó é refeito pelo endereço.
            var stale = new List<PlanetBlockAddress>();
            foreach (var pair in blocks)
                if (!pair.Value.HasData && !generator.IsLive(pair.Value.RawNode)) stale.Add(pair.Key);
            foreach (var address in stale) blocks.Remove(address);
            return freed;
        }

        private LevelBlock Wrap(PlanetBlock node)
        {
            var address = generator.GetAddress(node);
            if (!blocks.TryGetValue(address, out var block))
            {
                block = new LevelBlock(this, node);
                blocks.Add(address, block);
            }
            return block;
        }

        private sealed class LevelBlock : IPlanetBlock
        {
            internal PlanetLevel Owner { get; }
            private PlanetBlock node;
            private int nodeEpoch;
            private Dictionary<Type, object> data;

            internal PlanetBlock RawNode => node;
            internal bool HasData => data != null;

            // Depois de UnloadOutside o nó pode ter sido liberado: refaz o caminho pelo endereço.
            internal PlanetBlock Node
            {
                get
                {
                    var generator = Owner.generator;
                    if (nodeEpoch != generator.Epoch)
                    {
                        if (!generator.IsLive(node))
                        {
                            if (!generator.TryGetBlock(Address, out var current))
                                throw new InvalidOperationException("O bloco não existe mais no gerador.");
                            node = current;
                        }
                        nodeEpoch = generator.Epoch;
                    }
                    return node;
                }
            }
            public PlanetBlockAddress Address { get; }
            public PlanetBlockBounds Bounds { get; }
            public bool IsRemoved => Owner.IsRemoved(Node);

            internal LevelBlock(PlanetLevel owner, PlanetBlock node)
            {
                Owner = owner;
                this.node = node;
                nodeEpoch = owner.generator.Epoch;
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

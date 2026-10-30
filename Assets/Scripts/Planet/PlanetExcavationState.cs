using System.Collections.Generic;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Fronteira recursiva de escavação. Cada setor parcial guarda um contador byte
    /// por coluna angular: quantas faixas externas consecutivas estão completamente vazias.
    /// Estados filhos existem apenas para regiões parcialmente escavadas.
    /// </summary>
    internal sealed class PlanetExcavationState
    {
        private readonly PlanetBlockGenerator generator;
        private readonly Dictionary<int, SectorState> roots = new Dictionary<int, SectorState>();

        internal PlanetExcavationState(PlanetBlockGenerator generator) => this.generator = generator;

        internal bool IsRemoved(PlanetBlock block)
        {
            GetState(block, out bool empty);
            return empty;
        }
        internal bool IsPartial(PlanetBlock block) => GetState(block, out _) != null;

        private SectorState GetState(PlanetBlock block, out bool empty)
        {
            empty = false;
            var address = generator.GetAddress(block);
            if (!roots.TryGetValue(address.RootIndex, out var state)) return null;
            for (int depth = 0; depth < address.Depth; depth++)
            {
                if (state.Empty) { empty = true; return null; }
                var index = address[depth];
                if (index.Radial >= state.Radial - state.RemovedRows[index.Angular])
                { empty = true; return null; }
                int key = index.Radial * state.Angular + index.Angular;
                if (!state.Children.TryGetValue(key, out var child)) return null;
                state = child;
            }
            empty = state.Empty;
            return empty ? null : state;
        }

        /// <summary>Registra uma remoção já autorizada e compacta filhos vazios nos contadores do pai.</summary>
        internal void Remove(PlanetBlock block)
        {
            var address = generator.GetAddress(block);
            generator.TryGetBlock(new PlanetBlockAddress(address.RootIndex), out var root);
            if (!roots.TryGetValue(address.RootIndex, out var state))
            {
                state = new SectorState();
                roots.Add(address.RootIndex, state);
            }
            Remove(root, state, address, 0);
        }

        private void Remove(PlanetBlock node, SectorState state, PlanetBlockAddress address, int depth)
        {
            if (depth == address.Depth)
            {
                state.Empty = true;
                state.RemovedRows = null;
                state.Children = null;
                return;
            }
            if (state.RemovedRows == null)
            {
                generator.GetDivision(node, out state.Radial, out state.Angular);
                state.RemovedRows = new byte[state.Angular];
                state.Children = new Dictionary<int, SectorState>();
            }
            var index = address[depth];
            int key = index.Radial * state.Angular + index.Angular;
            if (!state.Children.TryGetValue(key, out var child))
            {
                child = new SectorState();
                state.Children.Add(key, child);
            }
            Remove(node.Children[key], child, address, depth + 1);
            if (!child.Empty) return;
            // A regra de exposição garante remoção da faixa externa para a interna.
            state.Children.Remove(key);
            state.RemovedRows[index.Angular]++;
            bool allEmpty = true;
            foreach (byte rows in state.RemovedRows)
                if (rows != state.Radial) { allEmpty = false; break; }
            if (allEmpty)
            {
                state.Empty = true;
                state.RemovedRows = null;
                state.Children = null;
            }
        }

        private sealed class SectorState
        {
            internal bool Empty;
            internal int Radial, Angular;
            internal byte[] RemovedRows;
            internal Dictionary<int, SectorState> Children;
        }
    }
}

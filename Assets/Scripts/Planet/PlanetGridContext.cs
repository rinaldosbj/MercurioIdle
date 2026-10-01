using System;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Grade local da divisão imediata de um setor, independente de sua profundidade.
    /// X é o índice angular; Y é o índice radial, crescendo em direção à superfície.
    /// Não atravessa o pai nem envolve X na emenda angular. Células vazias mantêm seu endereço.
    /// </summary>
    public sealed class PlanetGridContext
    {
        private readonly IPlanetLevel level;
        private readonly double[] radialBoundaries;
        private readonly double[] angularBoundaries;
        public PlanetBlockAddress SectorAddress { get; }
        public int Width { get; }
        public int Height { get; }

        internal PlanetGridContext(IPlanetLevel level, PlanetBlockAddress sectorAddress, int width, int height,
            PlanetBlock sector, double radiusMeters)
        {
            this.level = level;
            SectorAddress = sectorAddress;
            Width = width;
            Height = height;
            radialBoundaries = new double[height + 1];
            angularBoundaries = new double[width + 1];
            for (int y = 0; y <= height; y++)
                radialBoundaries[y] = PlanetBlockGeometry.RadialBoundary(
                    sector.InnerRadius, sector.OuterRadius, y, height) * radiusMeters;
            for (int x = 0; x <= width; x++)
                angularBoundaries[x] = PlanetBlockGeometry.AngularBoundary(
                    sector.StartAngle, sector.EndAngle, x, width);
        }

        /// <summary>
        /// Converte uma posição polar física em índices do filho imediato neste contexto,
        /// sem consultar folhas ou o estado da escavação. Retorna false fora do setor.
        /// Bordas inferiores são inclusivas; superiores exclusivas, como na consulta do level.
        /// </summary>
        public bool TryGetCoordinates(PlanetPosition position, out int x, out int y)
        {
            x = y = 0;
            int radial = FindInterval(radialBoundaries, position.RadiusMeters);
            int angular = FindInterval(angularBoundaries, position.AngleRadians);
            if (radial < 0 || angular < 0) return false;
            x = angular;
            y = radial;
            return true;
        }

        private static int FindInterval(double[] boundaries, double value)
        {
            if (value < boundaries[0] || value >= boundaries[boundaries.Length - 1]) return -1;
            int low = 0, high = boundaries.Length - 1;
            while (low + 1 < high)
            {
                int middle = low + (high - low) / 2;
                if (value < boundaries[middle]) high = middle;
                else low = middle;
            }
            return low;
        }

        /// <summary>Resolve índices locais em endereço, mesmo quando a célula foi escavada.</summary>
        public bool TryGetAddress(int x, int y, out PlanetBlockAddress address)
        {
            address = default;
            if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
            address = SectorAddress.Append(new PlanetChildIndex(y, x));
            return true;
        }

        /// <summary>Retorna um filho vivo; false significa célula vazia ou fora da grade.</summary>
        public bool TryGetBlock(int x, int y, out IPlanetBlock block)
        {
            block = null;
            return TryGetAddress(x, y, out var address) && level.TryGetBlock(address, out block);
        }

        /// <summary>Converte o endereço de um filho imediato para índices locais, inclusive após remoção.</summary>
        public bool TryGetCoordinates(PlanetBlockAddress address, out int x, out int y)
        {
            x = y = 0;
            if (address.RootIndex != SectorAddress.RootIndex || address.Depth != SectorAddress.Depth + 1) return false;
            for (int i = 0; i < SectorAddress.Depth; i++)
                if (!address[i].Equals(SectorAddress[i])) return false;
            var index = address[SectorAddress.Depth];
            if (index.Angular >= Width || index.Radial >= Height) return false;
            x = index.Angular;
            y = index.Radial;
            return true;
        }
    }
}

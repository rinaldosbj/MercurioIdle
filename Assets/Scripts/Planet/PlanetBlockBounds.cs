using System;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Limites físicos de um setor: raios em metros e ângulos em radianos.
    /// Intervalos internos são semiabertos para evitar sobreposição entre vizinhos.
    /// </summary>
    public readonly struct PlanetBlockBounds
    {
        public double InnerRadiusMeters { get; }
        public double OuterRadiusMeters { get; }
        public double StartAngleRadians { get; }
        public double EndAngleRadians { get; }

        public PlanetBlockBounds(double innerRadiusMeters, double outerRadiusMeters,
            double startAngleRadians, double endAngleRadians)
        {
            if (!double.IsFinite(innerRadiusMeters) || !double.IsFinite(outerRadiusMeters) ||
                innerRadiusMeters < 0 || outerRadiusMeters <= innerRadiusMeters)
                throw new ArgumentOutOfRangeException(nameof(outerRadiusMeters));
            if (!double.IsFinite(startAngleRadians) || !double.IsFinite(endAngleRadians) ||
                startAngleRadians < 0 || endAngleRadians > 2 * Math.PI || endAngleRadians <= startAngleRadians)
                throw new ArgumentOutOfRangeException(nameof(endAngleRadians));
            InnerRadiusMeters = innerRadiusMeters;
            OuterRadiusMeters = outerRadiusMeters;
            StartAngleRadians = startAngleRadians;
            EndAngleRadians = endAngleRadians;
        }
    }

    /// <summary>
    /// Região de consulta polar. StartAngle é normalizado; AngularWidth está em (0, 2π].
    /// Pode atravessar a emenda angular; os blocos retornados podem exceder seus limites.
    /// </summary>
    public readonly struct PlanetRegion
    {
        public double InnerRadiusMeters { get; }
        public double OuterRadiusMeters { get; }
        public double StartAngleRadians { get; }
        public double AngularWidthRadians { get; }

        public PlanetRegion(double innerRadiusMeters, double outerRadiusMeters,
            double startAngleRadians, double angularWidthRadians)
        {
            var start = new PlanetPosition(innerRadiusMeters, startAngleRadians);
            if (!double.IsFinite(outerRadiusMeters) || outerRadiusMeters <= innerRadiusMeters)
                throw new ArgumentOutOfRangeException(nameof(outerRadiusMeters));
            if (!double.IsFinite(angularWidthRadians) || angularWidthRadians <= 0 || angularWidthRadians > 2 * Math.PI)
                throw new ArgumentOutOfRangeException(nameof(angularWidthRadians));
            InnerRadiusMeters = start.RadiusMeters;
            OuterRadiusMeters = outerRadiusMeters;
            StartAngleRadians = start.AngleRadians;
            AngularWidthRadians = angularWidthRadians;
        }
    }
}

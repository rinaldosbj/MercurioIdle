using System;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Posição polar no corte do planeta: raio em metros e ângulo em radianos.
    /// Zero aponta para +X; o ângulo cresce no sentido anti-horário.
    /// </summary>
    public readonly struct PlanetPosition
    {
        public double RadiusMeters { get; }
        public double AngleRadians { get; }

        /// <summary>Cria uma posição e normaliza o ângulo para [0, 2π).</summary>
        public PlanetPosition(double radiusMeters, double angleRadians)
        {
            if (!double.IsFinite(radiusMeters) || radiusMeters < 0)
                throw new ArgumentOutOfRangeException(nameof(radiusMeters));
            if (!double.IsFinite(angleRadians))
                throw new ArgumentOutOfRangeException(nameof(angleRadians));
            RadiusMeters = radiusMeters;
            double angle = angleRadians % (2 * Math.PI);
            AngleRadians = angle < 0 ? angle + 2 * Math.PI : angle;
        }
    }
}

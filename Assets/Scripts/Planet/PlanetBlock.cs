using System;
using System.Collections.Generic;
using UnityEngine;

namespace MercurioIdle.Planet
{
    // Um setor polar. Raios normalizados pelo raio do planeta; ângulos em radianos.
    // As raízes têm profundidade zero. O núcleo é um bloco especial indivisível.
    public sealed class PlanetBlock
    {
        public int Id { get; }
        public double InnerRadius { get; }
        public double OuterRadius { get; }
        public double StartAngle { get; }
        public double EndAngle { get; }
        public int Depth { get; }
        public bool IsCore { get; }

        /// <summary>
        /// Largura angular do setor, em radianos. Varia de 0 a 2π.
        /// </summary>
        public double AngularWidth => EndAngle - StartAngle;
        double Height => OuterRadius - InnerRadius;
        public double Area => 0.5 * (OuterRadius - InnerRadius) * (OuterRadius + InnerRadius) * AngularWidth;
        public double Scale => Math.Sqrt(Area);
        private double Mean(double x, double y) => (x + y) * 0.5;

        public double AspectRatio => IsCore ? double.NaN :
            Mean(InnerRadius, OuterRadius) * AngularWidth
            / Height;
        public bool IsLeaf => Children.Count == 0;
        public PlanetBlock Parent { get; }

        // Lista vazia representa uma folha. A subdivisão será implementada depois.
        public List<PlanetBlock> Children { get; } = new List<PlanetBlock>();

        public PlanetBlock(int id, double innerRadius, double outerRadius,
            double startAngle, double endAngle, PlanetBlock parent = null, bool isCore = false)
        {
            Id = id;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
            StartAngle = startAngle;
            EndAngle = endAngle;
            Parent = parent;
            Depth = parent == null ? 0 : parent.Depth + 1;
            IsCore = isCore;
        }
    }
}

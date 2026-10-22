using MercurioIdle.Planet;
using System;
using Unity.VisualScripting;
using UnityEngine;
using static comparisonExtensions;


public static class PlanetBlockGeometry
{
    public static bool IsValidSector(this PlanetBlock block, PlanetBlockSettings settings) =>

          !block.IsCore
        &&  double.IsFinite(block.InnerRadius)
        &&  double.IsFinite(block.OuterRadius)
        &&  double.IsFinite(block.StartAngle)
        &&  double.IsFinite(block.EndAngle)
        &&  block.InnerRadius > 0
        &&  block.OuterRadius > block.InnerRadius
        &&  block.AngularWidth.InRange((Open) 0, Math.PI * 2)
        &&!(block.OuterRadius / block.InnerRadius).GreaterThan(settings.MaxRadialRatio, epsilon: 1e-9)
        &&  block.AspectRatio.InRange(1.0 / settings.MaxAspectRatio, settings.MaxAspectRatio, epsilon: 1e-9);

    /// <summary>
    /// Calcula o raio da borda de índice <paramref name="index"/> em uma
    /// subdivisão radial com <paramref name="count"/> faixas em progressão geométrica.
    /// Preserva exatamente os raios inicial e final e não cria blocos filhos.
    /// </summary>
    /// <param name="innerRadius">Raio interno, positivo e finito.</param>
    /// <param name="outerRadius">Raio externo, finito e maior que o raio interno.</param>
    /// <param name="index">Índice da borda, de zero a <paramref name="count"/>, inclusive.</param>
    /// <param name="count">Quantidade de faixas radiais, maior que zero.</param>
    /// <returns>Raio da borda, nas mesmas unidades dos raios de entrada.</returns>
    /// <remarks>
    /// O chamador deve garantir as pré-condições; este método não valida os argumentos.
    /// Por exemplo, raios de 1 a 4 com duas faixas produzem as bordas 1, 2 e 4.
    /// </remarks>
    public static double RadialBoundary(double innerRadius, double outerRadius, int index, int count)
    {
        if (index == 0) return innerRadius;
        if (index == count) return outerRadius;
        return innerRadius * Math.Exp(Math.Log(outerRadius / innerRadius) * index / count);
    }

    /// <summary>
    /// Calcula uma borda angular de uma divisão em partes iguais, em radianos.
    /// Preserva os extremos exatos. O chamador garante count positivo e index entre zero e count.
    /// </summary>
    public static double AngularBoundary(double startAngle, double endAngle, int index, int count)
    {
        if (index == 0) return startAngle;
        if (index == count) return endAngle;
        return startAngle + (endAngle - startAngle) * index / count;
    }

    /// <summary>
    /// Avalia uma divisão radial/ angular sem criar filhos nem modificar o pai.
    /// Mantém bordas radiais geométricas, verifica Q/K e mede as áreas extremas em m².
    /// Aceita apenas filhos dentro da faixa comum de área da profundidade seguinte.
    /// </summary>
    /// <param name="block">Setor pai, com raios normalizados pelo raio do planeta.</param>
    /// <param name="settings">Parâmetros físicos e restrições de forma e área.</param>
    /// <param name="radialDivisions">Quantidade M de faixas radiais.</param>
    /// <param name="angularDivisions">Quantidade N de partes angulares.</param>
    /// <param name="evaluation">Áreas extremas e validade da divisão candidata.</param>
    /// <returns>Verdadeiro se forma e faixa de área forem respeitadas.</returns>
    /// <remarks>
    /// A faixa é alvo / sqrt(MaxAreaRatio) até alvo * sqrt(MaxAreaRatio).
    /// Assim, quaisquer filhos aceitos no mesmo nível têm razão de áreas limitada,
    /// inclusive quando pertencem a pais diferentes. Nenhum fallback ignora a restrição.
    /// Raízes são agrupadores isentos de Q/K; seus filhos continuam sujeitos às restrições.
    /// </remarks>
    public static bool TryEvaluateSubdivision(PlanetBlock block, PlanetBlockSettings settings,
        int radialDivisions, int angularDivisions, out SubdivisionEvaluation evaluation)
    {
        evaluation = default;
        if (block == null) throw new ArgumentNullException(nameof(block));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (!double.IsFinite(settings.MaxAreaRatio) || settings.MaxAreaRatio < 1)
            throw new ArgumentOutOfRangeException(nameof(settings.MaxAreaRatio));
        // Raízes agrupam setores; as restrições Q/K começam nos seus filhos.
        bool validParent = block.Depth == 0
            ? !block.IsCore && double.IsFinite(block.InnerRadius) && double.IsFinite(block.OuterRadius) &&
                double.IsFinite(block.StartAngle) && double.IsFinite(block.EndAngle) &&
                block.InnerRadius > 0 && block.OuterRadius > block.InnerRadius &&
                block.AngularWidth > 0 && block.AngularWidth <= 2 * Math.PI
            : block.IsValidSector(settings);
        if (!validParent || block.Depth >= settings.DetailLevels - 1 ||
            radialDivisions < settings.MinDivisionsPerAxis || radialDivisions > settings.MaxDivisionsPerAxis ||
            angularDivisions < settings.MinDivisionsPerAxis || angularDivisions > settings.MaxDivisionsPerAxis ||
            radialDivisions < 1 || angularDivisions < 1 ||
            (radialDivisions == 1 && angularDivisions == 1)) return false;

        double target = settings.GetTargetAreaSquareMeters(block.Depth + 1);
        double angle = block.AngularWidth / angularDivisions;
        double minArea = double.PositiveInfinity, maxArea = 0;
        bool validShape = true;
        // Em cada faixa radial todos os N filhos são congruentes: basta avaliar um.
        for (int i = 0; i < radialDivisions; i++)
        {
            double r0 = RadialBoundary(block.InnerRadius, block.OuterRadius, i, radialDivisions);
            double r1 = RadialBoundary(block.InnerRadius, block.OuterRadius, i + 1, radialDivisions);
            double aspect = (r0 + r1) * .5 * angle / (r1 - r0);
            validShape &= r1 > r0 && double.IsFinite(aspect) &&
                !(r1 / r0).GreaterThan(settings.MaxRadialRatio, 1e-9) &&
                aspect.InRange(1 / settings.MaxAspectRatio, settings.MaxAspectRatio, 1e-9);
            // Produto fatorado evita subtrair quadrados quase iguais nas folhas de 1 m.
            double area = .5 * ((r1 - r0) * settings.PlanetRadiusMeters) *
                ((r1 + r0) * settings.PlanetRadiusMeters) * angle;
            minArea = Math.Min(minArea, area);
            maxArea = Math.Max(maxArea, area);
        }
        double factor = Math.Sqrt(settings.MaxAreaRatio);
        bool validArea = double.IsFinite(minArea) && double.IsFinite(maxArea) &&
            minArea > 0 && minArea >= target / factor && maxArea <= target * factor;
        evaluation = new SubdivisionEvaluation(minArea, maxArea, validShape, validArea);
        return validShape && validArea;
    }

    /// <summary>Resultado sem alocações da avaliação de uma divisão candidata.</summary>
    public readonly struct SubdivisionEvaluation
    {
        public double MinChildAreaSquareMeters { get; }
        public double MaxChildAreaSquareMeters { get; }
        public bool HasValidShape { get; }
        public bool HasValidArea { get; }
        public double AreaRatio => MaxChildAreaSquareMeters / MinChildAreaSquareMeters;

        public SubdivisionEvaluation(double minArea, double maxArea, bool validShape, bool validArea)
        {
            MinChildAreaSquareMeters = minArea;
            MaxChildAreaSquareMeters = maxArea;
            HasValidShape = validShape;
            HasValidArea = validArea;
        }
    }

}
static class comparisonExtensions
{
    public static bool GreaterThan(this double a, double b, double epsilon = 0)
    {
        return a - b > epsilon;
    }
    public static bool LessThan(this double a, double b, double epsilon = 0)
    {
        return b - a > epsilon;
    }

    public static bool InRange(this double value, double start, double end, double epsilon = 0) =>
        value >= start - epsilon && value <= end + epsilon;
    public static bool InRange(this double value, Open start, double end, double epsilon = 0) =>
        value > start.Value + epsilon && value <= end + epsilon;
    public static bool InRange(this double value, double start, Open end, double epsilon = 0) =>
        value >= start - epsilon && value < end.Value - epsilon;

    public readonly struct Open
    {
        public Open(double value) => Value = value;
        public double Value { get; }
        public static explicit operator Open(double value) => new Open(value);
    }
}

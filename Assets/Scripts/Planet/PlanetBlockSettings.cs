using System;
using UnityEngine;

namespace MercurioIdle.Planet
{
    // Serializável para exposição no Inspector por um futuro componente Unity.
    // Mantém os valores padrão do protótipo, com nomes explícitos.
    [Serializable]
    public sealed class PlanetBlockSettings
    {
        [Tooltip("Raio físico do planeta em metros. Padrão aproximado de Mercúrio: 2440 km.")]
        public double PlanetRadiusMeters = 2440000;
        [Tooltip("Raiz quadrada da área alvo das folhas em metros. 1 corresponde a 1 m², não a lados exatamente iguais.")]
        public double LeafTargetSizeMeters = 1;
        [Tooltip("Razão máxima entre maior e menor área no mesmo nível. 1.25 permite até 25% de diferença. A raiz é exceção.")]
        public double MaxAreaRatio = 1.25;
        [Tooltip("Raio do núcleo indivisível, como fração do raio do planeta.")]
        public double CoreRadius = .12;
        [Tooltip("Q: limite da razão entre raio externo e interno de um setor.")]
        public double MaxRadialRatio = 1.8;
        [Tooltip("K: aspecto permitido entre 1/K e K. Aspecto = arco médio / espessura radial.")]
        public double MaxAspectRatio = 1.3;
        [Tooltip("T: razão de escalas acima da qual a variação recebe penalidade.")]
        public double ScaleVariationTolerance = 1.6;

        [Tooltip("Preferência de escala raiz normalizada. Ajustada para reduções inteiras e margem de área; não é um tamanho raiz obrigatório.")]
        public double RootTargetScale = .16;
        [Tooltip("Escala alvo da primeira subdivisão. Escala = raiz quadrada da área polar, em unidades normalizadas.")]
        public double FirstSubdivisionTargetScale = .018;

        [Tooltip("Quantidade mínima de divisões em cada eixo (radial e angular).")]
        public int MinDivisionsPerAxis = 1;
        [Tooltip("Quantidade máxima de divisões em cada eixo (radial e angular), até 255 para contadores byte.")]
        public int MaxDivisionsPerAxis = 8;
        [Tooltip("Total de níveis, incluindo raízes agrupadoras. O gerador sob demanda aceita de 2 a 32.")]
        public int DetailLevels = 8;

        [Tooltip("Peso da preferência por setores com aspecto próximo de 1.")]
        public double ShapeWeight = .5;
        [Tooltip("Peso da proximidade à escala alvo.")]
        public double ScaleWeight = 4;
        [Tooltip("Peso da penalidade por variação de escala acima da tolerância T.")]
        public double VariationWeight = 2;
        [Tooltip("Peso do custo da quantidade de setores ou filhos gerados.")]
        public double ChildCountWeight = .002;
        [Tooltip("Peso da estimativa de profundidade necessária para alcançar a escala alvo.")]
        public double DepthWeight = .4;

        /// <summary>Copia os parâmetros para isolar o level de alterações posteriores no Inspector.</summary>
        public PlanetBlockSettings Copy() => (PlanetBlockSettings)MemberwiseClone();

        /// <summary>
        /// Calcula a área alvo em m² de um nível, usando um fator inteiro de
        /// redução por eixo até a folha de tamanho físico configurado.
        /// Todos os pais usam o mesmo alvo para uma mesma profundidade.
        /// </summary>
        /// <remarks>
        /// Profundidade zero representa a raiz e não recebe restrição de área.
        /// Este alvo não garante que exista uma divisão M/N viável.
        /// </remarks>
        public double GetTargetAreaSquareMeters(int depth)
        {
            if (DetailLevels < 2 || depth < 0 || depth >= DetailLevels)
                throw new ArgumentOutOfRangeException(nameof(depth));
            if (!double.IsFinite(PlanetRadiusMeters) || PlanetRadiusMeters <= 0 ||
                !double.IsFinite(LeafTargetSizeMeters) || LeafTargetSizeMeters <= 0 ||
                !double.IsFinite(RootTargetScale) || RootTargetScale <= 0)
                throw new InvalidOperationException("Raio e escalas devem ser positivos e finitos.");

            double rootSize = RootTargetScale * PlanetRadiusMeters;
            // Uma redução fracionária pode não ter nenhum M/N inteiro viável.
            // A escala raiz é uma preferência; a escala física da folha é o alvo final.
            double desiredFactor = Math.Exp(Math.Log(rootSize / LeafTargetSizeMeters) / (DetailLevels - 1));
            double factor = Math.Max(2, Math.Round(desiredFactor));
            // A primeira faixa precisa reservar a variação acumulada até as folhas.
            // Limita a largura preferida pela coroa mais interna, onde a razão radial é maior.
            double maxFirstSize = CoreRadius * PlanetRadiusMeters * (Math.Sqrt(MaxAreaRatio) - 1);
            while (factor > 2 && LeafTargetSizeMeters * Math.Pow(factor, DetailLevels - 2) > maxFirstSize)
                factor--;
            double size = LeafTargetSizeMeters * Math.Pow(factor, DetailLevels - 1 - depth);
            return depth == DetailLevels - 1 ? LeafTargetSizeMeters * LeafTargetSizeMeters : size * size;
        }
    }
}

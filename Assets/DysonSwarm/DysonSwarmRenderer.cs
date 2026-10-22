using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

[Serializable]
public struct DysonRingSettings
{
    [Min(0.01f), Tooltip("Raio em unidades mundiais. Com espaçamento uniforme, somente o raio do primeiro anel define a base.")] public float radius;
    [Tooltip("Normal central mundial deste anel. Os planos variam por revolução ao redor deste vetor; todos passam pelo Sol.")] public Vector3 ringNormal;
    [Range(0, 30), Tooltip("Graus. No toro preenchido, soma raio central x seno do angulo ao desvio padrao vertical de Altitude Variation. Fora dele, define a inclinacao fixa. Use zero para controlar a altura somente em unidades mundiais.")] public float angularThickness;
    [Min(1), Tooltip("Maximo com Population Growth; quantidade fixa no modo manual. Orbitas por grupo, com nos regularmente espacados ao redor da normal do anel. Total preenchido = Plane Count x Orbit Groups x Slots Per Plane.")] public int planeCount;
    [Min(1), Tooltip("Maximo de coletores por orbita com Population Growth; quantidade fixa no modo manual. Coletores igualmente espacados por orbita. Aumentar este valor aumenta a quantidade e o custo, sem alterar a largura da nuvem.")] public int slotsPerPlane;
    [Tooltip("Fase inicial em graus para este anel, somada ao escalonamento entre orbitas.")] public float phaseOffset;
    [Tooltip("Velocidade orbital em graus por segundo. Valores negativos invertem o sentido.")] public float orbitalSpeed;
}

[ExecuteAlways]
public sealed class DysonSwarmRenderer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Centro mundial da estrela e das órbitas. Sem referência, usa a posição deste objeto.")]
    [SerializeField] private Transform sun;
    [Tooltip("Destino mundial da reflexão. Sem referência, usa Fallback Target Position.")]
    [SerializeField] private Transform target;
    [Tooltip("Malha opcional no plano XY com normal +Z e raio unitario. Sem referencia, gera um hexagono.")]
    [SerializeField] private Mesh panelMesh;
    [Tooltip("Material com o shader MercurioIdle/DysonSwarm.")]
    [SerializeField] private Material panelMaterial;
    [Tooltip("Posição mundial do alvo quando Target não está atribuído.")]
    [SerializeField] private Vector3 fallbackTargetPosition = new Vector3(18, 0, 0);
    [Header("Panel")]
    [Tooltip("Raio do hexágono em unidades mundiais, independente da escala do objeto.")]
    [SerializeField, Min(0.001f)] private float panelSize = 0.045f;
    [Tooltip("Cor HDR dos painéis.")]
    [SerializeField, ColorUsage(false, true)] private Color color = new Color(0.3f, 0.65f, 1f);
    [Tooltip("Intensidade do aspecto metálico na iluminação simplificada.")]
    [SerializeField, Range(0, 1)] private float metallic = 0.85f;
    [Tooltip("Concentração do brilho especular. Valores maiores deixam o brilho mais estreito.")]
    [SerializeField, Range(0, 1)] private float smoothness = 0.8f;
    [Tooltip("Cor e intensidade HDR de emissão dos painéis.")]
    [SerializeField, ColorUsage(false, true)] private Color emission = new Color(0.03f, 0.1f, 0.2f);
    [Header("Solar Reflection")]
    [SerializeField, ColorUsage(false, true), Tooltip("Cor HDR da luz refletida do Sol.")]
    private Color solarColor = new Color(1f, 0.83f, 0.48f);
    [SerializeField, ColorUsage(false, true), Tooltip("Cor dos coletores na face escura ou em eclipse.")]
    private Color shadowColor = new Color(0.025f, 0.04f, 0.085f);
    [SerializeField, Min(0), Tooltip("Intensidade do brilho quando a reflexão aponta para a câmera.")]
    private float reflectionStrength = 8f;
    [SerializeField, Range(0.1f, 15), Tooltip("Largura angular adicional do reflexo solar em graus. O tamanho aparente do Sol também é considerado.")]
    private float glintWidth = 1.5f;
    [SerializeField, Min(0.01f), Tooltip("Raio mundial do Sol usado no brilho e no bloqueio da reflexão para o alvo.")]
    private float solarRadius = 2f;
    [SerializeField, Tooltip("Corpo opcional que pode eclipsar a luz do Sol, como um planeta. Sem referência, não há eclipse por outro corpo.")]
    private Transform eclipseOccluder;
    [SerializeField, Min(0.001f), Tooltip("Raio mundial da esfera usada para representar o corpo que eclipsa o Sol.")]
    private float eclipseRadius = 1f;
    [SerializeField, Range(0.001f, 0.5f), Tooltip("Suavidade da borda do eclipse em relação ao raio do corpo.")]
    private float eclipseSoftness = 0.1f;
    [Header("Rendering")]
    [Tooltip("Tamanho minimo da regiao visivel em unidades mundiais; expandido para conter as orbitas e caudas da nuvem.")]
    [SerializeField, Min(0)] private float boundsSize = 0;
    [Tooltip("Anima o movimento fora do Play. Desativado, a prévia continua atualizando ao editar parâmetros.")]
    [SerializeField] private bool animateInEditor = false;
    [Tooltip("Verde indica reflexão dentro da tolerância; vermelho indica erro.")]
    [SerializeField] private bool debugReflection = false;
    [Tooltip("Erro máximo permitido entre a direção refletida e a direção do alvo.")]
    [SerializeField, Min(0.000001f)] private float reflectionTolerance = 0.001f;
    [Header("Point LOD")]
    [SerializeField, Tooltip("Troca coletores pequenos por pontos na GPU, sem reduzir a quantidade.")]
    private bool enablePointLod = true;
    private bool enableCollectorFrustumCulling = true;
    private bool enableOrbitCache = true;
    private bool enableSurfaceFaceCulling = true;
    private bool enableSharedSurfaceDepth = true;
    private bool enableSharedSurfaceResolve = true;
    private bool enableSharedCollectorOcclusion = true;
    private bool validateSharedCollectorOcclusion = false;
    private bool useReducedHexMesh = true;
    private bool ownedMeshReduced;
    [SerializeField, Min(0.1f), Tooltip("Diâmetro projetado em pixels abaixo do qual o coletor vira ponto.")]
    private float pointLodPixels = 2.5f;
    [SerializeField, Range(0, 1), Tooltip("Tamanho mínimo do ponto em pixels; zero permite desaparecer naturalmente abaixo de um pixel.")]
    private float minimumPointPixels = 0.5f;
    [Header("Orbit Distribution")]
    [SerializeField, HideInInspector, Range(0, 1), Tooltip("Varia o raio por órbita dentro de Radial Variation. A semente não muda durante a animação.")]
    private float orbitAltitudeRandomness = 1f;
    [SerializeField, HideInInspector, Range(0, 1), Tooltip("Legado: preservado para compatibilidade; nao altera a nuvem gaussiana.")]
    private float orbitInclinationRandomness = 0.75f;
    [SerializeField, Range(0, 1), Tooltip("Distribui a fase entre órbitas mantendo os coletores igualmente espaçados em cada plano.")]
    private float phaseStagger = 1f;
    [SerializeField, Tooltip("Semente reproduzível da distribuição de altitude, inclinação e fase.")]
    private int distributionSeed = 12345;
    [Header("Global Orbit Settings")]
    [SerializeField, Min(1), Tooltip("Maximo com Population Growth; quantidade fixa no modo manual. Orbitas por grupo, com nos regularmente espacados ao redor da normal do anel. Total preenchido = Plane Count x Orbit Groups x Slots Per Plane.")]
    [InspectorName("Maximum Plane Count")]
    private int planeCount = 24;
    [SerializeField, Min(1), Tooltip("Maximo de coletores por orbita com Population Growth; quantidade fixa no modo manual. Coletores igualmente espacados por orbita. Aumentar este valor aumenta a quantidade e o custo, sem alterar a largura da nuvem.")]
    [InspectorName("Maximum Slots Per Plane")]
    private int slotsPerPlane = 512;
    [SerializeField, Range(0,30), Tooltip("Graus. No toro preenchido, soma raio central x seno do angulo ao desvio padrao vertical de Altitude Variation. Fora dele, define a inclinacao fixa. Use zero para controlar a altura somente em unidades mundiais.")]
    private float angularThickness = 2f;
    [SerializeField, Tooltip("Velocidade orbital em graus por segundo. Valores negativos invertem o sentido.")]
    private float orbitalSpeed = 5f;
    [SerializeField, Tooltip("Avançado: substitui planos, slots, espessura e velocidade globais pelos valores individuais.")]
    private bool usePerRingOverrides;
    [Header("Population Growth")]
    [SerializeField, Tooltip("Ativa crescimento calculado na CPU ao editar. Ring Count, Plane Count e Slots Per Plane tornam-se maximos.")]
    private bool populationGrowth = true;
    [SerializeField, Tooltip("Quantidade real de espelhos. A aparencia usa o mapeamento linear/logaritmico abaixo; o valor real e preservado.")]
    private double mirrorCount = 540000;
    [Header("Real To Visual Mirror Mapping")]
    [SerializeField, Min(1), Tooltip("Quantidade real ate a qual cada espelho corresponde a um espelho visual (1:1).")]
    private double linearMirrorLimit = 1000;
    [SerializeField, Min(2), Tooltip("Populacao visual maxima. Acima do limite linear, a populacao cresce logaritmicamente ate este valor.")]
    private double maximumVisualMirrors = 30000000;
    [SerializeField, Min(2), Tooltip("Quantidade real que corresponde ao maximo visual. Valores maiores mantem a visualizacao no teto.")]
    private double maximumRealMirrors = 42100000000000;
    [Header("Visual Population Growth")]

    [SerializeField, Tooltip("Intervalo de populacao visual entre novos aneis. Cada etapa distribui apenas os espelhos novos entre seus aneis; os IDs existentes permanecem. Zero espelhos desativa todos.")]
    private double mirrorsPerAdditionalRing = 100000;
    [SerializeField, HideInInspector]
    private double mirrorsPerAdditionalPlane = 3600;
    [SerializeField, HideInInspector]
    private double mirrorsPerAdditionalSlot = 6800;
    [SerializeField, HideInInspector]
    private double widthLimitStart = 100000;
    [SerializeField, Min(0.001f), Tooltip("Largura maxima total da secao da nuvem em unidades mundiais, incluindo caudas de 4 sigma e curvatura das orbitas. Limita tanto a altura quanto a largura radial.")]
    private float maximumCloudWidth = 8f;
    [Header("Continuous Surface")]
    [SerializeField, Tooltip("Substitui o centro saturado por uma faixa e expande sua superficie com a populacao. Mantem coletores individuais nas bordas.")]
    private bool continuousSurface = true;
    [SerializeField, Range(0, 0.5f), Tooltip("Cosseno do angulo de visao em que a faixa fica opaca. Abaixo disso ela desaparece suavemente perto da tangente, devolvendo a visibilidade aos coletores. Zero desativa.")]
    private float surfaceGrazingFade = 0.22f;
    [SerializeField, Range(0.1f,1), Tooltip("Fracao da superficie ocupada por espelhos que define saturacao. Usa a area do hexagono de Physical Mirror Radius; nao depende da camera nem de Panel Size.")]
    private float saturationOccupancy = 0.8f;
    [SerializeField, Min(0.000001f), Tooltip("Raio fisico do espelho em unidades mundiais para calcular area e saturacao. Separado de Panel Size, que controla apenas a aparencia. O Inspector mostra a populacao total necessaria para saturar cada anel.")]
    private float physicalMirrorRadius = 0.01f;
    private int[] resolvedCounts, resolvedGroups;
    private Vector4[] resolvedSurfaces;
    private Vector4[][] resolvedOrbitCdfs;
    private float[] resolvedRadialMixtures;
    private const int BirthCoreSamples = 129;
    public static Vector4[] BuildBirthOrbitCdf(double sigma)
    {
        var table = new Vector4[BirthCoreSamples * 256];
        for (int i = 0; i < BirthCoreSamples; i++) {
            var cdf = BuildSurfaceOrbitCdf((double)i / (BirthCoreSamples - 1), sigma, out double mass);
            double fullMass = 2 * SurfaceFraction((double)i / (BirthCoreSamples - 1), sigma);
            for (int q = 0; q < 256; q++) {
                var entry = cdf[q];
                entry.z = (float)mass; entry.w = (float)fullMass;
                table[i * 256 + q] = entry;
            }
        }
        return table;
    }
    public static Vector4[] BuildSurfaceOrbitCdf(double core, double sigma, out double orbitMass)
    {
        // A flat core with Gaussian outskirts is a mixture of uniform spherical belts.
        // Each belt is generated by centered circular orbits, not latitude circles.
        const int bins=2048,quantiles=256;
        sigma=Math.Max(1e-6,sigma);core=Math.Max(0,Math.Min(1,core));
        var weights=new double[bins];var fullWeights=new double[bins];double sum=0,fullSum=0;
        double step=(1-core)/bins;
        for(int k=0;k<bins;k++) {
            double t=core+(k+0.5)*step;
            double d=(t-core)/sigma;
            double weight=2*Math.Sqrt(Math.Max(0,t*t-core*core))*d/sigma*Math.Exp(-0.5*d*d)*step;
            sum+=weight;weights[k]=sum;
            fullSum+=2*t*d/sigma*Math.Exp(-0.5*d*d)*step;fullWeights[k]=fullSum;
        }
        double edge=(1-core)/sigma;
        orbitMass=sum+2*Math.Exp(-0.5*edge*edge)*Math.Sqrt(Math.Max(0,1-core*core));
        double fullMass=fullSum+2*Math.Exp(-0.5*edge*edge);
        var result=new Vector4[quantiles];int index=0,fullIndex=0;
        for(int q=0;q<quantiles;q++) {
            double target=(q+0.5)/quantiles*orbitMass;
            while(index<bins-1 && weights[index]<target) index++;
            double t=target>sum ? 1 : core+(index+0.5)*step;
            double fullTarget=(q+0.5)/quantiles*fullMass;
            while(fullIndex<bins-1 && fullWeights[fullIndex]<fullTarget) fullIndex++;
            double fullT=fullTarget>fullSum ? 1 : core+(fullIndex+0.5)*step;
            result[q]=new Vector4((float)t,(float)fullT,0,0);
        }
        return result;
    }

    public double RepresentedMirrorCount => populationGrowth ? Math.Max(0,mirrorCount) : RenderedCollectorCount;
    public void GetSurfaceState(int index, out float coreLatitude, out double saturationCount, out int individualCount)
    {
        coreLatitude = Mathf.Asin(resolvedSurfaces[index].x)*Mathf.Rad2Deg;
        saturationCount = SurfacePopulation(Mathf.Max(0.01f,rings[index].radius), resolvedHeights[index], 0, Mathf.Max(0.000001f,physicalMirrorRadius), saturationOccupancy);
        individualCount = resolvedCounts[index];
    }
    private static double Erf(double x)
    {
        double t = 1/(1+0.3275911*x);
        return 1-(((((1.061405429*t-1.453152027)*t)+1.421413741)*t-0.284496736)*t+0.254829592)*t*Math.Exp(-x*x);
    }
    public static double SurfaceFraction(double core, double sigma)
    {
        core = Math.Max(0,Math.Min(1,core)); sigma = Math.Max(1e-6,sigma);
        // Flat saturated core plus Gaussian outskirts; integral over spherical area coordinate sin(latitude).
        return core + sigma*Math.Sqrt(Math.PI/2)*Erf((1-core)/(Math.Sqrt(2)*sigma));
    }
    public static double SurfacePopulation(double radius, double heightSigma, double core, double size, double occupancy)
    {
        double mirrorArea = 3*Math.Sqrt(3)*size*size/2;
        return 4*Math.PI*radius*radius*occupancy/mirrorArea*SurfaceFraction(core,heightSigma/radius);
    }
    public static double ResolveSurfaceCore(double population, double radius, double heightSigma, double size, double occupancy)
    {
        if (population <= SurfacePopulation(radius,heightSigma,0,size,occupancy)) return 0;
        if (population >= SurfacePopulation(radius,heightSigma,1,size,occupancy)) return 1;
        double low = 0, high = 1;
        for (int j=0;j<48;j++) {
            double mid=(low+high)/2;
            if (SurfacePopulation(radius,heightSigma,mid,size,occupancy)<population) low=mid; else high=mid;
        }
        return (low+high)/2;
    }
    private int activeRingCount;
    private int[] resolvedPlanes, resolvedSlots;
    private float[] resolvedWidths, resolvedHeights;
    public int ActiveRingCount => activeRingCount;
    public double MirrorCount => mirrorCount;
    public double MaximumRealMirrors => maximumRealMirrors;
    public double VisualMirrorCount => MapRealToVisualMirrors(mirrorCount, linearMirrorLimit, maximumVisualMirrors, maximumRealMirrors);

    public static double MapRealToVisualMirrors(double realCount, double linearLimit = 1000,
        double visualMaximum = 30000000, double realMaximum = 42100000000000)
    {
        if (double.IsNaN(linearLimit) || double.IsInfinity(linearLimit) || linearLimit < 1 ||
            double.IsNaN(visualMaximum) || double.IsInfinity(visualMaximum) || visualMaximum <= linearLimit ||
            double.IsNaN(realMaximum) || double.IsInfinity(realMaximum) || realMaximum < visualMaximum)
            throw new ArgumentOutOfRangeException(nameof(linearLimit), "Require 1 <= linearLimit < visualMaximum <= realMaximum.");
        if (double.IsNaN(realCount) || realCount <= 0) return 0;
        if (realCount <= linearLimit) return realCount;
        if (realCount >= realMaximum) return visualMaximum;
        if (realMaximum == visualMaximum) return realCount;
        double realSpan = realMaximum - linearLimit;
        double visualSpan = visualMaximum - linearLimit;
        // y = L + a * log(1 + (x-L)/a), so y'(L)=1 and y'=a/(a+x-L)<=1.
        // Solve a * log(1 + realSpan/a) = visualSpan in log space to keep
        // both configured endpoints without overflow or cancellation near the join.
        double logRatio = Math.Log(visualSpan) - Math.Log(realSpan);
        double low = -40, high = -logRatio + Math.Log(1024);
        for (int i = 0; i < 96; i++) {
            double middle = (low + high) * 0.5;
            double candidate = Math.Log(LogOnePlusExp(middle)) - middle;
            if (candidate > logRatio) low = middle; else high = middle;
        }
        double logQ = (low + high) * 0.5; // q = realSpan/a.
        double fraction = LogOnePlusExp(logQ + Math.Log(realCount - linearLimit) - Math.Log(realSpan))
            / LogOnePlusExp(logQ);
        return Math.Min(visualMaximum, linearLimit + visualSpan * fraction);
    }

    private static double LogOnePlusExp(double value)
    {
        return value > 0 ? value + LogOnePlus(Math.Exp(-value)) : LogOnePlus(Math.Exp(value));
    }

    private static double LogOnePlus(double value)
    {
        // Math.Log(1+x) loses small x; preserve the slope at the linear/log join.
        return value < 0.0001
            ? value * (1 - value * (0.5 - value * (1.0 / 3 - value * 0.25)))
            : Math.Log(1 + value);
    }

    private void ValidateMirrorMapping()
    {
        if (double.IsNaN(linearMirrorLimit) || double.IsInfinity(linearMirrorLimit)) linearMirrorLimit = 1000;
        linearMirrorLimit = Math.Max(1, Math.Min(1e15, linearMirrorLimit));
        if (double.IsNaN(maximumVisualMirrors) || double.IsInfinity(maximumVisualMirrors)) maximumVisualMirrors = 30000000;
        maximumVisualMirrors = Math.Max(linearMirrorLimit + 1, maximumVisualMirrors);
        if (double.IsNaN(maximumRealMirrors) || double.IsInfinity(maximumRealMirrors)) maximumRealMirrors = 42100000000000;
        maximumRealMirrors = Math.Max(maximumVisualMirrors, maximumRealMirrors);
    }
    public float WallpaperSolarRadius => Mathf.Max(solarRadius, viewSolarRadius);
    public DysonRingSettings GetRingSettings(int index)
    {
        var ring = rings[index];
        ring.radius = Mathf.Max(0.01f, useUniformRingSpacing ? baseRadius + index * Mathf.Max(0, ringSeparation) : ring.radius);
        ring.ringNormal = autoDistributeRingNormals ? DistributedRingNormal(index, RingCount)
            : (ring.ringNormal.sqrMagnitude > 1e-8f ? ring.ringNormal.normalized : Vector3.up);
        return ring;
    }

    public long AllocatedSwarmBufferBytes {
        get {
            long bytes = 0;
            bytes += BufferBytes(panels) + BufferBytes(nearIds) + BufferBytes(pointPositions);
            bytes += BufferBytes(pointArgs) + BufferBytes(counters) + BufferBytes(orbitParameters) + BufferBytes(args);
            bytes += BufferBytes(birthOrbitCdfs);
            return bytes;
        }
    }
    public void SetMirrorCount(double count)
    {
        mirrorCount = double.IsNaN(count) || double.IsInfinity(count) ? 0 : Math.Max(0, count);
        OnValidate();
    }
    public Vector3 FocusPosition => target != null ? target.position : fallbackTargetPosition;
    public Vector3 SunPosition => sun != null ? sun.position : transform.position;
    private float viewSolarRadius;
    public void SetViewSolarRadius(float radius) { viewSolarRadius = Mathf.Max(0,radius); }
    public void GetActiveRingLayout(int index, out int planes, out int slots, out float radialSigma, out float verticalSigma)
    {
        planes = resolvedPlanes[index]; slots = resolvedSlots[index];
        radialSigma = resolvedWidths[index]; verticalSigma = resolvedHeights[index];
    }
    private static long BufferBytes(GraphicsBuffer[] buffers) {
        long bytes = 0;
        if (buffers != null) foreach (var buffer in buffers)
            if (buffer != null && buffer.IsValid()) bytes += (long)buffer.count * buffer.stride;
        return bytes;
    }
    // Called only on enable, validation or explicit edits; never from the render loop.
    public static int StableRingPopulation(double population, double threshold, int ringIndex, int maximum)
    {
        if (maximum <= 0 || ringIndex < 0 || ringIndex >= maximum || double.IsNaN(population) || population <= 0) return 0;
        double remaining = Math.Ceiling(population), result = 0;
        double interval = double.IsNaN(threshold) || threshold <= 0 ? 1 : Math.Max(1, Math.Ceiling(threshold));
        // Each stage adds another ring. Distribute only NEW mirrors among its rings;
        // a ring's previous prefix never shrinks when another ring becomes visible.
        for (int stage = 1; stage <= maximum && remaining > 0; stage++) {
            double additions = stage == maximum ? remaining : Math.Min(remaining, interval);
            if (ringIndex < stage)
                result += Math.Floor(additions / stage) + (ringIndex >= stage - additions % stage ? 1 : 0);
            remaining -= additions;
        }
        if (result > int.MaxValue) throw new InvalidOperationException("GPU buffer indexing capacity exceeded.");
        return (int)result;
    }

    public static int ProgressiveSlotStride(int slots)
    {
        int stride = Math.Max(1, (int)(slots * 0.61803398875));
        while (true) {
            int a = slots, b = stride;
            while (b != 0) { int remainder = a % b; a = b; b = remainder; }
            if (a == 1) return stride;
            stride--;
        }
    }

    private void ResolvePopulation()
    {
        int maximum = rings != null ? rings.Length : 0;
        ValidateMirrorMapping();
        double population = VisualMirrorCount;
        activeRingCount = maximum;
        if (populationGrowth) {
            activeRingCount = 0;
            for (int i = 0; i < maximum; i++)
                if (StableRingPopulation(population, mirrorsPerAdditionalRing, i, maximum) > 0) activeRingCount = i + 1;
        }
        resolvedPlanes = new int[activeRingCount]; resolvedSlots = new int[activeRingCount];
        resolvedWidths = new float[activeRingCount]; resolvedHeights = new float[activeRingCount];
        resolvedCounts = new int[activeRingCount]; resolvedGroups = new int[activeRingCount];
        resolvedSurfaces = new Vector4[activeRingCount];
        resolvedOrbitCdfs = new Vector4[activeRingCount][];
        resolvedRadialMixtures = new float[activeRingCount];
        for (int i = 0; i < activeRingCount; i++) {
            var ring = rings[i];
            int maxPlanes = Mathf.Max(1, usePerRingOverrides ? ring.planeCount : planeCount);
            int maxSlots = Mathf.Max(1, usePerRingOverrides ? ring.slotsPerPlane : slotsPerPlane);
            resolvedPlanes[i] = maxPlanes;
            resolvedSlots[i] = maxSlots;
            float radius = Mathf.Max(0.01f, ring.radius);
            float angle = Mathf.Clamp(usePerRingOverrides ? ring.angularThickness : angularThickness,0,30);
            float radialSigma = filledTorus ? Mathf.Min(Mathf.Max(0,radialVariation),radius*0.225f) : 0;
            float verticalSigma = filledTorus ? Mathf.Max(0,altitudeVariation) + radius*Mathf.Sin(angle*Mathf.Deg2Rad) : 0;
            if (populationGrowth && filledTorus) {
                float width = Mathf.Max(0.001f,maximumCloudWidth);
                // Reserve half the radial envelope for radius scatter, half for tilt curvature.
                radialSigma = Mathf.Min(radialSigma,width/16);
                float minimumRadius = radius - 4*radialSigma;
                float inwardBudget = Mathf.Min(width/2,minimumRadius);
                float heightLimit = Mathf.Sqrt(Mathf.Max(0,2*minimumRadius*inwardBudget-inwardBudget*inwardBudget));
                verticalSigma = Mathf.Min(verticalSigma,Mathf.Min(width/8,heightLimit/4));
            }
            resolvedWidths[i] = radialSigma; resolvedHeights[i] = verticalSigma;
            int groups = filledTorus ? Mathf.Max(1,orbitGroups) : 1;
            double count = (double)resolvedPlanes[i]*resolvedSlots[i]*groups;
            if (populationGrowth) count = StableRingPopulation(population, mirrorsPerAdditionalRing, i, maximum);
            if (populationGrowth && continuousSurface && filledTorus) {
                double ringPopulation = count;
                double size = Math.Max(0.000001,physicalMirrorRadius);
                double occupancy = Math.Max(0.1,Math.Min(1,saturationOccupancy));
                // Keep thickness fixed: only the occupied spherical surface coordinate grows.
                double sigma = Math.Max(1e-6,verticalSigma/radius);
                double core = ResolveSurfaceCore(ringPopulation,radius,verticalSigma,size,occupancy);
                if (core > 0) {
                    // The band may hide mirrors, but must not resample their trajectories.
                    resolvedSurfaces[i] = new Vector4((float)core,(float)sigma,1,-1);
                }
                groups = Math.Max(1,(int)Math.Ceiling(count/((double)resolvedPlanes[i]*resolvedSlots[i])));
            }
            if (count > int.MaxValue) throw new InvalidOperationException("Individual outskirts exceed GPU indexing capacity; increase Panel Size or reduce orbital radius.");
            resolvedCounts[i] = (int)count; resolvedGroups[i] = groups;
        }
    }
    public static int ResolveGrowth(double population, double threshold, int maximum)
    {
        if (maximum <= 0 || double.IsNaN(population) || population <= 0) return 0;
        if (double.IsNaN(threshold) || threshold <= 0) threshold = 1;
        double steps = Math.Floor(population/threshold);
        return steps >= maximum-1 ? maximum : 1+(int)steps;
    }
    public long RenderedCollectorCount { get; private set; }
    [Header("Ring Spacing")]
    [SerializeField, Min(0.01f), Tooltip("Raio do primeiro anel. Os seguintes usam Base Radius + índice × Ring Separation.")]
    private float baseRadius = 5f;
    [SerializeField, HideInInspector] private bool spacingInitialized;
    [Tooltip("Usa Base Radius + indice do anel x Ring Separation. Desative para editar os raios centrais individualmente.")]
    [SerializeField] private bool useUniformRingSpacing = true;
    [Tooltip("Diferença entre os raios centrais de anéis vizinhos quando o espaçamento uniforme está ativo.")]
    [SerializeField, Min(0)] private float ringSeparation = 0.6f;
    [Header("Filled Torus")]
    [Tooltip("Ativa a nuvem volumetrica com raios e inclinacoes aleatorios e Orbit Groups. Desativado, usa um grupo com raio e inclinacao fixos. Os centros orbitais permanecem no Sol.")]
    [SerializeField] private bool filledTorus = true;
    [Tooltip("Desvio padrao radial em unidades mundiais. Raios aleatorios por orbita, com caudas ate 4 sigma, sem camadas fixas. Limitado a 22,5% do raio central para manter raios positivos.")]
    [SerializeField, Min(0)] private float radialVariation = 0.3f;
    [Tooltip("Desvio padrao vertical adicional em unidades mundiais, somado a raio central x seno de Angular Thickness. Zero ainda permite altura pelo angulo. Converte altura em inclinacao mantendo o centro orbital no Sol.")]
    [SerializeField, Min(0)] private float altitudeVariation = 0.3f;
    [Tooltip("1: uniform cross-section density. Higher values concentrate orbits near the central circle.")]
    [SerializeField, HideInInspector] private float densityConcentration = 2f;
    [Tooltip("Distribui as normais pela esfera usando o maximo de aneis, preservando as orientacoes durante o crescimento.")]
    [SerializeField] private bool autoDistributeRingNormals = false;
    [Header("Rings")]
    [Tooltip("Lista de anéis. Alterar o tamanho redistribui automaticamente as orientações se Auto Distribute Ring Normals estiver ativo.")]
    [SerializeField] private DysonRingSettings[] rings = CreateDefaultRings();

    [SerializeField, HideInInspector, Min(1), Tooltip("Camadas regulares de raio na seção preenchida. Multiplica a quantidade de órbitas.")]
    private int radialLayers = 3;
    [SerializeField, HideInInspector, Min(1), Tooltip("Legado: migrado para Orbit Groups; nao cria mais camadas.")]
    private int inclinationLayers = 3;
    [SerializeField, Min(1), Tooltip("Grupos de orbitas no toro preenchido. Total por anel = Orbit Groups x Plane Count x Slots Per Plane. Cada orbita recebe raio e inclinacao proprios. Aumenta a amostragem e o custo, sem mudar a largura da nuvem.")]
    private int orbitGroups = 9;
    [SerializeField, HideInInspector] private bool gaussianDistributionInitialized;
    private GraphicsBuffer[] args;
    private GraphicsBuffer[] panels, nearIds, pointPositions, pointArgs, counters, orbitParameters;
    private GraphicsBuffer[] birthOrbitCdfs;
    private float[] birthCdfSigmas;
    private Vector4[][] computedOrbitVectors;
    private bool[] orbitComputed;
    private Matrix4x4[] computedCameras;
    private Vector4[] computedLod, computedFrustumPadding;
    private Mesh pointMesh, surfaceMesh;
    private ComputeShader panelCompute;
    private Vector4[][] computedVectors;
    private float[] computedPhases;
    private bool[] computed;
    private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] command = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
    private uint[] cachedCounts;
    private Mesh ownedMesh, activeMesh;
    private MaterialPropertyBlock[] ringProperties;
    private Bounds[] ringBounds;
    private DysonPass renderPass;
    private GraphicsBuffer overdrawPixels;
    private Material overdrawMaterial;
    private readonly Material[] surfaceCullMaterials = new Material[6];
    private Material GetSurfaceMaterial(Material source, int cull, bool measured, bool sharedDepth = false)
    {
        int index = Mathf.Clamp(cull, 0, 2) + (measured ? 3 : 0);
        if (surfaceCullMaterials[index] == null)
            surfaceCullMaterials[index] = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
        var material = surfaceCullMaterials[index];
        material.CopyPropertiesFromMaterial(source);
        material.SetFloat("_DysonSurfaceCull", cull);
        if(sharedDepth) material.EnableKeyword("DYSON_SHARED_SURFACE_DEPTH"); else material.DisableKeyword("DYSON_SHARED_SURFACE_DEPTH");
        return material;
    }
    private Camera overdrawCamera;
    private bool overdrawQueued;
    private Action<uint[],int,int> overdrawCallback;
    public int LastOverdrawFrame { get; private set; }
    public void RequestOverdraw(Camera camera, bool rawCoverage, Action<uint[],int,int> completed) {
        if (camera == null || completed == null) throw new ArgumentNullException();
        if (overdrawCallback != null) throw new InvalidOperationException("Overdraw readback already pending.");
        if (!SystemInfo.supportsAsyncGPUReadback) throw new NotSupportedException("Async GPU readback unavailable.");
        if (overdrawMaterial == null) overdrawMaterial = new Material(panelMaterial) { hideFlags=HideFlags.HideAndDontSave };
        overdrawMaterial.CopyPropertiesFromMaterial(panelMaterial);
        overdrawMaterial.EnableKeyword("DYSON_MEASURE_OVERDRAW");
        overdrawMaterial.SetFloat("_DysonZTest",rawCoverage ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
        overdrawMaterial.SetFloat("_DysonZWrite",rawCoverage ? 0 : 1);
        overdrawCamera=camera; overdrawCallback=completed; overdrawQueued=true;
    }
    private bool dirty = true;
#if UNITY_EDITOR
    private bool editorRefreshRequested;
    private double nextEditorFrame;
    private void EditorTick()
    {
        if (this == null || !isActiveAndEnabled || Application.isPlaying) return;
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (!editorRefreshRequested && !animateInEditor) return;
        if (now < nextEditorFrame) return;
        nextEditorFrame = now + 1.0 / 30;
        editorRefreshRequested = false;
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditor.SceneView.RepaintAll();
    }
#endif
    public static Vector3 DistributedRingNormal(int index, int count)
    {
        float y = (index + 0.5f) / Mathf.Max(1, count);
        float angle = index * 2.39996323f;
        float r = Mathf.Sqrt(Mathf.Max(0, 1 - y * y));
        return new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle));
    }

    public static DysonRingSettings[] CreateDefaultRings()
    {
        var result = new DysonRingSettings[5];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new DysonRingSettings {
                radius = 5 + i * 0.6f, ringNormal = Vector3.up,
                angularThickness = 2, planeCount = 24, slotsPerPlane = 512,
                phaseOffset = i * 13, orbitalSpeed = 4 + i
            };
        }
        return result;
    }
    private void OnEnable()
    {
        SynchronizeRingSettings();
        dirty = true;
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        renderPass = new DysonPass(this);
#if UNITY_EDITOR
        editorRefreshRequested = true;
        UnityEditor.EditorApplication.update -= EditorTick;
        UnityEditor.EditorApplication.update += EditorTick;
#endif
    }
    private void OnValidate()
    {
        SynchronizeRingSettings();
        dirty = true;
#if UNITY_EDITOR
        // OnValidate may run off the main thread. Only flag work here.
        editorRefreshRequested = true;
#endif
    }
    private void SynchronizeRingSettings()
    {
        if (!gaussianDistributionInitialized) {
            orbitGroups = Mathf.Max(1, radialLayers) * Mathf.Max(1, inclinationLayers);
            gaussianDistributionInitialized = true;
        }
        if (rings == null) { ResolvePopulation(); return; }
        if (!spacingInitialized && rings.Length > 0) {
            baseRadius = Mathf.Max(0.01f, rings[0].radius);
            spacingInitialized = true;
        }
        baseRadius = Mathf.Max(0.01f, baseRadius);
        ringSeparation = Mathf.Max(0, ringSeparation);
        for (int i = 0; i < rings.Length; i++) {
            if (rings[i].planeCount < 1 || rings[i].slotsPerPlane < 1)
                rings[i] = CreateDefaultRing(i);
            if (useUniformRingSpacing) rings[i].radius = baseRadius + i * ringSeparation;
            if (autoDistributeRingNormals) rings[i].ringNormal = DistributedRingNormal(i, rings.Length);
        }
        ResolvePopulation();
    }
    private static DysonRingSettings CreateDefaultRing(int index)
    {
        return new DysonRingSettings {
            radius = 5 + index * 0.6f, ringNormal = Vector3.up,
            angularThickness = 2, planeCount = 24, slotsPerPlane = 512,
            phaseOffset = index * 13, orbitalSpeed = 4 + index
        };
    }
    public void SetRingCount(int count)
    {
        count = Mathf.Max(count, 0);
        int oldCount = rings != null ? rings.Length : 0;
        Array.Resize(ref rings, count);
        for (int i = oldCount; i < count; i++) rings[i] = CreateDefaultRing(i);
        OnValidate();
    }
    public int RingCount => rings != null ? rings.Length : 0;
    public float ComputeGpuMilliseconds => renderPass != null ? renderPass.computeSampler.gpuElapsedTime : 0;
    public float DrawGpuMilliseconds => renderPass != null ? renderPass.drawSampler.gpuElapsedTime : 0;
    public float SurfaceDepthGpuMilliseconds => renderPass != null ? renderPass.surfaceDepthSampler.gpuElapsedTime : 0;
    public float OcclusionTilesGpuMilliseconds => renderPass != null ? renderPass.occlusionTilesSampler.gpuElapsedTime : 0;
    public float SurfaceResolveGpuMilliseconds => renderPass != null ? renderPass.surfaceResolveSampler.gpuElapsedTime : 0;
    public float CollectorsGpuMilliseconds => renderPass != null ? renderPass.collectorsSampler.gpuElapsedTime : 0;
    public float SurfaceEdgesGpuMilliseconds => renderPass != null ? renderPass.surfaceEdgesSampler.gpuElapsedTime : 0;
    public void SetGpuProfiling(bool enabled)
    {
        if (renderPass == null) return;
        renderPass.computeSampler.enableRecording = enabled; renderPass.drawSampler.enableRecording = enabled;
        renderPass.surfaceDepthSampler.enableRecording = enabled;
        renderPass.occlusionTilesSampler.enableRecording = enabled;
        renderPass.surfaceResolveSampler.enableRecording = enabled;
        renderPass.collectorsSampler.enableRecording = enabled;
        renderPass.surfaceEdgesSampler.enableRecording = enabled;

    }
#if UNITY_EDITOR
    // Explicit diagnostics only: never block for counter readback in the rendering loop.
    public void ReadLodCounts(out long hexagons, out long points)
    {
        hexagons = 0; points = 0;
        if (counters == null) return;
        var values = new uint[2];
        for (int i = 0; i < counters.Length; i++) if (computed[i]) {
            counters[i].GetData(values); hexagons += values[0]; points += values[1];
        }
    }
#endif
    private void ReleaseArguments()
    {
        if (args != null) foreach (var buffer in args) {
            if (buffer == null) continue;
            // In edit mode old submitted draws may survive a repaint. Neutralize before retiring.
            command[0] = new GraphicsBuffer.IndirectDrawIndexedArgs();
            buffer.SetData(new uint[5]);
            buffer.Dispose();
        }
        if (panels != null) foreach (var buffer in panels) buffer?.Dispose();
        foreach (var buffers in new[] { nearIds, pointPositions, pointArgs, counters, orbitParameters, birthOrbitCdfs })
            if (buffers != null) foreach (var buffer in buffers) buffer?.Dispose();
        nearIds = null; pointPositions = null; pointArgs = null; counters = null; orbitParameters = null;
        birthOrbitCdfs = null; birthCdfSigmas = null;
        panels = null; args = null; ringProperties = null;
    }
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        renderPass?.computeSampler.Dispose(); renderPass?.drawSampler.Dispose();
        renderPass?.surfaceDepthSampler.Dispose();
        renderPass?.occlusionTilesSampler.Dispose();
        renderPass?.surfaceResolveSampler.Dispose();
        renderPass?.collectorsSampler.Dispose();
        renderPass?.surfaceEdgesSampler.Dispose();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
        Release();
    }
    private void OnDestroy() { Release(); }
    private void Release()
    {
        overdrawPixels?.Dispose(); overdrawPixels=null;
        if (overdrawMaterial != null) { if(Application.isPlaying) Destroy(overdrawMaterial); else DestroyImmediate(overdrawMaterial); }
        overdrawMaterial=null; overdrawQueued=false;
        for (int i=0;i<surfaceCullMaterials.Length;i++) {
            if(surfaceCullMaterials[i]!=null) {
                if(Application.isPlaying) Destroy(surfaceCullMaterials[i]); else DestroyImmediate(surfaceCullMaterials[i]);
                surfaceCullMaterials[i]=null;
            }
        }
        ReleaseArguments();
        if (ownedMesh != null) {
            if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh);
        }
        if (pointMesh != null) {
            if (Application.isPlaying) Destroy(pointMesh); else DestroyImmediate(pointMesh);
        }
        if (surfaceMesh != null) { if (Application.isPlaying) Destroy(surfaceMesh); else DestroyImmediate(surfaceMesh); }
        surfaceMesh = null; pointMesh = null; ownedMesh = null; activeMesh = null;
    }
    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (DysonWallpaperController.IsPresentationCamera(camera)) return;
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset && isActiveAndEnabled)
            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(renderPass);
    }
    private bool PrepareSwarm(Camera camera, double seconds)
    {
        if (panelMaterial == null || rings == null || activeRingCount == 0) { RenderedCollectorCount = 0; ReleaseArguments(); return false; }
        if (panelCompute == null) panelCompute = Resources.Load<ComputeShader>("DysonSwarm");
        if (panelCompute == null) return false;
        if (!SystemInfo.supportsInstancing || !SystemInfo.supportsComputeShaders) return false;
        if (pointMesh == null) pointMesh = CreatePointTriangle();
        Mesh mesh = panelMesh;
        if (mesh == null) {
            if (ownedMesh == null || ownedMeshReduced != useReducedHexMesh) {
                if (ownedMesh != null) { if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh); }
                ownedMesh = useReducedHexMesh ? CreateHexagon() : CreateLegacyHexagon();
                ownedMeshReduced = useReducedHexMesh;
            }
            mesh = ownedMesh;
        }
        if (activeMesh != mesh) ReleaseArguments();
        if (args == null || args.Length != activeRingCount) {
            int previous = args != null ? args.Length : 0;
            for (int i = activeRingCount; i < previous; i++) {
                args[i]?.Dispose(); panels[i]?.Dispose(); nearIds[i]?.Dispose(); pointPositions[i]?.Dispose();
                pointArgs[i]?.Dispose(); counters[i]?.Dispose(); orbitParameters[i]?.Dispose();
                birthOrbitCdfs[i]?.Dispose();
            }
            Array.Resize(ref computedVectors, activeRingCount); Array.Resize(ref computedPhases, activeRingCount); Array.Resize(ref computed, activeRingCount);
            Array.Resize(ref panels, activeRingCount); Array.Resize(ref orbitParameters, activeRingCount);
            Array.Resize(ref birthOrbitCdfs, activeRingCount); Array.Resize(ref birthCdfSigmas, activeRingCount);
            Array.Resize(ref computedOrbitVectors, activeRingCount); Array.Resize(ref orbitComputed, activeRingCount);
            Array.Resize(ref nearIds, activeRingCount); Array.Resize(ref pointPositions, activeRingCount);
            Array.Resize(ref pointArgs, activeRingCount); Array.Resize(ref counters, activeRingCount);
            Array.Resize(ref computedCameras, activeRingCount); Array.Resize(ref computedLod, activeRingCount); Array.Resize(ref computedFrustumPadding, activeRingCount);
            Array.Resize(ref args, activeRingCount); Array.Resize(ref cachedCounts, activeRingCount);
            Array.Resize(ref ringBounds, activeRingCount); Array.Resize(ref ringProperties, activeRingCount);
            for (int i = previous; i < activeRingCount; i++) {
                args[i] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));
                ringProperties[i] = new MaterialPropertyBlock();
            }
            activeMesh = mesh; dirty = true;
        }
        Vector3 center = sun != null ? sun.position : transform.position;
        RenderedCollectorCount = 0;
        for (int i = 0; i < activeRingCount; i++) {
            var properties = ringProperties[i];
        properties.SetVector("_SunPosition", center);
        properties.SetVector("_TargetPosition", target != null ? target.position : fallbackTargetPosition);
        properties.SetFloat("_ValidateSharedOcclusion",validateSharedCollectorOcclusion ? 1 : 0);
        properties.SetFloat("_PanelSize", Mathf.Max(0.001f, panelSize));
        properties.SetColor("_PanelColor", color); properties.SetColor("_Emission", emission);
        properties.SetFloat("_Metallic", metallic); properties.SetFloat("_Smoothness", smoothness);
        properties.SetColor("_SolarColor", solarColor); properties.SetColor("_ShadowColor", shadowColor);
        properties.SetVector("_SolarReflection", new Vector4(reflectionStrength, glintWidth * Mathf.Deg2Rad, Mathf.Max(0.01f, viewSolarRadius>0 ? viewSolarRadius : solarRadius), eclipseSoftness));
        properties.SetVector("_EclipseOccluder", eclipseOccluder != null ? new Vector4(eclipseOccluder.position.x, eclipseOccluder.position.y, eclipseOccluder.position.z, Mathf.Max(0.001f, eclipseRadius)) : Vector4.zero);
        properties.SetFloat("_DebugReflection", debugReflection ? 1 : 0);
        properties.SetFloat("_ReflectionTolerance", Mathf.Max(0.000001f, reflectionTolerance));
            var ring = rings[i];
            int planes = resolvedPlanes[i];
            int desiredSlots = resolvedSlots[i];
            int slots = desiredSlots;
            float thickness = Mathf.Clamp(usePerRingOverrides ? ring.angularThickness : angularThickness,0,30);
            float speed = usePerRingOverrides ? ring.orbitalSpeed : orbitalSpeed;
            int rLayers = resolvedGroups[i];
            int iLayers = 1;
            long count = resolvedCounts[i];
            long capacity = Math.Max(256, ((count + 255) / 256) * 256);
            if (panels[i] != null && count > panels[i].count)
                capacity = Math.Max(capacity, ((panels[i].count + (long)panels[i].count / 8 + 255) / 256) * 256);
            int allocationCount = (int)Math.Min(int.MaxValue, capacity);
            if (count > int.MaxValue) throw new InvalidOperationException("GPU buffer indexing capacity exceeded.");
            if (panels[i] == null || panels[i].count < allocationCount) {
                computed[i] = false;
                panels[i]?.Dispose();
                panels[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, allocationCount, 64);
                nearIds[i]?.Dispose(); pointPositions[i]?.Dispose(); pointArgs[i]?.Dispose(); counters[i]?.Dispose();
                nearIds[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, allocationCount, sizeof(uint));
                pointPositions[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, allocationCount, 16);
                counters[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(uint));
                pointArgs[i] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));
                pointArgs[i].SetData(new uint[] { 3, 0, 0, 0, 0 });
                properties.SetBuffer("_Panels", panels[i]);
                properties.SetBuffer("_NearIds", nearIds[i]); properties.SetBuffer("_Points", pointPositions[i]);
            }
            int orbitCount = Math.Max(1, (int)(((long)panels[i].count + Math.Max(1, slots) - 1) / Math.Max(1, slots)));
            if (orbitParameters[i] == null || orbitParameters[i].count < orbitCount) {
                orbitParameters[i]?.Dispose();
                orbitParameters[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, orbitCount, 48);
                orbitComputed[i] = false; computed[i] = false;
            }
            RenderedCollectorCount += count;
            if (dirty || cachedCounts[i] != (uint)count) {
                command[0] = new GraphicsBuffer.IndirectDrawIndexedArgs {
                    indexCountPerInstance = mesh.GetIndexCount(0), instanceCount = 0,
                    startIndex = mesh.GetIndexStart(0), baseVertexIndex = mesh.GetBaseVertex(0), startInstance = 0
                };
                args[i].SetData(new uint[] { command[0].indexCountPerInstance, 0, command[0].startIndex, command[0].baseVertexIndex, 0 }); computed[i] = false; cachedCounts[i] = (uint)count;
            }
            Vector3 normal = autoDistributeRingNormals ? DistributedRingNormal(i, RingCount) : (ring.ringNormal.sqrMagnitude > 1e-8f ? ring.ringNormal.normalized : Vector3.up);
            float radius = Mathf.Max(0.01f, useUniformRingSpacing ? baseRadius + i * Mathf.Max(0, ringSeparation) : ring.radius);
            float width = resolvedWidths[i];
            float height = resolvedHeights[i];
            float birthSigma = Mathf.Max(1e-6f, height / radius);
            bool useBirthDistribution = populationGrowth && continuousSurface && filledTorus;
            int birthTableSize = useBirthDistribution ? BirthCoreSamples * 256 : 1;
            if (birthOrbitCdfs[i] == null || birthOrbitCdfs[i].count != birthTableSize || birthCdfSigmas[i] != birthSigma) {
                birthOrbitCdfs[i]?.Dispose();
                birthOrbitCdfs[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, birthTableSize, 16);
                birthOrbitCdfs[i].SetData(useBirthDistribution ? BuildBirthOrbitCdf(birthSigma) : new Vector4[1]);
                birthCdfSigmas[i] = birthSigma;
                orbitComputed[i] = false; computed[i] = false;
            }
            double areaScale = 4 * Math.PI * radius * radius * Mathf.Clamp(saturationOccupancy, 0.1f, 1)
                / (3 * Math.Sqrt(3) * Math.Pow(Math.Max(0.000001, physicalMirrorRadius), 2) / 2);
            properties.SetVector("_BirthDistribution", new Vector4((float)areaScale, birthSigma,
                useBirthDistribution ? 1 : 0, BirthCoreSamples));
            properties.SetVector("_Distribution", new Vector4(orbitAltitudeRandomness, orbitInclinationRandomness, phaseStagger, distributionSeed + i * 101));
            properties.SetVector("_LatticeData", populationGrowth
                ? new Vector4(1, 1, ProgressiveSlotStride(slots), 1)
                : new Vector4(rLayers, iLayers, resolvedRadialMixtures[i], 0));
            properties.SetVector("_TorusData", new Vector4(width, height, Mathf.Clamp(densityConcentration, 1, 6), filledTorus ? 1 : 0));
            properties.SetVector("_SurfaceData", resolvedSurfaces[i]);
            properties.SetVector("_SurfaceGrazing", new Vector4(surfaceGrazingFade * 0.2f, surfaceGrazingFade, 0, 0));
            properties.SetFloat("_SurfaceOccupancy", Mathf.Clamp(saturationOccupancy,0.1f,1));
            properties.SetVector("_RingNormal", normal);
            properties.SetVector("_RingData", new Vector4(radius, thickness * Mathf.Deg2Rad, planes, Mathf.Max(1,slots)));
            float envelopeRadius=(radius+2*width)*1.002f;
            float cameraDistance=Vector3.Distance(camera.transform.position,center);
            float envelopeTolerance=Mathf.Max(0.0001f,envelopeRadius*0.00001f);
            // Envelope triangles face inward. Outside cameras need the near
            // back faces; inside cameras need the front faces. At the boundary,
            // leave both sides to the analytic shader's existing discard guard.
            CullMode envelopeCull=CullMode.Off;
            if(enableSurfaceFaceCulling && Mathf.Abs(cameraDistance-envelopeRadius)>envelopeTolerance)
                envelopeCull=cameraDistance>envelopeRadius ? CullMode.Front : CullMode.Back;
            properties.SetFloat("_DysonSurfaceCull",(float)envelopeCull);
            // Wrap each ring's phase in double precision before sending it to the GPU.
            properties.SetFloat("_Phase", (float)((ring.phaseOffset * Mathf.Deg2Rad + seconds * speed * Mathf.Deg2Rad) % (Math.PI * 2)));
            float meshExtent = mesh.bounds.center.magnitude + mesh.bounds.extents.magnitude;
            float diameter = 2 * (Mathf.Sqrt((radius + 4 * width) * (radius + 4 * width) + 16 * height * height) + meshExtent * Mathf.Max(0.001f, panelSize));
            if (resolvedSurfaces[i].z > 0) diameter = 2*(radius+4*width+meshExtent*Mathf.Max(0.001f,panelSize));
            ringBounds[i] = new Bounds(center, Vector3.one * Mathf.Max(boundsSize, diameter));
        }
        dirty = false;
        return true;
    }
    private sealed class DysonPass : ScriptableRenderPass
    {
        public readonly ProfilingSampler computeSampler = new ProfilingSampler("Dyson Panel Transforms");
        public readonly ProfilingSampler drawSampler = new ProfilingSampler("Dyson Orbital Lattice");
        public readonly ProfilingSampler surfaceDepthSampler = new ProfilingSampler("Dyson Stage Opaque Bands");
        public readonly ProfilingSampler occlusionTilesSampler = new ProfilingSampler("Dyson Stage Occlusion Tiles");
        public readonly ProfilingSampler surfaceResolveSampler = new ProfilingSampler("Dyson Stage Surface Resolve");
        public readonly ProfilingSampler collectorsSampler = new ProfilingSampler("Dyson Stage Collectors");
        public readonly ProfilingSampler surfaceEdgesSampler = new ProfilingSampler("Dyson Stage Band Edges");

        private readonly DysonSwarmRenderer owner;
        private sealed class ComputeData {
            public DysonSwarmRenderer owner; public bool[] visible;
            public Matrix4x4 cameraMatrix;
            public TextureHandle occlusionTiles;
            public bool occlusionActive;
            public Vector4 occlusionExtent, occlusionProjectionBounds;
            public Vector4 lod, cameraPosition, cameraForward, frustumPadding;
            // RenderGraph owns these per-pass objects until execution completes.
            // Reuse their arrays on later frames; never share a mutable camera cache.
            public Plane[] planes = new Plane[6];
            public Vector4[] frustum = new Vector4[6];
            public BufferHandle[] handles, nearHandles, pointHandles, counterHandles, argHandles, pointArgHandles, orbitHandles, birthCdfHandles;
            public void EnsureCapacity(int count) {
                if (visible != null && visible.Length == count) return;
                visible = new bool[count]; orbitHandles = new BufferHandle[count]; birthCdfHandles = new BufferHandle[count];
                handles = new BufferHandle[count]; nearHandles = new BufferHandle[count];
                pointHandles = new BufferHandle[count]; counterHandles = new BufferHandle[count];
                argHandles = new BufferHandle[count]; pointArgHandles = new BufferHandle[count];
            }
        }
        private static readonly string[] OrbitVectors = { "_RingNormal", "_RingData", "_TorusData", "_LatticeData", "_Distribution", "_SurfaceData", "_BirthDistribution" };
        private static readonly string[] ComputeVectors = { "_SunPosition", "_TargetPosition", "_RingNormal", "_RingData", "_TorusData", "_LatticeData", "_Distribution", "_SurfaceData", "_SurfaceGrazing", "_BirthDistribution" };
        private sealed class OverdrawData {
            public DysonSwarmRenderer owner;
            public GraphicsBuffer buffer;
            public ComputeShader shader;
            public int width,height,kernel;
        }
        private sealed class TileData {
            public ComputeShader shader; public TextureHandle hit, tiles;
            public Vector4 extent; public Vector3 cameraPosition,cameraForward;
        }
        private sealed class PassData
        {
            public Mesh mesh, pointMesh, surfaceMesh;
            public Material material;
            public Material[] surfaceMaterials;
            public GraphicsBuffer[] args, pointArgs;
            public MaterialPropertyBlock[] properties;
            public bool[] visible;
            public bool sharedResolve;
            public Plane[] depthPlanes=new Plane[6];
        }
        public DysonPass(DysonSwarmRenderer owner)
        {
            this.owner = owner;
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var camera = frameData.Get<UniversalCameraData>().camera;
            // Rendering a future wallpaper frame must not advance the game's clock.
            double seconds = Application.isPlaying ? Time.timeAsDouble : 0;
#if UNITY_EDITOR
            if (!Application.isPlaying && owner.animateInEditor)
                seconds = UnityEditor.EditorApplication.timeSinceStartup;
#endif
            if (DysonWallpaperController.TryGetSimulationTime(camera, out double wallpaperSeconds))
                seconds = wallpaperSeconds;
            if (!owner.PrepareSwarm(camera, seconds)) return;
            var resources = frameData.Get<UniversalResourceData>();
            bool measureOverdraw=owner.overdrawQueued && camera==owner.overdrawCamera;
            BufferHandle overdrawHandle=default;
            int overdrawWidth=camera.pixelWidth, overdrawHeight=camera.pixelHeight;
            if (measureOverdraw) {
                int pixels=checked(overdrawWidth*overdrawHeight);
                int elements=checked(pixels*10);
                if(owner.overdrawPixels==null || owner.overdrawPixels.count!=elements) {
                    owner.overdrawPixels?.Dispose();
                    owner.overdrawPixels=new GraphicsBuffer(GraphicsBuffer.Target.Structured,elements,sizeof(uint));
                }
                owner.LastOverdrawFrame=Time.frameCount;
                overdrawHandle=graph.ImportBuffer(owner.overdrawPixels);
                foreach(var properties in owner.ringProperties)
                    properties.SetVector("_DysonOverdrawSize",new Vector4(overdrawWidth,overdrawHeight,0,0));
                using(var builder=graph.AddComputePass<OverdrawData>("Clear Dyson overdraw",out var clear)) {
                    clear.buffer=owner.overdrawPixels; clear.shader=owner.panelCompute;
                    clear.width=overdrawWidth; clear.height=overdrawHeight; clear.kernel=owner.panelCompute.FindKernel("ClearOverdraw");
                    builder.UseBuffer(overdrawHandle,AccessFlags.Write); builder.AllowPassCulling(false);
                    builder.SetRenderFunc((OverdrawData d,ComputeGraphContext ctx)=> {
                        int pixels=d.width*d.height; int groups=(pixels+63)/64; int gx=Math.Min(groups,65535);
                        ctx.cmd.SetComputeBufferParam(d.shader,d.kernel,"_DysonOverdrawPixels",d.buffer);
                        ctx.cmd.SetComputeIntParam(d.shader,"_DysonOverdrawPixelCount",pixels);
                        ctx.cmd.SetComputeIntParam(d.shader,"_DysonOverdrawDispatchWidth",gx*64);
                        ctx.cmd.DispatchCompute(d.shader,d.kernel,gx,(groups+gx-1)/gx,1);
                    });
                }
            }
            TextureHandle sharedSurfaceDepth=default;
            bool useSharedDepth=false;
            if(owner.enableSharedSurfaceDepth) for(int i=0;i<owner.ringProperties.Length;i++)
                if(owner.ringProperties[i].GetVector("_SurfaceData").x>0) { useSharedDepth=true; break; }
            if(useSharedDepth) {
                if(owner.surfaceMesh==null) owner.surfaceMesh=CreateSurfaceSphere();
                var cameraDescriptor=frameData.Get<UniversalCameraData>().cameraTargetDescriptor;
                var depthDescriptor=new TextureDesc(cameraDescriptor.width,cameraDescriptor.height) {
                    name="Dyson nearest opaque surface", colorFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat,
                    clearBuffer=true, clearColor=new Color(0,0,0,-1), filterMode=FilterMode.Point
                };
                sharedSurfaceDepth=graph.CreateTexture(depthDescriptor);
                var zDescriptor=new TextureDesc(cameraDescriptor.width,cameraDescriptor.height) {
                    name="Dyson surface depth selection", depthBufferBits=DepthBits.Depth32, clearBuffer=true
                };
                var selectionDepth=graph.CreateTexture(zDescriptor);
                using(var builder=graph.AddRasterRenderPass<PassData>("Dyson Shared Surface Depth",out var depthData,surfaceDepthSampler)) {
                    depthData.surfaceMesh=owner.surfaceMesh; depthData.properties=owner.ringProperties;
                    if(depthData.visible==null || depthData.visible.Length!=owner.ringProperties.Length)
                        depthData.visible=new bool[owner.ringProperties.Length];
                    GeometryUtility.CalculateFrustumPlanes(camera,depthData.depthPlanes);
                    for(int i=0;i<depthData.visible.Length;i++)
                        depthData.visible[i]=GeometryUtility.TestPlanesAABB(depthData.depthPlanes,owner.ringBounds[i]);
                    if(depthData.surfaceMaterials==null || depthData.surfaceMaterials.Length!=owner.ringProperties.Length)
                        depthData.surfaceMaterials=new Material[owner.ringProperties.Length];
                    var source=measureOverdraw ? owner.overdrawMaterial : owner.panelMaterial;
                    var off=owner.GetSurfaceMaterial(source,0,measureOverdraw,true);
                    var front=owner.GetSurfaceMaterial(source,1,measureOverdraw,true);
                    var back=owner.GetSurfaceMaterial(source,2,measureOverdraw,true);
                    for(int i=0;i<depthData.surfaceMaterials.Length;i++) {
                        int cull=(int)owner.ringProperties[i].GetFloat("_DysonSurfaceCull");
                        depthData.surfaceMaterials[i]=cull==1 ? front : cull==2 ? back : off;
                    }
                    builder.SetRenderAttachment(sharedSurfaceDepth,0,AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(selectionDepth,AccessFlags.ReadWrite);
                    builder.SetGlobalTextureAfterPass(sharedSurfaceDepth,Shader.PropertyToID("_DysonSharedSurfaceDistance"));
                    if(measureOverdraw) builder.UseBufferRandomAccess(overdrawHandle,1,AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData d,RasterGraphContext ctx)=> {
                        for(int i=0;i<d.visible.Length;i++) if(d.visible[i] && d.properties[i].GetVector("_SurfaceData").x>0)
                            ctx.cmd.DrawMesh(d.surfaceMesh,Matrix4x4.identity,d.surfaceMaterials[i],0,4,d.properties[i]);
                    });
                }
            }
            var targetDescriptor=frameData.Get<UniversalCameraData>().cameraTargetDescriptor;
            bool useCollectorOcclusion=useSharedDepth && owner.enableSharedCollectorOcclusion
                && targetDescriptor.msaaSamples==1 && !camera.stereoEnabled
                && targetDescriptor.width==camera.pixelWidth && targetDescriptor.height==camera.pixelHeight;
            TextureHandle occlusionTiles=graph.defaultResources.whiteTexture;
            var occlusionExtent=new Vector4(targetDescriptor.width,targetDescriptor.height,
                (targetDescriptor.width+7)/8,(targetDescriptor.height+7)/8);
            if(useCollectorOcclusion) {
                occlusionTiles=graph.CreateTexture(new TextureDesc((int)occlusionExtent.z,(int)occlusionExtent.w) {
                    name="Dyson conservative opaque depth tiles",colorFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat,
                    enableRandomWrite=true,filterMode=FilterMode.Point
                });
                using(var builder=graph.AddComputePass<TileData>("Dyson Conservative Occlusion Tiles",out var tile,occlusionTilesSampler)) {
                    tile.shader=owner.panelCompute; tile.hit=sharedSurfaceDepth; tile.tiles=occlusionTiles;
                    tile.extent=occlusionExtent; tile.cameraPosition=camera.transform.position; tile.cameraForward=camera.transform.forward;
                    builder.UseTexture(sharedSurfaceDepth,AccessFlags.Read); builder.UseTexture(occlusionTiles,AccessFlags.Write);
                    builder.SetRenderFunc((TileData d,ComputeGraphContext ctx)=> {
                        int kernel=d.shader.FindKernel("BuildOcclusionTiles");
                        ctx.cmd.SetComputeTextureParam(d.shader,kernel,"_SharedSurfaceHit",d.hit);
                        ctx.cmd.SetComputeTextureParam(d.shader,kernel,"_CollectorOcclusionTilesRW",d.tiles);
                        ctx.cmd.SetComputeVectorParam(d.shader,"_OcclusionExtent",d.extent);
                        ctx.cmd.SetComputeVectorParam(d.shader,"_CameraPosition",d.cameraPosition);
                        ctx.cmd.SetComputeVectorParam(d.shader,"_CameraForward",d.cameraForward);
                        ctx.cmd.DispatchCompute(d.shader,kernel,(int)d.extent.z,(int)d.extent.w,1);
                    });
                }
            }
            ComputeData compute;
            using (var builder = graph.AddComputePass<ComputeData>("Dyson Panel Transforms", out compute, computeSampler)) {
                compute.occlusionTiles=occlusionTiles; compute.occlusionActive=useCollectorOcclusion; compute.occlusionExtent=occlusionExtent;
                builder.UseTexture(occlusionTiles,AccessFlags.Read);
                compute.owner = owner; compute.EnsureCapacity(owner.args.Length);
                GeometryUtility.CalculateFrustumPlanes(camera, compute.planes);
                for (int plane = 0; plane < 6; plane++) {
                    var p = compute.planes[plane];
                    compute.frustum[plane] = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
                }
                var visible = compute.visible;
                var handles = compute.handles;
                var nearHandles = compute.nearHandles; var pointHandles = compute.pointHandles;
                var counterHandles = compute.counterHandles; var argHandles = compute.argHandles;
                var pointArgHandles = compute.pointArgHandles;
                for (int i = 0; i < handles.Length; i++) {
                    handles[i] = graph.ImportBuffer(owner.panels[i]);
                    compute.orbitHandles[i] = graph.ImportBuffer(owner.orbitParameters[i]);
                    compute.birthCdfHandles[i] = graph.ImportBuffer(owner.birthOrbitCdfs[i]);
                    nearHandles[i] = graph.ImportBuffer(owner.nearIds[i]); pointHandles[i] = graph.ImportBuffer(owner.pointPositions[i]);
                    counterHandles[i] = graph.ImportBuffer(owner.counters[i]); argHandles[i] = graph.ImportBuffer(owner.args[i]);
                    pointArgHandles[i] = graph.ImportBuffer(owner.pointArgs[i]);
                }
                var cameraData=frameData.Get<UniversalCameraData>();
                compute.cameraMatrix = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true) * cameraData.GetViewMatrix();
                compute.occlusionProjectionBounds=new Vector4(((Vector3)compute.cameraMatrix.GetRow(0)).magnitude,
                    ((Vector3)compute.cameraMatrix.GetRow(1)).magnitude,((Vector3)compute.cameraMatrix.GetRow(3)).magnitude,camera.nearClipPlane);
                compute.lod = new Vector4(owner.enablePointLod && !owner.debugReflection ? owner.pointLodPixels : 0,
                    owner.minimumPointPixels, Mathf.Abs(camera.projectionMatrix.m11) * camera.pixelHeight * owner.panelSize, camera.orthographic ? 1 : 0);
                compute.cameraPosition = camera.transform.position; compute.cameraForward = camera.transform.forward;
                float meshExtent = owner.activeMesh.bounds.center.magnitude + owner.activeMesh.bounds.extents.magnitude;
                float panelRadius = Mathf.Max(0.001f, owner.panelSize) * meshExtent;
                float projectionPixels = Mathf.Min(Mathf.Abs(camera.projectionMatrix.m00) * Mathf.Max(1, camera.pixelWidth),
                    Mathf.Abs(camera.projectionMatrix.m11) * Mathf.Max(1, camera.pixelHeight));
                // Point vertices offset NDC by diameter/screen dimension. Include
                // the LOD transition diameter as well as the minimum pixel floor.
                float pointPadding = Mathf.Max(compute.lod.x, compute.lod.y) / Mathf.Max(projectionPixels, 1e-6f);
                compute.frustumPadding = new Vector4(panelRadius, pointPadding,
                    Mathf.Max(0.0001f, owner.ringBounds[0].extents.magnitude * 1e-6f), owner.enableCollectorFrustumCulling ? 1 : 0);
                for (int i = 0; i < visible.Length; i++) {
                    Bounds paddedBounds = owner.ringBounds[i];
                    float maximumDepth = Vector3.Dot(paddedBounds.center - camera.transform.position, camera.transform.forward)
                        + paddedBounds.extents.magnitude;
                    float pointRadius = pointPadding * (camera.orthographic ? 1 : Mathf.Max(0, maximumDepth));
                    paddedBounds.Expand(2 * (pointRadius + compute.frustumPadding.z));
                    visible[i] = GeometryUtility.TestPlanesAABB(compute.planes, paddedBounds);
                }
                for (int i = 0; i < handles.Length; i++) if (visible[i]) {
                    builder.UseBuffer(compute.orbitHandles[i], AccessFlags.ReadWrite);
                    builder.UseBuffer(compute.birthCdfHandles[i], AccessFlags.Read);
                    builder.UseBuffer(handles[i], AccessFlags.Write); builder.UseBuffer(nearHandles[i], AccessFlags.Write);
                    builder.UseBuffer(pointHandles[i], AccessFlags.Write); builder.UseBuffer(counterHandles[i], AccessFlags.ReadWrite);
                    builder.UseBuffer(argHandles[i], AccessFlags.Write); builder.UseBuffer(pointArgHandles[i], AccessFlags.Write);
                }
                builder.SetRenderFunc((ComputeData d, ComputeGraphContext ctx) => {
                    var o = d.owner;
                    for (int i = 0; i < o.panels.Length; i++) {
                        if (!d.visible[i]) continue;
                        var shader = o.panelCompute; var props = o.ringProperties[i];
                        if (o.computedVectors[i] == null) o.computedVectors[i] = new Vector4[ComputeVectors.Length];
                        bool changed = !o.computed[i] || o.computedPhases[i] != props.GetFloat("_Phase")
                            || !o.computedCameras[i].Equals(d.cameraMatrix) || !o.computedLod[i].Equals(d.lod)
                            || !o.computedFrustumPadding[i].Equals(d.frustumPadding);
                        for (int j = 0; j < ComputeVectors.Length; j++)
                            changed |= !o.computedVectors[i][j].Equals(props.GetVector(ComputeVectors[j]));
                        if (!changed) continue;
                        for (int j = 0; j < ComputeVectors.Length; j++) o.computedVectors[i][j] = props.GetVector(ComputeVectors[j]);
                        o.computedPhases[i] = props.GetFloat("_Phase"); o.computedCameras[i] = d.cameraMatrix;
                        o.computedLod[i] = d.lod; o.computedFrustumPadding[i] = d.frustumPadding; o.computed[i] = !d.occlusionActive; // Same-frame occlusion must never become a stale cached result.
                        ctx.cmd.SetComputeBufferParam(shader, 1, "_Counters", o.counters[i]);
                        ctx.cmd.DispatchCompute(shader, 1, 1, 1, 1);
                        ctx.cmd.SetComputeBufferParam(shader, 0, "_Panels", o.panels[i]);
                        ctx.cmd.SetComputeBufferParam(shader, 0, "_NearIds", o.nearIds[i]);
                        ctx.cmd.SetComputeBufferParam(shader, 0, "_Points", o.pointPositions[i]);
                        ctx.cmd.SetComputeBufferParam(shader, 0, "_Counters", o.counters[i]);
                        ctx.cmd.SetComputeTextureParam(shader,0,"_CollectorOcclusionTiles",d.occlusionTiles);
                        ctx.cmd.SetComputeFloatParam(shader,"_UseSharedOcclusion",d.occlusionActive ? 1 : 0);
                        ctx.cmd.SetComputeFloatParam(shader,"_ValidateSharedOcclusion",o.validateSharedCollectorOcclusion ? 1 : 0);
                        ctx.cmd.SetComputeMatrixParam(shader,"_OcclusionViewProjection",d.cameraMatrix);
                        ctx.cmd.SetComputeVectorParam(shader,"_OcclusionExtent",d.occlusionExtent);
                        ctx.cmd.SetComputeVectorParam(shader,"_OcclusionProjectionBounds",d.occlusionProjectionBounds);
                        ctx.cmd.SetComputeVectorParam(shader, "_Lod", d.lod);
                        ctx.cmd.SetComputeVectorParam(shader, "_CameraPosition", d.cameraPosition);
                        ctx.cmd.SetComputeVectorParam(shader, "_CameraForward", d.cameraForward);
                        ctx.cmd.SetComputeVectorArrayParam(shader, "_FrustumPlanes", d.frustum);
                        ctx.cmd.SetComputeVectorParam(shader, "_FrustumPadding", d.frustumPadding);
                        Vector3 ringNormal = props.GetVector("_RingNormal");
                        Vector3 centralTangent = Vector3.Cross(Mathf.Abs(ringNormal.y) < 0.99f ? Vector3.up : Vector3.right, ringNormal).normalized;
                        ctx.cmd.SetComputeVectorParam(shader, "_CentralTangent", centralTangent);
                        ctx.cmd.SetComputeVectorParam(shader, "_CentralBitangent", Vector3.Cross(ringNormal, centralTangent));
                        foreach (var name in ComputeVectors) ctx.cmd.SetComputeVectorParam(shader, name, props.GetVector(name));
                        if (o.resolvedOrbitCdfs[i] != null)
                            ctx.cmd.SetComputeVectorArrayParam(shader,"_SurfaceOrbitCdf",o.resolvedOrbitCdfs[i]);
                        float radius=props.GetVector("_RingData").x;
                        float extent=o.activeMesh.bounds.center.magnitude+o.activeMesh.bounds.extents.magnitude;
                        ctx.cmd.SetComputeFloatParam(shader,"_CullMargin",o.panelSize*extent+radius*0.003f);
                        ctx.cmd.SetComputeFloatParam(shader, "_Phase", props.GetFloat("_Phase"));
                        ctx.cmd.SetComputeFloatParam(shader, "_UseOrbitCache", o.enableOrbitCache ? 1 : 0);
                        ctx.cmd.SetComputeBufferParam(shader, 0, "_Orbits", o.orbitParameters[i]);
                        ctx.cmd.SetComputeBufferParam(shader, 0, "_BirthOrbitCdf", o.birthOrbitCdfs[i]);
                        if (o.enableOrbitCache) {
                            if (o.computedOrbitVectors[i] == null) o.computedOrbitVectors[i] = new Vector4[OrbitVectors.Length];
                            bool orbitChanged = !o.orbitComputed[i];
                            for (int j = 0; j < OrbitVectors.Length; j++)
                                if (OrbitVectors[j] != "_SurfaceData" || props.GetVector("_BirthDistribution").z < 0.5f)
                                    orbitChanged |= !o.computedOrbitVectors[i][j].Equals(props.GetVector(OrbitVectors[j]));
                            if (orbitChanged) {
                                for (int j = 0; j < OrbitVectors.Length; j++) o.computedOrbitVectors[i][j] = props.GetVector(OrbitVectors[j]);
                                int orbitGroups = (int)(((long)o.orbitParameters[i].count + 63) / 64);
                                int orbitGroupsX = Math.Max(1, Math.Min(orbitGroups, 65535));
                                ctx.cmd.SetComputeIntParam(shader, "_OrbitCount", o.orbitParameters[i].count);
                                ctx.cmd.SetComputeIntParam(shader, "_DispatchWidth", orbitGroupsX * 64);
                                ctx.cmd.SetComputeBufferParam(shader, 3, "_Orbits", o.orbitParameters[i]);
                                ctx.cmd.SetComputeBufferParam(shader, 3, "_BirthOrbitCdf", o.birthOrbitCdfs[i]);
                                ctx.cmd.DispatchCompute(shader, 3, orbitGroupsX, Math.Max(1, (orbitGroups + orbitGroupsX - 1) / orbitGroupsX), 1);
                                o.orbitComputed[i] = true;
                            }
                        }
                        int groups = (int)(((long)o.cachedCounts[i] + 63) / 64);
                        int groupsX = Math.Max(1,Math.Min(groups, 65535));
                        ctx.cmd.SetComputeIntParam(shader, "_Count", (int)o.cachedCounts[i]);
                        ctx.cmd.SetComputeIntParam(shader, "_DispatchWidth", groupsX * 64);
                        ctx.cmd.DispatchCompute(shader, 0, groupsX, Math.Max(1,(groups + groupsX - 1) / groupsX), 1);
                        ctx.cmd.SetComputeBufferParam(shader, 2, "_Counters", o.counters[i]);
                        ctx.cmd.SetComputeBufferParam(shader, 2, "_NearArgs", o.args[i]);
                        ctx.cmd.SetComputeBufferParam(shader, 2, "_PointArgs", o.pointArgs[i]);
                        ctx.cmd.DispatchCompute(shader, 2, 1, 1, 1);
                    }
                });
            }
            var visibleRings = compute.visible;
            var panelHandles = compute.handles; var nearIdHandles = compute.nearHandles;
            var pointPositionHandles = compute.pointHandles;
            var indirectHandles = compute.argHandles; var pointIndirectHandles = compute.pointArgHandles;
            using (var builder = graph.AddRasterRenderPass<PassData>("Dyson Orbital Lattice", out var data, drawSampler))
            {
                data.mesh = owner.activeMesh; data.pointMesh = owner.pointMesh;
                if (owner.surfaceMesh == null) owner.surfaceMesh = CreateSurfaceSphere();
                data.surfaceMesh = owner.surfaceMesh; data.material = measureOverdraw ? owner.overdrawMaterial : owner.panelMaterial;
                if (data.surfaceMaterials == null || data.surfaceMaterials.Length != owner.ringProperties.Length)
                    data.surfaceMaterials = new Material[owner.ringProperties.Length];
                var surfaceOff = owner.GetSurfaceMaterial(data.material,0,measureOverdraw,useSharedDepth);
                var surfaceFront = owner.GetSurfaceMaterial(data.material,1,measureOverdraw,useSharedDepth);
                var surfaceBack = owner.GetSurfaceMaterial(data.material,2,measureOverdraw,useSharedDepth);
                for (int i=0;i<data.surfaceMaterials.Length;i++) {
                    int cull=(int)owner.ringProperties[i].GetFloat("_DysonSurfaceCull");
                    data.surfaceMaterials[i] = cull==1 ? surfaceFront : cull==2 ? surfaceBack : surfaceOff;
                }
                if(measureOverdraw) builder.UseBufferRandomAccess(overdrawHandle,1,AccessFlags.ReadWrite);
                if(useSharedDepth) builder.UseTexture(sharedSurfaceDepth,AccessFlags.Read);
                data.sharedResolve=useSharedDepth && owner.enableSharedSurfaceResolve;
                data.args = owner.args; data.pointArgs = owner.pointArgs; data.properties = owner.ringProperties;
                for (int i = 0; i < panelHandles.Length; i++) if (visibleRings[i]) {
                    builder.UseBuffer(panelHandles[i], AccessFlags.Read); builder.UseBuffer(nearIdHandles[i], AccessFlags.Read);
                    builder.UseBuffer(pointPositionHandles[i], AccessFlags.Read); builder.UseBuffer(indirectHandles[i], AccessFlags.Read);
                    builder.UseBuffer(pointIndirectHandles[i], AccessFlags.Read);
                }
                data.visible = visibleRings;
                builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData d, RasterGraphContext ctx) => {
                    using (new ProfilingScope(ctx.cmd, surfaceResolveSampler)) {
                    // Solid interiors render before individuals and write their analytic depth.
                    if(d.sharedResolve)
                        ctx.cmd.DrawProcedural(Matrix4x4.identity,d.surfaceMaterials[0],5,MeshTopology.Triangles,3,1,d.properties[0]);
                    else for (int i = 0; i < d.args.Length; i++)
                        if (d.visible[i] && d.properties[i].GetVector("_SurfaceData").x > 0)
                            ctx.cmd.DrawMesh(d.surfaceMesh,Matrix4x4.identity,d.surfaceMaterials[i],0,2,d.properties[i]);
                    }
                    using (new ProfilingScope(ctx.cmd, collectorsSampler)) {
                    for (int i = 0; i < d.args.Length; i++)
                        if (d.visible[i]) {
                            ctx.cmd.DrawMeshInstancedIndirect(d.mesh,0,d.material,0,d.args[i],0,d.properties[i]);
                            ctx.cmd.DrawMeshInstancedIndirect(d.pointMesh,0,d.material,1,d.pointArgs[i],0,d.properties[i]);
                        }
                    }
                    using (new ProfilingScope(ctx.cmd, surfaceEdgesSampler)) {
                    // Only the edge transition blends; the core is fully opaque.
                    for (int i = 0; i < d.args.Length; i++)
                        if (d.visible[i] && d.properties[i].GetVector("_SurfaceData").x > 0)
                            ctx.cmd.DrawMesh(d.surfaceMesh,Matrix4x4.identity,d.surfaceMaterials[i],0,3,d.properties[i]);
                    }
                });
            }
            if(measureOverdraw) {
                owner.overdrawQueued=false;
                using(var builder=graph.AddUnsafePass<OverdrawData>("Read Dyson overdraw",out var read)) {
                    read.owner=owner; read.buffer=owner.overdrawPixels; read.width=overdrawWidth; read.height=overdrawHeight;
                    builder.UseBuffer(overdrawHandle,AccessFlags.Read); builder.AllowPassCulling(false);
                    builder.SetRenderFunc((OverdrawData d,UnsafeGraphContext ctx)=> {
                        ctx.cmd.RequestAsyncReadback(d.buffer,request=> {
                            var callback=d.owner.overdrawCallback; d.owner.overdrawCallback=null;
                            callback?.Invoke(request.hasError ? null : request.GetData<uint>().ToArray(),d.width,d.height);
                        });
                    });
                }
            }
        }
    }
    private static Mesh CreateSurfaceSphere()
    {
        // Conservative ray envelope; fragment shader resolves the actual closed band.
        const int longitude=128,latitude=64,faces=1;
        int stride=(longitude+1)*(latitude+1);
        var vertices=new Vector3[stride*faces];
        var indices=new int[longitude*latitude*6*faces];int j=0;
        for(int face=0;face<faces;face++) {
            for(int y=0;y<=latitude;y++) for(int x=0;x<=longitude;x++)
                vertices[face*stride+y*(longitude+1)+x]=new Vector3((float)x/longitude,(float)y/latitude,face);
            for(int y=0;y<latitude;y++) for(int x=0;x<longitude;x++) {
                int a=face*stride+y*(longitude+1)+x,b=a+longitude+1;
                indices[j++]=a;indices[j++]=b;indices[j++]=a+1;
                indices[j++]=a+1;indices[j++]=b;indices[j++]=b+1;
            }
        }
        var mesh=new Mesh { name="Dyson Dense Volume",hideFlags=HideFlags.HideAndDontSave };
        mesh.vertices=vertices;mesh.triangles=indices;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*2);
        return mesh;
    }
    private static Mesh CreatePointTriangle()
    {
        var mesh = new Mesh { name = "Shared Dyson Point", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[] { new Vector3(0,1,0), new Vector3(-0.8660254f,-0.5f,0), new Vector3(0.8660254f,-0.5f,0) };
        mesh.triangles = new[] { 0, 1, 2 }; mesh.RecalculateBounds(); return mesh;
    }
    public static Mesh CreateHexagon()
    {
        // A convex hexagon needs four triangles; the center vertex adds no detail.
        var vertices = new Vector3[6]; var normals = new Vector3[6]; var triangles = new int[12];
        for (int i = 0; i < 6; i++) {
            float angle = i * Mathf.PI / 3;
            vertices[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            normals[i] = Vector3.forward;
        }
        for (int i = 0; i < 4; i++) {
            triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2;
        }
        var mesh = new Mesh { name = "Shared Dyson Hexagon", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = vertices; mesh.normals = normals; mesh.triangles = triangles; mesh.RecalculateBounds();
        return mesh;
    }
    private static Mesh CreateLegacyHexagon()
    {
        var vertices = new Vector3[7]; var normals = new Vector3[7]; var triangles = new int[18];
        for (int i = 0; i < 7; i++) normals[i] = Vector3.forward;
        for (int i = 0; i < 6; i++) {
            float a = i * Mathf.PI / 3;
            vertices[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % 6 + 1;
        }
        var mesh = new Mesh { name = "Shared Dyson Hexagon", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = vertices; mesh.normals = normals; mesh.triangles = triangles; mesh.RecalculateBounds();
        return mesh;
    }
}

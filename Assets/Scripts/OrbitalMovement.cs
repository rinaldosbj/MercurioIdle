using UnityEngine;

public class OrbitalMovement : MonoBehaviour
{
    private float multiplicadorVelocidade = 1f;

    public float MultiplicadorVelocidade => multiplicadorVelocidade;

    [Header("Referência")]
    [SerializeField]
    public Transform centro;

    [Header("Órbita")]
    [SerializeField]
    private float raio = 5f;

    [Tooltip("Velocidade angular em graus por segundo")]
    [SerializeField]
    private float velocidade = 30f;

    [Tooltip("Rotação do plano da órbita")]
    [SerializeField]
    public Vector3 rotacaoOrbita = Vector3.zero;

    [Header("Posição inicial")]
    [SerializeField, Range(0f, 360f)]
    public float anguloInicial = 0f;

    [Header("Correção de posição")]
    [Tooltip("Tempo para o mirror se reorganizar")]
    [SerializeField]
    private float tempoInterpolacao = 0.75f;

    [Tooltip("Velocidade máxima usada para corrigir a fase")]
    [SerializeField]
    private float velocidadeMaximaCorrecao = 90f;

    [Header("Rotação do objeto")]
    [SerializeField]
    private bool olharParaOCentro = true;

    [SerializeField]
    private Vector3 offsetRotacao = Vector3.zero;

    // Movimento orbital puro.
    // Nunca volta.
    private float anguloOrbital;

    // Correção atual aplicada à fase da órbita.
    private float correcaoAtual;

    // Correção desejada.
    private float correcaoAlvo;

    private float velocidadeCorrecao;

    private bool corrigindo;

    /// <summary>
    /// Ângulo visual real do objeto.
    /// </summary>
    private float AnguloAtual
    {
        get => anguloOrbital + correcaoAtual;
    }

    private void Start()
    {
        if (transform.parent != null)
            centro = transform.parent;

        /*
         * Na criação começa diretamente na posição correta.
         */
        anguloOrbital = anguloInicial;

        correcaoAtual = 0f;
        correcaoAlvo = 0f;

        velocidadeCorrecao = 0f;
        corrigindo = false;

        AtualizarOrbita();
        AtualizarRotacao();
    }

    public void BackToStart()
    {
        /*
         * Posição real neste momento.
         */
        float anguloAtualReal = AnguloAtual;

        /*
         * Descobre quanto precisamos andar PARA FRENTE
         * até encontrar o ângulo inicial.
         *
         * Exemplo:
         *
         * atual = 350
         * alvo  = 20
         *
         * distância = 30
         *
         * Portanto o alvo real será 380.
         */
        float anguloNormalizado = Mathf.Repeat(anguloAtualReal, 360f);

        float distancia = Mathf.Repeat(anguloInicial - anguloNormalizado, 360f);

        /*
         * Se já está praticamente no lugar,
         * não precisa corrigir.
         */
        if (distancia < 0.01f)
        {
            correcaoAlvo = correcaoAtual;
            velocidadeCorrecao = 0f;
            corrigindo = false;

            return;
        }

        /*
         * Cria um alvo ABSOLUTO à frente.
         */
        float anguloAlvoReal = anguloAtualReal + distancia;

        /*
         * Converte esse alvo em uma correção de fase.
         *
         * O anguloOrbital continua andando normalmente.
         */
        correcaoAlvo = anguloAlvoReal - anguloOrbital;

        /*
         * Faz a nova correção partir do estado atual.
         */
        velocidadeCorrecao = 0f;
        corrigindo = true;
    }

    private void Update()
    {
        if (centro == null)
            return;

        /*
         * A órbita PRINCIPAL sempre anda para frente.
         */
        anguloOrbital += velocidade * multiplicadorVelocidade * Time.deltaTime;

        /*
         * Agora corrigimos somente a fase.
         */
        if (corrigindo)
        {
            correcaoAtual = Mathf.SmoothDamp(
                correcaoAtual,
                correcaoAlvo,
                ref velocidadeCorrecao,
                tempoInterpolacao,
                velocidadeMaximaCorrecao,
                Time.deltaTime
            );

            /*
             * Quando estiver suficientemente próximo,
             * fixa exatamente no alvo.
             */
            if (Mathf.Abs(correcaoAtual - correcaoAlvo) < 0.01f)
            {
                correcaoAtual = correcaoAlvo;

                velocidadeCorrecao = 0f;
                corrigindo = false;
            }
        }

        AtualizarOrbita();

        if (olharParaOCentro)
            AtualizarRotacao();
    }

    private void AtualizarOrbita()
    {
        float angulo = AnguloAtual * Mathf.Deg2Rad;

        Vector3 posicao = new Vector3(Mathf.Cos(angulo) * raio, 0f, Mathf.Sin(angulo) * raio);

        Quaternion rotacao = Quaternion.Euler(rotacaoOrbita);

        posicao = rotacao * posicao;

        transform.position = centro.position + posicao;
    }

    private void AtualizarRotacao()
    {
        Vector3 direcao = centro.position - transform.position;

        if (direcao.sqrMagnitude < 0.001f)
            return;

        Quaternion rotacao = Quaternion.LookRotation(direcao.normalized, Vector3.up);

        rotacao *= Quaternion.Euler(offsetRotacao);

        transform.rotation = rotacao;
    }

    private void OnDrawGizmosSelected()
    {
        if (centro == null)
            return;

        Gizmos.color = Color.cyan;

        Quaternion rotacao = Quaternion.Euler(rotacaoOrbita);

        Vector3 anterior = Vector3.zero;

        const int segmentos = 64;

        for (int i = 0; i <= segmentos; i++)
        {
            float angulo = (i / (float)segmentos) * Mathf.PI * 2f;

            Vector3 posicao = new Vector3(Mathf.Cos(angulo) * raio, 0f, Mathf.Sin(angulo) * raio);

            posicao = rotacao * posicao;
            posicao += centro.position;

            if (i > 0)
                Gizmos.DrawLine(anterior, posicao);

            anterior = posicao;
        }
    }

    public void SetMultiplicadorVelocidade(float valor)
    {
        multiplicadorVelocidade = valor;
    }
}

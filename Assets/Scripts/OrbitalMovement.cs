using UnityEngine;

public class OrbitalMovement : MonoBehaviour
{
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

    [Header("Rotação do objeto")]
    [SerializeField]
    private bool olharParaOCentro = true;

    [SerializeField]
    private Vector3 offsetRotacao = Vector3.zero;

    private float anguloAtual;

    private void Start()
    {
        BackToStart();
        AtualizarOrbita();

        if (transform.parent != null)
            centro = transform.parent;
    }

    public void BackToStart()
    {
        anguloAtual = anguloInicial;
    }

    private void Update()
    {
        if (centro == null)
            return;

        // Movimento orbital
        anguloAtual += velocidade * Time.deltaTime;

        if (anguloAtual >= 360f)
            anguloAtual -= 360f;

        AtualizarOrbita();

        // Rotação do objeto
        if (olharParaOCentro)
        {
            AtualizarRotacao();
        }
    }

    private void AtualizarOrbita()
    {
        // Converte o ângulo da órbita para radianos
        float angulo = anguloAtual * Mathf.Deg2Rad;

        // Cria a posição em uma órbita circular no plano XZ
        Vector3 posicao = new Vector3(Mathf.Cos(angulo) * raio, 0f, Mathf.Sin(angulo) * raio);

        // Rotaciona o plano da órbita
        Quaternion rotacao = Quaternion.Euler(rotacaoOrbita);

        posicao = rotacao * posicao;

        // Posiciona o objeto em relação ao centro
        transform.position = centro.position + posicao;
    }

    private void AtualizarRotacao()
    {
        // Direção do objeto para o centro
        Vector3 direcao = centro.position - transform.position;

        if (direcao.sqrMagnitude < 0.001f)
            return;

        // Faz o objeto olhar para o centro
        Quaternion rotacao = Quaternion.LookRotation(direcao.normalized, Vector3.up);

        // Permite corrigir a orientação do modelo
        rotacao *= Quaternion.Euler(offsetRotacao);

        transform.rotation = rotacao;
    }

    private void OnDrawGizmosSelected()
    {
        if (centro == null)
            return;

        // Mostra visualmente a órbita no editor
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
            {
                Gizmos.DrawLine(anterior, posicao);
            }

            anterior = posicao;
        }
    }
}

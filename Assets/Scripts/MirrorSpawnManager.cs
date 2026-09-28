using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class MirrorSpawnManager : MonoBehaviour
{
    [Header("Ring Acceleration")]
    [SerializeField]
    private float _ringSpeedMultiplier = 2f;

    [SerializeField]
    private float _ringAccelerationDuration = 0.15f;

    [SerializeField]
    private float _ringDecelerationDuration = 0.5f;

    [Header("Prefabs")]
    [SerializeField]
    private GameObject _mirrorPrefab;

    [SerializeField]
    private GameObject _sunMesh;

    [Header("Rings")]
    [SerializeField]
    private int _ringCount = 12;

    [SerializeField]
    private int _currentMaxMirrorsInRing = 5;

    [SerializeField]
    private int _absoluteMaxMirrorsInRing = 50;

    [Header("Sun Bonk")]
    [SerializeField]
    private Vector3 _sunPunchScale = new Vector3(0.15f, 0.15f, 0.15f);

    [SerializeField]
    private float _sunBonkDuration = 0.16f;

    [SerializeField]
    private int _sunBonkVibrato = 4;

    [SerializeField, Range(0f, 1f)]
    private float _sunBonkElasticity = 0.5f;

    // Cada índice representa um ring.
    private List<List<OrbitalMovement>> _mirrorMovements = new();

    // Referência para a lista do ring atual.
    private List<OrbitalMovement> _currentMirrorMovements;

    private int _currentRingIndex = 0;

    private bool _maxCapacity = false;

    // Vetores dos rings.
    private Vector3[] _ringVectors;

    private Tween _sunBonkTween;

    private Vector3 _sunBaseScale;

    private void Start()
    {
        GenerateRingVectors();
        InitializeMirrorLists();

        InitializeSun();
    }

    private void InitializeSun()
    {
        if (_sunMesh == null)
            return;

        _sunBaseScale = _sunMesh.transform.localScale;
    }

    private void GenerateRingVectors()
    {
        if (_ringCount <= 0)
        {
            _ringVectors = new Vector3[0];
            return;
        }

        _ringVectors = new Vector3[_ringCount];

        float angleStep = 180f / _ringCount;

        for (int i = 0; i < _ringCount; i++)
        {
            float angle;

            // Primeiro ring fica no centro.
            if (i == 0)
            {
                angle = 0f;
            }
            else
            {
                int step = (i + 1) / 2;

                angle = step * angleStep;

                // Alterna entre positivo e negativo.
                if (i % 2 == 0)
                    angle *= -1f;
            }

            _ringVectors[i] = new Vector3(5f, 0f, angle);
        }
    }

    private void InitializeMirrorLists()
    {
        _mirrorMovements.Clear();

        for (int i = 0; i < _ringVectors.Length; i++)
        {
            _mirrorMovements.Add(new List<OrbitalMovement>());
        }

        _currentRingIndex = 0;

        if (_mirrorMovements.Count > 0)
        {
            _currentMirrorMovements = _mirrorMovements[0];
        }
        else
        {
            _currentMirrorMovements = null;
        }
    }

    private void ManageMirrorLists()
    {
        if (_currentMirrorMovements == null)
            return;

        // Ainda tem espaço no ring atual.
        if (_currentMirrorMovements.Count < _currentMaxMirrorsInRing)
            return;

        // Vai para o próximo ring.
        _currentRingIndex++;

        // Se passou do último ring, volta para o primeiro.
        if (_currentRingIndex >= _ringVectors.Length)
        {
            _currentRingIndex = 0;

            // Se já estamos na capacidade máxima,
            // não existe mais espaço para crescer.
            if (_currentMaxMirrorsInRing >= _absoluteMaxMirrorsInRing)
            {
                _maxCapacity = true;
                return;
            }

            // Dobra a capacidade.
            _currentMaxMirrorsInRing *= 2;

            // Nunca ultrapassa o máximo absoluto.
            if (_currentMaxMirrorsInRing > _absoluteMaxMirrorsInRing)
            {
                _currentMaxMirrorsInRing = _absoluteMaxMirrorsInRing;
            }
        }

        _currentMirrorMovements = _mirrorMovements[_currentRingIndex];
    }

    public void AddMirror(int amount)
    {
        if (_maxCapacity)
            return;

        if (_ringVectors.Length == 0)
            return;

        if (amount <= 0)
            return;

        for (int i = 0; i < amount; i++)
        {
            OrbitalMovement mirrorMovement = Instantiate(_mirrorPrefab, transform)
                .GetComponent<OrbitalMovement>();

            mirrorMovement.centro = transform;

            mirrorMovement.gameObject.name = _currentRingIndex.ToString();

            mirrorMovement.rotacaoOrbita = _ringVectors[_currentRingIndex];

            _currentMirrorMovements.Add(mirrorMovement);

            UpdateInitialAngles();

            ManageMirrorLists();

            if (_maxCapacity)
                break;
        }

        AddMirrorAnimation();
    }

    private void UpdateInitialAngles()
    {
        if (_currentMirrorMovements == null)
            return;

        if (_currentMirrorMovements.Count == 0)
            return;

        for (int i = 0; i < _currentMirrorMovements.Count; i++)
        {
            _currentMirrorMovements[i].anguloInicial = i * 360f / _currentMirrorMovements.Count;

            _currentMirrorMovements[i].BackToStart();
        }
    }

    public void SetRingCount(int amount)
    {
        if (amount <= 0)
            return;

        // Pega todos os mirrors que já existem.
        List<OrbitalMovement> existingMirrors = GetAllExistingMirrors();

        // Atualiza a quantidade de rings.
        _ringCount = amount;

        // Gera os novos vetores.
        GenerateRingVectors();

        // Recria as listas dos rings.
        InitializeMirrorLists();

        // Redistribui os mirrors existentes.
        RedistributeMirrors(existingMirrors);
    }

    private List<OrbitalMovement> GetAllExistingMirrors()
    {
        List<OrbitalMovement> existingMirrors = new List<OrbitalMovement>();

        foreach (List<OrbitalMovement> ring in _mirrorMovements)
        {
            foreach (OrbitalMovement mirror in ring)
            {
                if (mirror != null)
                {
                    existingMirrors.Add(mirror);
                }
            }
        }

        return existingMirrors;
    }

    private void RedistributeMirrors(List<OrbitalMovement> mirrors)
    {
        if (mirrors.Count == 0)
            return;

        if (_ringVectors.Length == 0)
            return;

        /*
         * Distribui os mirrors de forma equilibrada.
         *
         * Exemplo:
         *
         * 10 mirrors / 3 rings
         *
         * Ring 0 -> 4
         * Ring 1 -> 3
         * Ring 2 -> 3
         *
         * O operador % faz o ciclo:
         *
         * 0, 1, 2, 0, 1, 2, 0, 1, 2...
         */

        for (int i = 0; i < mirrors.Count; i++)
        {
            OrbitalMovement mirror = mirrors[i];

            if (mirror == null)
                continue;

            int ringIndex = i % _ringVectors.Length;

            _mirrorMovements[ringIndex].Add(mirror);

            mirror.gameObject.name = ringIndex.ToString();

            mirror.rotacaoOrbita = _ringVectors[ringIndex];
        }

        // Atualiza os ângulos de TODOS os rings.
        UpdateAllRingAngles();

        // Procura o próximo ring que ainda tem espaço.
        UpdateCurrentRing();
    }

    private void UpdateAllRingAngles()
    {
        for (int ringIndex = 0; ringIndex < _mirrorMovements.Count; ringIndex++)
        {
            List<OrbitalMovement> ring = _mirrorMovements[ringIndex];

            if (ring.Count == 0)
                continue;

            for (int i = 0; i < ring.Count; i++)
            {
                ring[i].anguloInicial = i * 360f / ring.Count;

                ring[i].BackToStart();
            }
        }
    }

    private void UpdateCurrentRing()
    {
        if (_mirrorMovements.Count == 0)
        {
            _currentMirrorMovements = null;
            return;
        }

        // Procura um ring que ainda não atingiu a capacidade atual.
        for (int i = 0; i < _mirrorMovements.Count; i++)
        {
            if (_mirrorMovements[i].Count < _currentMaxMirrorsInRing)
            {
                _currentRingIndex = i;
                _currentMirrorMovements = _mirrorMovements[i];

                return;
            }
        }

        // Se todos estão cheios, deixa o ManageMirrorLists()
        // determinar a próxima etapa.
        _currentRingIndex = 0;

        _currentMirrorMovements = _mirrorMovements[0];

        ManageMirrorLists();
    }

    private void AddMirrorAnimation()
    {
        QueueSunBonk();
        AnimateAllRingsAcceleration(); // can be done via event system
    }

    // =========================================================
    // SUN BONK
    // =========================================================

    private void QueueSunBonk()
    {
        if (_sunMesh == null)
            return;

        // Cancela imediatamente o bonk atual.
        if (_sunBonkTween != null && _sunBonkTween.IsActive())
        {
            _sunBonkTween.Kill();
            _sunBonkTween = null;
        }

        PlaySunBonk();
    }

    private void PlaySunBonk()
    {
        if (_sunMesh == null)
            return;

        Transform sunTransform = _sunMesh.transform;

        // Sempre começa o novo bonk da escala original.
        sunTransform.localScale = _sunBaseScale;

        _sunBonkTween = sunTransform
            .DOPunchScale(_sunPunchScale, _sunBonkDuration, _sunBonkVibrato, _sunBonkElasticity)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                // Garante que termina exatamente na escala original.
                sunTransform.localScale = _sunBaseScale;

                _sunBonkTween = null;
            });
    }

    // =========================================================
    // RING ANIMATION
    // =========================================================

    private void AnimateAllRingsAcceleration()
    {
        foreach (List<OrbitalMovement> ring in _mirrorMovements)
        {
            foreach (OrbitalMovement mirror in ring)
            {
                if (mirror == null)
                    continue;

                /*
                 * Mata somente a animação de velocidade
                 * desse mirror.
                 *
                 * Se o jogador estiver spamando e a
                 * aceleração for chamada novamente,
                 * ela parte da velocidade atual.
                 */
                DOTween.Kill(mirror, false);

                DOTween
                    .To(
                        () => mirror.MultiplicadorVelocidade,
                        value => mirror.SetMultiplicadorVelocidade(value),
                        _ringSpeedMultiplier,
                        _ringAccelerationDuration
                    )
                    .SetEase(Ease.OutQuad)
                    .SetId(mirror)
                    .OnComplete(() =>
                    {
                        DOTween
                            .To(
                                () => mirror.MultiplicadorVelocidade,
                                value => mirror.SetMultiplicadorVelocidade(value),
                                1f,
                                _ringDecelerationDuration
                            )
                            .SetEase(Ease.OutQuad)
                            .SetId(mirror);
                    });
            }
        }
    }

    private void OnDestroy()
    {
        if (_sunBonkTween != null && _sunBonkTween.IsActive())
        {
            _sunBonkTween.Kill();
        }
    }

    private void OnValidate()
    {
        if (_ringCount <= 0)
            return;

        GenerateRingVectors();
    }
}

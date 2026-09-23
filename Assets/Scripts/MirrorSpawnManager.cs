using System.Collections.Generic;
using UnityEngine;

public class MirrorSpawnManager : MonoBehaviour
{
    [SerializeField]
    private GameObject _mirrorPrefab;

    // Cada índice representa um anel.
    // Exemplo:
    // _mirrorMovements[0] = espelhos do anel 0
    // _mirrorMovements[1] = espelhos do anel 1
    // ...
    private List<List<OrbitalMovement>> _mirrorMovements = new();

    // Referência para a lista do anel atual.
    private List<OrbitalMovement> _currentMirrorMovements;

    [SerializeField]
    private int _currentMaxMirrorsInRing = 5;

    [SerializeField]
    private int _absoluteMaxMirrorsInRing = 50;

    private int _currentRingIndex = 0;

    private bool _maxCapacity = false;

    private Vector3[] _ringVectors =
    {
        new Vector3(5, 5, 0),
        new Vector3(5, 0, 30),
        new Vector3(5, -5, -30),
        new Vector3(5, 10, 60),
        new Vector3(5, 10, -60),
        new Vector3(5, 0, 90)
    };

    private void Start()
    {
        InitializeMirrorLists();
    }

    private void InitializeMirrorLists()
    {
        // Cria uma lista para cada anel.
        for (int i = 0; i < _ringVectors.Length; i++)
        {
            _mirrorMovements.Add(new List<OrbitalMovement>());
        }

        // Começamos no anel 0.
        _currentMirrorMovements = _mirrorMovements[_currentRingIndex];
    }

    private void ManageMirrorLists()
    {
        // Ainda não atingiu o limite do anel atual.
        if (_currentMirrorMovements.Count < _currentMaxMirrorsInRing)
            return;

        // Vai para o próximo anel.
        _currentRingIndex++;

        // Se passou do último anel, volta para o primeiro.
        if (_currentRingIndex >= _ringVectors.Length)
        {
            _currentRingIndex = 0;

            // Se já estamos trabalhando com a capacidade máxima,
            // significa que todos os anéis já atingiram esse limite.
            if (_currentMaxMirrorsInRing >= _absoluteMaxMirrorsInRing)
            {
                _maxCapacity = true;
                return;
            }

            // Dobra a capacidade.
            _currentMaxMirrorsInRing *= 2;

            // Não deixa ultrapassar o máximo absoluto.
            if (_currentMaxMirrorsInRing > _absoluteMaxMirrorsInRing)
            {
                _currentMaxMirrorsInRing = _absoluteMaxMirrorsInRing;
            }
        }

        // Atualiza a referência para o novo anel.
        _currentMirrorMovements = _mirrorMovements[_currentRingIndex];
    }

    public void AddMirror(int amount)
    {
        if (_maxCapacity)
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
    }

    private void UpdateInitialAngles()
    {
        for (int i = 0; i < _currentMirrorMovements.Count; i++)
        {
            _currentMirrorMovements[i].anguloInicial = i * 360f / _currentMirrorMovements.Count;

            _currentMirrorMovements[i].BackToStart();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

public class MirrorSpawnManager : MonoBehaviour
{
    [SerializeField]
    private GameObject _mirrorPrefab;

    [SerializeField]
    private int _ringCount = 12;

    private List<List<OrbitalMovement>> _mirrorMovements = new();

    private List<OrbitalMovement> _currentMirrorMovements;

    [SerializeField]
    private int _currentMaxMirrorsInRing = 5;

    [SerializeField]
    private int _absoluteMaxMirrorsInRing = 50;

    private int _currentRingIndex = 0;

    private bool _maxCapacity = false;

    // Vetores dos rings.
    private Vector3[] _ringVectors;

    private void Start()
    {
        GenerateRingVectors();
        InitializeMirrorLists();
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

        if (_currentMirrorMovements.Count < _currentMaxMirrorsInRing)
            return;

        _currentRingIndex++;

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

            _currentMaxMirrorsInRing *= 2;

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

        GenerateRingVectors();

        InitializeMirrorLists();

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

        UpdateAllRingAngles();

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

        _currentRingIndex = 0;

        _currentMirrorMovements = _mirrorMovements[0];

        ManageMirrorLists();
    }

    private void OnValidate()
    {
        if (_ringCount <= 0)
            return;

        GenerateRingVectors();
    }
}

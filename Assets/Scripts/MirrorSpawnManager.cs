using System.Collections.Generic;
using UnityEngine;

public class MirrorSpawnManager : MonoBehaviour
{
    [SerializeField]
    private GameObject _mirrorPrefab;
    private List<OrbitalMovement> _mirrorMovements = new List<OrbitalMovement>();

    public void AddMirror(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            OrbitalMovement mirrorMovement = Instantiate(_mirrorPrefab, transform)
                .GetComponent<OrbitalMovement>();
            mirrorMovement.centro = transform;
            _mirrorMovements.Add(mirrorMovement);
            UpdateInitialAngles();
        }
    }

    private void UpdateInitialAngles()
    {
        for (int i = 0; i < _mirrorMovements.Count; i++)
        {
            _mirrorMovements[i].anguloInicial = i * 360 / _mirrorMovements.Count;
            _mirrorMovements[i].BackToStart();
        }
    }
}

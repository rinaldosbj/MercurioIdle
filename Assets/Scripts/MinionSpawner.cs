using UnityEngine;
using MercurioIdle.Construction;

namespace MercurioIdle.Robots
{
    public sealed class MinionSpawner : MonoBehaviour
    {
        [SerializeField] private Minion minionPrefab;
        [SerializeField, Min(0)] private int startingMinions = 1;
        [SerializeField, Range(0f, 0.45f)] private float viewMargin = 0.15f;

        [Header("Botão")]
        [SerializeField] private bool createButtonAutomatically = true;
        [SerializeField] private string buttonLabel = "Criar robô";

        private Camera viewCamera;
        private TerminalButton button;
        private bool initialSpawned;
        private int nextFactory;
        private float refreshTimer;

        public int Capacity => startingMinions + RobotFactory.TotalRobotCapacity;

        public int MinionCount => FindObjectsByType<Minion>(FindObjectsSortMode.None).Length;

        private void Start()
        {
            viewCamera = Camera.main;
            if (createButtonAutomatically) BuildButton();
        }

        private void Update()
        {
            if (!initialSpawned) TrySpawnInitial();

            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 0.25f;
                RefreshButton();
            }
        }

        public void Spawn()
        {
            if (minionPrefab == null)
            {
                Debug.LogError("Arraste o prefab do minion para o campo Minion Prefab do MinionSpawner.", this);
                return;
            }

            if (RobotFactory.All.Count == 0 || MinionCount >= Capacity) return;

            var factory = RobotFactory.All[nextFactory++ % RobotFactory.All.Count];
            var minion = Instantiate(minionPrefab, factory.DoorPoint, Quaternion.identity);
            minion.SetHome(factory.HomePoint);
            RefreshButton();
        }

        private void TrySpawnInitial()
        {
            var designations = DigDesignations.Instance;
            if (designations == null || designations.Terrain == null || designations.Terrain.Level == null) return;

            initialSpawned = true;
            if (minionPrefab == null) return;

            int missing = startingMinions - MinionCount;
            for (int i = 0; i < missing; i++)
            {
                Vector3 point = FindSpawnPoint();
                var minion = Instantiate(minionPrefab, point, Quaternion.identity);
                minion.SetHome(point);
            }
        }

        private Vector3 FindSpawnPoint()
        {
            float planeZ = TerrainQuery.PlaneZ;

            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null)
                return new Vector3(transform.position.x, transform.position.y, planeZ - 0.5f);

            float depth = Mathf.Abs(planeZ - viewCamera.transform.position.z);

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float viewX = Random.Range(viewMargin, 1f - viewMargin);
                float viewY = Random.Range(0.55f, 0.9f);
                Vector3 point = ViewportToPlane(viewX, viewY, depth, planeZ);
                if (!TerrainQuery.IsSolid(point)) return point;
            }

            return ViewportToPlane(0.5f, 0.75f, depth, planeZ);
        }

        private Vector3 ViewportToPlane(float viewX, float viewY, float depth, float planeZ)
        {
            Vector3 world = viewCamera.ViewportToWorldPoint(new Vector3(viewX, viewY, depth));
            world.z = planeZ - 0.5f;
            return world;
        }

        private void BuildButton()
        {
            var canvas = TerminalUI.CreateCanvas("Minion UI", 10);

            button = TerminalUI.CreateButton(canvas.transform, "Botão criar minion", buttonLabel.ToUpperInvariant(), 22);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta = new Vector2(320f, 84f);

            button.Clicked += Spawn;
            RefreshButton();
        }

        private void RefreshButton()
        {
            if (button == null) return;

            int count = MinionCount;
            int capacity = Capacity;
            bool hasFactory = RobotFactory.All.Count > 0;
            bool canCreate = hasFactory && count < capacity;

            string second = !hasFactory ? "REQUER FÁBRICA [B]" : count >= capacity ? "LIMITE ATINGIDO" : "PRONTO";

            button.Interactable = canCreate;
            button.Label.text = buttonLabel.ToUpperInvariant() + "  " + count + "/" + capacity + "\n<size=14>" + second + "</size>";
        }
    }
}

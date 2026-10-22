using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using MercurioIdle.Planet;

namespace MercurioIdle.Robots
{
    public sealed class MinionSpawner : MonoBehaviour
    {
        [SerializeField] private Minion minionPrefab;
        [SerializeField, Range(0f, 0.45f)] private float viewMargin = 0.15f;

        [Header("Botão")]
        [SerializeField] private bool createButtonAutomatically = true;
        [SerializeField] private string buttonLabel = "Criar minion";

        private Camera viewCamera;

        private void Start()
        {
            viewCamera = Camera.main;
            if (createButtonAutomatically) BuildButton();
        }

        public void Spawn()
        {
            if (minionPrefab == null)
            {
                Debug.LogError("Arraste o prefab do minion para o campo Minion Prefab do MinionSpawner.", this);
                return;
            }

            Vector3 point = FindSpawnPoint();
            var minion = Instantiate(minionPrefab, point, Quaternion.identity);
            minion.SetHome(point);
        }

        private Vector3 FindSpawnPoint()
        {
            var designations = DigDesignations.Instance;
            var terrain = designations != null ? designations.Terrain : null;
            float planeZ = terrain != null ? terrain.transform.position.z : 0f;

            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null)
                return new Vector3(transform.position.x, transform.position.y, planeZ - 0.5f);

            float depth = Mathf.Abs(planeZ - viewCamera.transform.position.z);

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float viewX = Random.Range(viewMargin, 1f - viewMargin);
                float viewY = Random.Range(0.55f, 0.9f);
                Vector3 point = ViewportToPlane(viewX, viewY, depth, planeZ);
                if (!IsSolid(terrain, point)) return point;
            }

            return ViewportToPlane(0.5f, 0.75f, depth, planeZ);
        }

        private Vector3 ViewportToPlane(float viewX, float viewY, float depth, float planeZ)
        {
            Vector3 world = viewCamera.ViewportToWorldPoint(new Vector3(viewX, viewY, depth));
            world.z = planeZ - 0.5f;
            return world;
        }

        private static bool IsSolid(PlanetExcavationPreview terrain, Vector3 world)
        {
            if (terrain == null) return false;

            var level = terrain.Level;
            var projection = terrain.Projection as PlanetProjection;
            if (level == null || projection == null) return false;

            try
            {
                Vector3 local = terrain.transform.InverseTransformPoint(world);
                var position = projection.FromLocal(new Vector3(local.x, local.y, 0f));
                return level.TryGetBlock(position, out _);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private void BuildButton()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("Minion UI");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var buttonObject = new GameObject("Botão criar minion", typeof(RectTransform));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(20f, -20f);
            buttonRect.sizeDelta = new Vector2(260f, 70f);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.55f, 0.6f, 1f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(Spawn);

            var labelObject = new GameObject("Texto", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.AddComponent<Text>();
            label.text = buttonLabel;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 28;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();

            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                legacy.enabled = false;
                Destroy(legacy);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                var module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }
    }
}

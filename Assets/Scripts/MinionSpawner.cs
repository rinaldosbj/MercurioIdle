using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MercurioIdle.Robots
{
    public sealed class MinionSpawner : MonoBehaviour
    {
        [SerializeField, Tooltip("Prefab do minion (precisa ter o script Minion).")]
        private Minion minionPrefab;
        [SerializeField, Tooltip("Onde os minions nascem. Se vazio, usa a posição deste objeto.")]
        private Transform spawnPoint;
        [SerializeField, Tooltip("Variação aleatória (m) para a esquerda/direita do ponto de spawn.")]
        private float spawnSpreadX = 3f;

        [Header("Botão")]
        [SerializeField] private bool createButtonAutomatically = true;
        [SerializeField] private string buttonLabel = "Criar minion";

        private void Start()
        {
            if (createButtonAutomatically) BuildButton();
        }

        /// <summary>Cria um minion. Também pode ser ligado ao OnClick de um botão seu.</summary>
        public void Spawn()
        {
            if (minionPrefab == null)
            {
                Debug.LogError("Arraste o prefab do minion para o campo Minion Prefab do MinionSpawner.", this);
                return;
            }

            Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
            origin.x += Random.Range(-spawnSpreadX, spawnSpreadX);
            Instantiate(minionPrefab, origin, Quaternion.identity);
        }

        // ---------- Interface ----------

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

        // O projeto usa só o Input System novo, então a UI precisa do InputSystemUIInputModule.
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

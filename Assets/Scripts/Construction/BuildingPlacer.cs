using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MercurioIdle.Construction
{
    public sealed class BuildingPlacer : MonoBehaviour
    {
        private const float SampleSpacing = 0.5f;
        private const float FootprintInset = 0.1f;
        private const float OverlapMargin = 0.2f;
        private const float MaxSearchDistance = 60f;
        private const float RefreshSeconds = 0.2f;

        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0f)] private float maxHeightDifference = 2f;

        private static BuildingPlacer instance;
        private static Texture2D dashTexture;

        private BuildingDefinition definition;
        private bool placing;
        private int closedFrame = -1;

        private SpriteRenderer ghost;
        private LineRenderer outline;
        private LineRenderer guide;
        private Material lineMaterial;

        private Vector3 lastMouseWorld;
        private float refreshTimer;
        private bool valid;
        private bool groundFound;
        private float centerX;
        private float baseY;

        public static bool Active => instance != null && (instance.placing || instance.closedFrame == Time.frameCount);

        public bool Placing => placing;
        public BuildingDefinition Current => definition;

        public event Action<BuildingDefinition> Placed;

        private void Awake()
        {
            instance = this;
            if (viewCamera == null) viewCamera = Camera.main;
            BuildGhost();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        public void BeginPlacing(BuildingDefinition target)
        {
            if (target == null) return;

            definition = target;
            placing = true;
            refreshTimer = 0f;
            lastMouseWorld = new Vector3(float.MaxValue, 0f, 0f);

            ghost.sprite = target.GetSprite();
            ghost.gameObject.SetActive(true);
            outline.gameObject.SetActive(true);
            guide.gameObject.SetActive(true);
        }

        public void CancelPlacing()
        {
            if (placing) EndPlacing();
        }

        private void EndPlacing()
        {
            placing = false;
            definition = null;
            closedFrame = Time.frameCount;

            ghost.gameObject.SetActive(false);
            outline.gameObject.SetActive(false);
            guide.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!placing) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                EndPlacing();
                return;
            }

            if (mouse == null || viewCamera == null) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                EndPlacing();
                return;
            }

            float planeZ = TerrainQuery.PlaneZ;
            Vector3 world = ScreenToPlane(mouse.position.ReadValue(), planeZ);

            refreshTimer -= Time.deltaTime;
            bool moved = (world - lastMouseWorld).sqrMagnitude > 0.0025f;
            if (moved || refreshTimer <= 0f)
            {
                refreshTimer = RefreshSeconds;
                lastMouseWorld = world;
                Evaluate(world, planeZ);
            }

            UpdateGhost(world, planeZ);

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUi && valid && mouse.leftButton.wasPressedThisFrame) Place(world, planeZ);
        }

        private Vector3 ScreenToPlane(Vector2 screen, float planeZ)
        {
            float depth = Mathf.Abs(planeZ - viewCamera.transform.position.z);
            return viewCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        }

        private void Evaluate(Vector3 mouseWorld, float planeZ)
        {
            centerX = mouseWorld.x;
            baseY = mouseWorld.y;
            valid = false;
            groundFound = false;

            float half = definition.Width * 0.5f;
            float span = definition.Width - FootprintInset * 2f;
            int samples = Mathf.Max(3, Mathf.CeilToInt(span / SampleSpacing) + 1);

            float low = float.MaxValue;
            float high = float.MinValue;
            for (int i = 0; i < samples; i++)
            {
                float x = centerX - half + FootprintInset + span * i / (samples - 1);
                if (!TerrainQuery.TryFindGround(x, planeZ, mouseWorld.y, MaxSearchDistance, out float ground)) return;
                low = Mathf.Min(low, ground);
                high = Mathf.Max(high, ground);
            }

            groundFound = true;
            baseY = high;

            if (high - low > maxHeightDifference) return;

            foreach (var factory in RobotFactory.All)
            {
                bool overlapsX = centerX - half < factory.Right + OverlapMargin && factory.Left - OverlapMargin < centerX + half;
                bool overlapsY = baseY < factory.Top && factory.Bottom < baseY + definition.Height;
                if (overlapsX && overlapsY) return;
            }

            valid = true;
        }

        private void UpdateGhost(Vector3 mouseWorld, float planeZ)
        {
            float half = definition.Width * 0.5f;
            float y = groundFound ? baseY : mouseWorld.y;
            float x = groundFound ? centerX : mouseWorld.x;

            ghost.transform.position = new Vector3(x, y, planeZ - 0.1f);
            ghost.color = valid ? new Color(1f, 1f, 1f, 0.65f) : new Color(0.55f, 0.55f, 0.55f, 0.3f);

            Color lineColor = valid ? new Color(0.95f, 0.95f, 0.95f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f);
            float z = planeZ - 0.15f;
            float top = y + definition.Height;

            outline.SetPosition(0, new Vector3(x - half, y, z));
            outline.SetPosition(1, new Vector3(x + half, y, z));
            outline.SetPosition(2, new Vector3(x + half, top, z));
            outline.SetPosition(3, new Vector3(x - half, top, z));
            outline.SetPosition(4, new Vector3(x - half, y, z));
            outline.startColor = lineColor;
            outline.endColor = lineColor;

            float depth = Mathf.Abs(planeZ - viewCamera.transform.position.z);
            float viewTop = viewCamera.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth)).y;
            bool showGuide = viewTop > top;
            if (guide.gameObject.activeSelf != showGuide) guide.gameObject.SetActive(showGuide);
            if (showGuide)
            {
                guide.SetPosition(0, new Vector3(x, viewTop, z));
                guide.SetPosition(1, new Vector3(x, top, z));
            }
        }

        private void Place(Vector3 mouseWorld, float planeZ)
        {
            var built = definition;
            float fall = Mathf.Clamp(mouseWorld.y - baseY, 1f, 14f);
            built.Build(new Vector3(centerX, baseY, planeZ - 0.1f), fall);

            EndPlacing();
            Placed?.Invoke(built);
        }

        private void BuildGhost()
        {
            if (dashTexture == null)
            {
                dashTexture = new Texture2D(4, 1, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };
                dashTexture.SetPixels(new[] { Color.white, Color.white, Color.clear, Color.clear });
                dashTexture.Apply();
            }

            var ghostObject = new GameObject("Placement ghost");
            ghostObject.transform.SetParent(transform, false);
            ghost = ghostObject.AddComponent<SpriteRenderer>();
            ghost.sortingOrder = 7;

            lineMaterial = new Material(ghost.sharedMaterial) { mainTexture = dashTexture };
            outline = CreateLine("Placement outline", 0.06f, 5, 8);
            guide = CreateLine("Placement guide", 0.04f, 2, 7);
            guide.startColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            guide.endColor = new Color(0.35f, 0.35f, 0.35f, 1f);

            ghostObject.SetActive(false);
            outline.gameObject.SetActive(false);
            guide.gameObject.SetActive(false);
        }

        private LineRenderer CreateLine(string lineName, float width, int points, int sortingOrder)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.textureMode = LineTextureMode.Tile;
            line.numCapVertices = 0;
            line.sortingOrder = sortingOrder;
            line.sharedMaterial = lineMaterial;
            return line;
        }
    }
}

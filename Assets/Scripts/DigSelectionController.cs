using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using MercurioIdle.Construction;
using MercurioIdle.Planet;

namespace MercurioIdle.Robots
{
    [RequireComponent(typeof(DigDesignations))]
    public sealed class DigSelectionController : MonoBehaviour
    {
        [SerializeField, Min(1f), Tooltip("Quantos pixels o mouse precisa andar para virar arrastar em vez de clique.")]
        private float dragThresholdPixels = 8f;
        [SerializeField, Tooltip("Limite de blocos por seleção, para evitar travar.")]
        private int maxBlocksPerSelection = 4000;
        [SerializeField] private Color markRectColor = new Color(1f, 0.85f, 0.1f, 0.25f);
        [SerializeField] private Color unmarkRectColor = new Color(1f, 0.25f, 0.2f, 0.25f);

        private DigDesignations designations;
        private bool pressing;
        private bool dragging;
        private bool markMode;
        private Vector2 startPosition;
        private Vector2 currentPosition;

        private void Awake() => designations = GetComponent<DigDesignations>();

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 position = mouse.position.ReadValue();

            if (!pressing)
            {
                bool left = mouse.leftButton.wasPressedThisFrame;
                bool right = mouse.rightButton.wasPressedThisFrame;
                if ((left || right) && !IsPointerOverUI() && !BuildingPlacer.Active)
                {
                    pressing = true;
                    dragging = false;
                    markMode = left;
                    startPosition = position;
                    currentPosition = position;
                }
                return;
            }

            currentPosition = position;
            if (!dragging && (currentPosition - startPosition).sqrMagnitude > dragThresholdPixels * dragThresholdPixels)
                dragging = true;

            var button = markMode ? mouse.leftButton : mouse.rightButton;
            if (!button.wasReleasedThisFrame) return;

            if (dragging)
            {
                if (ApplyRectangle() == 0) ApplyClick(startPosition);
            }
            else ApplyClick(currentPosition);

            pressing = false;
            dragging = false;
        }

        private static bool IsPointerOverUI() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void ApplyClick(Vector2 screenPosition)
        {
            var level = designations.Terrain != null ? designations.Terrain.Level : null;
            var projection = designations.Terrain != null ? designations.Terrain.Projection : null;
            if (level == null || projection == null) return;

            if (!projection.TryScreenToPlanet(screenPosition, out var planetPosition)) return;
            if (!level.TryGetBlock(planetPosition, out var block)) return;

            if (markMode) designations.Toggle(block);
            else designations.Unmark(block.Address);
        }

        private int ApplyRectangle()
        {
            var level = designations.Terrain != null ? designations.Terrain.Level : null;
            var projection = designations.Terrain != null ? designations.Terrain.Projection : null;
            if (level == null || projection == null) return 0;

            var rect = Rect.MinMaxRect(
                Mathf.Min(startPosition.x, currentPosition.x), Mathf.Min(startPosition.y, currentPosition.y),
                Mathf.Max(startPosition.x, currentPosition.x), Mathf.Max(startPosition.y, currentPosition.y));

            Vector2[] corners =
            {
                new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax)
            };

            double minRadius = double.MaxValue, maxRadius = double.MinValue;
            double minAngle = double.MaxValue, maxAngle = double.MinValue;
            double referenceAngle = 0;
            for (int i = 0; i < corners.Length; i++)
            {
                if (!projection.TryScreenToPlanet(corners[i], out var p)) return 0;
                if (i == 0) referenceAngle = p.AngleRadians;

                double relative = p.AngleRadians - referenceAngle;
                if (relative > System.Math.PI) relative -= 2 * System.Math.PI;
                else if (relative <= -System.Math.PI) relative += 2 * System.Math.PI;

                minRadius = System.Math.Min(minRadius, p.RadiusMeters);
                maxRadius = System.Math.Max(maxRadius, p.RadiusMeters);
                minAngle = System.Math.Min(minAngle, relative);
                maxAngle = System.Math.Max(maxAngle, relative);
            }

            const double paddingMeters = 1.5;
            double paddingAngle = paddingMeters / System.Math.Max(minRadius, 1.0);
            var region = new PlanetRegion(
                System.Math.Max(0.0, minRadius - paddingMeters),
                maxRadius + paddingMeters,
                referenceAngle + minAngle - paddingAngle,
                (maxAngle - minAngle) + 2 * paddingAngle);

            int count = 0;
            foreach (var block in level.EnumerateBlocks(region, designations.LeafDepth))
            {
                if (!projection.TryPlanetToScreen(DigDesignations.CenterOf(block), out var screen)) continue;
                if (!rect.Contains(screen)) continue;
                if (++count > maxBlocksPerSelection) break;

                if (markMode) designations.Mark(block);
                else designations.Unmark(block.Address);
            }

            return count;
        }

        private void OnGUI()
        {
            if (!dragging) return;

            float x = Mathf.Min(startPosition.x, currentPosition.x);
            float top = Screen.height - Mathf.Max(startPosition.y, currentPosition.y);
            float width = Mathf.Abs(currentPosition.x - startPosition.x);
            float height = Mathf.Abs(currentPosition.y - startPosition.y);
            var rect = new Rect(x, top, width, height);

            Color previous = GUI.color;
            Color fill = markMode ? markRectColor : unmarkRectColor;

            GUI.color = fill;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = new Color(fill.r, fill.g, fill.b, 1f);
            const float line = 2f;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, line), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - line, rect.width, line), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, line, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - line, rect.y, line, rect.height), Texture2D.whiteTexture);

            GUI.color = previous;
        }
    }
}

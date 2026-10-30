using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace MercurioIdle.Construction
{
    public sealed class ProbeHud : MonoBehaviour
    {
        private const float LogSeconds = 5f;

        private GameObject root;
        private Text countLabel;
        private Text rateLabel;
        private Text logLabel;
        private float logTimer;

        private void Start()
        {
            BuildUi();
            ProbeStock.Produced += OnProduced;
        }

        private void OnDestroy()
        {
            ProbeStock.Produced -= OnProduced;
        }

        private void Update()
        {
            if (root == null) return;

            bool visible = ProbeFactory.All.Count > 0 || ProbeStock.Count > 0;
            if (root.activeSelf != visible) root.SetActive(visible);
            if (!visible) return;

            countLabel.text = ProbeStock.Count.ToString();
            rateLabel.text = "+" + ProbeFactory.TotalRatePerSecond.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',') + "/S";

            if (logTimer > 0f)
            {
                logTimer -= Time.deltaTime;
                Color color = TerminalUI.Foreground;
                color.a = Mathf.Clamp01(logTimer);
                logLabel.color = color;
                if (logTimer <= 0f) logLabel.text = string.Empty;
            }
        }

        private void OnProduced(int count)
        {
            logLabel.text = "> SONDA-" + count.ToString("000") + " OPERACIONAL";
            logLabel.color = TerminalUI.Foreground;
            logTimer = LogSeconds;
        }

        private void BuildUi()
        {
            var canvas = TerminalUI.CreateCanvas("Probe UI", 10);

            var border = TerminalUI.CreateBox(canvas.transform, "Sondas", TerminalUI.Faint, TerminalUI.PanelFill, out _);
            var rect = border.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -116f);
            rect.sizeDelta = new Vector2(320f, 96f);
            root = border.gameObject;

            var title = TerminalUI.CreateLabel(rect, "Título", "SONDAS", 13, TerminalUI.Dim, TextAnchor.UpperLeft);
            TerminalUI.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -30f), new Vector2(-12f, -8f));

            rateLabel = TerminalUI.CreateLabel(rect, "Taxa", string.Empty, 13, TerminalUI.Dim, TextAnchor.UpperRight);
            TerminalUI.Anchor(rateLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -30f), new Vector2(-12f, -8f));

            countLabel = TerminalUI.CreateLabel(rect, "Contagem", "0", 30, TerminalUI.Foreground, TextAnchor.MiddleLeft, FontStyle.Bold);
            TerminalUI.Anchor(countLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 26f), new Vector2(-12f, -28f));

            logLabel = TerminalUI.CreateLabel(rect, "Log", string.Empty, 12, TerminalUI.Foreground, TextAnchor.LowerLeft);
            TerminalUI.Anchor(logLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 4f), new Vector2(-12f, 26f));

            root.SetActive(false);
        }
    }
}

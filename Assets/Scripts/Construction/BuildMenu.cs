using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MercurioIdle.Construction
{
    [RequireComponent(typeof(BuildingPlacer))]
    public sealed class BuildMenu : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelMargin = 20f;
        private const float RowHeight = 92f;

        private sealed class Entry
        {
            public BuildingDefinition Definition;
            public TerminalButton Button;
            public Text Info;
        }

        [SerializeField] private bool openAtStart;

        private readonly List<Entry> entries = new List<Entry>();
        private BuildingPlacer placer;
        private GameObject panel;
        private GameObject closedHint;
        private Text status;

        private void Awake()
        {
            placer = GetComponent<BuildingPlacer>();
        }

        private void Start()
        {
            if (GetComponent<ProbeHud>() == null) gameObject.AddComponent<ProbeHud>();

            BuildUi();
            placer.Placed += OnPlaced;
            SetOpen(openAtStart);
        }

        private void OnDestroy()
        {
            if (placer != null) placer.Placed -= OnPlaced;
        }

        private void Update()
        {
            if (panel == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.bKey.wasPressedThisFrame) SetOpen(!panel.activeSelf);

                if (panel.activeSelf)
                {
                    if (keyboard.escapeKey.wasPressedThisFrame && !BuildingPlacer.Active) SetOpen(false);
                    if (keyboard.digit1Key.wasPressedThisFrame && entries.Count > 0) Activate(entries[0]);
                    if (keyboard.digit2Key.wasPressedThisFrame && entries.Count > 1) Activate(entries[1]);
                }
            }

            RefreshEntries();
        }

        private void SetOpen(bool open)
        {
            panel.SetActive(open);
            closedHint.SetActive(!open);
            if (!open) placer.CancelPlacing();
        }

        private void Activate(Entry entry)
        {
            var definition = entry.Definition;

            if (placer.Placing && placer.Current == definition)
            {
                placer.CancelPlacing();
                status.text = "> CONSTRUÇÃO CANCELADA";
                return;
            }

            if (definition.BuiltCount() >= definition.MaxCount) return;

            placer.BeginPlacing(definition);
            status.text = "> CLIQUE NO NÍVEL PARA POSICIONAR";
        }

        private void OnPlaced(BuildingDefinition definition)
        {
            status.text = "> " + definition.Name + " CONSTRUÍDA · " + definition.Description;
        }

        private void RefreshEntries()
        {
            foreach (var entry in entries)
            {
                var definition = entry.Definition;
                int built = definition.BuiltCount();
                bool atMax = built >= definition.MaxCount;
                bool placingThis = placer.Placing && placer.Current == definition;

                entry.Info.text = definition.Description + "  ·  ×" + built + "/" + definition.MaxCount;
                entry.Button.Label.text = placingThis ? "CANCELAR" : atMax ? "CONSTRUÍDA" : "CONSTRUIR";
                entry.Button.Interactable = placingThis || !atMax;
            }
        }

        private void BuildUi()
        {
            var canvas = TerminalUI.CreateCanvas("Build UI", 20);

            var panelBorder = TerminalUI.CreateBox(canvas.transform, "Painel de construção", TerminalUI.Faint, TerminalUI.PanelFill, out _);
            var panelRect = panelBorder.rectTransform;
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, -2f * PanelMargin);
            panelRect.anchoredPosition = new Vector2(-PanelMargin, 0f);
            panel = panelBorder.gameObject;

            var title = TerminalUI.CreateLabel(panelRect, "Título", "CONSTRUÇÕES", 17, TerminalUI.Foreground, TextAnchor.UpperLeft, FontStyle.Bold);
            TerminalUI.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -44f), new Vector2(-16f, -14f));

            var shortcut = TerminalUI.CreateLabel(panelRect, "Atalho", "B", 15, TerminalUI.Dim, TextAnchor.UpperRight);
            TerminalUI.Anchor(shortcut.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -44f), new Vector2(-16f, -14f));

            var definitions = new[] { BuildingDefinition.FactoryBuilding, BuildingDefinition.ProbeFactoryBuilding };
            for (int i = 0; i < definitions.Length; i++) entries.Add(BuildRow(panelRect, definitions[i], i));

            status = TerminalUI.CreateLabel(panelRect, "Status", "> B ABRE E FECHA ESTE PAINEL", 13, TerminalUI.Foreground, TextAnchor.LowerLeft);
            TerminalUI.Anchor(status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 70f), new Vector2(-16f, 110f));

            var hint = TerminalUI.CreateLabel(panelRect, "Dica", "CLIQUE NO NÍVEL PARA POSICIONAR\nESC OU BOTÃO DIREITO CANCELA", 12, TerminalUI.Dim, TextAnchor.LowerLeft);
            TerminalUI.Anchor(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 14f), new Vector2(-16f, 60f));

            var closed = TerminalUI.CreateLabel(canvas.transform, "Dica fechada", "B · CONSTRUIR", 15, TerminalUI.Dim, TextAnchor.LowerRight);
            TerminalUI.Anchor(closed.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-240f, 20f), new Vector2(-20f, 50f));
            closedHint = closed.gameObject;
        }

        private Entry BuildRow(RectTransform parent, BuildingDefinition definition, int index)
        {
            var rowBorder = TerminalUI.CreateBox(parent, definition.Name, TerminalUI.Faint, Color.black, out _);
            var row = rowBorder.rectTransform;
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(-24f, RowHeight);
            row.anchoredPosition = new Vector2(0f, -60f - index * (RowHeight + 8f));

            var codeBorder = TerminalUI.CreateBox(row, "Código", TerminalUI.Foreground, Color.black, out _);
            var codeRect = codeBorder.rectTransform;
            codeRect.anchorMin = new Vector2(0f, 0.5f);
            codeRect.anchorMax = new Vector2(0f, 0.5f);
            codeRect.pivot = new Vector2(0f, 0.5f);
            codeRect.sizeDelta = new Vector2(56f, 56f);
            codeRect.anchoredPosition = new Vector2(12f, 0f);
            TerminalUI.CreateLabel(codeRect, "Texto", definition.Code, 20, TerminalUI.Foreground, TextAnchor.MiddleCenter, FontStyle.Bold);

            var hotkey = TerminalUI.CreateLabel(codeRect, "Atalho", definition.Hotkey, 11, TerminalUI.Dim, TextAnchor.UpperLeft);
            TerminalUI.Anchor(hotkey.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(0f, -2f));

            var nameLabel = TerminalUI.CreateLabel(row, "Nome", definition.Name, 15, TerminalUI.Foreground, TextAnchor.LowerLeft, FontStyle.Bold);
            TerminalUI.Anchor(nameLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(80f, 0f), new Vector2(-136f, -14f));

            var info = TerminalUI.CreateLabel(row, "Info", string.Empty, 13, TerminalUI.Dim, TextAnchor.UpperLeft);
            TerminalUI.Anchor(info.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(80f, 14f), new Vector2(-136f, -2f));

            var button = TerminalUI.CreateButton(row, "Botão", "CONSTRUIR", 13);
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.sizeDelta = new Vector2(112f, 52f);
            buttonRect.anchoredPosition = new Vector2(-12f, 0f);

            var entry = new Entry { Definition = definition, Button = button, Info = info };
            button.Clicked += () => Activate(entry);
            return entry;
        }
    }
}

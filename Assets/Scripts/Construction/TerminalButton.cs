using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MercurioIdle.Construction
{
    public sealed class TerminalButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Image border;
        private Image fill;
        private Text label;
        private bool hovered;
        private bool interactable = true;

        public event Action Clicked;

        public Text Label => label;

        public bool Interactable
        {
            get => interactable;
            set
            {
                if (interactable == value) return;
                interactable = value;
                Refresh();
            }
        }

        public void Setup(Image borderImage, Image fillImage, Text labelText)
        {
            border = borderImage;
            fill = fillImage;
            label = labelText;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            Refresh();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (interactable) Clicked?.Invoke();
        }

        private void OnDisable()
        {
            hovered = false;
            Refresh();
        }

        private void Refresh()
        {
            if (border == null || fill == null || label == null) return;

            if (!interactable)
            {
                border.color = TerminalUI.Faint;
                fill.color = Color.black;
                label.color = TerminalUI.Dim;
            }
            else if (hovered)
            {
                border.color = TerminalUI.Foreground;
                fill.color = TerminalUI.Foreground;
                label.color = Color.black;
            }
            else
            {
                border.color = TerminalUI.Foreground;
                fill.color = Color.black;
                label.color = TerminalUI.Foreground;
            }
        }
    }
}

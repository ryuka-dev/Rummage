using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ryuka.Sulfur.Rummage.UserInterface
{
    /// <summary>Reports the pointer entering and leaving a UI element, for showing a tooltip.</summary>
    internal sealed class HoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<bool> Hover;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hover?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hover?.Invoke(false);
        }

        private void OnDisable()
        {
            Hover?.Invoke(false);
        }
    }
}

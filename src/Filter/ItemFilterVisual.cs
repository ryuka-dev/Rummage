using PerfectRandom.Sulfur.Core.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Ryuka.Sulfur.Rummage.Filter
{
    internal enum FilterMark
    {
        Neutral,
        Match,
        Dimmed,
    }

    /// <summary>
    /// Rummage's own marking on one inventory item: faded when it does not match, framed when it
    /// does. It sits on the InventoryItem rather than on the grid slot visuals, which the game pools
    /// and hands to other slots, and it does not use the slot highlight, which the game clears on
    /// hover exit and drag end.
    /// </summary>
    internal sealed class ItemFilterVisual : MonoBehaviour
    {
        private const float DimmedAlpha = 0.22f;
        private const float FrameThickness = 2f;
        private static readonly Color FrameColor = new Color(0.9f, 0.63f, 0.18f, 1f);

        private CanvasGroup group;
        private RectTransform frame;

        public InventoryItem Item { get; private set; }

        public FilterMark Mark { get; private set; }

        public static ItemFilterVisual Attach(InventoryItem item)
        {
            ItemFilterVisual visual = item.GetComponent<ItemFilterVisual>();
            if (visual == null)
            {
                visual = item.gameObject.AddComponent<ItemFilterVisual>();
            }

            visual.Item = item;
            return visual;
        }

        public void Show(FilterMark mark)
        {
            Mark = mark;

            if (mark == FilterMark.Dimmed && group == null)
            {
                group = GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (group != null)
            {
                group.alpha = mark == FilterMark.Dimmed ? DimmedAlpha : 1f;
            }

            if (mark == FilterMark.Match)
            {
                EnsureFrame();
                frame.gameObject.SetActive(true);
                frame.SetAsLastSibling();
            }
            else if (frame != null)
            {
                frame.gameObject.SetActive(false);
            }
        }

        private void EnsureFrame()
        {
            if (frame != null)
            {
                return;
            }

            GameObject root = new GameObject("RummageMatchFrame", typeof(RectTransform));
            frame = root.GetComponent<RectTransform>();
            frame.SetParent(transform, false);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;

            // The item's own rect covers exactly the cells it occupies, rotated or not.
            Edge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, FrameThickness));
            Edge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, FrameThickness));
            Edge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(FrameThickness, 0f));
            Edge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(FrameThickness, 0f));
        }

        private void Edge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            GameObject edge = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.SetParent(frame, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            Image image = edge.GetComponent<Image>();
            image.color = FrameColor;
            image.raycastTarget = false;
        }
    }
}

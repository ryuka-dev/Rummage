using System.Collections.Generic;
using System.Text;
using Ryuka.Sulfur.Rummage.Config;
using Ryuka.Sulfur.Rummage.Game;
using Ryuka.Sulfur.Rummage.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ryuka.Sulfur.Rummage.UserInterface
{
    /// <summary>
    /// Where the matches are, shown next to the search bar while a filter is active.
    ///
    /// By default one line of counts per place, with a square button at its end. Expanded, a
    /// scrollable panel lists which items each place holds. Below the search bar (over the backpack)
    /// the panel grows with its list down to the bottom of the screen; above the search bar it is
    /// capped shorter and never grows past the top of the screen. It is never taller than its list.
    /// </summary>
    internal sealed class SearchResultsPanel
    {
        private const float Gap = 4f;
        private const float ToggleSize = 24f;
        private const float SummaryMinimumHeight = 30f;
        private const float SummaryMaximumHeight = 64f;
        private const float SummaryFontSize = 14f;
        private const float DetailFontSize = 14f;
        private const float DetailPadding = 6f;
        private const float DetailMinimumHeight = 40f;
        private const float AboveDetailMaximumHeight = 320f;
        private const float ScreenMargin = 8f;
        private const string Separator = "  ·  ";

        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>();

        private GameObject root;
        private RectTransform rootRect;
        private RectTransform summaryRow;
        private TextMeshProUGUI summaryText;
        private RectTransform toggleRect;
        private TextMeshProUGUI toggleLabel;
        private RectTransform detailPanel;
        private RectTransform detailContent;
        private TextMeshProUGUI detailText;
        private ScrollRect detailScroll;

        private List<LocationCount> counts;
        private bool expanded;
        private bool shown;
        private bool fontReady = true;
        private bool placed;
        private float left;
        private float barBottom;
        private float barTop;
        private float width;
        private ResultsPosition position;

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public void Build(RectTransform parent, TMP_FontAsset font)
        {
            texts.Clear();
            placed = false;
            fontReady = true;

            root = new GameObject("Rummage_SearchResults", typeof(RectTransform), typeof(LayoutElement));
            root.GetComponent<LayoutElement>().ignoreLayout = true;
            rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = Vector2.zero;

            BuildSummaryRow(font);
            BuildDetailPanel(font);

            Render();
            UpdateVisibility();
        }

        /// <summary>The search bar's left edge, bottom, top and width, in the parent's local space.</summary>
        public void Place(float barLeft, float barBottomY, float barTopY, float barWidth, ResultsPosition resultsPosition)
        {
            left = barLeft;
            barBottom = barBottomY;
            barTop = barTopY;
            width = barWidth;
            position = resultsPosition;
            placed = true;

            if (root != null)
            {
                rootRect.SetAsLastSibling();
                Layout();
            }
        }

        /// <summary>Null while no filter is active, which hides the panel.</summary>
        public void SetCounts(List<LocationCount> value)
        {
            counts = value;
            Render();
            UpdateVisibility();
        }

        public void SetShown(bool value)
        {
            shown = value;
            UpdateVisibility();
        }

        public void RefreshTexts()
        {
            Render();
        }

        /// <summary>
        /// The language changed: the texts may still carry a font whose material is released, so
        /// nothing is measured until <see cref="ApplyFont"/> brings the new one.
        /// </summary>
        public void SetFontStale()
        {
            fontReady = false;
        }

        public void ApplyFont(TMP_FontAsset font)
        {
            if (font != null)
            {
                foreach (TextMeshProUGUI text in texts)
                {
                    if (text != null)
                    {
                        text.font = font;
                    }
                }
            }

            fontReady = true;
            Layout();
        }

        public void Destroy()
        {
            if (root != null)
            {
                Object.Destroy(root);
            }

            root = null;
        }

        private void BuildSummaryRow(TMP_FontAsset font)
        {
            GameObject row = new GameObject("Summary", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            summaryRow = row.GetComponent<RectTransform>();
            summaryRow.SetParent(rootRect, false);
            Image background = row.GetComponent<Image>();
            background.color = UiTheme.Field;

            // Clicks pass through the line to the backpack under it; only its button takes them.
            background.raycastTarget = false;

            // Overflow, and a height measured from the text: TMP's Ellipsis mode blanks a line that
            // is taller than its rect, which CJK lines easily are.
            summaryText = UiParts.CreateText(summaryRow, "Text", font, SummaryFontSize, UiTheme.Text, TextAlignmentOptions.MidlineLeft, texts, TextOverflowModes.Overflow);
            summaryText.textWrappingMode = TextWrappingModes.Normal;
            summaryText.rectTransform.offsetMin = new Vector2(8f, 2f);
            summaryText.rectTransform.offsetMax = new Vector2(-(ToggleSize + 12f), -2f);

            Button toggle = UiParts.CreateButton(summaryRow, "Expand", font, texts, out toggleLabel);
            toggleRect = toggle.GetComponent<RectTransform>();
            toggleLabel.enableAutoSizing = false;
            toggleLabel.fontSize = 18f;
            toggleLabel.overflowMode = TextOverflowModes.Overflow;
            toggleLabel.color = UiTheme.Text;
            toggle.onClick.AddListener(OnToggleClicked);
        }

        private void BuildDetailPanel(TMP_FontAsset font)
        {
            GameObject panel = new GameObject("Details", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            detailPanel = panel.GetComponent<RectTransform>();
            detailPanel.SetParent(rootRect, false);
            panel.GetComponent<Image>().color = UiTheme.Panel;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.SetParent(detailPanel, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(DetailPadding, DetailPadding);
            viewportRect.offsetMax = new Vector2(-DetailPadding, -DetailPadding);

            GameObject content = new GameObject("Content", typeof(RectTransform));
            detailContent = content.GetComponent<RectTransform>();
            detailContent.SetParent(viewportRect, false);
            detailContent.anchorMin = new Vector2(0f, 1f);
            detailContent.anchorMax = new Vector2(1f, 1f);
            detailContent.pivot = new Vector2(0.5f, 1f);
            detailContent.sizeDelta = Vector2.zero;
            detailContent.anchoredPosition = Vector2.zero;

            detailText = UiParts.CreateText(detailContent, "Text", font, DetailFontSize, UiTheme.TextMuted, TextAlignmentOptions.TopLeft, texts, TextOverflowModes.Overflow);
            detailText.textWrappingMode = TextWrappingModes.Normal;
            detailText.richText = true;

            detailScroll = panel.AddComponent<ScrollRect>();
            detailScroll.viewport = viewportRect;
            detailScroll.content = detailContent;
            detailScroll.horizontal = false;
            detailScroll.vertical = true;
            detailScroll.movementType = ScrollRect.MovementType.Clamped;
            detailScroll.inertia = false;
            detailScroll.scrollSensitivity = 28f;

            panel.SetActive(false);
        }

        private void OnToggleClicked()
        {
            expanded = !expanded;
            Render();
            UiParts.ClearSelection();
        }

        private void Render()
        {
            if (root == null)
            {
                return;
            }

            bool hasMatches = counts != null && counts.Count > 0;
            summaryText.text = counts == null ? string.Empty : hasMatches ? SummaryLine(counts) : RummageText.Get("summary.none", "No matches");
            toggleRect.gameObject.SetActive(hasMatches);
            toggleLabel.text = expanded ? "-" : "+";
            detailText.text = hasMatches && expanded ? DetailLines(counts) : string.Empty;

            Layout();
        }

        private void Layout()
        {
            if (root == null || !placed || !fontReady)
            {
                return;
            }

            bool below = position == ResultsPosition.BelowSearchBar;

            float summaryTextWidth = Mathf.Max(40f, width - ToggleSize - 20f);
            float summaryHeight = Mathf.Clamp(PreferredHeight(summaryText, summaryTextWidth) + 8f, SummaryMinimumHeight, SummaryMaximumHeight);

            bool showDetail = expanded && counts != null && counts.Count > 0;
            if (detailPanel.gameObject.activeSelf != showDetail)
            {
                detailPanel.gameObject.SetActive(showDetail);
            }

            float detailHeight = 0f;
            if (showDetail)
            {
                float contentWidth = Mathf.Max(40f, width - DetailPadding * 2f);
                float contentHeight = PreferredHeight(detailText, contentWidth);
                detailContent.sizeDelta = new Vector2(0f, contentHeight);

                // Never taller than the list itself; beyond that, bounded by the screen edge it grows
                // towards, and above the bar also by a fixed cap.
                float room = below
                    ? RoomBelow(barBottom - Gap - summaryHeight - Gap)
                    : Mathf.Min(AboveDetailMaximumHeight, RoomAbove(barTop + Gap + summaryHeight + Gap));
                float maximum = Mathf.Max(DetailMinimumHeight, room);
                detailHeight = Mathf.Clamp(contentHeight + DetailPadding * 2f, DetailMinimumHeight, maximum);
            }

            float total = summaryHeight + (showDetail ? Gap + detailHeight : 0f);

            if (below)
            {
                // Hangs down from the bar: the counts line first, the list under it.
                UiParts.SetBottomLeft(summaryRow, 0f, showDetail ? detailHeight + Gap : 0f, width, summaryHeight);
                if (showDetail)
                {
                    UiParts.SetBottomLeft(detailPanel, 0f, 0f, width, detailHeight);
                }

                rootRect.localPosition = new Vector3(left, barBottom - Gap - total, 0f);
            }
            else
            {
                // Rises from the bar: the counts line first, the list above it.
                UiParts.SetBottomLeft(summaryRow, 0f, 0f, width, summaryHeight);
                if (showDetail)
                {
                    UiParts.SetBottomLeft(detailPanel, 0f, summaryHeight + Gap, width, detailHeight);
                }

                rootRect.localPosition = new Vector3(left, barTop + Gap, 0f);
            }

            UiParts.SetBottomLeft(toggleRect, width - ToggleSize - 4f, (summaryHeight - ToggleSize) * 0.5f, ToggleSize, ToggleSize);
            rootRect.sizeDelta = new Vector2(width, total);
        }

        private void UpdateVisibility()
        {
            if (root == null)
            {
                return;
            }

            bool visible = shown && counts != null;
            if (root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }

        private static string SummaryLine(List<LocationCount> locations)
        {
            StringBuilder text = new StringBuilder();
            foreach (LocationCount location in locations)
            {
                if (text.Length > 0)
                {
                    text.Append(Separator);
                }

                text.Append(location.Label).Append(" ×").Append(location.Quantity);
            }

            return text.ToString();
        }

        private static string DetailLines(List<LocationCount> locations)
        {
            StringBuilder text = new StringBuilder();
            foreach (LocationCount location in locations)
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append("<color=").Append(UiTheme.TextHex).Append('>')
                    .Append(NoParse(location.Label)).Append(" ×").Append(location.Quantity)
                    .Append("</color>\n");

                for (int i = 0; i < location.Items.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(Separator);
                    }

                    text.Append(NoParse(location.Items[i].Name)).Append(" ×").Append(location.Items[i].Quantity);
                }
            }

            return text.ToString();
        }

        /// <summary>Keeps a name that happens to contain '&lt;' from being read as a rich text tag.</summary>
        private static string NoParse(string value)
        {
            return string.IsNullOrEmpty(value) || value.IndexOf('<') < 0 ? value : "<noparse>" + value + "</noparse>";
        }

        private static float PreferredHeight(TextMeshProUGUI text, float width)
        {
            return string.IsNullOrEmpty(text.text) ? 0f : text.GetPreferredValues(text.text, width, 0f).y;
        }

        /// <summary>Height left between a point in the parent and the top of the screen.</summary>
        private float RoomAbove(float localY)
        {
            float edge;
            return TryScreenEdge(1, out edge) ? edge - localY - ScreenMargin : AboveDetailMaximumHeight;
        }

        /// <summary>Height left between a point in the parent and the bottom of the screen.</summary>
        private float RoomBelow(float localY)
        {
            float edge;
            return TryScreenEdge(0, out edge) ? localY - edge - ScreenMargin : AboveDetailMaximumHeight;
        }

        /// <summary>The y of the root canvas's bottom-left (corner 0) or top-left (corner 1) in the parent's space.</summary>
        private bool TryScreenEdge(int corner, out float y)
        {
            y = 0f;
            Canvas canvas = rootRect.GetComponentInParent<Canvas>();
            RectTransform parent = rootRect.parent as RectTransform;
            RectTransform canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect == null || parent == null)
            {
                return false;
            }

            canvasRect.GetWorldCorners(Corners);
            y = parent.InverseTransformPoint(Corners[corner]).y;
            return true;
        }
    }
}

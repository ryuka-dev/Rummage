using System.Collections.Generic;
using System.Text;
using Ryuka.Sulfur.Rummage.Game;
using Ryuka.Sulfur.Rummage.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ryuka.Sulfur.Rummage.UserInterface
{
    /// <summary>
    /// Where the matches are, shown just above the search bar while a filter is active.
    ///
    /// By default one line of counts per place, with a square button at its end. Expanded, a
    /// scrollable panel above that line lists which items each place holds. The panel never grows
    /// past the top of the screen.
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
        private const float DetailMaximumHeight = 320f;
        private const float ScreenTopMargin = 8f;
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
        private bool placed;
        private Vector2 bottomLeft;
        private float width;

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public void Build(RectTransform parent, TMP_FontAsset font)
        {
            texts.Clear();
            placed = false;

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

        /// <summary>Anchors the panel's bottom-left corner in the parent's local space.</summary>
        public void Place(Vector2 position, float barWidth)
        {
            bottomLeft = position;
            width = barWidth;
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

        public void ApplyFont(TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }

            foreach (TextMeshProUGUI text in texts)
            {
                if (text != null)
                {
                    text.font = font;
                }
            }

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
            if (root == null || !placed)
            {
                return;
            }

            float summaryTextWidth = Mathf.Max(40f, width - ToggleSize - 20f);
            float summaryHeight = Mathf.Clamp(PreferredHeight(summaryText, summaryTextWidth) + 8f, SummaryMinimumHeight, SummaryMaximumHeight);

            UiParts.SetBottomLeft(summaryRow, 0f, 0f, width, summaryHeight);
            UiParts.SetBottomLeft(toggleRect, width - ToggleSize - 4f, (summaryHeight - ToggleSize) * 0.5f, ToggleSize, ToggleSize);

            bool showDetail = expanded && counts != null && counts.Count > 0;
            float total = summaryHeight;

            if (detailPanel.gameObject.activeSelf != showDetail)
            {
                detailPanel.gameObject.SetActive(showDetail);
            }

            if (showDetail)
            {
                float contentWidth = Mathf.Max(40f, width - DetailPadding * 2f);
                float contentHeight = PreferredHeight(detailText, contentWidth);
                detailContent.sizeDelta = new Vector2(0f, contentHeight);

                float room = RoomAbove(bottomLeft.y + summaryHeight + Gap);
                float maximum = Mathf.Max(DetailMinimumHeight, Mathf.Min(DetailMaximumHeight, room));
                float detailHeight = Mathf.Clamp(contentHeight + DetailPadding * 2f, DetailMinimumHeight, maximum);

                UiParts.SetBottomLeft(detailPanel, 0f, summaryHeight + Gap, width, detailHeight);
                total += Gap + detailHeight;
            }

            rootRect.sizeDelta = new Vector2(width, total);
            rootRect.localPosition = new Vector3(bottomLeft.x, bottomLeft.y, 0f);
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

        /// <summary>How much height is left between a point in the parent and the top of the screen.</summary>
        private float RoomAbove(float localY)
        {
            Canvas canvas = rootRect.GetComponentInParent<Canvas>();
            RectTransform parent = rootRect.parent as RectTransform;
            if (canvas == null || parent == null)
            {
                return DetailMaximumHeight;
            }

            RectTransform canvasRect = canvas.rootCanvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return DetailMaximumHeight;
            }

            canvasRect.GetWorldCorners(Corners);
            float top = parent.InverseTransformPoint(Corners[1]).y;
            return top - localY - ScreenTopMargin;
        }
    }
}

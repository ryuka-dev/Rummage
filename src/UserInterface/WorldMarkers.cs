using System;
using System.Collections.Generic;
using PerfectRandom.Sulfur.Core;
using Ryuka.Sulfur.Rummage.Diagnostics;
using Ryuka.Sulfur.Rummage.Game;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Ryuka.Sulfur.Rummage.UserInterface
{
    /// <summary>
    /// Floating labels in the world above the storages that hold matches, such as "Wardrobe ×5".
    ///
    /// One label per storage identifier. When several objects share an identifier (the main stash
    /// stands in six places), only the one nearest the player is labelled, re-chosen every frame.
    /// The labels face the camera, keep a readable size with distance and draw over walls.
    /// </summary>
    internal sealed class WorldMarkers : IDisposable
    {
        private const float HeightAboveAnchor = 0.9f;
        private const float WorldUnitsPerPixel = 0.004f;
        private const float ReferenceDistance = 5f;
        private const float MaximumDistanceScale = 3f;
        private const float FontSize = 36f;
        private const float PaddingX = 18f;
        private const float PaddingY = 8f;
        private const int SortingOrder = 500;

        private readonly StorageIndex index;
        private readonly Dictionary<string, Marker> markers = new Dictionary<string, Marker>(StringComparer.Ordinal);
        private readonly HashSet<string> shownThisFrame = new HashSet<string>(StringComparer.Ordinal);

        private GameObject root;
        private List<LocationCount> counts;
        private TMP_FontAsset font;
        private bool fontStale = true;
        private bool failureReported;

        private sealed class Marker
        {
            public GameObject Root;
            public RectTransform Rect;
            public RectTransform Background;
            public TextMeshProUGUI Text;
            public string Shown;
        }

        public WorldMarkers(DiagnosticsConfig diagnostics)
        {
            index = new StorageIndex(diagnostics);
        }

        /// <summary>Null hides every marker.</summary>
        public void SetCounts(List<LocationCount> value)
        {
            counts = value;
        }

        /// <summary>A new level: the storage objects are different ones.</summary>
        public void OnPlayerSpawned()
        {
            index.Invalidate();
        }

        public void OnLanguageChanged()
        {
            fontStale = true;
        }

        public void LateTick()
        {
            try
            {
                Update();
            }
            catch (Exception ex)
            {
                if (!failureReported)
                {
                    failureReported = true;
                    RummagePlugin.Log.LogError("Updating world markers failed; further failures are not logged. " + ex);
                }

                HideAll();
            }
        }

        public void Dispose()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }

            root = null;
            markers.Clear();
        }

        private void Update()
        {
            if (counts == null || counts.Count == 0)
            {
                HideAll();
                return;
            }

            GameManager gameManager = StaticInstance<GameManager>.Instance;
            GameObject player = gameManager != null ? gameManager.PlayerObject : null;
            Camera camera = PlayerCamera(gameManager);
            if (player == null || camera == null)
            {
                HideAll();
                return;
            }

            if (fontStale)
            {
                TMP_FontAsset current = GameFonts.Current();
                if (current != null)
                {
                    font = current;
                    fontStale = false;
                    foreach (Marker marker in markers.Values)
                    {
                        ApplyFont(marker);
                    }
                }
            }

            Vector3 playerPosition = player.transform.position;
            Vector3 cameraPosition = camera.transform.position;
            shownThisFrame.Clear();

            foreach (LocationCount count in counts)
            {
                if (count.Identifier == null)
                {
                    continue;
                }

                StorageIndex.Entry target = index.Nearest(count.Identifier, playerPosition);
                if (target == null)
                {
                    continue;
                }

                Marker marker = GetOrCreate(count.Identifier);
                Place(marker, target, count, cameraPosition);
                shownThisFrame.Add(count.Identifier);
            }

            foreach (KeyValuePair<string, Marker> pair in markers)
            {
                if (!shownThisFrame.Contains(pair.Key) && pair.Value.Root.activeSelf)
                {
                    pair.Value.Root.SetActive(false);
                }
            }
        }

        private static Camera PlayerCamera(GameManager gameManager)
        {
            if (gameManager != null && gameManager.PlayerScript != null && gameManager.PlayerScript.playerCamera != null)
            {
                return gameManager.PlayerScript.playerCamera;
            }

            return Camera.main;
        }

        private void Place(Marker marker, StorageIndex.Entry target, LocationCount count, Vector3 cameraPosition)
        {
            if (!marker.Root.activeSelf)
            {
                marker.Root.SetActive(true);
            }

            string text = count.Label + " ×" + count.Quantity;
            if (marker.Shown != text)
            {
                marker.Shown = text;
                marker.Text.text = text;
                Vector2 size = marker.Text.GetPreferredValues(text);
                marker.Rect.sizeDelta = new Vector2(size.x + PaddingX * 2f, size.y + PaddingY * 2f);
            }

            Transform transform = marker.Root.transform;
            transform.position = target.Anchor.position + Vector3.up * HeightAboveAnchor;

            // Face the camera: a world-space canvas is read from its back side otherwise.
            Vector3 away = transform.position - cameraPosition;
            if (away.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }

            float distanceScale = Mathf.Clamp(away.magnitude / ReferenceDistance, 1f, MaximumDistanceScale);
            transform.localScale = Vector3.one * (WorldUnitsPerPixel * distanceScale);

            if (marker.Root.layer != target.Owner.layer)
            {
                SetLayer(marker.Root, target.Owner.layer);
            }
        }

        private Marker GetOrCreate(string identifier)
        {
            Marker marker;
            if (markers.TryGetValue(identifier, out marker) && marker.Root != null)
            {
                return marker;
            }

            if (root == null)
            {
                root = new GameObject("Rummage_WorldMarkers");
            }

            GameObject go = new GameObject("Marker_" + identifier, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(root.transform, false);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = SortingOrder;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0f);

            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.SetParent(rect, false);
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            Image image = background.GetComponent<Image>();
            image.color = UiTheme.Panel;
            image.raycastTarget = false;
            image.material = OnTop(new Material(Canvas.GetDefaultCanvasMaterial()));

            List<TextMeshProUGUI> unused = new List<TextMeshProUGUI>();
            TextMeshProUGUI text = UiParts.CreateText(rect, "Text", null, FontSize, UiTheme.Text, TextAlignmentOptions.Center, unused, TextOverflowModes.Overflow);

            marker = new Marker { Root = go, Rect = rect, Background = backgroundRect, Text = text };
            ApplyFont(marker);
            markers[identifier] = marker;
            return marker;
        }

        private void ApplyFont(Marker marker)
        {
            if (font != null)
            {
                marker.Text.font = font;
            }

            // The instance material follows the font; set it to draw over the scene again.
            OnTop(marker.Text.fontMaterial);
            marker.Shown = null;
        }

        /// <summary>
        /// UI shaders read their depth test from unity_GUIZTestMode, which a world-space canvas leaves
        /// at LessEqual. Always makes the label visible through walls.
        /// </summary>
        private static Material OnTop(Material material)
        {
            if (material != null)
            {
                material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            }

            return material;
        }

        private void HideAll()
        {
            foreach (Marker marker in markers.Values)
            {
                if (marker.Root != null && marker.Root.activeSelf)
                {
                    marker.Root.SetActive(false);
                }
            }
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayer(child.gameObject, layer);
            }
        }
    }
}

using System;
using PerfectRandom.Sulfur.Core;
using UnityEngine;

namespace Ryuka.Sulfur.Rummage.Localization
{
    /// <summary>
    /// Keeps Rummage's text in the game's language and tells listeners when it changes.
    ///
    /// Follows the game's own onLanguageChange event. That event only fires on an actual change,
    /// and localisation loads asynchronously, so until the game first reports a language the load is
    /// retried once a second; after that only the event triggers reloads.
    /// </summary>
    internal sealed class LanguageWatcher : IDisposable
    {
        private const float RetrySeconds = 1f;

        private readonly string pluginDirectory;
        private readonly AsyncAssetLoading.OnLanguageChange handler;
        private AsyncAssetLoading subscribedTo;
        private bool gameLanguageKnown;
        private float nextAttempt;

        public LanguageWatcher(string pluginDirectory)
        {
            this.pluginDirectory = pluginDirectory;
            handler = OnGameLanguageChanged;
        }

        public event Action LanguageChanged;

        public void Tick()
        {
            AsyncAssetLoading current = StaticInstance<AsyncAssetLoading>.Instance;
            if (!ReferenceEquals(current, subscribedTo))
            {
                Unsubscribe();
                if (current != null)
                {
                    current.onLanguageChange += handler;
                    subscribedTo = current;
                }
            }

            if (!gameLanguageKnown && Time.unscaledTime >= nextAttempt)
            {
                nextAttempt = Time.unscaledTime + RetrySeconds;
                Load(false);
            }
        }

        public void Dispose()
        {
            Unsubscribe();
            LanguageChanged = null;
        }

        private void OnGameLanguageChanged()
        {
            Load(true);
        }

        private void Load(bool fromGameEvent)
        {
            bool reloaded = RummageText.Reload(pluginDirectory);
            gameLanguageKnown = RummageText.GameCode != null;

            if (reloaded)
            {
                string line = "Language: " + RummageText.LoadedCode
                    + " (game language code: " + (RummageText.GameCode ?? "<not loaded yet>") + ") - "
                    + RummageText.LoadedCount + " strings from " + (RummageText.LoadedFrom ?? "<nowhere>");

                if (RummageText.LoadedCount == 0 || RummageText.LastLoadNote != null)
                {
                    RummagePlugin.Log.LogWarning(line + " | " + (RummageText.LastLoadNote ?? "no strings"));
                }
                else
                {
                    RummagePlugin.Log.LogInfo(line);
                }
            }

            // The game's event also means its own names changed, even when Rummage's file did not.
            if (reloaded || fromGameEvent)
            {
                Raise();
            }
        }

        private void Raise()
        {
            Action listeners = LanguageChanged;
            if (listeners == null)
            {
                return;
            }

            foreach (Delegate listener in listeners.GetInvocationList())
            {
                try
                {
                    ((Action)listener)();
                }
                catch (Exception ex)
                {
                    RummagePlugin.Log.LogError("Language change listener failed: " + ex);
                }
            }
        }

        private void Unsubscribe()
        {
            if (!ReferenceEquals(subscribedTo, null))
            {
                subscribedTo.onLanguageChange -= handler;
                subscribedTo = null;
            }
        }
    }
}

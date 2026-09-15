using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Ryuka.Sulfur.Rummage.Localization
{
    /// <summary>
    /// Rummage's own player-visible strings, read from the same lang/&lt;code&gt;.json files the
    /// config UI reads, so all of a language's text lives in one file. Every lookup carries the
    /// English text as its fallback.
    /// </summary>
    internal static class RummageText
    {
        // DataContractJsonSerializer rather than JsonUtility: JsonUtility cannot read this shape.
#pragma warning disable 0649
        [DataContract]
        private sealed class LangEntry
        {
            [DataMember(Name = "key")]
            public string Key;

            [DataMember(Name = "value")]
            public string Value;
        }

        [DataContract]
        private sealed class LangFile
        {
            [DataMember(Name = "entries")]
            public LangEntry[] Entries;
        }
#pragma warning restore 0649

        private static readonly Dictionary<string, string> Strings = new Dictionary<string, string>(StringComparer.Ordinal);

        public static string LoadedCode { get; private set; }

        /// <summary>The code the game reported, or null while it has not reported one.</summary>
        public static string GameCode { get; private set; }

        public static int LoadedCount
        {
            get { return Strings.Count; }
        }

        /// <summary>Why the last load fell short. Diagnostics only.</summary>
        public static string LastLoadNote { get; private set; }

        public static string LoadedFrom { get; private set; }

        /// <summary>
        /// Loads the table for the game's current language. A no-op when neither the game's code nor
        /// the resolved file changed; returns true when the table was reloaded.
        /// </summary>
        public static bool Reload(string pluginDirectory)
        {
            string gameCode = GameLanguage.CurrentCode();
            string resolvedCode = GameLanguage.ResolveCode(gameCode);

            if (LoadedCode == resolvedCode && GameCode == gameCode)
            {
                return false;
            }

            Strings.Clear();
            LastLoadNote = null;
            LoadedFrom = null;

            // English first, so an incomplete translation falls through to it per key.
            if (resolvedCode != GameLanguage.FallbackCode)
            {
                LoadFirstMatch(pluginDirectory, GameLanguage.FallbackCode);
            }

            LoadFirstMatch(pluginDirectory, resolvedCode);

            LoadedCode = resolvedCode;
            GameCode = gameCode;
            return true;
        }

        public static string Get(string key, string englishFallback)
        {
            string value;
            return Strings.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : englishFallback;
        }

        /// <summary>
        /// Packaged layout (lang/ beside the DLL), a flattened install (files beside the DLL), and a
        /// manager that put lang/ next to the plugin folder.
        /// </summary>
        private static IEnumerable<string> CandidateDirectories(string pluginDirectory)
        {
            if (string.IsNullOrEmpty(pluginDirectory))
            {
                yield break;
            }

            yield return Path.Combine(pluginDirectory, "lang");
            yield return pluginDirectory;

            string parent = null;
            try
            {
                DirectoryInfo info = Directory.GetParent(pluginDirectory);
                parent = info != null ? info.FullName : null;
            }
            catch (Exception)
            {
                parent = null;
            }

            if (!string.IsNullOrEmpty(parent))
            {
                yield return Path.Combine(parent, "lang");
            }
        }

        /// <summary>The first directory that actually yields entries wins, not the first that exists.</summary>
        private static void LoadFirstMatch(string pluginDirectory, string code)
        {
            foreach (string directory in CandidateDirectories(pluginDirectory))
            {
                if (LoadInto(directory, code))
                {
                    LoadedFrom = directory;
                    return;
                }
            }

            LastLoadNote = LastLoadNote ?? "no readable " + code + ".json under " + (pluginDirectory ?? "<unknown>");
        }

        private static bool LoadInto(string directory, string code)
        {
            string path = Path.Combine(directory, code + ".json");
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                LangFile parsed;
                using (MemoryStream stream = new MemoryStream(File.ReadAllBytes(path)))
                {
                    parsed = new DataContractJsonSerializer(typeof(LangFile)).ReadObject(stream) as LangFile;
                }

                if (parsed == null || parsed.Entries == null)
                {
                    LastLoadNote = "no entries in " + path;
                    return false;
                }

                bool rightToLeft = GameLanguage.IsRightToLeft();
                int loaded = 0;
                foreach (LangEntry entry in parsed.Entries)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.Key) && entry.Value != null)
                    {
                        Strings[entry.Key] = rightToLeft ? GameLanguage.FixRightToLeft(entry.Value) : entry.Value;
                        loaded++;
                    }
                }

                return loaded > 0;
            }
            catch (Exception ex)
            {
                LastLoadNote = ex.GetType().Name + " reading " + path;
                return false;
            }
        }
    }
}

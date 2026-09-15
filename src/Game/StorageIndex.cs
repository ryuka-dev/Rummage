using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.World;
using Ryuka.Sulfur.Rummage.Diagnostics;
using UnityEngine;

namespace Ryuka.Sulfur.Rummage.Game
{
    /// <summary>
    /// The storage objects in the current scene and where a marker above each belongs.
    ///
    /// Covers every PlayerStash and every station with a frame identifier (item frames and wall
    /// mounts). Inactive objects are indexed too, because a storage can be switched on later; only
    /// active ones are offered as marker targets. The index is rebuilt when a level spawns the player
    /// and whenever an indexed object has been destroyed.
    /// </summary>
    internal sealed class StorageIndex
    {
        internal sealed class Entry
        {
            public string Identifier;
            public GameObject Owner;
            public Transform Anchor;
        }

        private const string FrameIdentifierField = "frameIdentifier";

        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<Type, FieldInfo> identifierFields = new Dictionary<Type, FieldInfo>();
        private readonly DiagnosticsConfig diagnostics;
        private bool built;

        public StorageIndex(DiagnosticsConfig diagnostics)
        {
            this.diagnostics = diagnostics;
        }

        public void Invalidate()
        {
            built = false;
        }

        /// <summary>The nearest active storage object for an identifier, or null when none is in the scene.</summary>
        public Entry Nearest(string identifier, Vector3 position)
        {
            EnsureBuilt();

            Entry best = null;
            float bestDistance = float.MaxValue;

            foreach (Entry entry in entries)
            {
                if (entry.Owner == null || !entry.Owner.activeInHierarchy || entry.Anchor == null
                    || !string.Equals(entry.Identifier, identifier, StringComparison.Ordinal))
                {
                    continue;
                }

                float distance = (entry.Anchor.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = entry;
                }
            }

            return best;
        }

        private void EnsureBuilt()
        {
            if (built)
            {
                bool destroyed = false;
                foreach (Entry entry in entries)
                {
                    if (entry.Owner == null)
                    {
                        destroyed = true;
                        break;
                    }
                }

                if (!destroyed)
                {
                    return;
                }
            }

            Build();
        }

        private void Build()
        {
            entries.Clear();
            built = true;

            foreach (ServiceStation station in UnityEngine.Object.FindObjectsByType<ServiceStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string identifier = Identify(station);
                if (identifier == null)
                {
                    continue;
                }

                Interactable interactable = station.GetComponent<Interactable>();
                if (interactable == null)
                {
                    interactable = station.GetComponentInParent<Interactable>(true);
                }

                // The same point the game's own interaction indicator rises from.
                Transform anchor = interactable != null
                    ? (interactable.abovePoint != null ? interactable.abovePoint : interactable.transform)
                    : station.transform;

                entries.Add(new Entry { Identifier = identifier, Owner = station.gameObject, Anchor = anchor });
            }

            if (diagnostics.LogSearchFilter.Value)
            {
                StringBuilder summary = new StringBuilder();
                foreach (Entry entry in entries)
                {
                    if (summary.Length > 0)
                    {
                        summary.Append(", ");
                    }

                    summary.Append('\'').Append(entry.Identifier).Append('\'')
                        .Append(entry.Owner.activeInHierarchy ? string.Empty : " (inactive)");
                }

                RummagePlugin.Log.LogInfo("[WorldMarkers] indexed " + entries.Count + " storage objects: " + summary);
            }
        }

        /// <summary>A PlayerStash's identifier, a frame station's frameIdentifier, or null for any other station.</summary>
        private string Identify(ServiceStation station)
        {
            PlayerStash stash = station as PlayerStash;
            if (stash != null)
            {
                return stash.StashIdentifier ?? string.Empty;
            }

            Type type = station.GetType();
            FieldInfo field;
            if (!identifierFields.TryGetValue(type, out field))
            {
                field = type.GetField(FrameIdentifierField, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null && field.FieldType != typeof(string))
                {
                    field = null;
                }

                identifierFields[type] = field;

                if (field == null && type.Name == "ItemFrameStation")
                {
                    RummagePlugin.Log.LogWarning("ItemFrameStation." + FrameIdentifierField + " was not found; item frames and wall mounts get no world marker.");
                }
            }

            return field != null ? field.GetValue(station) as string : null;
        }
    }
}

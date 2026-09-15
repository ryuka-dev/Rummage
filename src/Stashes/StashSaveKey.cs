using System;

namespace Ryuka.Sulfur.Rummage.Stashes
{
    /// <summary>
    /// The key a stash's contents are stored under in the save's STASH table.
    ///
    /// GameManager.SaveStashData writes "Stash" for the unnamed stash and "Stash_" + identifier for
    /// every other one. This type only depends on the base class library so it can be tested
    /// without the game.
    /// </summary>
    internal static class StashSaveKey
    {
        public const string Prefix = "Stash";
        private const char Separator = '_';

        public static string FromIdentifier(string identifier)
        {
            return string.IsNullOrEmpty(identifier) ? Prefix : Prefix + Separator + identifier;
        }

        /// <summary>
        /// Recovers the stash identifier from a save key. Returns false for any key the game's save
        /// routine could not have produced, including "Stash_" with nothing after it.
        /// </summary>
        public static bool TryGetIdentifier(string key, out string identifier)
        {
            identifier = null;

            if (key == null || !key.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (key.Length == Prefix.Length)
            {
                identifier = string.Empty;
                return true;
            }

            if (key[Prefix.Length] != Separator || key.Length == Prefix.Length + 1)
            {
                return false;
            }

            identifier = key.Substring(Prefix.Length + 1);
            return true;
        }
    }
}

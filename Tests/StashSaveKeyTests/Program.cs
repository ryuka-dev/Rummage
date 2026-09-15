using System;
using Ryuka.Sulfur.Rummage.Stashes;

namespace Ryuka.Sulfur.Rummage.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            // Keys the game's SaveStashData produces.
            RoundTrip(string.Empty, "Stash");
            RoundTrip("WeaponRack", "Stash_WeaponRack");
            RoundTrip("a_b", "Stash_a_b");
            Equal("Stash", StashSaveKey.FromIdentifier(null), "null identifier maps to the unnamed stash");

            // Keys it cannot produce.
            Rejects(null);
            Rejects(string.Empty);
            Rejects("Stash_");
            Rejects("StashLegacy");
            Rejects("stash_lower");
            Rejects("MyStash_x");

            Console.WriteLine(failures == 0 ? "All stash key tests passed." : failures + " stash key test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void RoundTrip(string identifier, string key)
        {
            Equal(key, StashSaveKey.FromIdentifier(identifier), "FromIdentifier('" + identifier + "')");

            string parsed;
            bool ok = StashSaveKey.TryGetIdentifier(key, out parsed);
            Equal(true, ok, "TryGetIdentifier('" + key + "') succeeds");
            Equal(identifier, parsed, "TryGetIdentifier('" + key + "') identifier");
        }

        private static void Rejects(string key)
        {
            string parsed;
            bool ok = StashSaveKey.TryGetIdentifier(key, out parsed);
            Equal(false, ok, "TryGetIdentifier('" + key + "') rejects");
            Equal(null, parsed, "TryGetIdentifier('" + key + "') leaves identifier null");
        }

        private static void Equal<T>(T expected, T actual, string what)
        {
            if (Equals(expected, actual))
            {
                return;
            }

            failures++;
            Console.WriteLine("FAIL " + what + ": expected '" + expected + "', got '" + actual + "'");
        }
    }
}

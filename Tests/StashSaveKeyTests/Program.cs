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

            // Every identifier seen in the church (probe round Misc/23).
            Named("", StashKind.Main, 0);
            Named("Wardrobe", StashKind.Wardrobe, 0);
            Named("Suitcase", StashKind.Suitcase, 0);
            Named("Safe", StashKind.Safe, 0);
            Named("Sideboard", StashKind.Sideboard, 0);
            Named("Fridge", StashKind.Fridge, 0);
            Named("SnakeBasket", StashKind.SnakeBasket, 0);
            Named("GunCabinet", StashKind.GunCabinet, 0);
            Named("Coffin", StashKind.Coffin, 0);
            Named("Toilet", StashKind.Toilet, 0);
            Named("WeaponRack0", StashKind.WeaponRack, 1);
            Named("WeaponRack2", StashKind.WeaponRack, 3);
            Named("ItemFrame01", StashKind.ItemFrame, 1);
            Named("ItemFrame04", StashKind.ItemFrame, 4);
            Named("WallMount0", StashKind.WallMount, 1);
            Named("WallMount1", StashKind.WallMount, 2);
            Named(null, StashKind.Main, 0);

            // Anything else is not guessed at.
            Named("WeaponRack", StashKind.Unknown, 0);
            Named("WeaponRackA", StashKind.Unknown, 0);
            Named("ItemFrame00", StashKind.Unknown, 0);
            Named("wardrobe", StashKind.Unknown, 0);
            Named("Stash_Wardrobe", StashKind.Unknown, 0);
            Named("WeaponRack123456", StashKind.Unknown, 0);

            Console.WriteLine(failures == 0 ? "All stash key tests passed." : failures + " stash key test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void Named(string identifier, StashKind kind, int number)
        {
            StashName name = StashNaming.Resolve(identifier);
            Equal(kind, name.Kind, "Resolve('" + identifier + "') kind");
            Equal(number, name.Number, "Resolve('" + identifier + "') number");
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

using System;

namespace Ryuka.Sulfur.Rummage.Stashes
{
    /// <summary>Which storage a stash identifier refers to. Declaration order is display order.</summary>
    internal enum StashKind
    {
        Main,
        Wardrobe,
        Suitcase,
        Safe,
        Sideboard,
        Fridge,
        SnakeBasket,
        GunCabinet,
        WeaponRack,
        Coffin,
        Toilet,
        ItemFrame,
        WallMount,
        Unknown,
    }

    internal readonly struct StashName
    {
        public StashName(StashKind kind, int number)
        {
            Kind = kind;
            Number = number;
        }

        public StashKind Kind { get; }

        /// <summary>1-based position within a numbered family (weapon racks, frames, mounts); 0 otherwise.</summary>
        public int Number { get; }
    }

    /// <summary>
    /// Maps the stash identifiers seen in the church (probe round Misc/23, build 25257948) to kinds.
    /// Identifiers not seen there resolve to Unknown rather than to a guess. Game independent.
    /// </summary>
    internal static class StashNaming
    {
        public static StashName Resolve(string identifier)
        {
            switch (identifier ?? string.Empty)
            {
                case "":
                    return new StashName(StashKind.Main, 0);
                case "Wardrobe":
                    return new StashName(StashKind.Wardrobe, 0);
                case "Suitcase":
                    return new StashName(StashKind.Suitcase, 0);
                case "Safe":
                    return new StashName(StashKind.Safe, 0);
                case "Sideboard":
                    return new StashName(StashKind.Sideboard, 0);
                case "Fridge":
                    return new StashName(StashKind.Fridge, 0);
                case "SnakeBasket":
                    return new StashName(StashKind.SnakeBasket, 0);
                case "GunCabinet":
                    return new StashName(StashKind.GunCabinet, 0);
                case "Coffin":
                    return new StashName(StashKind.Coffin, 0);
                case "Toilet":
                    return new StashName(StashKind.Toilet, 0);
            }

            int number;

            // WeaponRack0..2 and WallMount0..1 count from zero; ItemFrame01..04 from one.
            if (TryNumberedSuffix(identifier, "WeaponRack", out number))
            {
                return new StashName(StashKind.WeaponRack, number + 1);
            }

            if (TryNumberedSuffix(identifier, "WallMount", out number))
            {
                return new StashName(StashKind.WallMount, number + 1);
            }

            if (TryNumberedSuffix(identifier, "ItemFrame", out number) && number > 0)
            {
                return new StashName(StashKind.ItemFrame, number);
            }

            return new StashName(StashKind.Unknown, 0);
        }

        private static bool TryNumberedSuffix(string identifier, string prefix, out int number)
        {
            number = 0;
            if (identifier == null
                || identifier.Length <= prefix.Length
                || !identifier.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            for (int i = prefix.Length; i < identifier.Length; i++)
            {
                char digit = identifier[i];
                if (digit < '0' || digit > '9')
                {
                    return false;
                }

                number = number * 10 + (digit - '0');
                if (number > 9999)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

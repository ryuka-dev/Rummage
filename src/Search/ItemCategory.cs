using System;

namespace Ryuka.Sulfur.Rummage.Search
{
    /// <summary>What kind of item something is, as far as the category buttons are concerned.</summary>
    internal enum ItemCategory
    {
        /// <summary>As a filter: no category restriction. Never an item's own category.</summary>
        All,
        Oils,
        Scrolls,
        Weapons,
        Attachments,
        Consumables,

        /// <summary>An item no button selects on its own. Never offered as a filter.</summary>
        Other,
    }

    internal static class ItemCategories
    {
        /// <summary>The categories a player can filter by, in button order.</summary>
        public static readonly ItemCategory[] Selectable =
        {
            ItemCategory.All,
            ItemCategory.Oils,
            ItemCategory.Scrolls,
            ItemCategory.Weapons,
            ItemCategory.Attachments,
            ItemCategory.Consumables,
        };

        /// <summary>The next selectable category after <paramref name="current"/>, wrapping to All.</summary>
        public static ItemCategory Next(ItemCategory current)
        {
            int index = Array.IndexOf(Selectable, current);
            return Selectable[(index + 1) % Selectable.Length];
        }
    }
}

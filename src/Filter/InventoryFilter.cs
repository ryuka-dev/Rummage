using System;
using System.Collections.Generic;
using Ryuka.Sulfur.Rummage.Search;

namespace Ryuka.Sulfur.Rummage.Filter
{
    /// <summary>
    /// The single owner of what the player is currently looking for: the typed text and the
    /// selected category. It lives for the session and is never saved. Game independent.
    /// </summary>
    internal sealed class InventoryFilter
    {
        public event Action Changed;

        public string Text { get; private set; } = string.Empty;

        public SearchQuery Query { get; private set; } = SearchQuery.Empty;

        public ItemCategory Category { get; private set; } = ItemCategory.All;

        /// <summary>False when nothing is typed and no category is selected; items then look untouched.</summary>
        public bool IsActive
        {
            get { return !Query.IsEmpty || Category != ItemCategory.All; }
        }

        /// <summary>
        /// Whether typed text also matches the oils and scrolls applied to an item. A preference, so
        /// clearing the search keeps it.
        /// </summary>
        public bool IncludeAppliedEnchantments { get; private set; }

        public bool Accepts(ItemCategory itemCategory, SearchableText text)
        {
            return Accepts(itemCategory, text, null);
        }

        /// <summary>
        /// The category always applies to the item itself; <paramref name="applied"/> (the texts of
        /// the enchantments on it) only widen the text match, and only while
        /// <see cref="IncludeAppliedEnchantments"/> is on.
        /// </summary>
        public bool Accepts(ItemCategory itemCategory, SearchableText text, IReadOnlyList<SearchableText> applied)
        {
            if (Category != ItemCategory.All && itemCategory != Category)
            {
                return false;
            }

            return Query.Matches(text, IncludeAppliedEnchantments ? applied : null);
        }

        public void SetIncludeAppliedEnchantments(bool value)
        {
            if (value == IncludeAppliedEnchantments)
            {
                return;
            }

            IncludeAppliedEnchantments = value;
            Changed?.Invoke();
        }

        public void SetText(string text)
        {
            text = text ?? string.Empty;
            if (text == Text)
            {
                return;
            }

            Text = text;
            Query = SearchQuery.Parse(text);
            Changed?.Invoke();
        }

        public void SetCategory(ItemCategory category)
        {
            if (category == ItemCategory.Other)
            {
                category = ItemCategory.All;
            }

            if (category == Category)
            {
                return;
            }

            Category = category;
            Changed?.Invoke();
        }

        public void CycleCategory()
        {
            SetCategory(ItemCategories.Next(Category));
        }

        public void Clear()
        {
            bool changed = Text.Length > 0 || Category != ItemCategory.All;
            Text = string.Empty;
            Query = SearchQuery.Empty;
            Category = ItemCategory.All;

            if (changed)
            {
                Changed?.Invoke();
            }
        }
    }
}

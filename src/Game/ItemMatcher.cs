using System.Collections.Generic;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using Ryuka.Sulfur.Rummage.Filter;
using Ryuka.Sulfur.Rummage.Search;

namespace Ryuka.Sulfur.Rummage.Game
{
    /// <summary>
    /// Whether one item matches the filter, including the enchantments applied to it when the
    /// filter asks for that. The same rule for an item in a live grid and for one saved in a stash.
    /// </summary>
    internal static class ItemMatcher
    {
        private static readonly List<SearchableText> Applied = new List<SearchableText>();

        public static bool Matches(InventoryItem item, InventoryFilter filter)
        {
            ItemSearchCatalog.Entry entry = ItemSearchCatalog.Get(item.itemDefinition);
            Applied.Clear();

            if (UsesApplied(filter) && item.enchantments != null)
            {
                foreach (EnchantmentDefinition enchantment in item.enchantments)
                {
                    SearchableText text = ItemSearchCatalog.GetApplied(enchantment);
                    if (text != null)
                    {
                        Applied.Add(text);
                    }
                }
            }

            return filter.Accepts(entry.Category, entry.Text, Applied);
        }

        /// <summary>A saved stash entry: its enchantments are stored as the ids of the items that applied them.</summary>
        public static bool Matches(InventoryData data, ItemDefinition definition, InventoryFilter filter)
        {
            ItemSearchCatalog.Entry entry = ItemSearchCatalog.Get(definition);
            Applied.Clear();

            if (UsesApplied(filter) && data.enchantmentIds != null)
            {
                foreach (ItemId id in data.enchantmentIds)
                {
                    ItemDefinition applied = id.IsValid ? id.GetAsset() : null;
                    if (applied != null)
                    {
                        Applied.Add(ItemSearchCatalog.Get(applied).Text);
                    }
                }
            }

            return filter.Accepts(entry.Category, entry.Text, Applied);
        }

        private static bool UsesApplied(InventoryFilter filter)
        {
            return filter.IncludeAppliedEnchantments && !filter.Query.IsEmpty;
        }
    }
}

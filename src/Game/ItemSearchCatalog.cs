using System;
using System.Collections.Generic;
using System.Text;
using I2.Loc;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;
using PerfectRandom.Sulfur.Core.Stats;
using Ryuka.Sulfur.Rummage.Search;

namespace Ryuka.Sulfur.Rummage.Game
{
    /// <summary>
    /// Turns an item definition into what search and the category buttons need, once per item per
    /// language. Everything read here is the definition, never a particular item's state.
    /// </summary>
    internal static class ItemSearchCatalog
    {
        internal sealed class Entry
        {
            public Entry(ItemCategory category, SearchableText text)
            {
                Category = category;
                Text = text;
            }

            public ItemCategory Category { get; }

            public SearchableText Text { get; }
        }

        private static readonly Dictionary<ItemDefinition, Entry> Entries = new Dictionary<ItemDefinition, Entry>();
        private static readonly Entry Unindexed = new Entry(ItemCategory.Other, SearchableText.Create(null));

        private static bool indexFailureReported;

        /// <summary>Drops every entry; the next lookups rebuild them in the current language.</summary>
        public static void Invalidate()
        {
            Entries.Clear();
        }

        public static Entry Get(ItemDefinition item)
        {
            if (item == null)
            {
                return Unindexed;
            }

            Entry entry;
            if (!Entries.TryGetValue(item, out entry))
            {
                entry = Build(item);
                Entries[item] = entry;
            }

            return entry;
        }

        private static Entry Build(ItemDefinition item)
        {
            try
            {
                List<string> sources = new List<string>();

                // Current language first, then the English name, so an English query still works
                // in any language.
                sources.Add(Translate("Items/" + item.name, item.displayName));
                sources.Add(item.displayName);

                EnchantmentDefinition enchantment = item.appliesEnchantment.IsValid ? item.appliesEnchantment.GetAsset() : null;
                if (enchantment != null)
                {
                    sources.Add(Translate("EnchantmentDefinitions/" + enchantment.name, enchantment.enchantmentName));
                    sources.Add(enchantment.enchantmentName);
                    AddModifierTexts(enchantment.modifiersApplied, true, sources);
                }

                AddModifierTexts(item.modifiersOnAttachToItem, false, sources);

                return new Entry(Classify(item, enchantment), SearchableText.Create(sources));
            }
            catch (Exception ex)
            {
                if (!indexFailureReported)
                {
                    indexFailureReported = true;
                    RummagePlugin.Log.LogWarning("Could not index item '" + item.name + "' for search; it will not match any search or category. "
                        + "Further indexing failures are not logged. " + ex);
                }

                return Unindexed;
            }
        }

        /// <summary>
        /// The same effect lines the item description shows: the game's item description adds a
        /// modifier's "simplified" increase or decrease text, or else its description name, and
        /// only lists enchantment modifiers marked to show.
        /// </summary>
        private static void AddModifierTexts(List<ItemModifierContainer> modifiers, bool onlyShownInDescription, List<string> sources)
        {
            if (modifiers == null)
            {
                return;
            }

            foreach (ItemModifierContainer modifier in modifiers)
            {
                if (modifier == null || modifier.attribute == ItemAttributes.None)
                {
                    continue;
                }

                ItemAttribute attribute = modifier.attribute.GetAsset();
                if (attribute == null || (onlyShownInDescription && !attribute.showInItemDescription))
                {
                    continue;
                }

                string term = "ItemAttributes/" + modifier.attribute;
                if (attribute.simplifiedModAmount)
                {
                    sources.Add(modifier.value > 0f
                        ? Translate(term + "_simplifiedIncrease", attribute.simplifiedIncreaseString)
                        : Translate(term + "_simplifiedDecrease", attribute.simplifiedDecreaseString));
                }
                else
                {
                    sources.Add(Translate(term + "_itemDescription", attribute.itemDescriptionName));
                }
            }
        }

        /// <summary>
        /// Oils and scrolls are both enchantment items. In build 25257948 every scroll's
        /// enchantment is elemental and no oil's is, which is what separates them here.
        /// </summary>
        private static ItemCategory Classify(ItemDefinition item, EnchantmentDefinition enchantment)
        {
            if (item.useType == UseType.Enchantment && enchantment != null)
            {
                return enchantment.IsElemental ? ItemCategory.Scrolls : ItemCategory.Oils;
            }

            switch (item.ItemType)
            {
                case ItemType.Weapon:
                    return ItemCategory.Weapons;
                case ItemType.Attachment:
                    return ItemCategory.Attachments;
                case ItemType.Consumable:
                    return ItemCategory.Consumables;
                default:
                    return ItemCategory.Other;
            }
        }

        /// <summary>
        /// Looks a term up without right-to-left reordering, because the query is typed in logical
        /// order, and without rich text tags.
        /// </summary>
        private static string Translate(string term, string fallback)
        {
            string translation;
            if (LocalizationManager.TryGetTranslation(term, out translation, false) && !string.IsNullOrEmpty(translation))
            {
                return StripTags(translation);
            }

            return fallback;
        }

        private static string StripTags(string text)
        {
            if (text.IndexOf('<') < 0)
            {
                return text;
            }

            StringBuilder plain = new StringBuilder(text.Length);
            bool inTag = false;
            foreach (char character in text)
            {
                if (character == '<')
                {
                    inTag = true;
                }
                else if (character == '>' && inTag)
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    plain.Append(character);
                }
            }

            return plain.ToString();
        }
    }
}

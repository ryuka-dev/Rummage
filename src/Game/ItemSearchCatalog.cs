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

        private static readonly Dictionary<EnchantmentDefinition, SearchableText> EnchantmentTexts = new Dictionary<EnchantmentDefinition, SearchableText>();

        /// <summary>Drops every entry; the next lookups rebuild them in the current language.</summary>
        public static void Invalidate()
        {
            Entries.Clear();
            EnchantmentTexts.Clear();
        }

        /// <summary>
        /// What an enchantment applied to an item can be found by: the texts of the oil or scroll that
        /// applied it when the game recorded one (it does when an enchantment is added to an item),
        /// otherwise the enchantment's own names and effect lines.
        /// </summary>
        public static SearchableText GetApplied(EnchantmentDefinition enchantment)
        {
            if (enchantment == null)
            {
                return null;
            }

            if (enchantment.ItemThatAppliedThis != null)
            {
                return Get(enchantment.ItemThatAppliedThis).Text;
            }

            SearchableText text;
            if (!EnchantmentTexts.TryGetValue(enchantment, out text))
            {
                List<string> sources = new List<string>();
                try
                {
                    sources.Add(Translate("EnchantmentDefinitions/" + enchantment.name, enchantment.enchantmentName));
                    sources.Add(enchantment.enchantmentName);
                    AddModifierTexts(enchantment.modifiersApplied, true, sources);
                }
                catch (Exception)
                {
                    // Whatever was collected before the failure still counts.
                }

                text = SearchableText.Create(sources);
                EnchantmentTexts[enchantment] = text;
            }

            return text;
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
                AddDescriptionTexts(item, sources);

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
        /// The rest of what the item description shows, built from the same terms: the item's own
        /// description, repair, healing and other buffs, statuses added or removed on consuming,
        /// and the stats it gives when equipped. Numbers are left out; only the words are searched.
        /// </summary>
        private static void AddDescriptionTexts(ItemDefinition item, List<string> sources)
        {
            if (item.hasCustomDescription)
            {
                sources.Add(Translate("Items/" + item.name + "_description", null));
            }

            if (item.valueChangeOnItemConsume != null)
            {
                foreach (ItemConsumeContainer change in item.valueChangeOnItemConsume)
                {
                    if (change != null && change.attribute == ItemAttributes.Durability)
                    {
                        sources.Add(Template("ItemDescriptions/Instruction_Repair"));
                        sources.Add(Template("ItemDescriptions/DynamicString_RepairValue"));
                    }
                }
            }

            if (item.buffsOnConsume != null)
            {
                foreach (BuffDefinition buff in item.buffsOnConsume)
                {
                    if (buff == null || buff.attributeId == EntityAttributes.None)
                    {
                        continue;
                    }

                    if (buff.attributeId == EntityAttributes.Stat_HealthRegen)
                    {
                        sources.Add(Template("ItemDescriptions/DynamicString_HealthConsumable"));
                    }
                    else if (buff.attributeId == EntityAttributes.Stat_MovementSpeed)
                    {
                        // The game writes this line in English in every language.
                        sources.Add("extra movement speed");
                    }
                    else
                    {
                        sources.Add(Fill("ItemDescriptions/DynamicString_AttributeBonusBuff", "ATTRIBUTE_X", EntityName(buff.attributeId)));
                    }
                }
            }

            if (item.addStatusOnConsume != null)
            {
                foreach (ApplyStatusContainer status in item.addStatusOnConsume)
                {
                    if (status != null && status.status != EntityAttributes.None)
                    {
                        sources.Add(Fill("ItemDescriptions/DynamicString_StatusAdd", "STATUS_X", EntityName(status.status)));
                    }
                }
            }

            if (item.removeStatusOnConsume != null)
            {
                foreach (EntityAttributes status in item.removeStatusOnConsume)
                {
                    if (status != EntityAttributes.None && status != EntityAttributes.End)
                    {
                        sources.Add(Fill("ItemDescriptions/DynamicString_RemovesStatus", "STATUS_X", EntityName(status)));
                    }
                }
            }

            if (item.modifiersOnEquipNew != null)
            {
                foreach (ModifierContainer modifier in item.modifiersOnEquipNew)
                {
                    if (modifier == null || modifier.attribute == EntityAttributes.None)
                    {
                        continue;
                    }

                    EntityAttribute attribute = modifier.attribute.GetAsset();
                    if (attribute != null && !attribute.hideFromItemDescriptions)
                    {
                        sources.Add(Translate("EntityAttributes/" + modifier.attribute + "_itemDescription", attribute.itemDescriptionName));
                    }
                }
            }
        }

        private static string EntityName(EntityAttributes attribute)
        {
            return Translate("EntityAttributes/" + attribute + "_itemDescription", attribute.ToString());
        }

        private static string Fill(string term, string token, string value)
        {
            string template = Translate(term, null);
            return template == null ? null : StripPlaceholders(template.Replace(token, value ?? string.Empty));
        }

        private static string Template(string term)
        {
            string template = Translate(term, null);
            return template == null ? null : StripPlaceholders(template);
        }

        /// <summary>The description templates carry VALUE_X-style placeholders the game fills with numbers.</summary>
        private static string StripPlaceholders(string text)
        {
            foreach (string placeholder in Placeholders)
            {
                text = text.Replace(placeholder, string.Empty);
            }

            return text;
        }

        private static readonly string[] Placeholders = { "VALUE_X", "DURATION_X", "AMOUNT_X", "STATUS_X", "ATTRIBUTE_X" };

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

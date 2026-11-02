using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Items;

namespace Ryuka.Sulfur.Rummage.Diagnostics
{
    /// <summary>
    /// Development probe that lists every item the game defines as an enchantment, or tags as an
    /// oil, once per session. Its purpose is to find a field that separates oils from the other
    /// enchantment items (both report IsEnchantment) before any filter relies on one.
    /// </summary>
    internal static class EnchantmentCatalogProbe
    {
        private const string Tag = "[EnchantmentCatalog] ";

        private static DiagnosticsConfig config;
        private static bool dumped;

        public static void Initialize(DiagnosticsConfig diagnostics)
        {
            config = diagnostics;
        }

        public static void OnPlayerSpawned()
        {
            if (dumped || config == null || !config.LogEnchantmentCatalog.Value)
            {
                return;
            }

            dumped = true;

            try
            {
                Dump();
            }
            catch (Exception ex)
            {
                RummagePlugin.Log.LogWarning(Tag + "catalog dump failed: " + ex);
            }
        }

        private static void Dump()
        {
            AsyncAssetLoading loading = StaticInstance<AsyncAssetLoading>.Instance;
            ItemDatabase database = loading != null ? loading.itemDatabase : null;
            List<ItemDefinition> definitions = database != null ? database.GetRawList() : null;
            if (definitions == null)
            {
                RummagePlugin.Log.LogWarning(Tag + "item database is not available; the enchantment catalog was not written.");
                return;
            }

            ItemTag oilTags = OilTagMask();
            int listed = 0;
            int withOilTag = 0;

            RummagePlugin.Log.LogInfo(Tag + "scanning " + definitions.Count + " item definitions; oil tag mask=" + oilTags);

            foreach (ItemDefinition item in definitions)
            {
                if (item == null)
                {
                    continue;
                }

                bool oilTagged = (item.itemTags & oilTags) != 0;
                if (item.useType != UseType.Enchantment && !item.appliesEnchantment.IsValid && !oilTagged)
                {
                    continue;
                }

                listed++;
                if (oilTagged)
                {
                    withOilTag++;
                }

                RummagePlugin.Log.LogInfo(Tag + Describe(item));
            }

            RummagePlugin.Log.LogInfo(Tag + listed + " item(s) listed, " + withOilTag + " carry an Oil* tag.");
        }

        private static string Describe(ItemDefinition item)
        {
            StringBuilder line = new StringBuilder();
            line.Append("asset='").Append(item.name).Append('\'')
                .Append(", id=").Append(item.id.value)
                .Append(", displayName='").Append(item.displayName).Append('\'')
                .Append(", localized='").Append(item.LocalizedDisplayName).Append('\'')
                .Append(", useType=").Append(item.useType)
                .Append(", itemType=").Append(item.ItemType)
                .Append(", isEnchantment=").Append(item.IsEnchantment)
                .Append(", tags=").Append(item.itemTags)
                .Append(", size=").Append(item.inventorySize.x).Append('x').Append(item.inventorySize.y)
                .Append(", quality=").Append(item.itemQuality)
                .Append(", slot=").Append(item.slotType)
                .Append(", deprecated=").Append(item.deprecated)
                .Append(", excludedFromDatabase=").Append(item.excludeFromDatabase);

            if (!item.appliesEnchantment.IsValid)
            {
                line.Append(", enchantment=<none>");
                return line.ToString();
            }

            EnchantmentDefinition enchantment = item.appliesEnchantment.GetAsset();
            if (enchantment == null)
            {
                line.Append(", enchantment=<id ").Append(item.appliesEnchantment.value).Append(" not in database>");
                return line.ToString();
            }

            line.Append(", enchantment asset='").Append(enchantment.name).Append('\'')
                .Append(", enchantmentName='").Append(enchantment.enchantmentName).Append('\'')
                .Append(", enchantment localized='").Append(enchantment.LocalizedName).Append('\'')
                .Append(", elemental=").Append(enchantment.IsElemental)
                .Append(", costsDurability=").Append(enchantment.CostsDurability)
                .Append(", modifiers=[");

            if (enchantment.modifiersApplied != null)
            {
                for (int i = 0; i < enchantment.modifiersApplied.Count; i++)
                {
                    ItemModifierContainer modifier = enchantment.modifiersApplied[i];
                    if (i > 0)
                    {
                        line.Append("; ");
                    }

                    if (modifier == null)
                    {
                        line.Append("<null>");
                        continue;
                    }

                    line.Append(modifier.attribute).Append(' ').Append(modifier.modType).Append(' ')
                        .Append(modifier.value.ToString("0.###", CultureInfo.InvariantCulture));
                }
            }

            line.Append(']');
            return line.ToString();
        }

        /// <summary>Every ItemTag flag whose name starts with "Oil", read from the enum at run time.</summary>
        private static ItemTag OilTagMask()
        {
            ItemTag mask = 0;
            foreach (ItemTag tag in Enum.GetValues(typeof(ItemTag)))
            {
                if (tag.ToString().StartsWith("Oil", StringComparison.Ordinal))
                {
                    mask |= tag;
                }
            }

            return mask;
        }
    }
}

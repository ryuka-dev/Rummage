using System;
using System.Text;
using Ryuka.Sulfur.Rummage.Filter;
using Ryuka.Sulfur.Rummage.Search;

namespace Ryuka.Sulfur.Rummage
{
    /// <summary>Stands in for the plugin's logger so the linked files compile on their own.</summary>
    internal static class RummagePlugin
    {
        internal static readonly StubLog Log = new StubLog();

        internal sealed class StubLog
        {
            public void LogWarning(object message)
            {
                Console.WriteLine("WARN: " + message);
            }
        }
    }
}

namespace Ryuka.Sulfur.Rummage.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Check(PinyinTable.IsAvailable, "pinyin table loads from the embedded resource");

            QueryParsing();
            TextAndPinyinMatching();
            FilterState();

            Console.WriteLine(failures == 0 ? "All search tests passed." : failures + " search test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void QueryParsing()
        {
            Check(SearchQuery.Parse(null).IsEmpty, "null query is empty");
            Check(SearchQuery.Parse("   ").IsEmpty, "blank query is empty");
            Check(SearchQuery.Parse("  Fire   OIL ").TermCount == 2, "whitespace splits terms");
        }

        private static void TextAndPinyinMatching()
        {
            SearchableText actionOil = SearchableText.Create(new[] { "动作魔油", "Action Oil", "动作魔油", "装填速度提升" });

            Hit(actionOil, "", "oil", "OIL", "action oil", "oil action", "动作", "魔油", "dongzuo", "dz", "moyou", "my", "zhuangtian");
            Miss(actionOil, "fire", "action fire", "火", "huo");

            SearchableText scroll = SearchableText.Create(new[] { "余震卷轴", "Scroll of Aftershock", "余震" });
            Hit(scroll, "juanzhou", "jz", "aftershock", "卷轴");
            Miss(scroll, "moyou");

            Miss(SearchableText.Create(null), "a");
            Check(SearchQuery.Parse("").Matches(null), "empty query matches an item with no texts");
        }

        private static void FilterState()
        {
            InventoryFilter filter = new InventoryFilter();
            int changes = 0;
            filter.Changed += () => changes++;

            Check(!filter.IsActive, "new filter is inactive");

            filter.SetText("oil");
            Check(filter.IsActive && changes == 1, "typing activates the filter and notifies once");
            filter.SetText("oil");
            Check(changes == 1, "setting the same text does not notify");

            SearchableText oil = SearchableText.Create(new[] { "Action Oil" });
            Check(filter.Accepts(ItemCategory.Oils, oil), "text match with no category");
            filter.SetCategory(ItemCategory.Scrolls);
            Check(!filter.Accepts(ItemCategory.Oils, oil), "category restricts matches");

            filter.Clear();
            Check(!filter.IsActive && filter.Text.Length == 0 && filter.Category == ItemCategory.All, "clear resets text and category");

            filter.SetCategory(ItemCategory.Other);
            Check(filter.Category == ItemCategory.All, "Other is never selectable");

            ItemCategory walked = ItemCategory.All;
            for (int i = 0; i < ItemCategories.Selectable.Length; i++)
            {
                walked = ItemCategories.Next(walked);
            }

            Check(walked == ItemCategory.All, "cycling visits every category and wraps to All");
            Check(ItemCategories.Next(ItemCategory.Other) == ItemCategory.All, "cycling from an unselectable category starts at All");
        }

        private static void Hit(SearchableText text, params string[] queries)
        {
            foreach (string query in queries)
            {
                Check(SearchQuery.Parse(query).Matches(text), "'" + query + "' should match");
            }
        }

        private static void Miss(SearchableText text, params string[] queries)
        {
            foreach (string query in queries)
            {
                Check(!SearchQuery.Parse(query).Matches(text), "'" + query + "' should not match");
            }
        }

        private static void Check(bool condition, string what)
        {
            if (!condition)
            {
                failures++;
                Console.WriteLine("FAIL " + what);
            }
        }
    }
}

using PerfectRandom.Sulfur.Core;
using TMPro;

namespace Ryuka.Sulfur.Rummage.UserInterface
{
    /// <summary>
    /// The game's font for the current language, from the same per-language cache its
    /// FontLocalizer components apply (I2 term "Font/&lt;style&gt;"). A font sampled from an arbitrary
    /// game text is not enough: some keep a Latin-only font in every language.
    /// </summary>
    internal static class GameFonts
    {
        /// <summary>Null when the cache is not available yet.</summary>
        public static TMP_FontAsset Current()
        {
            AsyncAssetLoading loading = StaticInstance<AsyncAssetLoading>.Instance;
            TMP_FontAsset[] cache = loading != null ? loading.fontCache : null;
            int index = (int)PerfectRandom.Sulfur.Core.FontStyle.RoundRegular;

            if (cache == null || index >= cache.Length)
            {
                return null;
            }

            if (cache[index] == null)
            {
                FontLocalizer.LoadFontCache();
            }

            return cache[index];
        }
    }
}

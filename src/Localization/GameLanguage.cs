using System;

namespace Ryuka.Sulfur.Rummage.Localization
{
    /// <summary>
    /// Resolves which language the game is displaying, as a plain language code that matches the
    /// lang/&lt;code&gt;.json files shipped in the package. The only place Rummage's own text
    /// lookup touches I2.Loc.
    /// </summary>
    internal static class GameLanguage
    {
        public const string FallbackCode = "en";

        /// <summary>The language codes the game ships, which are also the lang/ file names.</summary>
        private static readonly string[] ShippedCodes =
        {
            "en", "sv", "fr", "it", "de", "es", "pt", "ru", "pl", "ja", "ko", "zh-CN", "tr", "ar",
        };

        /// <summary>The current language code ("en", "zh-CN", ...), or null before localisation has loaded.</summary>
        public static string CurrentCode()
        {
            try
            {
                string code = I2.Loc.LocalizationManager.CurrentLanguageCode;
                return string.IsNullOrEmpty(code) ? null : code;
            }
            catch (Exception)
            {
                // Localisation loads asynchronously; before it is up this can throw rather than return empty.
                return null;
            }
        }

        public static bool IsRightToLeft()
        {
            try
            {
                return I2.Loc.LocalizationManager.IsRight2Left;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Reorders right-to-left text for display, as the game does with its own translations.</summary>
        public static string FixRightToLeft(string text)
        {
            try
            {
                return I2.Loc.LocalizationManager.FixRTL_IfNeeded(text);
            }
            catch (Exception)
            {
                return text;
            }
        }

        /// <summary>The exact code, else the same language under another region, else English.</summary>
        public static string ResolveCode(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                return FallbackCode;
            }

            foreach (string shipped in ShippedCodes)
            {
                if (string.Equals(shipped, languageCode, StringComparison.OrdinalIgnoreCase))
                {
                    return shipped;
                }
            }

            string wanted = LanguageOf(languageCode);
            foreach (string shipped in ShippedCodes)
            {
                if (string.Equals(LanguageOf(shipped), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return shipped;
                }
            }

            return FallbackCode;
        }

        private static string LanguageOf(string languageCode)
        {
            int hyphen = languageCode.IndexOf('-');
            return hyphen > 0 ? languageCode.Substring(0, hyphen) : languageCode;
        }
    }
}

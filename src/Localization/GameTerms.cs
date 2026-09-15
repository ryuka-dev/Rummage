using System;
using I2.Loc;

namespace Ryuka.Sulfur.Rummage.Localization
{
    /// <summary>
    /// The game's own translation of one of its localisation terms, for labels where the game has
    /// a matching word. Found with the game term probe (round Misc/29).
    /// </summary>
    internal static class GameTerms
    {
        public const string Consumable = "ItemDescriptions/UseType_Consumable";
        public const string Attachments = "ItemDescriptions/Label_Attachments";

        /// <summary>Null when the game has no non-empty translation, so the caller uses Rummage's own text.</summary>
        public static string Get(string term)
        {
            try
            {
                string translation;
                return LocalizationManager.TryGetTranslation(term, out translation) && !string.IsNullOrWhiteSpace(translation)
                    ? translation
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

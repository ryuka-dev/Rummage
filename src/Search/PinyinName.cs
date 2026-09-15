using System;

namespace Ryuka.Sulfur.Rummage.Search
{
    /// <summary>
    /// A name with han characters in it, prepared so a latin query can be matched against how the
    /// name is pronounced. Readings are resolved once, when an item is indexed, so a
    /// keystroke only costs the match itself.
    ///
    /// The query may be typed the way a chinese keyboard is typed, which is what makes this useful:
    ///
    /// <list type="bullet">
    /// <item>full syllables - <c>shouqiang</c></item>
    /// <item>initials - <c>sq</c></item>
    /// <item>the two mixed - <c>shouq</c>, <c>sqiang</c></item>
    /// <item>a syllable left half-typed - <c>shouqia</c></item>
    /// <item>the <c>zh</c>/<c>ch</c>/<c>sh</c> initials typed in full - <c>zhq</c></item>
    /// </list>
    ///
    /// Characters with several readings match on any of them, so <c>changqiang</c> and
    /// <c>zhangqiang</c> both find a name starting with the character read either way.
    ///
    /// The match is a substring match, like the search this widens: the query may start at any
    /// character of the name.
    /// </summary>
    internal sealed class PinyinName
    {
        /// <summary>Names longer than this are matched on their first characters only.</summary>
        private const int MaxLength = 64;

        private readonly string text;

        /// <summary>Per character: its readings, or null when the character is not han.</summary>
        private readonly string[][] readings;

        private PinyinName(string text, string[][] readings)
        {
            this.text = text;
            this.readings = readings;
        }

        /// <summary>
        /// Prepares <paramref name="text"/> for pinyin matching, or returns null when it has no
        /// han characters and plain substring search already covers it.
        /// </summary>
        internal static PinyinName Create(string text)
        {
            if (string.IsNullOrEmpty(text) || !PinyinTable.IsAvailable)
            {
                return null;
            }

            if (text.Length > MaxLength)
            {
                text = text.Substring(0, MaxLength);
            }

            string[][] readings = null;

            for (int i = 0; i < text.Length; i++)
            {
                string[] characterReadings = PinyinTable.GetReadings(text[i]);
                if (characterReadings.Length == 0)
                {
                    continue;
                }

                if (readings == null)
                {
                    readings = new string[text.Length][];
                }

                readings[i] = characterReadings;
            }

            return readings == null ? null : new PinyinName(text, readings);
        }

        /// <summary>Whether <paramref name="query"/>, lower-case latin, spells part of this name.</summary>
        internal bool Matches(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            for (int start = 0; start < text.Length; start++)
            {
                if (MatchFrom(start, query, 0))
                {
                    return true;
                }
            }

            return false;
        }

        private bool MatchFrom(int index, string query, int queryIndex)
        {
            if (queryIndex >= query.Length)
            {
                return true;
            }

            if (index >= text.Length)
            {
                return false;
            }

            string[] characterReadings = readings[index];
            if (characterReadings == null)
            {
                char character = text[index];
                if (character == query[queryIndex] && MatchFrom(index + 1, query, queryIndex + 1))
                {
                    return true;
                }

                // Spaces, brackets and dashes sit between spoken characters rather than being
                // typed, so a query is allowed to step over them.
                return !char.IsLetterOrDigit(character) && MatchFrom(index + 1, query, queryIndex);
            }

            for (int r = 0; r < characterReadings.Length; r++)
            {
                string reading = characterReadings[r];

                int shared = 0;
                while (shared < reading.Length
                    && queryIndex + shared < query.Length
                    && reading[shared] == query[queryIndex + shared])
                {
                    shared++;
                }

                if (shared == 0)
                {
                    continue;
                }

                // The query ran out inside this syllable: everything typed so far lines up.
                if (queryIndex + shared >= query.Length)
                {
                    return true;
                }

                // Only a whole syllable or a way it is abbreviated may carry on to the next
                // character. Stopping anywhere else would let "shou" match a name read "shu".
                if (shared == reading.Length && MatchFrom(index + 1, query, queryIndex + shared))
                {
                    return true;
                }

                if (MatchFrom(index + 1, query, queryIndex + 1))
                {
                    return true;
                }

                if (shared >= 2 && IsRetroflex(reading) && MatchFrom(index + 1, query, queryIndex + 2))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether the syllable starts zh, ch or sh, which are abbreviated to one letter but are
        /// also often typed in full.
        /// </summary>
        private static bool IsRetroflex(string reading)
        {
            if (reading.Length < 2 || reading[1] != 'h')
            {
                return false;
            }

            char initial = reading[0];
            return initial == 'z' || initial == 'c' || initial == 's';
        }
    }
}

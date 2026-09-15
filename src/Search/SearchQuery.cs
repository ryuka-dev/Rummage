using System;
using System.Collections.Generic;

namespace Ryuka.Sulfur.Rummage.Search
{
    /// <summary>
    /// What the player typed, split into lower-case terms that must all match. "fire oil" finds an
    /// item whose texts contain both words, in any of its texts. Game independent.
    /// </summary>
    internal sealed class SearchQuery
    {
        public static readonly SearchQuery Empty = new SearchQuery(new string[0]);

        private readonly string[] terms;

        /// <summary>Per term: whether it is plain a-z, the only shape pinyin matching accepts.</summary>
        private readonly bool[] latinOnly;

        private SearchQuery(string[] terms)
        {
            this.terms = terms;
            latinOnly = new bool[terms.Length];
            for (int i = 0; i < terms.Length; i++)
            {
                latinOnly[i] = IsLatin(terms[i]);
            }
        }

        public bool IsEmpty
        {
            get { return terms.Length == 0; }
        }

        public int TermCount
        {
            get { return terms.Length; }
        }

        public static SearchQuery Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Empty;
            }

            string[] parts = text.ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? Empty : new SearchQuery(parts);
        }

        /// <summary>An empty query matches everything.</summary>
        public bool Matches(SearchableText searchable)
        {
            if (terms.Length == 0)
            {
                return true;
            }

            if (searchable == null)
            {
                return false;
            }

            for (int i = 0; i < terms.Length; i++)
            {
                if (!searchable.Contains(terms[i], latinOnly[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLatin(string term)
        {
            for (int i = 0; i < term.Length; i++)
            {
                if (term[i] < 'a' || term[i] > 'z')
                {
                    return false;
                }
            }

            return term.Length > 0;
        }
    }

    /// <summary>
    /// The texts one item can be found by, lower-cased once, with pinyin prepared for the ones
    /// written in han characters.
    /// </summary>
    internal sealed class SearchableText
    {
        private static readonly PinyinName[] NoSpokenNames = new PinyinName[0];

        private readonly string[] texts;
        private readonly PinyinName[] spokenNames;

        private SearchableText(string[] texts, PinyinName[] spokenNames)
        {
            this.texts = texts;
            this.spokenNames = spokenNames;
        }

        public static SearchableText Create(IEnumerable<string> sources)
        {
            List<string> unique = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            if (sources != null)
            {
                foreach (string source in sources)
                {
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        continue;
                    }

                    string lower = source.Trim().ToLowerInvariant();
                    if (seen.Add(lower))
                    {
                        unique.Add(lower);
                    }
                }
            }

            List<PinyinName> spoken = null;
            foreach (string text in unique)
            {
                PinyinName name = PinyinName.Create(text);
                if (name != null)
                {
                    (spoken ??= new List<PinyinName>()).Add(name);
                }
            }

            return new SearchableText(unique.ToArray(), spoken == null ? NoSpokenNames : spoken.ToArray());
        }

        internal bool Contains(string term, bool latinOnly)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].IndexOf(term, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            if (!latinOnly)
            {
                return false;
            }

            for (int i = 0; i < spokenNames.Length; i++)
            {
                if (spokenNames[i].Matches(term))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Ryuka.Sulfur.Rummage.Search
{
    /// <summary>
    /// Toneless mandarin readings for han characters, read once from the table embedded in this
    /// assembly. A character can have several readings; they are ordered most common first.
    ///
    /// The table (shared with Dev Mode QoL, which carries its generator) is built from data derived from the
    /// Unicode Han Database, and covers U+4E00..U+9FFF - the block that holds simplified and
    /// traditional Chinese. Its layout, little-endian throughout:
    ///
    /// <code>
    /// uint16   syllable count
    /// uint32   byte length of the syllable block
    /// bytes    syllables, UTF-8, separated by '\n'
    /// uint16   first code point covered
    /// uint16   last code point covered
    /// repeat, one per code point in that range:
    ///   uint8    number of readings
    ///   uint16[] syllable indices
    /// </code>
    ///
    /// A missing or unreadable table costs pinyin search and nothing else.
    /// </summary>
    internal static class PinyinTable
    {
        private const string ResourceName = "Ryuka.Sulfur.Rummage.Search.PinyinTable.bin";

        private static readonly string[] Empty = new string[0];

        /// <summary>Reading arrays handed out so far, so repeated characters share one array.</summary>
        private static readonly Dictionary<char, string[]> Cache = new Dictionary<char, string[]>();

        private static string[] syllables;
        private static ushort[] starts;
        private static ushort[] readingIds;
        private static char rangeStart;
        private static char rangeEnd;
        private static bool loaded;

        /// <summary>Whether the table is present and any pinyin lookup can succeed.</summary>
        internal static bool IsAvailable
        {
            get
            {
                Load();
                return syllables != null;
            }
        }

        /// <summary>How many characters the table carries a reading for. Diagnostics only.</summary>
        internal static int CoveredCharacters
        {
            get
            {
                Load();
                return readingIds == null ? 0 : readingIds.Length;
            }
        }

        /// <summary>Whether this character is one the table can be asked about.</summary>
        internal static bool IsHan(char character)
        {
            Load();
            return syllables != null && character >= rangeStart && character <= rangeEnd;
        }

        /// <summary>
        /// The character's readings, most common first. Empty when the character is not han or the
        /// table carries no reading for it.
        /// </summary>
        internal static string[] GetReadings(char character)
        {
            if (!IsHan(character))
            {
                return Empty;
            }

            string[] cached;
            if (Cache.TryGetValue(character, out cached))
            {
                return cached;
            }

            int index = character - rangeStart;
            int from = starts[index];
            int count = starts[index + 1] - from;

            string[] readings = Empty;
            if (count > 0)
            {
                readings = new string[count];
                for (int i = 0; i < count; i++)
                {
                    readings[i] = syllables[readingIds[from + i]];
                }
            }

            Cache[character] = readings;
            return readings;
        }

        private static void Load()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;

            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                {
                    if (stream == null)
                    {
                        RummagePlugin.Log.LogWarning(
                            "The pinyin table is missing from this build; pinyin search is off.");
                        return;
                    }

                    Read(stream);
                }
            }
            catch (Exception exception)
            {
                syllables = null;
                starts = null;
                readingIds = null;
                RummagePlugin.Log.LogWarning("Could not read the pinyin table: " + exception.Message
                    + " Pinyin search is off.");
            }
        }

        private static void Read(Stream stream)
        {
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int syllableCount = reader.ReadUInt16();
                int syllableBytes = (int)reader.ReadUInt32();
                syllables = Encoding.UTF8.GetString(reader.ReadBytes(syllableBytes)).Split('\n');

                if (syllables.Length != syllableCount)
                {
                    throw new InvalidDataException(
                        "expected " + syllableCount + " syllables but found " + syllables.Length);
                }

                rangeStart = (char)reader.ReadUInt16();
                rangeEnd = (char)reader.ReadUInt16();

                int characterCount = rangeEnd - rangeStart + 1;
                starts = new ushort[characterCount + 1];

                // Readings are stored per character; flattening them here turns every later lookup
                // into two array reads.
                List<ushort> flattened = new List<ushort>(characterCount + characterCount / 2);
                for (int i = 0; i < characterCount; i++)
                {
                    starts[i] = (ushort)flattened.Count;

                    int count = reader.ReadByte();
                    for (int r = 0; r < count; r++)
                    {
                        flattened.Add(reader.ReadUInt16());
                    }
                }

                starts[characterCount] = (ushort)flattened.Count;
                readingIds = flattened.ToArray();
            }
        }
    }
}

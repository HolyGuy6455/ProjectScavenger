using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace Scavenger.Audio
{
    /// <summary>Maps stable gameplay keys to serialized FMOD GUIDs.</summary>
    [CreateAssetMenu(fileName = "SoundCatalog", menuName = "Audio/Sound Catalog")]
    public sealed class SoundCatalog : ScriptableObject
    {
        public const string Address = "SoundCatalog";

        [Serializable]
        public struct Entry
        {
            public SoundKey Key;
            public EventReference Event;

            public Entry(SoundKey key, EventReference eventReference)
            {
                Key = key;
                Event = eventReference;
            }
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        /// <summary>
        /// Returns a complete lookup or an error without exposing a partial dictionary.
        /// Paths are editor metadata; player builds resolve events by GUID only.
        /// </summary>
        public bool TryCreateLookup(out Dictionary<SoundKey, EventReference> lookup, out string error)
        {
            return TryCreateLookup(entries, out lookup, out error);
        }

        private static bool TryCreateLookup(Entry[] source,
            out Dictionary<SoundKey, EventReference> lookup, out string error)
        {
            lookup = null;
            error = null;
            if (source == null)
            {
                error = "SoundCatalog has no entries.";
                return false;
            }

            var candidate = new Dictionary<SoundKey, EventReference>(source.Length);
            foreach (Entry entry in source)
            {
                if (entry.Key == SoundKey.None || !Enum.IsDefined(typeof(SoundKey), entry.Key))
                {
                    error = $"SoundCatalog contains an invalid key: {entry.Key}.";
                    return false;
                }

                if (entry.Event.Guid.IsNull)
                {
                    error = $"SoundCatalog key {entry.Key} has an empty FMOD GUID.";
                    return false;
                }

                if (candidate.ContainsKey(entry.Key))
                {
                    error = $"SoundCatalog contains duplicate key {entry.Key}.";
                    return false;
                }

                candidate.Add(entry.Key, entry.Event);
            }

            foreach (SoundKey key in Enum.GetValues(typeof(SoundKey)))
            {
                if (key != SoundKey.None && !candidate.ContainsKey(key))
                {
                    error = $"SoundCatalog is missing key {key}.";
                    return false;
                }
            }

            lookup = candidate;
            return true;
        }

#if UNITY_EDITOR
        public IReadOnlyList<Entry> Entries => Array.AsReadOnly(entries);

        /// <summary>Validates the entire replacement before changing this asset.</summary>
        public void ReplaceEntries(Entry[] replacement)
        {
            if (!TryCreateLookup(replacement, out _, out string error))
                throw new ArgumentException(error, nameof(replacement));

            entries = (Entry[])replacement.Clone();
        }
#endif
    }
}

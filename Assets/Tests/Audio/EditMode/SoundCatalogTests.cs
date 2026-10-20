using System;
using System.Collections.Generic;
using FMODUnity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scavenger.Audio.Tests
{
    public sealed class SoundCatalogTests
    {
        private SoundCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            SoundCatalog source = RegisteredCatalog(out _);
            catalog = Object.Instantiate(source);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void RuntimeAssetContainsEveryDefinedKey()
        {
            Assert.That(catalog.TryCreateLookup(out var lookup, out string error), Is.True, error);
            Assert.That(lookup.Count, Is.EqualTo(Enum.GetValues(typeof(SoundKey)).Length - 1));
            Assert.That(lookup.ContainsKey(SoundKey.None), Is.False);
        }

        [Test]
        public void MovedCatalogIsRegisteredAndResolvesOutsideResources()
        {
            SoundCatalog registered = RegisteredCatalog(out string assetPath);
            Assert.That(assetPath, Does.Not.Contain("/Resources/"),
                "The runtime catalog must load through its address after moving out of Resources.");
            Assert.That(registered.TryCreateLookup(out var lookup, out string error), Is.True, error);
            Assert.That(lookup.Count, Is.EqualTo(catalog.Entries.Count));
        }

        [Test]
        public void DuplicateKeyIsRejectedWithoutReturningPartialLookup()
        {
            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("Key").intValue =
                entries.GetArrayElementAtIndex(0).FindPropertyRelative("Key").intValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssertRejected("duplicate");
        }

        [Test]
        public void MissingRequiredKeyIsRejectedWithoutReturningPartialLookup()
        {
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("entries").arraySize--;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssertRejected("missing");
        }

        [Test]
        public void EmptyGuidIsRejectedEvenWhenEditorPathIsPresent()
        {
            var serialized = new SerializedObject(catalog);
            SerializedProperty reference = serialized.FindProperty("entries")
                .GetArrayElementAtIndex(0).FindPropertyRelative("Event");
            Assert.That(reference.FindPropertyRelative("Path").stringValue, Is.Not.Empty);
            SerializedProperty guid = reference.FindPropertyRelative("Guid");
            for (int index = 1; index <= 4; index++)
                guid.FindPropertyRelative("Data" + index).intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssertRejected("empty FMOD GUID");
        }

        [TestCase(0)]
        [TestCase(99999)]
        public void NoneAndUndefinedKeysAreRejected(int key)
        {
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("entries").GetArrayElementAtIndex(0)
                .FindPropertyRelative("Key").intValue = key;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssertRejected("invalid key");
        }

        [Test]
        public void InvalidReplacementLeavesExistingCatalogIntact()
        {
            Assert.That(catalog.TryCreateLookup(out var before, out string error), Is.True, error);
            var replacement = new SoundCatalog.Entry[catalog.Entries.Count];
            for (int index = 0; index < replacement.Length; index++)
                replacement[index] = catalog.Entries[index];
            replacement[1] = replacement[0];

            Assert.Throws<ArgumentException>(() => catalog.ReplaceEntries(replacement));
            Assert.That(catalog.TryCreateLookup(out var after, out error), Is.True, error);
            Assert.That(after.Count, Is.EqualTo(before.Count));
            foreach (KeyValuePair<SoundKey, EventReference> entry in before)
                Assert.That(after[entry.Key].Guid, Is.EqualTo(entry.Value.Guid));
        }

        private void AssertRejected(string expectedError)
        {
            Assert.That(catalog.TryCreateLookup(out var lookup, out string error), Is.False);
            Assert.That(lookup, Is.Null, "Callers must never receive a partially valid lookup.");
            StringAssert.Contains(expectedError, error);
        }

        private static SoundCatalog RegisteredCatalog(out string assetPath)
        {
            Assert.That(AddressableAssetSettingsDefaultObject.SettingsExists, Is.True,
                "Addressables settings must already be registered; tests must not create them.");
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null, "Addressables settings must already exist.");
            var matches = new List<AddressableAssetEntry>();
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                    continue;
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry.address == SoundCatalog.Address)
                        matches.Add(entry);
                }
            }

            Assert.That(matches.Count, Is.EqualTo(1),
                $"Exactly one Addressables entry must use '{SoundCatalog.Address}'.");
            assetPath = AssetDatabase.GUIDToAssetPath(matches[0].guid);
            Assert.That(assetPath, Is.Not.Empty, "The registered GUID must resolve after the catalog moves.");
            SoundCatalog source = AssetDatabase.LoadAssetAtPath<SoundCatalog>(assetPath);
            Assert.That(source, Is.Not.Null, "The registered asset must be a SoundCatalog.");
            return source;
        }
    }
}

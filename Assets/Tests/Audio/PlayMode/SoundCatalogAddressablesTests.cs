using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace Scavenger.Audio.Tests
{
    /// <summary>Checks the runtime address independently of FMOD and the client/server audio guard.</summary>
    public sealed class SoundCatalogAddressablesTests
    {
        [UnityTest]
        public IEnumerator CatalogLoadsByAddressAndRetainsAllRuntimeKeys()
        {
            AsyncOperationHandle<SoundCatalog> operation =
                Addressables.LoadAssetAsync<SoundCatalog>(SoundCatalog.Address);
            try
            {
                yield return operation;
                Assert.That(operation.Status, Is.EqualTo(AsyncOperationStatus.Succeeded),
                    $"Address '{SoundCatalog.Address}' must resolve after moving the catalog out of Resources.");
                Assert.That(operation.Result, Is.Not.Null);
                Assert.That(operation.Result.TryCreateLookup(out var lookup, out string error), Is.True, error);
                Assert.That(lookup.Count, Is.EqualTo(Enum.GetValues(typeof(SoundKey)).Length - 1));
            }
            finally
            {
                if (operation.IsValid())
                    Addressables.Release(operation);
            }
        }
    }
}

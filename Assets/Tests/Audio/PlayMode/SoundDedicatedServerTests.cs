#if UNITY_SERVER && !UNITY_EDITOR
using FMODUnity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scavenger.Audio.Tests
{
    public sealed class SoundDedicatedServerTests
    {
        [Test]
        public void PlaybackAndListenerApisRemainDisabledWithoutCreatingAudioRuntime()
        {
            bool fmodWasInitialized = RuntimeManager.IsInitialized;
            int managersBefore = Object.FindObjectsByType<SoundManager>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
            int listenersBefore = Object.FindObjectsByType<StudioListener>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
            var cameraObject = new GameObject("Dedicated-server sound guard test camera");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                Assert.That(SoundManager.IsAudioEnabled, Is.False);
                Assert.That(SoundManager.Initialize(), Is.False);
                var initialization = SoundManager.InitializeAsync();
                Assert.That(initialization.IsCompleted, Is.True);
                Assert.That(initialization.Result, Is.False);
                Assert.That(SoundManager.SetWorldListener(camera, Vector3.zero), Is.False);
                Assert.That(SoundManager.IsWorldListenerReady, Is.False);
                Assert.That(SoundManager.Play(SoundKey.UIClick), Is.False);
                Assert.That(SoundManager.PlayMusic(SoundKey.LobbyMusic), Is.False);
                SoundManager.StopMusic();
                Assert.That(SoundManager.PlayAt(SoundKey.GrenadeExplosion, Vector3.zero), Is.False);
                Assert.That(SoundManager.PlayLoopAt(SoundKey.BoarCharge, Vector3.zero).IsValid, Is.False);
                SoundManager.ClearWorldListener(camera);

                Assert.That(RuntimeManager.IsInitialized, Is.EqualTo(fmodWasInitialized));
                Assert.That(Object.FindObjectsByType<SoundManager>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length, Is.EqualTo(managersBefore));
                Assert.That(Object.FindObjectsByType<StudioListener>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length, Is.EqualTo(listenersBefore));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
#endif

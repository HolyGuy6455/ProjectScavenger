using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Scavenger.Audio
{
    public enum SoundBus
    {
        Master,
        UI,
        SFX,
        Music
    }

    /// <summary>
    /// Shared FMOD playback service, created lazily on first use and retained across scenes.
    /// All calls must run on Unity's main thread. Listener setup is owned by the game.
    /// Await InitializeAsync before playback; calls made while loading return false/an invalid handle.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class SoundManager : MonoBehaviour
    {
        // Music is optional; public distributions omit the licensed BGM bank.
        private static readonly string[] RequiredBanks = { "Master.strings", "Master", "SFX" };
        private static SoundManager current;
        private static bool quitting;
        private static bool dedicatedServer;
        private static bool sceneTransition;

        private readonly List<SoundHandle> loops = new List<SoundHandle>();
        // These have already been released to FMOD, but remain stoppable while playing.
        private readonly List<EventInstance> spatialOneShots = new List<EventInstance>();
        private readonly List<string> ownedBanks = new List<string>();
        private readonly HashSet<string> warnings = new HashSet<string>();
        private Dictionary<SoundKey, EventReference> events;
        private AsyncOperationHandle<SoundCatalog> catalogLoad;
        private Task<bool> initializationTask;
        private bool initialized;
        private bool initializationFailed;
        private bool shuttingDown;
        private SoundListenerRig listenerRig;
        private SoundKey musicKey;
        private SoundHandle music;

        public static bool IsAudioEnabled => Application.isPlaying && !quitting && !dedicatedServer;

        public static bool IsWorldListenerReady => IsAudioEnabled && !sceneTransition
            && current != null && current.listenerRig != null && current.listenerRig.IsWorldReady;

        public static bool IsInitialized => current != null && current.initialized
            && !quitting && RuntimeManager.IsInitialized;

        internal bool OwnsLiveHandles => current == this && !shuttingDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (current != null)
            {
                current.Shutdown();
                Destroy(current.gameObject);
            }
            current = null;
            quitting = false;
            sceneTransition = false;
#if UNITY_SERVER && !UNITY_EDITOR
            dedicatedServer = true;
#else
            dedicatedServer = Array.Exists(System.Environment.GetCommandLineArgs(),
                argument => string.Equals(argument, "-server", StringComparison.OrdinalIgnoreCase));
#endif
        }

        /// <summary>
        /// Starts loading without blocking; returns true only once ready. Update continues pending loads.
        /// Also retries a previous failure. Prefer InitializeAsync when playback must wait for readiness.
        /// </summary>
        public static bool Initialize()
        {
            SoundManager manager = GetManager();
            if (manager == null)
                return false;
            manager.initializationFailed = false;
            return manager.EnsureInitialized();
        }

        /// <summary>
        /// Loads the Addressable catalog and required banks before allowing playback.
        /// Concurrent callers share one task. Returns false on failure, shutdown, or dedicated servers.
        /// </summary>
        public static Task<bool> InitializeAsync()
        {
            SoundManager manager = GetManager();
            if (manager == null)
                return Task.FromResult(false);
            if (manager.initializationTask != null && !manager.initializationTask.IsCompleted)
                return manager.initializationTask;

            manager.initializationFailed = false;
            return manager.initializationTask = manager.WaitForInitializationAsync();
        }

        private async Task<bool> WaitForInitializationAsync()
        {
            while (this != null && current == this && !quitting && !shuttingDown)
            {
                if (EnsureInitialized())
                    return true;
                // An independently destroyed FMOD runtime cannot make this session ready again.
                if (initializationFailed || initialized)
                    return false;
                await Task.Yield();
            }
            return false;
        }

        /// <summary>Plays a 2D one-shot, such as UIClick.</summary>
        public static bool Play(SoundKey key) => PlayOneShot(key, false, default);

        /// <summary>Plays a 3D one-shot at a fixed world position.</summary>
        public static bool PlayAt(SoundKey key, Vector3 position) => PlayOneShot(key, true, position);

        /// <summary>
        /// Called after moving the local player's camera. The caller checks local client ownership;
        /// emitters have no ownership restriction. Distance uses the supplied focus, panning the camera.
        /// </summary>
        public static bool SetWorldListener(Camera camera, Vector3 attenuationPosition)
        {
            if (sceneTransition || camera == null || !camera.isActiveAndEnabled)
                return false;
            SoundManager manager = GetManager();
            if (manager == null)
                return false;
            try
            {
                if (manager.listenerRig == null)
                    manager.listenerRig = new SoundListenerRig(manager.transform);
                if (manager.listenerRig.HasWorldListener && !manager.listenerRig.UsesCamera(camera))
                    manager.StopWorldSounds();
                manager.listenerRig.SetWorld(camera, attenuationPosition);
                return true;
            }
            catch (Exception exception)
            {
                manager.WarnOnce($"Cannot set world listener: {exception.Message}");
                return false;
            }
        }

        /// <summary>A stale camera cannot detach a newer camera's listener.</summary>
        public static void ClearWorldListener(Camera camera)
        {
            if (current != null && current.listenerRig != null && current.listenerRig.UsesCamera(camera))
                current.ClearWorldListener();
        }

        /// <summary>Loading keeps 2D audio available and stops all owned 3D audio.</summary>
        public static void BeginSceneTransition()
        {
            sceneTransition = true;
            if (current != null)
                current.ClearWorldListener();
        }

        /// <summary>World audio still waits for a local gameplay camera after loading completes.</summary>
        public static void CompleteSceneTransition() => sceneTransition = false;

        private void ClearWorldListener()
        {
            StopWorldSounds();
            if (!quitting && !shuttingDown)
                listenerRig?.ClearWorld();
        }

        private void StopWorldSounds()
        {
            foreach (SoundHandle handle in loops)
            {
                if (handle.IsSpatial)
                    handle.Stop(true);
            }
            if (RuntimeManager.IsInitialized)
            {
                foreach (EventInstance instance in spatialOneShots)
                {
                    if (instance.isValid())
                        instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                }
            }
            spatialOneShots.Clear();
        }

        /// <summary>Plays a 2D loop; stop the returned handle when finished.</summary>
        public static SoundHandle PlayLoop(SoundKey key, SoundLifetime lifetime = SoundLifetime.Scene)
            => StartLoop(key, false, default, null, lifetime);

        public static SoundHandle PlayLoopAt(SoundKey key, Vector3 position,
            SoundLifetime lifetime = SoundLifetime.Scene)
            => StartLoop(key, true, position, null, lifetime);

        /// <summary>Follows an emitter and stops when it is disabled, destroyed, or its scene unloads.</summary>
        public static SoundHandle PlayLoopAttached(SoundKey key, Transform emitter)
        {
            if (emitter == null || !emitter.gameObject.activeInHierarchy)
                return SoundHandle.Invalid;
            return StartLoop(key, true, emitter.position, emitter, SoundLifetime.Scene);
        }

        /// <summary>Plays one music loop owned by the active scene. Repeated requests keep its position.</summary>
        public static bool PlayMusic(SoundKey key) => PlayMusic(key, SceneManager.GetActiveScene());

        /// <summary>
        /// Plays one 2D music loop until the supplied scene unloads. Capture that scene before awaiting
        /// initialization; loading an additive scene must not change which scene owns the music.
        /// Invalid requests leave the previous music playing.
        /// </summary>
        public static bool PlayMusic(SoundKey key, Scene ownerScene)
        {
            if (!ownerScene.IsValid() || !ownerScene.isLoaded)
                return false;
            SoundManager manager = GetManager();
            if (manager == null)
                return false;
            if (manager.musicKey == key && manager.music != null && manager.music.IsPlaying)
                return true;

            SoundHandle next = StartLoop(key, false, default, null, SoundLifetime.Scene, ownerScene.handle);
            if (!next.IsValid)
                return false;

            manager.music?.Stop();
            manager.musicKey = key;
            manager.music = next;
            return true;
        }

        /// <summary>Stops the current music, allowing its authored fade-out by default.</summary>
        public static void StopMusic(bool immediately = false)
        {
            if (current == null)
                return;
            current.music?.Stop(immediately);
            current.ClearMusic();
        }

        private void ClearMusic()
        {
            musicKey = SoundKey.None;
            music = null;
        }

        public static void StopAllLoops(bool immediately = false)
        {
            if (current == null)
                return;
            foreach (SoundHandle handle in current.loops)
                handle.Stop(immediately);
            current.ClearMusic();
        }

        /// <summary>Sets a bus volume in the range 0–1. Saving settings is the caller's responsibility.</summary>
        public static bool SetVolume(SoundBus bus, float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume))
                return false;
            string path;
            switch (bus)
            {
                case SoundBus.Master: path = "bus:/"; break;
                case SoundBus.UI: path = "bus:/UI"; break;
                case SoundBus.SFX: path = "bus:/SFX"; break;
                case SoundBus.Music: path = "bus:/Music"; break;
                default: return false;
            }
            SoundManager manager = GetManager();
            if (manager == null || !manager.EnsureInitialized())
                return false;
            FMOD.RESULT result = RuntimeManager.StudioSystem.getBus(path, out Bus target);
            return manager.Check(result, $"Find {path}")
                && manager.Check(target.setVolume(Mathf.Clamp01(volume)), $"Set volume: {path}");
        }

        private static SoundManager GetManager()
        {
            if (!IsAudioEnabled)
                return null;
            if (current == null)
                new GameObject("SoundManager").AddComponent<SoundManager>();
            return current;
        }

        private void Awake()
        {
            if (current != null && current != this)
            {
                Destroy(gameObject);
                return;
            }
            current = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private bool EnsureInitialized()
        {
            if (initialized)
                return RuntimeManager.IsInitialized;
            if (initializationFailed || shuttingDown || quitting)
                return false;

            try
            {
                if (events == null)
                {
                    if (!catalogLoad.IsValid())
                        catalogLoad = Addressables.LoadAssetAsync<SoundCatalog>(SoundCatalog.Address);
                    if (!catalogLoad.IsDone)
                        return false;
                    if (catalogLoad.Status != AsyncOperationStatus.Succeeded || catalogLoad.Result == null)
                        throw new InvalidOperationException(
                            $"Could not load Addressable '{SoundCatalog.Address}': {catalogLoad.OperationException?.Message}");

                    SoundCatalog catalog = catalogLoad.Result;
                    if (!catalog.TryCreateLookup(out events, out string error))
                        throw new InvalidOperationException(error);
                    // The dictionary copies value-type EventReferences; it no longer needs the asset.
                    ReleaseCatalogLoad();
                }

                // FMOD's auto-load setting may already own these banks. Only acquire missing ones.
                if (listenerRig == null)
                    listenerRig = new SoundListenerRig(transform);
                listenerRig.EnsureFallback();
                foreach (string bank in RequiredBanks)
                {
                    if (!RuntimeManager.HasBankLoaded(bank) && !ownedBanks.Contains(bank))
                    {
                        RuntimeManager.LoadBank(bank);
                        ownedBanks.Add(bank);
                    }
                    // Async bank sources can complete on a later frame; don't request them twice.
                    if (!RuntimeManager.HasBankLoaded(bank))
                        return false;
                }
                initialized = true;
                return true;
            }
            catch (Exception exception)
            {
                ReleaseCatalogLoad();
                initializationFailed = true;
                WarnOnce($"Initialization failed: {exception.Message} Call InitializeAsync() to retry after fixing the catalog/banks.");
                return false;
            }
        }

        private bool TryCreate(SoundKey key, bool spatial, bool loop, Vector3 position,
            Transform emitter, out EventInstance instance)
        {
            instance = default;
            if (spatial && !IsWorldListenerReady)
                return false;
            if (!EnsureInitialized())
                return false;
            // Keep UI and gameplay audio available when the optional BGM is not distributed.
            if (key == SoundKey.LobbyMusic && !RuntimeManager.HasBankLoaded("Music"))
                return false;
            if (!events.TryGetValue(key, out EventReference reference))
            {
                WarnOnce($"Unknown sound key: {key}.");
                return false;
            }

            try
            {
                EventDescription description = RuntimeManager.GetEventDescription(reference);
                if (!Check(description.is3D(out bool is3D), $"Read spatial mode: {key}")
                    || !Check(description.isOneshot(out bool isOneShot), $"Read playback mode: {key}"))
                    return false;
                if (is3D != spatial || isOneShot == loop)
                {
                    WarnOnce($"{key} is a {(is3D ? "3D" : "2D")} {(isOneShot ? "one-shot" : "loop")}; use the matching Play/PlayAt/PlayLoop API.");
                    return false;
                }
                if (!Check(description.createInstance(out instance), $"Create: {key}"))
                    return false;
                if (spatial)
                {
                    FMOD.ATTRIBUTES_3D attributes = emitter != null
                        ? RuntimeUtils.To3DAttributes(emitter) : RuntimeUtils.To3DAttributes(position);
                    if (!Check(instance.set3DAttributes(attributes), $"Position: {key}"))
                    {
                        instance.release();
                        instance = default;
                        return false;
                    }
                }
                return true;
            }
            catch (Exception exception)
            {
                if (instance.isValid())
                    instance.release();
                instance = default;
                WarnOnce($"Cannot create {key}: {exception.Message}");
                return false;
            }
        }

        private static bool PlayOneShot(SoundKey key, bool spatial, Vector3 position)
        {
            SoundManager manager = GetManager();
            if (manager == null || !manager.TryCreate(key, spatial, false, position, null, out EventInstance instance))
                return false;
            bool started = manager.Check(instance.start(), $"Start: {key}");
            // A released one-shot finishes naturally; a failed start must still release its instance.
            manager.Check(instance.release(), $"Release: {key}");
            if (started && spatial)
                manager.spatialOneShots.Add(instance);
            return started;
        }

        private static SoundHandle StartLoop(SoundKey key, bool spatial, Vector3 position,
            Transform emitter, SoundLifetime lifetime, int? ownerSceneHandle = null)
        {
            if (lifetime != SoundLifetime.Scene && lifetime != SoundLifetime.Persistent)
                return SoundHandle.Invalid;
            SoundManager manager = GetManager();
            if (manager == null || !manager.TryCreate(key, spatial, true, position, emitter, out EventInstance instance))
                return SoundHandle.Invalid;
            if (!manager.Check(instance.start(), $"Start loop: {key}"))
            {
                instance.release();
                return SoundHandle.Invalid;
            }
            int sceneHandle = emitter != null ? emitter.gameObject.scene.handle
                : ownerSceneHandle ?? SceneManager.GetActiveScene().handle;
            var handle = new SoundHandle(manager, instance, lifetime, sceneHandle, emitter, spatial);
            manager.loops.Add(handle);
            return handle;
        }

        private void Update()
        {
            if (listenerRig != null && listenerRig.HasWorldListener && !listenerRig.IsWorldReady)
                ClearWorldListener();
            if (!initialized && !initializationFailed && (catalogLoad.IsValid() || events != null))
                EnsureInitialized();
            for (int i = loops.Count - 1; i >= 0; i--)
            {
                if (!loops[i].Tick())
                {
                    if (ReferenceEquals(loops[i], music))
                        ClearMusic();
                    loops.RemoveAt(i);
                }
            }
            for (int i = spatialOneShots.Count - 1; i >= 0; i--)
            {
                EventInstance instance = spatialOneShots[i];
                if (!RuntimeManager.IsInitialized || !instance.isValid()
                    || instance.getPlaybackState(out PLAYBACK_STATE state) != FMOD.RESULT.OK
                    || state == PLAYBACK_STATE.STOPPED)
                    spatialOneShots.RemoveAt(i);
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (music != null && music.BelongsToScene(scene.handle))
                ClearMusic();
            foreach (SoundHandle handle in loops)
            {
                if (handle.BelongsToScene(scene.handle))
                    handle.Stop();
            }
        }

        private bool Check(FMOD.RESULT result, string operation)
        {
            if (result == FMOD.RESULT.OK)
                return true;
            WarnOnce($"{operation}: {result}.");
            return false;
        }

        private void WarnOnce(string message)
        {
            if (warnings.Add(message))
                Debug.LogWarning($"[SoundManager] {message}", this);
        }

        private void OnApplicationQuit()
        {
            if (current != this)
                return;
            quitting = true;
            Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
            if (current == this)
                current = null;
        }

        private void ReleaseCatalogLoad()
        {
            if (!catalogLoad.IsValid())
                return;
            Addressables.Release(catalogLoad);
            catalogLoad = default;
        }

        private void Shutdown()
        {
            if (shuttingDown)
                return;
            listenerRig?.Dispose();
            listenerRig = null;
            StopWorldSounds();
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            foreach (SoundHandle handle in loops)
                handle.Stop(true);
            loops.Clear();
            ClearMusic();
            // Releasing a pending handle also releases its asset when the asynchronous load completes.
            ReleaseCatalogLoad();
            // Never resurrect FMOD while Unity is tearing objects down.
            if (RuntimeManager.IsInitialized)
            {
                for (int i = ownedBanks.Count - 1; i >= 0; i--)
                    RuntimeManager.UnloadBank(ownedBanks[i]);
            }
            ownedBanks.Clear();
            events = null;
            initialized = false;
            shuttingDown = true;
        }
    }
}

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
        private static readonly string[] RequiredBanks = { "Master.strings", "Master", "SFX", "Music" };
        private static SoundManager current;
        private static bool quitting;
        private static bool dedicatedServer;

        private readonly List<SoundHandle> loops = new List<SoundHandle>();
        private readonly List<string> ownedBanks = new List<string>();
        private readonly HashSet<string> warnings = new HashSet<string>();
        private Dictionary<SoundKey, EventReference> events;
        private AsyncOperationHandle<SoundCatalog> catalogLoad;
        private Task<bool> initializationTask;
        private bool initialized;
        private bool initializationFailed;
        private bool shuttingDown;

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
#if UNITY_SERVER
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

        public static void StopAllLoops(bool immediately = false)
        {
            if (current == null)
                return;
            foreach (SoundHandle handle in current.loops)
                handle.Stop(immediately);
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
            if (!Application.isPlaying || quitting || dedicatedServer)
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
            if (!EnsureInitialized())
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
            return started;
        }

        private static SoundHandle StartLoop(SoundKey key, bool spatial, Vector3 position,
            Transform emitter, SoundLifetime lifetime)
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
            int sceneHandle = emitter != null ? emitter.gameObject.scene.handle : SceneManager.GetActiveScene().handle;
            var handle = new SoundHandle(manager, instance, lifetime, sceneHandle, emitter);
            manager.loops.Add(handle);
            return handle;
        }

        private void Update()
        {
            if (!initialized && !initializationFailed && (catalogLoad.IsValid() || events != null))
                EnsureInitialized();
            for (int i = loops.Count - 1; i >= 0; i--)
            {
                if (!loops[i].Tick())
                    loops.RemoveAt(i);
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
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
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            foreach (SoundHandle handle in loops)
                handle.Stop(true);
            loops.Clear();
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

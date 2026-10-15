using System;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace Scavenger.Audio
{
    public enum SoundLifetime
    {
        Scene,
        Persistent
    }

    /// <summary>
    /// Owns one looping sound. Stop/Dispose are safe to call more than once.
    /// Use on Unity's main thread, like SoundManager itself.
    /// </summary>
    public sealed class SoundHandle : IDisposable
    {
        internal static readonly SoundHandle Invalid = new SoundHandle();

        private SoundManager manager;
        private EventInstance instance;
        private readonly Transform followTarget;
        private readonly bool followsTarget;
        private bool stopping;

        internal int SceneHandle { get; private set; }
        internal SoundLifetime Lifetime { get; }

        private SoundHandle() { }

        internal SoundHandle(SoundManager manager, EventInstance instance,
            SoundLifetime lifetime, int sceneHandle, Transform followTarget)
        {
            this.manager = manager;
            this.instance = instance;
            Lifetime = lifetime;
            SceneHandle = sceneHandle;
            this.followTarget = followTarget;
            followsTarget = followTarget != null;
        }

        public bool IsValid => manager != null && manager.OwnsLiveHandles
            && RuntimeManager.IsInitialized && instance.isValid();

        public bool IsPlaying => IsValid
            && instance.getPlaybackState(out PLAYBACK_STATE state) == FMOD.RESULT.OK
            && state != PLAYBACK_STATE.STOPPED;

        /// <summary>Allows the event's authored fade-out by default.</summary>
        public void Stop(bool immediately = false)
        {
            if (!IsValid)
            {
                Invalidate();
                return;
            }

            if (stopping && !immediately)
                return;

            stopping = true;
            FMOD.RESULT result = instance.stop(immediately
                ? FMOD.Studio.STOP_MODE.IMMEDIATE : FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            if (immediately || result != FMOD.RESULT.OK)
                Release();
        }

        public void Dispose() => Stop();

        // A fading instance remains owned until it stops, so shutdown can still stop it immediately.
        internal bool Tick()
        {
            if (!IsValid)
            {
                Invalidate();
                return false;
            }

            if (!stopping && followsTarget)
            {
                if (followTarget == null || !followTarget.gameObject.activeInHierarchy)
                    Stop();
                else
                {
                    SceneHandle = followTarget.gameObject.scene.handle;
                    if (instance.set3DAttributes(RuntimeUtils.To3DAttributes(followTarget)) != FMOD.RESULT.OK)
                        Stop(true);
                }
            }

            if (!IsValid)
                return false;

            if (instance.getPlaybackState(out PLAYBACK_STATE state) != FMOD.RESULT.OK
                || state == PLAYBACK_STATE.STOPPED)
            {
                Release();
                return false;
            }

            return true;
        }

        internal bool BelongsToScene(int sceneHandle)
        {
            // A target moved into another scene (including DontDestroyOnLoad) keeps its sound.
            if (followsTarget && followTarget != null)
                SceneHandle = followTarget.gameObject.scene.handle;
            return Lifetime == SoundLifetime.Scene && SceneHandle == sceneHandle;
        }

        private void Release()
        {
            if (IsValid)
                instance.release();
            Invalidate();
        }

        private void Invalidate()
        {
            instance = default;
            manager = null;
            stopping = true;
        }
    }
}

using FMODUnity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scavenger.Audio
{
    /// <summary>
    /// Owns one active FMOD listener: a nominal 2D listener, or the current gameplay camera.
    /// SoundManager creates this only on clients. Networking policy stays in CameraFollow.
    /// </summary>
    internal sealed class SoundListenerRig
    {
        private readonly Transform owner;
        private StudioListener fallback;
        private StudioListener world;
        private Camera camera;
        private GameObject focus;

        internal bool HasWorldListener { get; private set; }
        internal bool IsWorldReady => HasWorldListener && camera != null && camera.isActiveAndEnabled
            && world != null && world.isActiveAndEnabled && RuntimeManager.IsInitialized;

        internal SoundListenerRig(Transform owner) => this.owner = owner;

        internal bool UsesCamera(Camera candidate) => HasWorldListener && ReferenceEquals(camera, candidate);

        internal void EnsureFallback()
        {
            if (HasWorldListener)
                return;
            if (fallback == null)
            {
                var nominal = new GameObject("2D Sound Listener");
                nominal.SetActive(false);
                nominal.transform.SetParent(owner, false);
                fallback = nominal.AddComponent<StudioListener>();
                nominal.SetActive(true);
            }
            else
                fallback.enabled = true;
        }

        internal void SetWorld(Camera newCamera, Vector3 attenuationPosition)
        {
            if (!UsesCamera(newCamera))
            {
                RemoveWorld();
                // Remove the previous listener before enabling its replacement.
                if (fallback != null)
                    fallback.enabled = false;
                camera = newCamera;
                focus = new GameObject("Sound Attenuation Focus");
                focus.transform.SetParent(camera.transform, false);
                focus.transform.position = attenuationPosition;
                world = camera.gameObject.AddComponent<StudioListener>();
                world.AttenuationObject = focus;
                HasWorldListener = true;
            }
            focus.transform.position = attenuationPosition;
            // CameraFollow moves in LateUpdate, after StudioListener.Update. Send the final pose
            // now as well, including on the first frame, instead of using the previous camera pose.
            RuntimeManager.SetListenerLocation(world.ListenerNumber, camera.gameObject, focus);
        }

        internal void ClearWorld()
        {
            RemoveWorld();
            EnsureFallback();
        }

        private void RemoveWorld()
        {
            HasWorldListener = false;
            if (world != null)
            {
                world.enabled = false;
                Object.Destroy(world);
            }
            if (focus != null)
                Object.Destroy(focus);
            world = null;
            focus = null;
            camera = null;
        }

        internal void Dispose()
        {
            // Called during OnApplicationQuit, before FMOD's OnDestroy. StudioListener.OnDisable
            // accesses RuntimeManager, so leaving this until after FMOD destruction is unsafe.
            RemoveWorld();
            if (fallback != null)
            {
                fallback.enabled = false;
                Object.Destroy(fallback.gameObject);
                fallback = null;
            }
        }
    }
}

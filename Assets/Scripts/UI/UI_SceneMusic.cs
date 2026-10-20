using Scavenger.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scavenger.UI
{
    /// <summary>
    /// Requests music when this panel opens. The scene owns playback, so hiding the panel keeps it playing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UI_SceneMusic : MonoBehaviour
    {
        [SerializeField] private SoundKey soundKey = SoundKey.LobbyMusic;

        private uint requestVersion;

        private async void OnEnable()
        {
            uint version = ++requestVersion;
            if (!SoundManager.IsAudioEnabled || soundKey == SoundKey.None)
                return;

            Scene ownerScene = gameObject.scene;
            SoundKey requestedKey = soundKey;

            try
            {
                if (!await SoundManager.InitializeAsync())
                    return;

                // Disabling a cached panel does not cancel its scene's music request.
                // A destroyed/moved panel or a newer request must not start stale music.
                if (this == null || !enabled || version != requestVersion
                    || !ownerScene.IsValid() || !ownerScene.isLoaded
                    || gameObject.scene != ownerScene)
                    return;

                SoundManager.PlayMusic(requestedKey, ownerScene);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}

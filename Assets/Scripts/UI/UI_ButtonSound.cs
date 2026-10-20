using Scavenger.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    /// <summary>
    /// Plays the configured sound through a persistent Button.onClick binding.
    /// This binding survives runtime RemoveAllListeners calls.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UI_ButtonSound : MonoBehaviour
    {
        [SerializeField] private SoundKey soundKey = SoundKey.UIClick;

        public void Play()
        {
            if (!isActiveAndEnabled || soundKey == SoundKey.None)
                return;

            Button button = GetComponent<Button>();
            if (!button.IsActive() || !button.IsInteractable())
                return;

            SoundManager.Play(soundKey);
        }
    }
}

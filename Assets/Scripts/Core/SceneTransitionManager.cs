using Scavenger.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scavenger.Core
{
    public enum LoadingIntent
    {
        None,
        EnterInGame,
        ReturnToLobby,
        StartSingleplayerHost,
        ReturnToIngameTest,
        // 나중에 EnterDungeon 등 추가 가능
    }

    public static class SceneTransitionManager
    {
        public static LoadingIntent CurrentIntent { get; private set; } = LoadingIntent.None;

        public static void LoadSceneWithIntent(LoadingIntent intent, string loadingSceneName = "LoadingScene")
        {
            CurrentIntent = intent;
            // 이전 씬 카메라가 로딩 중 다시 리스너를 등록하지 못하게 막습니다.
            SoundManager.BeginSceneTransition();
            Debug.Log($"[SceneTransition] 로딩 씬으로 이동합니다. 인텐트: {intent}");
            SceneManager.LoadScene(loadingSceneName);
        }

        // 인텐트를 소모(확인)한 후 초기화하는 유틸리티 메서드 (실수 방지용)
        public static void ConsumeIntent()
        {
            CurrentIntent = LoadingIntent.None;
        }
    }
}

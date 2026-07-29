using System.IO;
using UnityEditor;
using UnityEngine;

namespace Scavenger.Editor
{
    public class AutoBuilder
    {
        // 씬 경로 정의
        private static readonly string[] ClientScenes = {
            "Assets/Scenes/OutgameScene.unity",
            "Assets/Scenes/LoadingScene.unity"
        };

        private static readonly string[] ServerScenes = {
            "Assets/Scenes/LoadingScene.unity"
        };

        [MenuItem("Build/1. Build Windows Client")]
        public static void BuildWindowsClient()
        {
            string buildPath = "Builds/Windows/ScavengerClient.exe";
            EnsureDirectoryExists(buildPath);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = ClientScenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player, // 일반 클라이언트로 명시
                options = BuildOptions.None
            };

            Debug.Log("[AutoBuilder] Windows 클라이언트 빌드를 시작합니다...");
            BuildPipeline.BuildPlayer(buildPlayerOptions);
            Debug.Log("[AutoBuilder] Windows 클라이언트 빌드 완료!");
        }

        [MenuItem("Build/2. Build Android Client")]
        public static void BuildAndroidClient()
        {
            string buildPath = "Builds/Android/ScavengerClient.apk";
            EnsureDirectoryExists(buildPath);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = ClientScenes,
                locationPathName = buildPath,
                target = BuildTarget.Android,
                subtarget = (int)StandaloneBuildSubtarget.Player, // 일반 클라이언트로 명시
                options = BuildOptions.AutoRunPlayer // 빌드 완료 후 연결된 기기에 자동 설치 및 실행
            };

            Debug.Log("[AutoBuilder] Android 클라이언트 빌드를 시작합니다...");
            BuildPipeline.BuildPlayer(buildPlayerOptions);
            Debug.Log("[AutoBuilder] Android 클라이언트 빌드 완료!");
        }

        [MenuItem("Build/3. Build Linux Dedicated Server")]
        public static void BuildLinuxServer()
        {
            // 백엔드가 접근하기 쉬운 위치로 설정
            string buildPath = "../../ScavengerServer/LinuxServer/linuxBuild.x86_64";
            EnsureDirectoryExists(buildPath);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = ServerScenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server, // 헤드리스 데디케이티드 서버 플래그
                options = BuildOptions.None
            };

            Debug.Log("[AutoBuilder] Linux Dedicated Server 빌드를 시작합니다...");
            BuildPipeline.BuildPlayer(buildPlayerOptions);
            Debug.Log("[AutoBuilder] Linux Dedicated Server 빌드 완료!");
        }

        [MenuItem("Build/4. Build ALL (Windows, Android, Linux)")]
        public static void BuildAll()
        {
            Debug.Log("[AutoBuilder] 전체 자동 빌드를 시작합니다...");
            BuildWindowsClient();
            BuildAndroidClient();
            BuildLinuxServer();
            Debug.Log("[AutoBuilder] 전체 빌드가 성공적으로 끝났습니다!");
        }

        // 경로의 폴더가 존재하지 않으면 생성해주는 유틸리티 함수
        private static void EnsureDirectoryExists(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}

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
            "Assets/Scenes/LoadingScene.unity",
            "Assets/Scenes/ServerBootScene.unity",
            "Assets/Scenes/IngameScene.unity"
        };

        private static readonly string[] ServerScenes = {
            "Assets/Scenes/ServerBootScene.unity",
            "Assets/Scenes/IngameScene.unity"
        };

        [MenuItem("Build/1. Build Windows Client")]
        public static void BuildWindowsClient()
        {
            string buildPath = "Builds/Windows/ProjectScavenger.exe";
            EnsureDirectoryExists(buildPath);

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);


            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = ClientScenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player, // 일반 클라이언트로 명시
                options = BuildOptions.Development
            };

            Debug.Log("[AutoBuilder] Windows 클라이언트 빌드를 시작합니다...");
            BuildPipeline.BuildPlayer(buildPlayerOptions);
            Debug.Log("[AutoBuilder] Windows 클라이언트 빌드 완료!");
        }

        [MenuItem("Build/2. Build Android Client")]
        public static void BuildAndroidClient()
        {
            string buildPath = "Builds/Android/ProjectScavenger.apk";
            EnsureDirectoryExists(buildPath);

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);


            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = ClientScenes,
                locationPathName = buildPath,
                target = BuildTarget.Android,
                subtarget = (int)StandaloneBuildSubtarget.Player, // 일반 클라이언트로 명시
                options = BuildOptions.AutoRunPlayer | BuildOptions.Development // 빌드 완료 후 연결된 기기에 자동 설치 및 실행 및 개발자 모드 켜기
            };

            Debug.Log("[AutoBuilder] Android 클라이언트 빌드를 시작합니다...");
            BuildPipeline.BuildPlayer(buildPlayerOptions);
            Debug.Log("[AutoBuilder] Android 클라이언트 빌드 완료!");
        }

        [MenuItem("Build/3. Build Linux Dedicated Server")]
        public static void BuildLinuxServer()
        {
            // 백엔드가 직접 실행할 수 있도록 상위 폴더의 ScavengerServer 안으로 바로 빌드합니다. (윈도우/맥 공통 호환을 위해 상대 경로 사용)
            string buildPath = "../ScavengerServer/LinuxServer/linuxBuild.x86_64";
            EnsureDirectoryExists(buildPath);

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);


            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = ServerScenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server, // 데디케이티드 서버로 명시
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

        [MenuItem("Build/5. Deploy to AWS Server")]
        public static void DeployToAWS()
        {
            UnityEngine.Debug.Log("[AutoBuilder] WSL을 통해 AWS 서버 배포를 시작합니다. 팝업되는 콘솔 창을 확인해주세요.");
            
            // Unity 프로젝트의 최상위 폴더
            string projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            
            // Windows에서 wsl 명령어 실행
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = "./deploy.sh",
                WorkingDirectory = projectRoot,
                UseShellExecute = true, // 새 창을 띄워서 진행 상황을 보여줌
            };
            
            System.Diagnostics.Process.Start(startInfo);
        }
    }
}

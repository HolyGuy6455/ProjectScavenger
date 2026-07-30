using System.Linq;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Scavenger.Network
{
    public class DedicatedServerBootstrapper : MonoBehaviour
    {
        private void Start()
        {
            if (IsDedicatedServer())
            {
                StartAsServer();
            }
        }

        private bool IsDedicatedServer()
        {
            // 유니티 실행 인자에 -server 가 포함되어 있으면 서버로 간주
            string[] args = System.Environment.GetCommandLineArgs();
            return args.Contains("-server");
        }

        private void StartAsServer()
        {
            Debug.Log("[NGO] 초기화 중... 데디케이티드 서버 전용 씬 부팅");
            
            // 포트 파싱 (-port 7777)
            string[] args = System.Environment.GetCommandLineArgs();
            int port = 7777; // 기본값
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-port" && args.Length > i + 1)
                {
                    int.TryParse(args[i + 1], out port);
                }
            }

            Debug.Log($"[NGO] 데디케이티드 서버 모드로 가동합니다. 포트: {port}");

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", (ushort)port);
            }

            // 서버 구동
            if (NetworkManager.Singleton.StartServer())
            {
                Debug.Log("[NGO] 전용 서버 구동 성공. InGameScene으로 동기화합니다.");
                NetworkManager.Singleton.SceneManager.LoadScene("InGameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[NGO] 서버 구동 실패.");
            }
        }
    }
}

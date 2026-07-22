using Unity.Netcode;
using UnityEngine;

namespace UI
{
    public class NetworkDebugUI : MonoBehaviour
    {
        public void SetIPAddress(string ipAddress)
        {
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ipAddress, transport.ConnectionData.Port);
                Debug.Log($"Server IP is changed : {ipAddress}");
            }
        }

        public void OnStartHostClicked()
        {
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.LogLevel = LogLevel.Developer;
                bool success = NetworkManager.Singleton.StartHost();
                if (success)
                    Debug.Log("Started Host successfully!");
                else
                    Debug.LogError("CRITICAL ERROR: StartHost() returned false! Host failed to start.");
            }
        }

        public void OnStartClientClicked()
        {
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.LogLevel = LogLevel.Developer;
                var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                if (transport != null)
                {
                    if (string.IsNullOrEmpty(transport.ConnectionData.Address) || transport.ConnectionData.Address == "0.0.0.0")
                    {
                        Debug.LogWarning("[Network] IP가 비어있거나 0.0.0.0입니다. 로컬 접속을 위해 127.0.0.1로 강제 설정합니다.");
                        transport.SetConnectionData("127.0.0.1", transport.ConnectionData.Port);
                    }
                    Debug.Log($"[Network] Attempting to connect to IP: {transport.ConnectionData.Address} on Port {transport.ConnectionData.Port}");
                }
                
                bool success = NetworkManager.Singleton.StartClient();
                if (success)
                    Debug.Log("Started Client successfully!");
                else
                    Debug.LogError("CRITICAL ERROR: StartClient() returned false! Client failed to start.");
            }
        }

        public void OnStartServerClicked()
        {
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.StartServer();
                Debug.Log("Started Dedicated Server");
            }
        }

        public void OnDisconnectClicked()
        {
            if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("Disconnected");
            }
        }
    }
}

using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace CustomEditorTools
{
    public class MultiplayerSetupTool
    {
        [MenuItem("Tools/Setup Multiplayer (NGO)")]
        public static void SetupMultiplayer()
        {
            // Check if NetworkManager already exists
            if (Object.FindObjectOfType<NetworkManager>() != null)
            {
                Debug.LogWarning("NetworkManager already exists in the scene!");
                return;
            }

            // Create NetworkManager GameObject
            GameObject nmGo = new GameObject("NetworkManager");
            
            // Add NetworkManager component
            NetworkManager nm = nmGo.AddComponent<NetworkManager>();
            
            // Add UnityTransport component
            UnityTransport transport = nmGo.AddComponent<UnityTransport>();
            
            // Link transport to NetworkManager
            nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            
            // Add our debug UI
            nmGo.AddComponent<UI.NetworkDebugUI>();
            
            Debug.Log("Successfully created NetworkManager with UnityTransport and NetworkDebugUI in the scene.");
            
            // Mark scene as dirty so the user can save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}

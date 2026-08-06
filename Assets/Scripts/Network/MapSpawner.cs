using UnityEngine;
using Unity.Netcode;
using Scavenger.Network;

namespace Scavenger.Core
{
    public static class MapSpawner
    {
        public static void SpawnMapObjects()
        {
            var spawnPoints = Object.FindObjectsOfType<NetworkSpawnPoint>();
            int spawnedCount = 0;

            foreach (var sp in spawnPoints)
            {
                GameObject prefab = null;
                foreach (var networkPrefab in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
                {
                    if (networkPrefab.Prefab != null && networkPrefab.Prefab.name == sp.prefabName)
                    {
                        prefab = networkPrefab.Prefab;
                        break;
                    }
                }

                if (prefab != null)
                {
                    var go = Object.Instantiate(prefab, sp.transform.position, sp.transform.rotation);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, sp.gameObject.scene);
                    var no = go.GetComponent<NetworkObject>();
                    if (no != null)
                    {
                        no.Spawn();
                        spawnedCount++;
                    }
                }
                else
                {
                    Debug.LogError($"[MapSpawner] Could not find NetworkPrefab with name {sp.prefabName} for dynamic spawning!");
                }
            }
            Debug.Log($"[MapSpawner] Successfully dynamically spawned {spawnedCount} map objects.");
        }
    }
}

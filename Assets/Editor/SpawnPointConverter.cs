using UnityEngine;
using UnityEditor;
using Scavenger.Network;
using Unity.Netcode;
using System.Collections.Generic;

namespace Scavenger.EditorTools
{
    public class SpawnPointConverter : EditorWindow
    {
        [MenuItem("Tools/Convert Stage001 Objects to Spawn Points")]
        public static void ConvertObjects()
        {
            var networkObjects = FindObjectsOfType<NetworkObject>(true);
            int convertedCount = 0;
            
            // Collect objects to process to avoid modifying collection while iterating
            var objectsToConvert = new List<GameObject>();
            foreach (var no in networkObjects)
            {
                if (no.gameObject.name.Contains("Enemy") || no.gameObject.name.Contains("Cover"))
                {
                    objectsToConvert.Add(no.gameObject);
                }
            }

            foreach (var go in objectsToConvert)
            {
                // Determine type
                bool isEnemy = go.name.Contains("Enemy");
                string originalName = go.name.Replace(" (Clone)", "").Split(' ')[0];
                if (!isEnemy && originalName.Contains("Cover"))
                {
                    originalName = "CoverObstacle"; // Default prefab name used in project
                }
                if (isEnemy) originalName = "Enemy";

                // Create new GameObject
                GameObject spawnPointGo = new GameObject(originalName + "_SpawnPoint");
                spawnPointGo.transform.position = go.transform.position;
                spawnPointGo.transform.rotation = go.transform.rotation;
                
                // Keep the parent hierarchy
                spawnPointGo.transform.SetParent(go.transform.parent);

                // Add SpawnPoint component
                var sp = spawnPointGo.AddComponent<NetworkSpawnPoint>();
                sp.spawnType = isEnemy ? NetworkSpawnPoint.SpawnType.Enemy : NetworkSpawnPoint.SpawnType.Cover;
                sp.prefabName = originalName;

                // Register undo for both creation and destruction
                Undo.RegisterCreatedObjectUndo(spawnPointGo, "Convert to Spawn Point");
                Undo.DestroyObjectImmediate(go);
                
                convertedCount++;
            }
            
            Debug.Log($"[SpawnPointConverter] Successfully converted {convertedCount} NetworkObjects into pure SpawnPoints!");
        }
    }
}

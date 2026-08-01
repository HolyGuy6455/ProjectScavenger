using UnityEngine;

namespace Scavenger.Network
{
    /// <summary>
    /// Represents a point in the map where a specific NetworkObject should be dynamically spawned by the Server.
    /// Replaces hard-coded NetworkObjects in the scene to avoid hash synchronization issues.
    /// </summary>
    public class NetworkSpawnPoint : MonoBehaviour
    {
        public enum SpawnType
        {
            Enemy,
            Cover
        }

        [Tooltip("Type of object to spawn here.")]
        public SpawnType spawnType = SpawnType.Enemy;

        [Tooltip("Optional: Name of the specific prefab to spawn. If empty, the Spawner will use a default based on the SpawnType.")]
        public string prefabName;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Draw a distinct Gizmo based on the spawn type
            if (spawnType == SpawnType.Enemy)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f); // Semi-transparent red
                Gizmos.DrawCube(transform.position + Vector3.up * 1f, new Vector3(1f, 2f, 1f));
                Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(1f, 2f, 1f));
                
                // Draw forward direction
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, transform.position + Vector3.up * 1.5f + transform.forward * 1.5f);
            }
            else if (spawnType == SpawnType.Cover)
            {
                Gizmos.color = new Color(0f, 0f, 1f, 0.5f); // Semi-transparent blue
                Gizmos.DrawCube(transform.position + Vector3.up * 0.5f, new Vector3(2f, 1f, 1f));
                Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(2f, 1f, 1f));
            }
        }
#endif
    }
}

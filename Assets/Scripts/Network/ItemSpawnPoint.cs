using UnityEngine;

namespace Scavenger.Network
{
    public class ItemSpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}

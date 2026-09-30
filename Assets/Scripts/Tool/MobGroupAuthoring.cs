using UnityEngine;

public class MobGroupAuthoring : MonoBehaviour
{
    [Header("Current Editing Information (Read Only)")]
    public string CurrentLoadedGroupID = "";
    
    [Header("Gizmo Settings")]
    public float radius = 5f;
    
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawSphere(transform.position, radius);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

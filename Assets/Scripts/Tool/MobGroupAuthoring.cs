using UnityEngine;

public class MobGroupAuthoring : MonoBehaviour
{
    [Header("Current Editing Information (Read Only)")]
    public string CurrentLoadedGroupID = "";
    
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawSphere(transform.position, 0.5f);
    }
}

using Unity.Netcode;
using UnityEngine;

/// <summary>지정한 스포너의 보스를 찾아 입장 연출을 시작하는 고정 지역입니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class BossRoomTrigger : MonoBehaviour
{
    [Tooltip("이 구역에서 등장할 보스를 생성하는 씬의 MobSpawner를 연결하세요.")]
    [SerializeField] private MobSpawner bossSpawner;

    private bool _activated;

    private void Reset()
    {
        // 입장 구역이 플레이어 조준/바닥 검사 레이캐스트에 잡히지 않게 합니다.
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(10f, 4f, 10f);
        box.center = new Vector3(0f, 2f, 0f);
    }

    private void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (bossSpawner == null)
            Debug.LogWarning("[BossRoomTrigger] Boss Spawner에 브라우니를 생성하는 MobSpawner를 연결하세요.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryActivate(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // 플레이어가 먼저 들어온 뒤 보스가 스폰되는 경우에도 다시 진입할 필요가 없습니다.
        TryActivate(other);
    }

    private void TryActivate(Collider other)
    {
        var manager = NetworkManager.Singleton;
        if (!isActiveAndEnabled || _activated || bossSpawner == null || manager == null || !manager.IsListening || !manager.IsServer)
            return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.IsSpawned) return;

        foreach (var mob in bossSpawner.SpawnedMobs)
        {
            if (mob == null) continue;
            var intro = mob.GetComponent<BossIntroController>();
            if (intro != null && intro.TryBeginIntro())
            {
                _activated = true;
                return;
            }
        }
    }

    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        var previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.55f, 0.05f, 0.15f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, 0.55f, 0.05f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previousMatrix;
    }
}

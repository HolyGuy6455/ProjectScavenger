using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Char03_Ultimate : BaseUltimateSkill
{
    [Header("Char03 Settings")]
    [SerializeField] private GameObject obstaclePrefab; // 힐 토템, 혹은 장판 프리팹
    [SerializeField] private Transform spawnPoint;      // 구조물이 소환될 위치(빈 오브젝트)

    public override void OnServerExecute(UltimateSkillData data, int eventId)
    {
        // 서버에서만 실행되는 실제 로직
        if (eventId == 0)
        {
            Debug.Log($"[Char03_Ultimate] (서버) 3번 캐릭터 구조물 설치 이벤트 실행!");

            if (obstaclePrefab != null)
            {
                // 지정된 빈 오브젝트(spawnPoint)의 위치와 회전값을 그대로 사용합니다.
                Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
                Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
                
                GameObject obstacle = Instantiate(obstaclePrefab, spawnPos, spawnRot);
                
                // 생성된 캠프파이어에 궁극기 데이터(수명 등)를 넘겨줍니다.
                Char03Campfire campfire = obstacle.GetComponent<Char03Campfire>();
                if (campfire != null)
                {
                    campfire.Initialize(data);
                }

                NetworkObject no = obstacle.GetComponent<NetworkObject>();
                if (no != null)
                {
                    no.Spawn(); // 모든 클라이언트에게 구조물 생성 동기화
                }
            }
            else
            {
                Debug.LogWarning("[Char03_Ultimate] obstaclePrefab이 연결되지 않았습니다.");
            }
        }
    }

    public override void OnClientExecute(UltimateSkillData data, bool isOwner)
    {
        // 애니메이션, 카메라 시네마틱 등 클라이언트(특히 오너) 연출
        base.OnClientExecute(data, isOwner);
        
        // TODO: 3번 캐릭터 특유의 설치 애니메이션이나 이펙트 재생 (예: 망치로 땅을 내리찍는 이펙트 등)
    }
}

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq;

namespace Scavenger.Network
{
    public class ItemSpawnArea : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Loot.tsv에 정의된 루트 테이블 ID를 입력하세요.")]
        public int lootTableId;

        private List<ItemSpawnPoint> _spawnPoints;

        private void Start()
        {
            // 오직 서버(호스트)에서만 스폰 로직을 실행합니다.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            _spawnPoints = GetComponentsInChildren<ItemSpawnPoint>().ToList();
            SpawnItems();
        }

        private void SpawnItems()
        {
            if (_spawnPoints.Count == 0 || lootTableId <= 0) return;

            LootData lootData = DataManager.Instance.GetData<LootData>("Loot", lootTableId);
            if (lootData == null || lootData.itemRef == null || lootData.chance == null) return;

            // 스폰 포인트를 섞어서 매번 무작위 위치에 등장하도록 합니다.
            ShuffleList(_spawnPoints);

            int pointIndex = 0;

            // 루트 테이블의 모든 항목에 대해 독립 확률(Probability)로 주사위를 굴립니다.
            for (int i = 0; i < lootData.itemRef.Length; i++)
            {
                string refValue = lootData.itemRef[i];
                if (string.IsNullOrEmpty(refValue) || refValue == "0") continue;

                int chance = i < lootData.chance.Length ? lootData.chance[i] : 1000;

                // 0 ~ 999의 난수와 비교합니다. (예: 500 = 50%, 1000 = 100%)
                if (Random.Range(0, 1000) < chance)
                {
                    // 당첨된 아이템을 현재 할당된 스폰 포인트에 생성합니다.
                    if (int.TryParse(refValue, out int itemId))
                    {
                        SpawnNetworkItem(itemId, _spawnPoints[pointIndex].transform.position);
                    }
                    else
                    {
                        SpawnItemByTag(refValue, _spawnPoints[pointIndex].transform.position);
                    }

                    pointIndex++;
                    
                    // 만약 당첨된 아이템 개수가 스폰 포인트 개수를 초과하면 더 이상 스폰하지 않습니다.
                    if (pointIndex >= _spawnPoints.Count)
                    {
                        break;
                    }
                }
            }
        }

        private void SpawnItemByTag(string tag, Vector3 position)
        {
            var tagItems = DataManager.Instance.GetItemsByTag(tag);
            
            if (tagItems.Count > 0)
            {
                int randomItemId = tagItems[Random.Range(0, tagItems.Count)].id;
                SpawnNetworkItem(randomItemId, position);
            }
            else
            {
                Debug.LogWarning($"[ItemSpawnArea] 기획 데이터 오류: '{tag}' 태그를 가진 아이템을 찾을 수 없습니다.");
            }
        }

        private void SpawnNetworkItem(int id, Vector3 position)
        {
            // 중앙화된 ItemInstanceManager의 스폰 로직을 사용합니다.
            ItemInstanceManager.Instance.SpawnFieldItem(id, 1, position);
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                T temp = list[i];
                int randomIndex = Random.Range(i, list.Count);
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }
    }
}

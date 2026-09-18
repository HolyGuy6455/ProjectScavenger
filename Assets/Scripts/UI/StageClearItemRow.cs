using UnityEngine;
using System.Collections.Generic;

namespace Scavenger.UI
{
    public class StageClearItemRow : MonoBehaviour
    {
        [SerializeField] private StageClearItemNode nodePrefab;
        private List<StageClearItemNode> _nodes = new List<StageClearItemNode>();

        public void AddNode(ItemData itemData, int count, bool isBonus)
        {
            if (nodePrefab == null) return;

            StageClearItemNode newNode = Instantiate(nodePrefab, transform);
            newNode.Setup(itemData, count, isBonus);
            _nodes.Add(newNode);
        }

        public void Clear()
        {
            foreach (var node in _nodes)
            {
                if (node != null) Destroy(node.gameObject);
            }
            _nodes.Clear();
        }
    }
}

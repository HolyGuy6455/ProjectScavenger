using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AIBrain : NetworkBehaviour
{
    [SerializeReference]
    public BTNode rootNode;

    [SerializeReference]
    public List<BTSharedVariable> blackboard = new List<BTSharedVariable>();

    public T GetVariable<T>(string key) where T : BTSharedVariable
    {
        if (blackboard == null) return null;
        foreach (var v in blackboard)
        {
            if (v.key == key && v is T typedVar)
            {
                return typedVar;
            }
        }
        return null;
    }

    private void Start()
    {
        // Now using the serialized rootNode created in the Editor.
        // We no longer hardcode the tree here.
    }

    private void Update()
    { 
        if (!IsServer) return; // 서버에서만 AI 로직 실행

        if (rootNode != null)
        {
            rootNode.ResetState();
            rootNode.Evaluate(this);
        }
    }
}

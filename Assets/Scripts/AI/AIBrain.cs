using System.Collections.Generic;
using UnityEngine;

public class AIBrain : MonoBehaviour
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
        if (rootNode != null)
        {
            rootNode.ResetState();
            rootNode.Evaluate(this);
        }
    }
}

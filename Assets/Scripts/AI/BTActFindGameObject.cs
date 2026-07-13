using System;
using UnityEngine;

public enum FindMethod
{
    ByTag,
    ByName
}

[Serializable]
public class BTActFindGameObject : BTActionLogic
{
    public FindMethod findMethod = FindMethod.ByTag;
    public string targetStringKey = "";
    public string outGameObjectKey = "";
    
    public bool findClosest = false;
    public string mySelfKey = "";

    public override string GetDescription(AIBrain brain)
    {
        string methodStr = findMethod == FindMethod.ByTag ? "Tag" : "Name";
        string targetStr = GetKeyValueString<BTSharedString>(brain, targetStringKey);
        
        string closestStr = findClosest ? $"\n('{mySelfKey}'에서 가장 가까운 오브젝트 우선선택)" : "";
        
        return $"{methodStr}가 {targetStr}인 게임 오브젝트를 찾아 '{outGameObjectKey}'에 저장합니다. {closestStr}";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var stringProp = brain.GetVariable<BTSharedString>(targetStringKey);
        var outGoProp = brain.GetVariable<BTSharedGameObject>(outGameObjectKey);

        if (stringProp == null || outGoProp == null)
        {
            Debug.LogError("[BTActFindGameObject] String key or out GameObject key is missing!");
            return NodeState.FAILURE;
        }

        string searchStr = stringProp.value;
        if (string.IsNullOrEmpty(searchStr)) return NodeState.FAILURE;

        GameObject[] foundObjs = null;

        if (findMethod == FindMethod.ByTag)
        {
            try {
                foundObjs = GameObject.FindGameObjectsWithTag(searchStr);
            } catch (UnityException) {
                // Occurs if the tag is not defined
                return NodeState.FAILURE;
            }
        }
        else
        {
            GameObject singleFound = GameObject.Find(searchStr);
            if (singleFound != null)
                foundObjs = new GameObject[] { singleFound };
        }

        if (foundObjs == null || foundObjs.Length == 0)
        {
            outGoProp.value = null;
            return NodeState.FAILURE;
        }

        if (findClosest)
        {
            var mySelfProp = brain.GetVariable<BTSharedGameObject>(mySelfKey);
            if (mySelfProp != null && mySelfProp.value != null)
            {
                Vector3 myPos = mySelfProp.value.transform.position;
                GameObject closestObj = null;
                float closestDist = float.MaxValue;

                foreach (var obj in foundObjs)
                {
                    float dist = Vector3.SqrMagnitude(obj.transform.position - myPos);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestObj = obj;
                    }
                }
                outGoProp.value = closestObj;
                return NodeState.SUCCESS;
            }
        }

        // If not finding closest or mySelfKey is invalid, just return the first one found
        outGoProp.value = foundObjs[0];
        return NodeState.SUCCESS;
    }
}

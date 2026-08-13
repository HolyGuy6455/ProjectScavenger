using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MobGroupData
{
    public string GroupID;
    public string Memo;
    public MobCategory Category;
    public int DangerLevel;
    public float AreaSize;
    public List<MobSpawnInfo> SpawnInfos = new List<MobSpawnInfo>();
}

[Serializable]
public struct MobSpawnInfo
{
    public string MobID;
    public Vector3 LocalPosition;
    public Vector3 LocalEulerAngles;
}

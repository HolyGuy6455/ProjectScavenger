using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GunData : IData
{
    public string gunId;
    public string gunName;
    public float damage;
    public float fireRate;
    public int maxAmmo;

    public string GetId()
    {
        return gunId;
    }
}

[System.Serializable]
public class GunDataList : IDataList<GunData>
{
    // JsonUtility는 최상위 배열을 지원하지 않으므로 List를 감싸는 래퍼 클래스가 필요합니다.
    public List<GunData> guns;

    public List<GunData> GetList()
    {
        return guns;
    }
}

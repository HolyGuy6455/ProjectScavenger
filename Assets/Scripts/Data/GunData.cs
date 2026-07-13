using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GunData : IData
{
    public string id;
    public string gunName;
    public float SearchRadius;
    public float SearchAngle;
    public float damage;
    public int bulletsPerShot;
    public float bulletDegree;
    public float fireRate;
    public float spreadPerShot;
    public float maxSpread;
    public int maxAmmo;

    public string GetId()
    {
        return id;
    }
}

[System.Serializable]
public class GunDataList : IDataList<GunData>
{
    public List<GunData> guns = new List<GunData>();

    public List<GunData> GetList()
    {
        return guns;
    }
}

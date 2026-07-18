using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GunData : IData
{
    public int id;
    public string gunName;
    public int gunGroup;
    public int shotType;
    public float SearchRadius;
    public float SearchAngle;
    public float ExplosionRadius;
    public float damage;
    public int bulletsPerShot;
    public float bulletDegree;
    public float fireRate;
    public float spreadPerShot;
    public float maxSpread;
    public float spreadRecoveryRate = 1f;
    public int maxAmmo;
    public string resourceSilhouette;

    public int GetId()
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

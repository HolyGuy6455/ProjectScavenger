using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class AmmoData : IData
{
    public int id;
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
    public int specialBuff;

    public int GetId()
    {
        return id;
    }
}

[System.Serializable]
public class AmmoDataList : IDataList<AmmoData>
{
    public List<AmmoData> ammos = new List<AmmoData>();

    public List<AmmoData> GetList()
    {
        return ammos;
    }
}

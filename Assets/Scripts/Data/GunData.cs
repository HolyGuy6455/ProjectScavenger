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

    // JSON에 값이 없어 0으로 초기화되었을 때 기본값을 반환하도록 프로퍼티 구현
    public float searchRadius;
    public float searchAngle;

    public float SearchRadius => searchRadius > 0f ? searchRadius : 15f;
    public float SearchAngle => searchAngle > 0f ? searchAngle : 90f;

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

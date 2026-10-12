using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    // 카테고리별로 데이터를 분리하여 저장하는 이중 딕셔너리
    // 구조: <Category, <ID, IData>>
    private Dictionary<string, Dictionary<int, IData>> categoryDataMap = new Dictionary<string, Dictionary<int, IData>>();

    // 몹 스폰 그룹 데이터 리스트 (JSON)
    private List<MobGroupData> allMobGroups = new List<MobGroupData>();

    public bool IsDataLoaded { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // TODO: 나중에 전체 데이터 로드 시점(예: 로딩 화면, 타이틀 화면 등)이 정해지면 
        // 아래 로직은 외부 흐름 관리자에서 직접 호출해야 합니다.
        if (categoryDataMap.Count == 0)
        {
            StartCoroutine(LoadAllInitialDataAsync());
        }
    }

    /// <summary>
    /// 게임 시작에 필요한 초기 데이터들을 일괄 비동기 로드하는 매서드입니다.
    /// </summary>
    public IEnumerator LoadAllInitialDataAsync()
    {
        IsDataLoaded = false;

        // 어드레서블 주소(Address)는 에디터에서 세팅한 주소(보통 확장자가 빠진 이름)를 사용합니다.
        yield return StartCoroutine(LoadDataAsync<AmmoDataList, AmmoData>("Ammo", "AmmoData"));
        yield return StartCoroutine(LoadDataAsync<ItemDataList, ItemData>("Item", "ItemData"));
        yield return StartCoroutine(LoadDataAsync<UserCurrencyDataList, UserCurrencyData>("UserCurrency", "UserCurrencyData"));
        yield return StartCoroutine(LoadDataAsync<CharDataList, CharData>("Char", "CharData"));
        yield return StartCoroutine(LoadDataAsync<SkillDataList, SkillData>("Skill", "SkillData"));
        yield return StartCoroutine(LoadDataAsync<LootDataList, LootData>("Loot", "LootData"));
        yield return StartCoroutine(LoadDataAsync<EnemyDataList, EnemyData>("Enemy", "EnemyData"));
        yield return StartCoroutine(LoadDataAsync<ObstacleDataList, ObstacleData>("Obstacle", "ObstacleData"));
        yield return StartCoroutine(LoadDataAsync<LevelDataList, LevelData>("Level", "LevelData"));
        yield return StartCoroutine(LoadDataAsync<QuestDataList, QuestData>("Quest", "QuestData"));

        // 몹 그룹 데이터 로드 (하나로 합쳐진 어드레서블 'MobGroupData' 파일 사용)
        yield return StartCoroutine(LoadMobGroupsAsync());

        InjectDataRelations();

        IsDataLoaded = true;
        Debug.Log("[DataManager] All Initial Data Loaded Successfully via Addressables.");
    }

    private void InjectDataRelations()
    {
        // 2. 스킬 데이터 주입
        foreach (var skillData in GetAllData<SkillData>("Skill"))
        {
            var charData = GetData<CharData>("Char", skillData.charId);
            if (charData != null)
            {
                charData.skill.Add(skillData.id);
                if (skillData.func == 1)
                {
                    charData.ultimateSkill = skillData.id;
                }
            }
        }
        
        Debug.Log("[DataManager] Data Relations Injected Successfully.");
    }

    /// <summary>
    /// 어드레서블 "MobGroupData" 파일을 비동기로 로드하고 복호화하여 파싱합니다.
    /// </summary>
    public IEnumerator LoadMobGroupsAsync()
    {
        allMobGroups.Clear();
        
        var handle = Addressables.LoadAssetAsync<TextAsset>("MobGroupData");
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            try
            {
                string decryptedJson = CryptoUtility.Decrypt(handle.Result.bytes);
                MobGroupDataList dataList = JsonUtility.FromJson<MobGroupDataList>(decryptedJson);
                
                if (dataList != null && dataList.Groups != null)
                {
                    allMobGroups = dataList.Groups;
                    Debug.Log($"[DataManager] Successfully loaded {allMobGroups.Count} Mob Groups from Addressables.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DataManager] Failed to parse MobGroupData: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[DataManager] Failed to load MobGroupData via Addressables.");
        }

        Addressables.Release(handle);
    }

    /// <summary>
    /// 조건에 맞는 몹 그룹 중 하나를 무작위로 뽑아서 반환합니다.
    /// </summary>
    public MobGroupData GetRandomMatchingGroup(MobCategory allowedCategories, int maxDangerLevel, float maxAreaSize)
    {
        if (allMobGroups.Count == 0)
        {
            Debug.LogWarning("[DataManager] No mob groups loaded!");
            return null;
        }

        var validGroups = allMobGroups.Where(g => 
            (g.Category & allowedCategories) != 0 &&
            g.DangerLevel <= maxDangerLevel &&
            g.AreaSize <= maxAreaSize
        ).ToList();

        if (validGroups.Count == 0)
        {
            Debug.LogWarning($"[DataManager] No mob group matches criteria: Category={allowedCategories}, Danger<={maxDangerLevel}, Size<={maxAreaSize}");
            return null;
        }

        int randomIndex = Random.Range(0, validGroups.Count);
        return validGroups[randomIndex];
    }

    /// <summary>
    /// 어드레서블을 이용해 데이터를 비동기 로드합니다.
    /// </summary>
    public IEnumerator LoadDataAsync<TList, TData>(string category, string address) 
        where TList : IDataList<TData> 
        where TData : class, IData
    {
        var handle = Addressables.LoadAssetAsync<TextAsset>(address);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            ParseEncryptedData<TList, TData>(category, handle.Result.bytes);
        }
        else
        {
            Debug.LogError($"[DataManager] Addressable load failed for address: {address}");
        }

        Addressables.Release(handle);
    }

    private void ParseEncryptedData<TList, TData>(string category, byte[] encryptedBytes)
        where TList : IDataList<TData>
        where TData : class, IData
    {
        try
        {
            // 1. 복호화
            string decryptedJson = CryptoUtility.Decrypt(encryptedBytes);
            
            // 2. JSON 파싱
            TList dataList = JsonUtility.FromJson<TList>(decryptedJson);

            // 3. 맵 초기화 및 데이터 캐싱
            if (!categoryDataMap.ContainsKey(category))
            {
                categoryDataMap[category] = new Dictionary<int, IData>();
            }
            
            Dictionary<int, IData> targetMap = categoryDataMap[category];
            targetMap.Clear(); // 새로운 데이터 파일 로드 시 해당 카테고리 데이터 초기화

            if (dataList != null && dataList.GetList() != null)
            {
                foreach (TData data in dataList.GetList())
                {
                    targetMap[data.GetId()] = data;
                }
                Debug.Log($"[DataManager] Successfully loaded {targetMap.Count} '{category}' datas.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DataManager] Failed to parse data for category '{category}': {e.Message}");
        }
    }

    /// <summary>
    /// 특정 카테고리와 ID를 가진 데이터를 제네릭 형태로 가져옵니다.
    /// 사용 예: GunData myGun = DataManager.Instance.GetData<GunData>("Gun", 2);
    /// </summary>
    public T GetData<T>(string category, int id) where T : class, IData
    {
        if (categoryDataMap.TryGetValue(category, out var dataMap))
        {
            if (dataMap.TryGetValue(id, out IData data))
            {
                return data as T;
            }
        }
        
        Debug.LogWarning($"[DataManager] Data not found - Category: {category}, ID: {id}");
        return null;
    }

    /// <summary>
    /// 특정 카테고리의 모든 데이터를 리스트 형태로 가져옵니다.
    /// </summary>
    public List<T> GetAllData<T>(string category) where T : class, IData
    {
        List<T> list = new List<T>();
        if (categoryDataMap.TryGetValue(category, out var dataMap))
        {
            foreach (var data in dataMap.Values)
            {
                if (data is T tData)
                {
                    list.Add(tData);
                }
            }
        }
        return list;
    }

    /// <summary>
    /// 지정한 태그를 포함하고 있는 모든 아이템(ItemData) 리스트를 반환합니다.
    /// </summary>
    public List<ItemData> GetItemsByTag(string tag)
    {
        List<ItemData> allItems = GetAllData<ItemData>("Item");
        if (allItems == null) return new List<ItemData>();

        return allItems.Where(item => item.tags != null && item.tags.Contains(tag)).ToList();
    }
}

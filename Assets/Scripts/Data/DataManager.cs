using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    // 카테고리별로 데이터를 분리하여 저장하는 이중 딕셔너리
    // 구조: <Category, <ID, IData>>
    private Dictionary<string, Dictionary<int, IData>> categoryDataMap = new Dictionary<string, Dictionary<int, IData>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // TODO: 나중에 전체 데이터 로드 시점(예: 로딩 화면, 타이틀 화면 등)이 정해지면 
        // 아래 로직은 외부 흐름 관리자에서 직접 LoadAllInitialData()를 호출해야 합니다.
        if (categoryDataMap.Count == 0)
        {
            LoadAllInitialData();
        }
    }

    /// <summary>
    /// 게임 시작에 필요한 초기 데이터들을 일괄 로드하는 매서드입니다.
    /// 차후 다른 데이터(적 데이터, 아이템 데이터 등)가 생기면 이곳에 추가하여 한꺼번에 로드할 수 있습니다.
    /// </summary>
    public void LoadAllInitialData()
    {
        LoadData<GunDataList, GunData>("Gun", "GunData.dat");
        LoadData<ItemDataList, ItemData>("Item", "ItemData.dat");
        LoadData<CharDataList, CharData>("Char", "CharData.dat");
        LoadData<SkillDataList, SkillData>("Skill", "SkillData.dat");
        LoadData<UltimateSkillDataList, UltimateSkillData>("UltimateSkill", "UltimateSkillData.dat");
        // LoadData<EnemyDataList, EnemyData>("Enemy", "EnemyData.dat");
    }

    /// <summary>
    /// 지정된 카테고리의 데이터를 지정된 파일명(.dat)을 통해 에디터의 Data 폴더나 실제 빌드 기기의 저장소에서 찾아 로드합니다.
    /// 사용 예: DataManager.Instance.LoadData<GunDataList, GunData>("Gun", "GunData.dat");
    /// </summary>
    public void LoadData<TList, TData>(string category, string fileName) 
        where TList : IDataList<TData> 
        where TData : class, IData
    {
        // 1순위: 기기에 다운로드 받은 파일이 있는지 확인 (패치 적용 시)
        string persistentPath = Path.Combine(Application.persistentDataPath, fileName);
        
        // 2순위: 유니티 에디터 로컬 환경의 Data 폴더
        string editorDataPath = Path.Combine(Application.dataPath, "Data", fileName);

        string targetPath = string.Empty;

        if (File.Exists(persistentPath))
        {
            targetPath = persistentPath;
        }
        else if (File.Exists(editorDataPath))
        {
            targetPath = editorDataPath;
        }
        else
        {
            Debug.LogError($"[DataManager] '{fileName}'을 찾을 수 없습니다.\n확인한 경로:\n1. {persistentPath}\n2. {editorDataPath}");
            return;
        }

        LoadDataFromFile<TList, TData>(category, targetPath);
    }

    /// <summary>
    /// 파일 절대 경로를 통해 데이터를 로드하고 특정 카테고리에 매핑합니다.
    /// </summary>
    public void LoadDataFromFile<TList, TData>(string category, string filePath)
        where TList : IDataList<TData>
        where TData : class, IData
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[DataManager] Data file not found at: {filePath}");
            return;
        }

        byte[] encryptedBytes = File.ReadAllBytes(filePath);
        ParseEncryptedData<TList, TData>(category, encryptedBytes);
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
}

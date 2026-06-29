using UnityEngine;
using UnityEditor;
using System.IO;

public class DataConverterEditor : EditorWindow
{
    [MenuItem("Tools/Data/Convert JSON to Binary")]
    public static void ShowWindow()
    {
        GetWindow<DataConverterEditor>("Data Converter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Data Converter", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Convert Gun Data"))
        {
            ConvertGunData();
        }

        if (GUILayout.Button("Convert Item Data"))
        {
            ConvertItemData();
        }
        
        GUILayout.Space(10);

        if (GUILayout.Button("Convert All Data"))
        {
            ConvertAllData();
        }
    }

    private void ConvertGunData()
    {
        // 1. JSON 파일 선택
        string inputPath = EditorUtility.OpenFilePanel("Select Gun JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(inputPath)) return;

        string jsonContent = File.ReadAllText(inputPath);

        // 2. 파싱 테스트 (JSON 포맷 검증)
        try
        {
            GunDataList testParse = JsonUtility.FromJson<GunDataList>(jsonContent);
            if (testParse == null || testParse.guns == null || testParse.guns.Count == 0)
            {
                Debug.LogWarning("JSON 텍스트가 GunDataList 형태( {\"guns\": [...]} )가 아니거나 데이터가 비어있을 수 있습니다.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JSON 파싱 에러: {e.Message}\n입력된 파일이 올바른 형식이 아닙니다.");
            return;
        }

        // 3. 암호화 진행
        byte[] encryptedBytes = CryptoUtility.Encrypt(jsonContent);

        // 4. 저장할 경로 지정 (이제 Resources 제약이 없으므로 Data 폴더에 .dat로 저장)
        string dataPath = Application.dataPath + "/Data";
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        string savePath = EditorUtility.SaveFilePanel("Save Encrypted Data", dataPath, "GunData", "dat");
        if (string.IsNullOrEmpty(savePath)) return;

        // 5. 이진 파일 쓰기
        File.WriteAllBytes(savePath, encryptedBytes);
        
        AssetDatabase.Refresh();
        Debug.Log($"Successfully converted and encrypted GunData to: {savePath}");
    }

    private void ConvertItemData()
    {
        string inputPath = EditorUtility.OpenFilePanel("Select Item JSON", Application.dataPath, "json");
        if (string.IsNullOrEmpty(inputPath)) return;

        string jsonContent = File.ReadAllText(inputPath);

        try
        {
            ItemDataList testParse = JsonUtility.FromJson<ItemDataList>(jsonContent);
            if (testParse == null || testParse.items == null || testParse.items.Count == 0)
            {
                Debug.LogWarning("JSON 텍스트가 ItemDataList 형태가 아니거나 데이터가 비어있을 수 있습니다.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JSON 파싱 에러: {e.Message}\n입력된 파일이 올바른 형식이 아닙니다.");
            return;
        }

        byte[] encryptedBytes = CryptoUtility.Encrypt(jsonContent);

        string dataPath = Application.dataPath + "/Data";
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        string savePath = EditorUtility.SaveFilePanel("Save Encrypted Data", dataPath, "ItemData", "dat");
        if (string.IsNullOrEmpty(savePath)) return;

        File.WriteAllBytes(savePath, encryptedBytes);
        
        AssetDatabase.Refresh();
        Debug.Log($"Successfully converted and encrypted ItemData to: {savePath}");
    }

    private void ConvertAllData()
    {
        string dataPath = Application.dataPath + "/Data";
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        // Convert Gun Data
        string gunJsonPath = Application.dataPath + "/test_gun.json";
        if (File.Exists(gunJsonPath))
        {
            string jsonContent = File.ReadAllText(gunJsonPath);
            byte[] encryptedBytes = CryptoUtility.Encrypt(jsonContent);
            string savePath = dataPath + "/GunData.dat";
            File.WriteAllBytes(savePath, encryptedBytes);
            Debug.Log($"Auto-converted GunData to: {savePath}");
        }
        else
        {
            Debug.LogWarning($"Could not find {gunJsonPath}");
        }

        // Convert Item Data
        string itemJsonPath = Application.dataPath + "/test_item.json";
        if (File.Exists(itemJsonPath))
        {
            string jsonContent = File.ReadAllText(itemJsonPath);
            byte[] encryptedBytes = CryptoUtility.Encrypt(jsonContent);
            string savePath = dataPath + "/ItemData.dat";
            File.WriteAllBytes(savePath, encryptedBytes);
            Debug.Log($"Auto-converted ItemData to: {savePath}");
        }
        else
        {
            Debug.LogWarning($"Could not find {itemJsonPath}");
        }

        AssetDatabase.Refresh();
        Debug.Log("Finished Convert All Data operation.");
    }
}

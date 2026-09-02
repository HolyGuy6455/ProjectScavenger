using UnityEngine;
using System.Collections.Generic;
using Scavenger.Network.DTOs;

public class TrainCharacterPanel : MonoBehaviour
{
    [SerializeField] private Transform leftCharacterRack;
    [SerializeField] private GameObject polaroidPrefab;
    
    private List<GameObject> _spawnedPolaroids = new List<GameObject>();
    private readonly float[] _polaroidRotations = { 2f, -3f, 1.5f, -2f };

    private void OnEnable()
    {
        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.OnCharacterDataUpdated += RefreshUI;
            // 만약 이미 데이터가 로드되어 있다면 바로 UI 갱신
            if (UserDataManager.Instance.OwnedCharacters.Count > 0)
            {
                RefreshUI();
            }
        }
    }

    private void OnDisable()
    {
        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.OnCharacterDataUpdated -= RefreshUI;
        }
    }

    private void RefreshUI()
    {
        ClearRack();

        if (UserDataManager.Instance == null || DataManager.Instance == null) return;

        var ownedCharacters = UserDataManager.Instance.OwnedCharacters;

        foreach (var charDto in ownedCharacters)
        {
            CharData masterData = DataManager.Instance.GetData<CharData>("Char", charDto.charId);
            if (masterData != null)
            {
                SpawnPolaroid(charDto, masterData);
            }
        }
    }

    private void ClearRack()
    {
        foreach (var obj in _spawnedPolaroids)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedPolaroids.Clear();

        // 에디터에서 미리 만들어둔 더미 오브젝트가 있다면 삭제
        foreach (Transform child in leftCharacterRack)
        {
            Destroy(child.gameObject);
        }
    }

    private void SpawnPolaroid(UserCharacterDto charDto, CharData masterData)
    {
        if (polaroidPrefab == null || leftCharacterRack == null) return;

        GameObject obj = Instantiate(polaroidPrefab, leftCharacterRack);
        
        float zRot = _polaroidRotations[_spawnedPolaroids.Count % _polaroidRotations.Length];
        obj.transform.localRotation = Quaternion.Euler(0, 0, zRot);

        _spawnedPolaroids.Add(obj);

        TrainCharacterPolaroid polaroid = obj.GetComponent<TrainCharacterPolaroid>();
        if (polaroid != null)
        {
            Sprite icon = LoadSprite(masterData.resourceSlot);
            polaroid.Setup(charDto, masterData, icon);
            polaroid.OnPolaroidClicked += HandlePolaroidClicked;
        }
    }

    private void HandlePolaroidClicked(UserCharacterDto charDto, CharData masterData)
    {
        Debug.Log($"[TrainCharacterPanel] 캐릭터 선택됨: {masterData.name} (Lv.{charDto.level})");
        // TODO: 우측 패널에 해당 캐릭터의 상세 정보와 스킬 정보를 표시하는 로직 추가
    }

    private Sprite LoadSprite(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;
        string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(resourceName);
        
        Sprite loadedSprite = Resources.Load<Sprite>($"char/{nameWithoutExt}");
        if (loadedSprite == null)
        {
            loadedSprite = Resources.Load<Sprite>(nameWithoutExt);
        }
        return loadedSprite;
    }
}

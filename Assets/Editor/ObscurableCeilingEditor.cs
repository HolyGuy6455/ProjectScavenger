using UnityEngine;
using UnityEditor;
using Scavenger.Environment;

public class ObscurableCeilingEditor
{
    [MenuItem("Tools/Environment/Hide All Ceilings")]
    public static void HideAllCeilings()
    {
        // 씬 내의 비활성화된 오브젝트까지 모두 포함해서 컴포넌트를 찾습니다.
        var ceilings = Resources.FindObjectsOfTypeAll<ObscurableCeiling>();
        int count = 0;

        foreach (var ceiling in ceilings)
        {
            // 씬에 존재하는 오브젝트만 처리 (프로젝트 폴더 내의 프리팹 에셋 제외)
            if (ceiling.gameObject.hideFlags == HideFlags.NotEditable || ceiling.gameObject.hideFlags == HideFlags.HideAndDontSave)
                continue;
            
            if (EditorUtility.IsPersistent(ceiling.transform.root.gameObject))
                continue;

            if (ceiling.gameObject.activeSelf)
            {
                // Undo(Ctrl+Z) 기능을 위해 변경 사항 기록
                Undo.RecordObject(ceiling.gameObject, "Hide Ceiling");
                ceiling.gameObject.SetActive(false);
                count++;
            }
        }

        Debug.Log($"[ObscurableCeilingEditor] {count}개의 천장을 비활성화(Hide) 했습니다.");
    }

    [MenuItem("Tools/Environment/Show All Ceilings")]
    public static void ShowAllCeilings()
    {
        var ceilings = Resources.FindObjectsOfTypeAll<ObscurableCeiling>();
        int count = 0;

        foreach (var ceiling in ceilings)
        {
            if (ceiling.gameObject.hideFlags == HideFlags.NotEditable || ceiling.gameObject.hideFlags == HideFlags.HideAndDontSave)
                continue;

            if (EditorUtility.IsPersistent(ceiling.transform.root.gameObject))
                continue;

            if (!ceiling.gameObject.activeSelf)
            {
                Undo.RecordObject(ceiling.gameObject, "Show Ceiling");
                ceiling.gameObject.SetActive(true);
                count++;
            }
        }

        Debug.Log($"[ObscurableCeilingEditor] {count}개의 천장을 활성화(Show) 했습니다.");
    }
}

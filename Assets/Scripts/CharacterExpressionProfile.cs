using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>UV0의 sourceRect를 표정별 atlas rect로 옮기는 캐릭터별 데이터입니다.</summary>
[CreateAssetMenu(menuName = "Characters/Expression Profile")]
public sealed class CharacterExpressionProfile : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public int index;
        public string id;
        public string displayName;
        public Rect rect;
    }

    [SerializeField] private string characterId;
    [SerializeField] private Texture2D atlasTexture;
    [SerializeField] private Rect sourceRect = new Rect(0, 0, 1, 1);
    [SerializeField] private string defaultId = "normal";
    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    public string CharacterId => characterId;
    public Texture2D AtlasTexture => atlasTexture;
    public Rect SourceRect => sourceRect;
    public string DefaultId => defaultId;
    public IReadOnlyList<Entry> Entries => entries;
    public int DefaultIndex => TryGetEntry(defaultId, out Entry entry) ? entry.index : 0;

    public bool TryGetEntry(int index, out Entry entry)
    {
        foreach (Entry candidate in entries)
        {
            if (candidate.index != index) continue;
            entry = candidate;
            return true;
        }
        entry = default;
        return false;
    }

    public bool TryGetEntry(string id, out Entry entry)
    {
        foreach (Entry candidate in entries)
        {
            if (!string.Equals(candidate.id, id, StringComparison.Ordinal)) continue;
            entry = candidate;
            return true;
        }
        entry = default;
        return false;
    }

    public Vector4 GetTextureTransform(Entry entry)
    {
        // 이미 기본 타일에 들어간 UV에는 scale=1입니다. 정규화한 얼굴 UV도 지원합니다.
        return CalculateTextureTransform(sourceRect, entry.rect);
    }

    public static Vector4 CalculateTextureTransform(Rect source, Rect target)
    {
        if (!IsValidRect(source) || !IsValidRect(target))
            throw new ArgumentException("Expression UV rectangles must be finite, positive and inside 0..1.");
        float scaleX = target.width / source.width;
        float scaleY = target.height / source.height;
        return new Vector4(scaleX, scaleY,
            target.x - source.x * scaleX, target.y - source.y * scaleY);
    }

    /// <summary>에디터 이관 도구에서 검증 후 프로필을 설정합니다. 기존 데이터는 실패 시 유지됩니다.</summary>
    public void Configure(string character, Texture2D atlas, Rect source, string defaultExpression,
        Entry[] expressionEntries)
    {
        if (!Validate(atlas, source, defaultExpression, expressionEntries, out string error))
            throw new ArgumentException(error);
        characterId = character;
        atlasTexture = atlas;
        sourceRect = source;
        defaultId = defaultExpression;
        entries = (Entry[])expressionEntries.Clone();
    }

    public bool TryValidate(out string error)
    {
        return Validate(atlasTexture, sourceRect, defaultId, entries, out error);
    }

    private static bool Validate(Texture2D atlas, Rect source, string defaultExpression,
        Entry[] expressionEntries, out string error)
    {
        error = null;
        if (atlas == null) error = "Expression atlas texture is missing.";
        else if (!IsValidRect(source)) error = "Expression sourceRect is invalid.";
        else if (expressionEntries == null || expressionEntries.Length == 0)
            error = "Expression entries are empty.";
        if (error != null) return false;

        var indices = new HashSet<int>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Entry entry in expressionEntries)
        {
            if (entry.index < 0 || !indices.Add(entry.index))
                error = $"Expression index {entry.index} is negative or duplicated.";
            else if (string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id))
                error = $"Expression ID '{entry.id}' is empty or duplicated.";
            else if (!IsValidRect(entry.rect)) error = $"Expression '{entry.id}' has an invalid rect.";
            if (error != null) return false;
        }
        if (!ids.Contains(defaultExpression ?? string.Empty))
            error = $"Default expression '{defaultExpression}' is not registered.";
        return error == null;
    }

    private static bool IsValidRect(Rect value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.width) && IsFinite(value.height)
            && value.width > 0 && value.height > 0 && value.x >= 0 && value.y >= 0
            && value.xMax <= 1.00001f && value.yMax <= 1.00001f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

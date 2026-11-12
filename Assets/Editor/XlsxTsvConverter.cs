using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

public static class XlsxTsvConverter
{
    public const string TsvPrefix = "Scavenger - ";
    public const string TsvExtension = ".tsv";
    private const string XlsxPathPrefKey = "Scavenger.DataConverter.XlsxPath";

    private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";

    // Excel-only reference sheets that should never be exported as TSV files and are always preserved as-is.
    private static readonly HashSet<string> HelperSheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SoundRef",
        "DescList"
    };

    // Canonical table ordering matching Scavenger.xlsx tabs.
    public static readonly string[] DefaultSheetOrder =
    {
        "Char",
        "CharLvl",
        "Level",
        "Ammo",
        "Skill",
        "Buff",
        "Item",
        "Loot",
        "Enemy",
        "Obstacle",
        "UserCurrency",
        "Quest",
        "Stage",
        "Localization"
    };

    private static readonly Regex FormulaCellRefRegex = new Regex(
        @"(""[^""]*"")|(?<![A-Za-z0-9_$])(\$?[A-Z]{1,3})(\$?)(\d+)(?![A-Za-z0-9_!(])",
        RegexOptions.Compiled);

    private static readonly Regex CleanFloatRegex = new Regex(
        @"^-?(0|[1-9]\d*)\.\d+$",
        RegexOptions.Compiled);

    public enum SyncState
    {
        Identical,
        CommentOnlyDiff,
        DataDiff,
        MissingInXlsx,
        MissingInTsv,
        XlsxNotFound
    }

    public class SheetComparison
    {
        public string SheetName;
        public SyncState State;
        public int XlsxRowCount;
        public int TsvRowCount;
        public int DataDiffRows;
        public int CommentDiffRows;
        public DateTime XlsxLastWriteUtc;
        public DateTime TsvLastWriteUtc;

        public bool IsXlsxFileNewer => XlsxLastWriteUtc > TsvLastWriteUtc.AddSeconds(1);
        public bool IsTsvFileNewer => TsvLastWriteUtc > XlsxLastWriteUtc.AddSeconds(1);
    }

    public static string GetDefaultXlsxPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Temp", "Data", "Scavenger.xlsx"));
    }

    public static string GetXlsxPath()
    {
        string saved = EditorPrefs.GetString(XlsxPathPrefKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(saved))
        {
            return saved;
        }
        return GetDefaultXlsxPath();
    }

    public static void SetXlsxPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            EditorPrefs.DeleteKey(XlsxPathPrefKey);
        }
        else
        {
            EditorPrefs.SetString(XlsxPathPrefKey, Path.GetFullPath(path));
        }
    }

    public static string GetTsvDirectory()
    {
        return Path.Combine(Application.dataPath, "Data");
    }

    public static string GetTsvPath(string sheetName)
    {
        return Path.Combine(GetTsvDirectory(), $"{TsvPrefix}{sheetName}{TsvExtension}");
    }

    public static bool IsHelperSheet(string sheetName)
    {
        return HelperSheets.Contains(sheetName);
    }

    public static bool CanOpenForWrite(string filePath)
    {
        if (!File.Exists(filePath))
        {
            string dir = Path.GetDirectoryName(filePath);
            return !string.IsNullOrEmpty(dir) && Directory.Exists(dir);
        }

        try
        {
            using (new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                return true;
            }
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static List<string> GetAllManagedSheetNames(string xlsxPath = null)
    {
        var result = new List<string>(DefaultSheetOrder);
        var seen = new HashSet<string>(DefaultSheetOrder, StringComparer.OrdinalIgnoreCase);

        string tsvDir = GetTsvDirectory();
        if (Directory.Exists(tsvDir))
        {
            foreach (string file in Directory.GetFiles(tsvDir, $"{TsvPrefix}*{TsvExtension}"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith(TsvPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string name = fileName.Substring(TsvPrefix.Length).Trim();
                    if (!string.IsNullOrEmpty(name) && !IsHelperSheet(name) && seen.Add(name))
                    {
                        result.Add(name);
                    }
                }
            }
        }

        string targetXlsx = xlsxPath ?? GetXlsxPath();
        if (File.Exists(targetXlsx))
        {
            try
            {
                var xlsxSheets = ReadSheetNamesFromXlsx(targetXlsx);
                foreach (string name in xlsxSheets)
                {
                    if (!IsHelperSheet(name) && seen.Add(name))
                    {
                        result.Add(name);
                    }
                }
            }
            catch
            {
                // Ignore read failure during discovery; CompareAll will surface status.
            }
        }

        return result;
    }

    public static List<string> ReadSheetNamesFromXlsx(string xlsxPath)
    {
        var names = new List<string>();
        using var ms = ReadFileToMemoryStream(xlsxPath);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var wbEntry = archive.GetEntry("xl/workbook.xml");
        if (wbEntry == null) return names;

        var wbDoc = LoadXDocument(wbEntry);
        foreach (var sheetEl in wbDoc.Descendants(MainNs + "sheet"))
        {
            string name = sheetEl.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }
        return names;
    }

    public static Dictionary<string, List<string[]>> ReadAllDataSheetsFromXlsx(string xlsxPath)
    {
        var result = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        using var ms = ReadFileToMemoryStream(xlsxPath);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        List<string> sharedStrings = ReadSharedStrings(archive);
        var sheetMap = ReadWorkbookSheetPaths(archive);

        foreach (var kvp in sheetMap)
        {
            string sheetName = kvp.Key;
            if (IsHelperSheet(sheetName)) continue;

            var wsEntry = archive.GetEntry(kvp.Value);
            if (wsEntry == null) continue;

            var wsDoc = LoadXDocument(wsEntry);
            result[sheetName] = ParseWorksheetToRows(wsDoc, sharedStrings);
        }

        return result;
    }

    public static List<SheetComparison> CompareAll(string xlsxPath = null)
    {
        string targetXlsx = xlsxPath ?? GetXlsxPath();
        var managed = GetAllManagedSheetNames(targetXlsx);
        var comparisons = new List<SheetComparison>(managed.Count);

        bool xlsxExists = File.Exists(targetXlsx);
        DateTime xlsxWriteUtc = xlsxExists ? File.GetLastWriteTimeUtc(targetXlsx) : DateTime.MinValue;
        Dictionary<string, List<string[]>> xlsxSheets = null;

        if (xlsxExists)
        {
            try
            {
                xlsxSheets = ReadAllDataSheetsFromXlsx(targetXlsx);
            }
            catch (Exception e)
            {
                Debug.LogError($"[XlsxTsvConverter] Failed to read '{targetXlsx}': {e.Message}");
                xlsxExists = false;
            }
        }

        foreach (string sheetName in managed)
        {
            string tsvPath = GetTsvPath(sheetName);
            bool tsvExists = File.Exists(tsvPath);
            DateTime tsvWriteUtc = tsvExists ? File.GetLastWriteTimeUtc(tsvPath) : DateTime.MinValue;
            List<string[]> tsvRows = tsvExists ? ReadTsvRows(tsvPath) : null;

            var comp = new SheetComparison
            {
                SheetName = sheetName,
                XlsxLastWriteUtc = xlsxWriteUtc,
                TsvLastWriteUtc = tsvWriteUtc,
                TsvRowCount = tsvRows != null ? Math.Max(0, tsvRows.Count - 1) : 0
            };

            if (!xlsxExists || xlsxSheets == null)
            {
                comp.State = SyncState.XlsxNotFound;
                comparisons.Add(comp);
                continue;
            }

            if (!xlsxSheets.TryGetValue(sheetName, out var xlsxRows))
            {
                comp.State = SyncState.MissingInXlsx;
                comparisons.Add(comp);
                continue;
            }

            comp.XlsxRowCount = Math.Max(0, xlsxRows.Count - 1);

            if (!tsvExists || tsvRows == null)
            {
                comp.State = SyncState.MissingInTsv;
                comparisons.Add(comp);
                continue;
            }

            ComputeDiffCounts(xlsxRows, tsvRows, out int dataDiffs, out int commentDiffs);
            comp.DataDiffRows = dataDiffs;
            comp.CommentDiffRows = commentDiffs;

            if (dataDiffs > 0)
            {
                comp.State = SyncState.DataDiff;
            }
            else if (commentDiffs > 0)
            {
                comp.State = SyncState.CommentOnlyDiff;
            }
            else
            {
                comp.State = SyncState.Identical;
            }

            comparisons.Add(comp);
        }

        return comparisons;
    }

    public static void LogSheetDiff(string sheetName, string xlsxPath = null)
    {
        string targetXlsx = xlsxPath ?? GetXlsxPath();
        if (!File.Exists(targetXlsx))
        {
            Debug.LogWarning($"[XlsxTsvConverter] XLSX file not found: {targetXlsx}");
            return;
        }

        var xlsxSheets = ReadAllDataSheetsFromXlsx(targetXlsx);
        if (!xlsxSheets.TryGetValue(sheetName, out var xlsxRows))
        {
            Debug.Log($"[XlsxTsvConverter] '{sheetName}' 시트가 XLSX에 아직 없습니다. TSV → XLSX 실행 시 새로 생성됩니다.");
            return;
        }

        string tsvPath = GetTsvPath(sheetName);
        if (!File.Exists(tsvPath))
        {
            Debug.Log($"[XlsxTsvConverter] '{tsvPath}' 파일이 없습니다. XLSX → TSV 실행 시 새로 생성됩니다.");
            return;
        }

        var tsvRows = ReadTsvRows(tsvPath);
        string[] headers = xlsxRows.Count > 0 ? xlsxRows[0] : (tsvRows.Count > 0 ? tsvRows[0] : Array.Empty<string>());
        int maxRows = Math.Max(xlsxRows.Count, tsvRows.Count);

        var sb = new StringBuilder();
        sb.AppendLine($"[XlsxTsvConverter] === '{sheetName}' 비교 결과 (XLSX {Math.Max(0, xlsxRows.Count - 1)}행 vs TSV {Math.Max(0, tsvRows.Count - 1)}행) ===");

        int shown = 0;
        int totalDiffRows = 0;
        for (int r = 0; r < maxRows; r++)
        {
            string[] xr = r < xlsxRows.Count ? xlsxRows[r] : null;
            string[] tr = r < tsvRows.Count ? tsvRows[r] : null;
            int cols = Math.Max(xr?.Length ?? 0, tr?.Length ?? 0);

            var cellDiffs = new List<string>();
            for (int c = 0; c < cols; c++)
            {
                string xv = xr != null && c < xr.Length ? xr[c] : "<없음>";
                string tv = tr != null && c < tr.Length ? tr[c] : "<없음>";
                if (!string.Equals(xv, tv, StringComparison.Ordinal))
                {
                    string colName = c < headers.Length ? headers[c] : IndexToColumnName(c);
                    cellDiffs.Add($"{colName}: XLSX='{xv}' | TSV='{tv}'");
                }
            }

            if (cellDiffs.Count > 0)
            {
                totalDiffRows++;
                if (shown < 20)
                {
                    string rowId = (tr != null && tr.Length > 0 ? tr[0] : (xr != null && xr.Length > 0 ? xr[0] : ""));
                    sb.AppendLine($"  • 행 {r + 1} (key={rowId}): {string.Join(", ", cellDiffs.Take(6))}{(cellDiffs.Count > 6 ? $" 외 {cellDiffs.Count - 6}개" : "")}");
                    shown++;
                }
            }
        }

        if (totalDiffRows == 0)
        {
            sb.AppendLine("  차이가 없습니다 (완전 일치).");
        }
        else if (totalDiffRows > shown)
        {
            sb.AppendLine($"  ... 외 {totalDiffRows - shown}개 행 차이 생략 (총 {totalDiffRows}개 행 다름)");
        }

        Debug.Log(sb.ToString());
    }

    public static bool ImportSheetToTsv(string sheetName, string xlsxPath = null, bool refreshAssets = true)
    {
        return ImportSheetsToTsv(new[] { sheetName }, xlsxPath, refreshAssets) > 0;
    }

    public static int ImportAllXlsxToTsv(string xlsxPath = null, bool refreshAssets = true)
    {
        string targetXlsx = xlsxPath ?? GetXlsxPath();
        if (!File.Exists(targetXlsx))
        {
            Debug.LogError($"[XlsxTsvConverter] XLSX 파일을 찾을 수 없습니다: {targetXlsx}");
            return 0;
        }

        var sheetNames = ReadSheetNamesFromXlsx(targetXlsx)
            .Where(n => !IsHelperSheet(n))
            .ToList();

        return ImportSheetsToTsv(sheetNames, targetXlsx, refreshAssets);
    }

    public static int ImportSheetsToTsv(IEnumerable<string> sheetNames, string xlsxPath = null, bool refreshAssets = true)
    {
        string targetXlsx = xlsxPath ?? GetXlsxPath();
        if (!File.Exists(targetXlsx))
        {
            Debug.LogError($"[XlsxTsvConverter] XLSX 파일을 찾을 수 없습니다: {targetXlsx}");
            return 0;
        }

        var allSheets = ReadAllDataSheetsFromXlsx(targetXlsx);
        string tsvDir = GetTsvDirectory();
        if (!Directory.Exists(tsvDir))
        {
            Directory.CreateDirectory(tsvDir);
        }

        int count = 0;
        bool localizationUpdated = false;

        foreach (string sheetName in sheetNames)
        {
            if (IsHelperSheet(sheetName)) continue;
            if (!allSheets.TryGetValue(sheetName, out var rows))
            {
                Debug.LogWarning($"[XlsxTsvConverter] '{sheetName}' 시트가 XLSX 파일에 없어 건너뜁니다.");
                continue;
            }

            string tsvPath = GetTsvPath(sheetName);
            WriteRowsToTsv(tsvPath, rows);
            count++;

            if (string.Equals(sheetName, "Localization", StringComparison.OrdinalIgnoreCase))
            {
                localizationUpdated = true;
            }
        }

        if (localizationUpdated)
        {
            LocalizationManager.ReloadEditorTsvCache();
        }

        if (refreshAssets && count > 0)
        {
            AssetDatabase.Refresh();
        }

        if (count > 0)
        {
            Debug.Log($"[XlsxTsvConverter] XLSX → TSV 완료: {count}개 시트를 '{tsvDir}'에 저장했습니다.");
        }

        return count;
    }

    public static bool ExportSingleTsvToXlsx(string sheetName, string xlsxPath = null, bool interactive = false)
    {
        return ExportSheetsToXlsx(new[] { sheetName }, xlsxPath, interactive) > 0;
    }

    public static int ExportAllTsvToXlsx(string xlsxPath = null, bool interactive = false)
    {
        string targetXlsx = xlsxPath ?? GetXlsxPath();
        var sheets = GetAllManagedSheetNames(targetXlsx)
            .Where(name => File.Exists(GetTsvPath(name)))
            .ToList();

        return ExportSheetsToXlsx(sheets, targetXlsx, interactive);
    }

    public static int ExportSheetsToXlsx(IEnumerable<string> sheetNames, string xlsxPath = null, bool interactive = false)
    {
        string targetXlsx = xlsxPath ?? GetXlsxPath();
        if (!File.Exists(targetXlsx))
        {
            string msg = $"XLSX 파일을 찾을 수 없습니다:\n{targetXlsx}";
            Debug.LogError($"[XlsxTsvConverter] {msg}");
            if (interactive) EditorUtility.DisplayDialog("XLSX 파일 없음", msg, "확인");
            return 0;
        }

        if (!CanOpenForWrite(targetXlsx))
        {
            string msg = $"'{Path.GetFileName(targetXlsx)}' 파일이 엑셀(또는 다른 프로그램)에서 열려 있어 수정할 수 없습니다.\n\n엑셀을 먼저 저장/종료한 뒤 다시 시도해 주세요.";
            Debug.LogError($"[XlsxTsvConverter] {msg.Replace("\n", " ")}");
            if (interactive) EditorUtility.DisplayDialog("엑셀 파일 잠김", msg, "확인");
            return 0;
        }

        var targetList = sheetNames
            .Where(s => !string.IsNullOrEmpty(s) && !IsHelperSheet(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targetList.Count == 0) return 0;

        // Read all existing zip entries into memory so untouched files remain byte-for-byte identical.
        var entryOrder = new List<string>();
        var entryBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        using (var fs = new FileStream(targetXlsx, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
        {
            foreach (var entry in archive.Entries)
            {
                entryOrder.Add(entry.FullName);
                using var es = entry.Open();
                using var ms = new MemoryStream();
                es.CopyTo(ms);
                entryBytes[entry.FullName] = ms.ToArray();
            }
        }

        var contentTypesDoc = ParseXmlBytes(entryBytes["[Content_Types].xml"]);
        var wbDoc = ParseXmlBytes(entryBytes["xl/workbook.xml"]);
        var wbRelsDoc = ParseXmlBytes(entryBytes["xl/_rels/workbook.xml.rels"]);

        XDocument sstDoc;
        var sharedStrings = new List<string>();
        var sharedStringIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        if (entryBytes.TryGetValue("xl/sharedStrings.xml", out byte[] sstRaw))
        {
            sstDoc = ParseXmlBytes(sstRaw);
            foreach (var si in sstDoc.Root.Elements(MainNs + "si"))
            {
                string text = ExtractSharedStringText(si);
                if (!sharedStringIndex.ContainsKey(text))
                {
                    sharedStringIndex[text] = sharedStrings.Count;
                }
                sharedStrings.Add(text);
            }
        }
        else
        {
            sstDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(MainNs + "sst", new XAttribute("count", "0"), new XAttribute("uniqueCount", "0")));
            entryOrder.Add("xl/sharedStrings.xml");
        }

        int initialSstCount = sharedStrings.Count;
        int GetOrAddSst(string val)
        {
            if (sharedStringIndex.TryGetValue(val, out int idx))
            {
                return idx;
            }
            int newIdx = sharedStrings.Count;
            sharedStrings.Add(val);
            sharedStringIndex[val] = newIdx;
            return newIdx;
        }

        bool hasDefaultStyle1 = HasCellStyleIndex1(entryBytes);
        var relIdToPath = ParseRelIdToEntryPath(wbRelsDoc);
        var sheetsParent = wbDoc.Root.Element(MainNs + "sheets");

        int updatedCount = 0;
        int createdCount = 0;
        int preservedFormulasTotal = 0;
        int extendedFormulasTotal = 0;

        foreach (string sheetName in targetList)
        {
            string tsvPath = GetTsvPath(sheetName);
            if (!File.Exists(tsvPath))
            {
                Debug.LogWarning($"[XlsxTsvConverter] TSV 파일이 없어 건너뜁니다: {tsvPath}");
                continue;
            }

            var tsvRows = ReadTsvRows(tsvPath);
            if (tsvRows.Count == 0)
            {
                Debug.LogWarning($"[XlsxTsvConverter] 비어 있는 TSV 파일을 건너뜁니다: {tsvPath}");
                continue;
            }

            var existingSheetEl = sheetsParent?
                .Elements(MainNs + "sheet")
                .FirstOrDefault(el => string.Equals(el.Attribute("name")?.Value, sheetName, StringComparison.OrdinalIgnoreCase));

            bool forceStringCells = string.Equals(sheetName, "Localization", StringComparison.OrdinalIgnoreCase);

            if (existingSheetEl != null)
            {
                string rId = existingSheetEl.Attribute(RelNs + "id")?.Value;
                if (string.IsNullOrEmpty(rId) || !relIdToPath.TryGetValue(rId, out string wsPath) || !entryBytes.ContainsKey(wsPath))
                {
                    Debug.LogError($"[XlsxTsvConverter] 워크시트 경로를 찾을 수 없습니다: {sheetName} ({rId})");
                    continue;
                }

                var wsDoc = ParseXmlBytes(entryBytes[wsPath]);
                UpdateExistingWorksheet(
                    wsDoc,
                    tsvRows,
                    sharedStrings,
                    GetOrAddSst,
                    forceStringCells,
                    hasDefaultStyle1,
                    out int preservedFormulas,
                    out int extendedFormulas);

                entryBytes[wsPath] = SerializeXmlToBytes(wsDoc);
                preservedFormulasTotal += preservedFormulas;
                extendedFormulasTotal += extendedFormulas;
                updatedCount++;
            }
            else
            {
                string newWsEntryPath = CreateNewWorksheetEntry(
                    sheetName,
                    tsvRows,
                    GetOrAddSst,
                    forceStringCells,
                    hasDefaultStyle1,
                    contentTypesDoc,
                    wbDoc,
                    wbRelsDoc,
                    entryOrder,
                    entryBytes);

                relIdToPath = ParseRelIdToEntryPath(wbRelsDoc);
                createdCount++;
                updatedCount++;
                Debug.Log($"[XlsxTsvConverter] 새 엑셀 시트 생성됨: '{sheetName}' ({newWsEntryPath})");
            }
        }

        if (updatedCount == 0)
        {
            return 0;
        }

        // Append newly added shared strings without disturbing existing indices.
        var sstRoot = sstDoc.Root;
        for (int i = initialSstCount; i < sharedStrings.Count; i++)
        {
            string s = sharedStrings[i];
            var tEl = new XElement(MainNs + "t", s);
            if (s.Length > 0 && (char.IsWhiteSpace(s[0]) || char.IsWhiteSpace(s[s.Length - 1])))
            {
                tEl.SetAttributeValue(XNamespace.Xml + "space", "preserve");
            }
            sstRoot.Add(new XElement(MainNs + "si", tEl));
        }
        sstRoot.SetAttributeValue("count", sharedStrings.Count.ToString(CultureInfo.InvariantCulture));
        sstRoot.SetAttributeValue("uniqueCount", sharedStrings.Count.ToString(CultureInfo.InvariantCulture));

        // Ask Excel to recalculate formula caches on load so VLOOKUP columns always show fresh results.
        var calcPr = wbDoc.Root.Element(MainNs + "calcPr");
        if (calcPr == null)
        {
            calcPr = new XElement(MainNs + "calcPr", new XAttribute("calcId", "191029"));
            wbDoc.Root.Add(calcPr);
        }
        calcPr.SetAttributeValue("fullCalcOnLoad", "1");

        entryBytes["[Content_Types].xml"] = SerializeXmlToBytes(contentTypesDoc);
        entryBytes["xl/workbook.xml"] = SerializeXmlToBytes(wbDoc);
        entryBytes["xl/_rels/workbook.xml.rels"] = SerializeXmlToBytes(wbRelsDoc);
        entryBytes["xl/sharedStrings.xml"] = SerializeXmlToBytes(sstDoc);

        // Write atomically via temp file.
        string tempPath = targetXlsx + ".tmp";
        try
        {
            using (var outFs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var outArchive = new ZipArchive(outFs, ZipArchiveMode.Create))
            {
                foreach (string entryName in entryOrder)
                {
                    if (!entryBytes.TryGetValue(entryName, out byte[] data)) continue;
                    var zipEntry = outArchive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
                    using var zs = zipEntry.Open();
                    zs.Write(data, 0, data.Length);
                }
            }

            if (File.Exists(targetXlsx))
            {
                File.Delete(targetXlsx);
            }
            File.Move(tempPath, targetXlsx);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        Debug.Log($"[XlsxTsvConverter] TSV → XLSX 완료: {updatedCount}개 시트 반영 (신규 시트 {createdCount}개, 보존된 수식 {preservedFormulasTotal}개, 자동 연장된 수식 {extendedFormulasTotal}개) → {targetXlsx}");
        return updatedCount;
    }

    private struct ExistingFormulaInfo
    {
        public string Formula;
        public string CachedValue;
        public string CellType;
    }

    private struct FormulaTemplate
    {
        public int SourceRow;
        public string Formula;
    }

    private static void UpdateExistingWorksheet(
        XDocument wsDoc,
        List<string[]> tsvRows,
        List<string> existingSharedStrings,
        Func<string, int> getOrAddSst,
        bool forceStringCells,
        bool hasDefaultStyle1,
        out int preservedFormulas,
        out int extendedFormulas)
    {
        preservedFormulas = 0;
        extendedFormulas = 0;

        var wsRoot = wsDoc.Root;
        var sheetData = wsRoot.Element(MainNs + "sheetData");
        if (sheetData == null)
        {
            sheetData = new XElement(MainNs + "sheetData");
            wsRoot.Add(sheetData);
        }

        var oldHeaders = new Dictionary<int, string>();
        var cellStyles = new Dictionary<(int row, int col), string>();
        var headerColStyles = new Dictionary<int, string>();
        var dataColStyles = new Dictionary<int, string>();
        var rowAttributes = new Dictionary<int, List<XAttribute>>();
        var existingFormulas = new Dictionary<(int row, int col), ExistingFormulaInfo>();
        var formulaTemplates = new Dictionary<int, FormulaTemplate>();
        int maxExistingDataRow = 1;

        foreach (var rowEl in sheetData.Elements(MainNs + "row"))
        {
            if (!int.TryParse(rowEl.Attribute("r")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rNum))
            {
                continue;
            }

            var attrs = rowEl.Attributes().Where(a => a.Name != "r" && a.Name != "spans").ToList();
            if (attrs.Count > 0)
            {
                rowAttributes[rNum] = attrs;
            }

            bool rowHasNonFormulaValue = false;
            var rowFormulasBuffer = new List<(int col, string formula)>();

            foreach (var cEl in rowEl.Elements(MainNs + "c"))
            {
                string cRef = cEl.Attribute("r")?.Value;
                if (string.IsNullOrEmpty(cRef)) continue;
                int colIdx = CellRefToColumnIndex(cRef);

                string styleAttr = cEl.Attribute("s")?.Value;
                if (!string.IsNullOrEmpty(styleAttr))
                {
                    cellStyles[(rNum, colIdx)] = styleAttr;
                    if (rNum == 1)
                    {
                        headerColStyles[colIdx] = styleAttr;
                    }
                    else if (!dataColStyles.ContainsKey(colIdx))
                    {
                        dataColStyles[colIdx] = styleAttr;
                    }
                }

                if (rNum == 1)
                {
                    oldHeaders[colIdx] = ParseCellString(cEl, existingSharedStrings);
                    continue;
                }

                var fEl = cEl.Element(MainNs + "f");
                if (fEl != null && !string.IsNullOrWhiteSpace(fEl.Value))
                {
                    string fText = fEl.Value;
                    string cachedV = cEl.Element(MainNs + "v")?.Value;
                    string cType = cEl.Attribute("t")?.Value;
                    existingFormulas[(rNum, colIdx)] = new ExistingFormulaInfo
                    {
                        Formula = fText,
                        CachedValue = cachedV,
                        CellType = cType
                    };
                    rowFormulasBuffer.Add((colIdx, fText));
                }
                else
                {
                    string val = ParseCellString(cEl, existingSharedStrings);
                    if (!string.IsNullOrEmpty(val))
                    {
                        rowHasNonFormulaValue = true;
                    }
                }
            }

            if (rNum >= 2 && rowHasNonFormulaValue && rNum > maxExistingDataRow)
            {
                maxExistingDataRow = rNum;
            }

            if (rNum >= 2 && (rowHasNonFormulaValue || formulaTemplates.Count == 0))
            {
                foreach (var item in rowFormulasBuffer)
                {
                    formulaTemplates[item.col] = new FormulaTemplate
                    {
                        SourceRow = rNum,
                        Formula = item.formula
                    };
                }
            }
        }

        string[] newHeaders = tsvRows[0];
        int numCols = newHeaders.Length;
        string fallbackHeaderStyle = headerColStyles.Values.FirstOrDefault() ?? (hasDefaultStyle1 ? "1" : null);
        string fallbackDataStyle = dataColStyles.Values.FirstOrDefault() ?? (hasDefaultStyle1 ? "1" : null);

        sheetData.RemoveNodes();

        for (int rowIdx = 0; rowIdx < tsvRows.Count; rowIdx++)
        {
            int excelRow = rowIdx + 1;
            string[] rowValues = tsvRows[rowIdx];

            var newRowEl = new XElement(MainNs + "row", new XAttribute("r", excelRow.ToString(CultureInfo.InvariantCulture)));
            if (rowAttributes.TryGetValue(excelRow, out var savedRowAttrs))
            {
                foreach (var a in savedRowAttrs)
                {
                    newRowEl.SetAttributeValue(a.Name, a.Value);
                }
            }

            for (int colIdx = 0; colIdx < numCols; colIdx++)
            {
                string val = colIdx < rowValues.Length ? rowValues[colIdx] : string.Empty;
                string cellRef = IndexToColumnName(colIdx) + excelRow.ToString(CultureInfo.InvariantCulture);

                if (excelRow == 1)
                {
                    string hStyle = cellStyles.TryGetValue((1, colIdx), out string s1)
                        ? s1
                        : (headerColStyles.TryGetValue(colIdx, out string s2) ? s2 : fallbackHeaderStyle);

                    var hCell = CreateValueCell(cellRef, val, hStyle, forceString: true, getOrAddSst);
                    if (hCell != null) newRowEl.Add(hCell);
                    continue;
                }

                string dStyle = cellStyles.TryGetValue((excelRow, colIdx), out string ds1)
                    ? ds1
                    : (dataColStyles.TryGetValue(colIdx, out string ds2) ? ds2 : fallbackDataStyle);

                bool sameColumnHeader = oldHeaders.TryGetValue(colIdx, out string oldHeader)
                    && string.Equals(oldHeader?.Trim(), newHeaders[colIdx]?.Trim(), StringComparison.OrdinalIgnoreCase);

                if (sameColumnHeader && existingFormulas.TryGetValue((excelRow, colIdx), out var existingF))
                {
                    newRowEl.Add(CreateFormulaCell(cellRef, dStyle, existingF.Formula, val, existingF.CachedValue, existingF.CellType));
                    preservedFormulas++;
                    continue;
                }

                if (sameColumnHeader && formulaTemplates.TryGetValue(colIdx, out var tmpl) &&
                    (excelRow > maxExistingDataRow || string.IsNullOrEmpty(val)))
                {
                    string shifted = ShiftFormulaRow(tmpl.Formula, tmpl.SourceRow, excelRow);
                    newRowEl.Add(CreateFormulaCell(cellRef, dStyle, shifted, val, null, null));
                    extendedFormulas++;
                    continue;
                }

                var dCell = CreateValueCell(cellRef, val, dStyle, forceStringCells, getOrAddSst);
                if (dCell != null)
                {
                    newRowEl.Add(dCell);
                }
            }

            sheetData.Add(newRowEl);
        }

        string lastColName = IndexToColumnName(Math.Max(0, numCols - 1));
        string dimRef = $"A1:{lastColName}{Math.Max(1, tsvRows.Count)}";
        var dimEl = wsRoot.Element(MainNs + "dimension");
        if (dimEl != null)
        {
            dimEl.SetAttributeValue("ref", dimRef);
        }
    }

    private static string CreateNewWorksheetEntry(
        string sheetName,
        List<string[]> tsvRows,
        Func<string, int> getOrAddSst,
        bool forceStringCells,
        bool hasDefaultStyle1,
        XDocument contentTypesDoc,
        XDocument wbDoc,
        XDocument wbRelsDoc,
        List<string> entryOrder,
        Dictionary<string, byte[]> entryBytes)
    {
        // 1. Determine next worksheet file number (worksheets/sheet{N}.xml).
        int nextFileNum = 1;
        foreach (string key in entryBytes.Keys)
        {
            if (key.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                string numPart = key.Substring("xl/worksheets/sheet".Length, key.Length - "xl/worksheets/sheet".Length - 4);
                if (int.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= nextFileNum)
                {
                    nextFileNum = n + 1;
                }
            }
        }

        string relTarget = $"worksheets/sheet{nextFileNum}.xml";
        string zipEntryPath = $"xl/{relTarget}";

        // 2. Allocate next rId in xl/_rels/workbook.xml.rels.
        int nextRelNum = 1;
        foreach (var relEl in wbRelsDoc.Root.Elements(PkgRelNs + "Relationship"))
        {
            string id = relEl.Attribute("Id")?.Value;
            if (!string.IsNullOrEmpty(id) && id.StartsWith("rId", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rn) && rn >= nextRelNum)
            {
                nextRelNum = rn + 1;
            }
        }
        string newRelId = $"rId{nextRelNum}";

        wbRelsDoc.Root.Add(new XElement(PkgRelNs + "Relationship",
            new XAttribute("Id", newRelId),
            new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
            new XAttribute("Target", relTarget)));

        // 3. Register sheet in xl/workbook.xml (insert before SoundRef/Localization/DescList if present).
        var sheetsEl = wbDoc.Root.Element(MainNs + "sheets");
        int nextSheetId = 1;
        XElement insertBeforeEl = null;
        foreach (var sEl in sheetsEl.Elements(MainNs + "sheet"))
        {
            if (int.TryParse(sEl.Attribute("sheetId")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sid) && sid >= nextSheetId)
            {
                nextSheetId = sid + 1;
            }
            string existingName = sEl.Attribute("name")?.Value;
            if (insertBeforeEl == null &&
                (IsHelperSheet(existingName) || string.Equals(existingName, "Localization", StringComparison.OrdinalIgnoreCase)))
            {
                insertBeforeEl = sEl;
            }
        }

        var newSheetEl = new XElement(MainNs + "sheet",
            new XAttribute("name", sheetName),
            new XAttribute("sheetId", nextSheetId.ToString(CultureInfo.InvariantCulture)),
            new XAttribute(RelNs + "id", newRelId));

        if (insertBeforeEl != null && !string.Equals(sheetName, "Localization", StringComparison.OrdinalIgnoreCase))
        {
            insertBeforeEl.AddBeforeSelf(newSheetEl);
        }
        else
        {
            sheetsEl.Add(newSheetEl);
        }

        // 4. Register Override in [Content_Types].xml.
        string partName = "/" + zipEntryPath;
        bool hasOverride = contentTypesDoc.Root
            .Elements(ContentTypesNs + "Override")
            .Any(o => string.Equals(o.Attribute("PartName")?.Value, partName, StringComparison.OrdinalIgnoreCase));

        if (!hasOverride)
        {
            contentTypesDoc.Root.Add(new XElement(ContentTypesNs + "Override",
                new XAttribute("PartName", partName),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
        }

        // 5. Build worksheet XML adhering strictly to OOXML CT_Worksheet element order.
        int numCols = tsvRows[0].Length;
        string defaultStyle = hasDefaultStyle1 ? "1" : null;
        string lastCol = IndexToColumnName(Math.Max(0, numCols - 1));
        string dimRef = $"A1:{lastCol}{Math.Max(1, tsvRows.Count)}";

        var colsEl = new XElement(MainNs + "cols");
        for (int c = 0; c < numCols; c++)
        {
            int maxWidthChars = 6;
            for (int r = 0; r < tsvRows.Count; r++)
            {
                if (c < tsvRows[r].Length)
                {
                    int w = EstimateDisplayWidth(tsvRows[r][c]);
                    if (w > maxWidthChars) maxWidthChars = w;
                }
            }
            double width = Math.Min(42.0, Math.Max(6.0, maxWidthChars + 3.0));
            colsEl.Add(new XElement(MainNs + "col",
                new XAttribute("min", (c + 1).ToString(CultureInfo.InvariantCulture)),
                new XAttribute("max", (c + 1).ToString(CultureInfo.InvariantCulture)),
                new XAttribute("width", width.ToString("0.##", CultureInfo.InvariantCulture)),
                new XAttribute("customWidth", "1")));
        }

        var sheetDataEl = new XElement(MainNs + "sheetData");
        for (int r = 0; r < tsvRows.Count; r++)
        {
            int excelRow = r + 1;
            var rowEl = new XElement(MainNs + "row", new XAttribute("r", excelRow.ToString(CultureInfo.InvariantCulture)));
            string[] vals = tsvRows[r];
            for (int c = 0; c < numCols; c++)
            {
                string val = c < vals.Length ? vals[c] : string.Empty;
                string cellRef = IndexToColumnName(c) + excelRow.ToString(CultureInfo.InvariantCulture);
                var cellEl = CreateValueCell(cellRef, val, defaultStyle, forceString: excelRow == 1 || forceStringCells, getOrAddSst);
                if (cellEl != null) rowEl.Add(cellEl);
            }
            sheetDataEl.Add(rowEl);
        }

        var wsDoc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(MainNs + "worksheet",
                new XAttribute(XNamespace.Xmlns + "r", RelNs.NamespaceName),
                new XElement(MainNs + "sheetPr",
                    new XElement(MainNs + "outlinePr", new XAttribute("summaryBelow", "0"), new XAttribute("summaryRight", "0"))),
                new XElement(MainNs + "dimension", new XAttribute("ref", dimRef)),
                new XElement(MainNs + "sheetViews",
                    new XElement(MainNs + "sheetView",
                        new XAttribute("workbookViewId", "0"),
                        new XElement(MainNs + "pane",
                            new XAttribute("ySplit", "1"),
                            new XAttribute("topLeftCell", "A2"),
                            new XAttribute("activePane", "bottomLeft"),
                            new XAttribute("state", "frozen")),
                        new XElement(MainNs + "selection",
                            new XAttribute("pane", "bottomLeft"),
                            new XAttribute("activeCell", "A2"),
                            new XAttribute("sqref", "A2")))),
                new XElement(MainNs + "sheetFormatPr",
                    new XAttribute("defaultColWidth", "12.57421875"),
                    new XAttribute("defaultRowHeight", "15.75"),
                    new XAttribute("customHeight", "1")),
                colsEl,
                sheetDataEl,
                new XElement(MainNs + "pageMargins",
                    new XAttribute("left", "0.7"),
                    new XAttribute("right", "0.7"),
                    new XAttribute("top", "0.75"),
                    new XAttribute("bottom", "0.75"),
                    new XAttribute("header", "0.3"),
                    new XAttribute("footer", "0.3")),
                new XElement(MainNs + "pageSetup", new XAttribute("orientation", "portrait"))));

        entryOrder.Add(zipEntryPath);
        entryBytes[zipEntryPath] = SerializeXmlToBytes(wsDoc);
        return zipEntryPath;
    }

    private static XElement CreateFormulaCell(
        string cellRef,
        string styleIdx,
        string formula,
        string tsvValue,
        string previousCachedValue,
        string previousCellType)
    {
        var cell = new XElement(MainNs + "c", new XAttribute("r", cellRef));
        if (!string.IsNullOrEmpty(styleIdx))
        {
            cell.SetAttributeValue("s", styleIdx);
        }

        string effectiveVal = !string.IsNullOrEmpty(tsvValue) ? tsvValue : previousCachedValue;
        if (!string.IsNullOrEmpty(effectiveVal))
        {
            if (effectiveVal.StartsWith("#", StringComparison.Ordinal))
            {
                cell.SetAttributeValue("t", "e");
            }
            else if (!string.IsNullOrEmpty(tsvValue))
            {
                cell.SetAttributeValue("t", "str");
            }
            else if (!string.IsNullOrEmpty(previousCellType) && previousCellType != "s")
            {
                cell.SetAttributeValue("t", previousCellType);
            }
            else
            {
                cell.SetAttributeValue("t", "str");
            }
        }

        // CT_Cell requires <f> strictly before <v>.
        cell.Add(new XElement(MainNs + "f", formula));
        if (!string.IsNullOrEmpty(effectiveVal))
        {
            cell.Add(new XElement(MainNs + "v", effectiveVal));
        }

        return cell;
    }

    private static XElement CreateValueCell(
        string cellRef,
        string value,
        string styleIdx,
        bool forceString,
        Func<string, int> getOrAddSst)
    {
        if (string.IsNullOrEmpty(value))
        {
            if (!string.IsNullOrEmpty(styleIdx))
            {
                return new XElement(MainNs + "c",
                    new XAttribute("r", cellRef),
                    new XAttribute("s", styleIdx));
            }
            return null;
        }

        var cell = new XElement(MainNs + "c", new XAttribute("r", cellRef));
        if (!string.IsNullOrEmpty(styleIdx))
        {
            cell.SetAttributeValue("s", styleIdx);
        }

        if (!forceString)
        {
            if (string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase))
            {
                cell.SetAttributeValue("t", "b");
                cell.Add(new XElement(MainNs + "v", "1"));
                return cell;
            }
            if (string.Equals(value, "FALSE", StringComparison.OrdinalIgnoreCase))
            {
                cell.SetAttributeValue("t", "b");
                cell.Add(new XElement(MainNs + "v", "0"));
                return cell;
            }

            if (long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long lVal) &&
                string.Equals(lVal.ToString(CultureInfo.InvariantCulture), value, StringComparison.Ordinal))
            {
                cell.Add(new XElement(MainNs + "v", value));
                return cell;
            }

            if (CleanFloatRegex.IsMatch(value) &&
                double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double dVal) &&
                !double.IsNaN(dVal) && !double.IsInfinity(dVal) &&
                string.Equals(dVal.ToString("G15", CultureInfo.InvariantCulture), value, StringComparison.Ordinal))
            {
                cell.Add(new XElement(MainNs + "v", value));
                return cell;
            }
        }

        int sstIdx = getOrAddSst(value);
        cell.SetAttributeValue("t", "s");
        cell.Add(new XElement(MainNs + "v", sstIdx.ToString(CultureInfo.InvariantCulture)));
        return cell;
    }

    public static string ShiftFormulaRow(string formula, int sourceRow, int targetRow)
    {
        if (string.IsNullOrEmpty(formula) || sourceRow == targetRow) return formula;
        int delta = targetRow - sourceRow;

        return FormulaCellRefRegex.Replace(formula, match =>
        {
            if (match.Groups[1].Success)
            {
                // String literal inside double quotes.
                return match.Value;
            }

            string colPart = match.Groups[2].Value;
            string rowDollar = match.Groups[3].Value;
            string rowDigits = match.Groups[4].Value;

            if (rowDollar == "$")
            {
                return match.Value;
            }

            if (int.TryParse(rowDigits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r))
            {
                int shifted = Math.Max(1, r + delta);
                return colPart + shifted.ToString(CultureInfo.InvariantCulture);
            }

            return match.Value;
        });
    }

    private static List<string[]> ParseWorksheetToRows(XDocument wsDoc, List<string> sharedStrings)
    {
        var result = new List<string[]>();
        var sheetData = wsDoc.Root?.Element(MainNs + "sheetData");
        if (sheetData == null) return result;

        var rowElements = sheetData.Elements(MainNs + "row").ToList();
        if (rowElements.Count == 0) return result;

        // Parse header row (first row element).
        var headerMap = new Dictionary<int, string>();
        int maxHeaderCol = -1;
        foreach (var cEl in rowElements[0].Elements(MainNs + "c"))
        {
            string cRef = cEl.Attribute("r")?.Value;
            if (string.IsNullOrEmpty(cRef)) continue;
            int colIdx = CellRefToColumnIndex(cRef);
            string val = SanitizeCellForTsv(ParseCellString(cEl, sharedStrings));
            headerMap[colIdx] = val;
            if (colIdx > maxHeaderCol) maxHeaderCol = colIdx;
        }

        if (maxHeaderCol < 0) return result;

        var headers = new List<string>(maxHeaderCol + 1);
        for (int i = 0; i <= maxHeaderCol; i++)
        {
            headers.Add(headerMap.TryGetValue(i, out string h) ? h : string.Empty);
        }
        while (headers.Count > 0 && string.IsNullOrEmpty(headers[headers.Count - 1]))
        {
            headers.RemoveAt(headers.Count - 1);
        }

        int numCols = headers.Count;
        if (numCols == 0) return result;

        result.Add(headers.ToArray());

        for (int r = 1; r < rowElements.Count; r++)
        {
            string[] rowValues = new string[numCols];
            for (int i = 0; i < numCols; i++) rowValues[i] = string.Empty;

            bool hasRealData = false;
            foreach (var cEl in rowElements[r].Elements(MainNs + "c"))
            {
                string cRef = cEl.Attribute("r")?.Value;
                if (string.IsNullOrEmpty(cRef)) continue;
                int colIdx = CellRefToColumnIndex(cRef);
                if (colIdx < 0 || colIdx >= numCols) continue;

                string val = SanitizeCellForTsv(ParseCellString(cEl, sharedStrings));
                rowValues[colIdx] = val;

                bool hasFormula = cEl.Element(MainNs + "f") != null;
                bool isCommentCol = headers[colIdx].StartsWith("#", StringComparison.Ordinal);
                if (!string.IsNullOrEmpty(val) && !(hasFormula && isCommentCol))
                {
                    hasRealData = true;
                }
            }

            if (hasRealData)
            {
                result.Add(rowValues);
            }
        }

        return result;
    }

    private static string ParseCellString(XElement cEl, List<string> sharedStrings)
    {
        string t = cEl.Attribute("t")?.Value;
        if (t == "inlineStr")
        {
            var isEl = cEl.Element(MainNs + "is");
            return isEl != null ? ExtractSharedStringText(isEl) : string.Empty;
        }

        string raw = cEl.Element(MainNs + "v")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        if (t == "s")
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx) &&
                idx >= 0 && idx < sharedStrings.Count)
            {
                return sharedStrings[idx];
            }
            return string.Empty;
        }

        if (t == "b")
        {
            if (raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)) return "TRUE";
            if (raw == "0" || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)) return "FALSE";
            return raw;
        }

        if (string.IsNullOrEmpty(t) || t == "n")
        {
            if ((raw.IndexOf('.') >= 0 || raw.IndexOf('E') >= 0 || raw.IndexOf('e') >= 0) &&
                double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double dVal) &&
                !double.IsNaN(dVal) && !double.IsInfinity(dVal))
            {
                return dVal.ToString("G15", CultureInfo.InvariantCulture);
            }
        }

        return raw;
    }

    private static string ExtractSharedStringText(XElement container)
    {
        var directT = container.Element(MainNs + "t");
        if (directT != null)
        {
            return directT.Value ?? string.Empty;
        }

        // Concatenate rich-text runs <r><t>...</t></r> while ignoring phonetic <rPh> elements.
        var sb = new StringBuilder();
        foreach (var rEl in container.Elements(MainNs + "r"))
        {
            var tEl = rEl.Element(MainNs + "t");
            if (tEl != null && tEl.Value != null)
            {
                sb.Append(tEl.Value);
            }
        }
        return sb.ToString();
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var list = new List<string>();
        var sstEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sstEntry == null) return list;

        var doc = LoadXDocument(sstEntry);
        if (doc.Root == null) return list;

        foreach (var si in doc.Root.Elements(MainNs + "si"))
        {
            list.Add(ExtractSharedStringText(si));
        }
        return list;
    }

    private static Dictionary<string, string> ReadWorkbookSheetPaths(ZipArchive archive)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var wbEntry = archive.GetEntry("xl/workbook.xml");
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (wbEntry == null || relsEntry == null) return result;

        var wbDoc = LoadXDocument(wbEntry);
        var relsDoc = LoadXDocument(relsEntry);
        var relMap = ParseRelIdToEntryPath(relsDoc);

        foreach (var sEl in wbDoc.Descendants(MainNs + "sheet"))
        {
            string name = sEl.Attribute("name")?.Value;
            string rId = sEl.Attribute(RelNs + "id")?.Value;
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(rId) && relMap.TryGetValue(rId, out string path))
            {
                result[name] = path;
            }
        }
        return result;
    }

    private static Dictionary<string, string> ParseRelIdToEntryPath(XDocument wbRelsDoc)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (wbRelsDoc.Root == null) return map;

        foreach (var rel in wbRelsDoc.Root.Elements(PkgRelNs + "Relationship"))
        {
            string id = rel.Attribute("Id")?.Value;
            string target = rel.Attribute("Target")?.Value;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target)) continue;

            string normalized = target.StartsWith("/", StringComparison.Ordinal)
                ? target.TrimStart('/')
                : (target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target : "xl/" + target);

            map[id] = normalized;
        }
        return map;
    }

    private static bool HasCellStyleIndex1(Dictionary<string, byte[]> entryBytes)
    {
        if (!entryBytes.TryGetValue("xl/styles.xml", out byte[] stylesBytes)) return false;
        try
        {
            var stylesDoc = ParseXmlBytes(stylesBytes);
            var cellXfs = stylesDoc.Root?.Element(MainNs + "cellXfs");
            return cellXfs != null && cellXfs.Elements(MainNs + "xf").Count() > 1;
        }
        catch
        {
            return false;
        }
    }

    private static List<string[]> ReadTsvRows(string tsvPath)
    {
        var rows = new List<string[]>();
        string content = File.ReadAllText(tsvPath, Encoding.UTF8);
        if (string.IsNullOrEmpty(content)) return rows;

        string[] lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (i == 0)
            {
                line = line.TrimStart('\uFEFF');
            }
            if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line)) continue;
            rows.Add(line.Split('\t'));
        }
        return rows;
    }

    private static void WriteRowsToTsv(string tsvPath, List<string[]> rows)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            sb.Append(string.Join("\t", rows[i]));
            if (i < rows.Count - 1)
            {
                sb.Append('\n');
            }
        }
        sb.Append('\n');
        File.WriteAllText(tsvPath, sb.ToString(), new UTF8Encoding(false));
    }

    private static void ComputeDiffCounts(List<string[]> xlsxRows, List<string[]> tsvRows, out int dataDiffRows, out int commentDiffRows)
    {
        dataDiffRows = 0;
        commentDiffRows = 0;

        string[] headers = xlsxRows.Count > 0 ? xlsxRows[0] : (tsvRows.Count > 0 ? tsvRows[0] : Array.Empty<string>());
        int maxRows = Math.Max(xlsxRows.Count, tsvRows.Count);

        for (int r = 0; r < maxRows; r++)
        {
            if (r >= xlsxRows.Count || r >= tsvRows.Count)
            {
                dataDiffRows++;
                continue;
            }

            string[] xr = xlsxRows[r];
            string[] tr = tsvRows[r];
            int cols = Math.Max(xr.Length, tr.Length);

            bool rowDataDiff = false;
            bool rowCommentDiff = false;

            for (int c = 0; c < cols; c++)
            {
                string xv = c < xr.Length ? xr[c] : string.Empty;
                string tv = c < tr.Length ? tr[c] : string.Empty;
                if (!string.Equals(xv, tv, StringComparison.Ordinal))
                {
                    bool isComment = r > 0 && c < headers.Length && headers[c].StartsWith("#", StringComparison.Ordinal);
                    if (isComment) rowCommentDiff = true;
                    else rowDataDiff = true;
                }
            }

            if (rowDataDiff) dataDiffRows++;
            else if (rowCommentDiff) commentDiffRows++;
        }
    }

    private static string SanitizeCellForTsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOfAny(new[] { '\t', '\r', '\n' }) < 0) return value;
        return value.Replace("\t", " ").Replace("\r\n", "\\n").Replace("\r", "\\n").Replace("\n", "\\n");
    }

    private static int EstimateDisplayWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int w = 0;
        foreach (char ch in text)
        {
            w += ch > 0x7F ? 2 : 1;
        }
        return w;
    }

    public static int CellRefToColumnIndex(string cellRef)
    {
        int col = 0;
        for (int i = 0; i < cellRef.Length; i++)
        {
            char ch = cellRef[i];
            if (ch >= 'A' && ch <= 'Z')
            {
                col = col * 26 + (ch - 'A' + 1);
            }
            else if (ch >= 'a' && ch <= 'z')
            {
                col = col * 26 + (ch - 'a' + 1);
            }
            else
            {
                break;
            }
        }
        return col - 1;
    }

    public static string IndexToColumnName(int index)
    {
        var sb = new StringBuilder();
        int n = index + 1;
        while (n > 0)
        {
            int rem = (n - 1) % 26;
            sb.Insert(0, (char)('A' + rem));
            n = (n - 1) / 26;
        }
        return sb.ToString();
    }

    private static MemoryStream ReadFileToMemoryStream(string path)
    {
        var ms = new MemoryStream();
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            fs.CopyTo(ms);
        }
        ms.Position = 0;
        return ms;
    }

    private static XDocument LoadXDocument(ZipArchiveEntry entry)
    {
        using var s = entry.Open();
        return XDocument.Load(s, LoadOptions.PreserveWhitespace);
    }

    private static XDocument ParseXmlBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return XDocument.Load(ms, LoadOptions.PreserveWhitespace);
    }

    private static byte[] SerializeXmlToBytes(XDocument doc)
    {
        if (doc.Declaration == null)
        {
            doc.Declaration = new XDeclaration("1.0", "UTF-8", "yes");
        }

        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            OmitXmlDeclaration = false
        };
        using (var writer = XmlWriter.Create(ms, settings))
        {
            doc.Save(writer);
        }
        return ms.ToArray();
    }
}

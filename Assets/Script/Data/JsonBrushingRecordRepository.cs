using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// IBrushingRecordRepository의 JSON 파일 구현.
/// Application.persistentDataPath 아래 단일 JSON 파일에 레코드 배열을 누적 저장한다.
/// (Resources는 런타임 읽기 전용이라 사용 불가 — 쓰기 가능한 persistentDataPath 사용.)
///
/// 파일 포맷: { "records": [ {BrushingRecord}, ... ] }
/// (JsonUtility는 최상위 배열/리스트를 직렬화하지 못해 래퍼 클래스로 감싼다.)
///
/// DB 전환 시: 이 클래스 대신 IBrushingRecordRepository의 DB 구현을 만들어
/// BrushingRecordWriter.SetRepository(...)로 주입하면 된다.
/// </summary>
public class JsonBrushingRecordRepository : IBrushingRecordRepository
{
    // JsonUtility용 래퍼 — 최상위가 객체여야 하므로 records 리스트를 필드로 감싼다.
    [System.Serializable]
    private class RecordFile
    {
        public List<BrushingRecord> records = new List<BrushingRecord>();
    }

    private const string DefaultFileName = "brushing_records.json";

    private readonly string _filePath;

    /// <summary>기본 경로(persistentDataPath/brushing_records.json)에 저장.</summary>
    public JsonBrushingRecordRepository()
        : this(Path.Combine(Application.persistentDataPath, DefaultFileName)) { }

    /// <summary>저장 경로를 직접 지정(테스트/커스텀 위치용).</summary>
    public JsonBrushingRecordRepository(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>실제 저장 파일 경로(검증/디버깅용).</summary>
    public string FilePath => _filePath;

    public void Save(BrushingRecord record)
    {
        if (record == null) return;

        var file = ReadFile();
        file.records.Add(record);

        // prettyPrint=true: 사람이 열어보기 쉽게(검증 단계). 용량 민감해지면 false로.
        var json = JsonUtility.ToJson(file, true);

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(_filePath, json);
    }

    public List<BrushingRecord> LoadAll()
    {
        return ReadFile().records;
    }

    private RecordFile ReadFile()
    {
        if (!File.Exists(_filePath)) return new RecordFile();

        try
        {
            var json = File.ReadAllText(_filePath);
            var file = JsonUtility.FromJson<RecordFile>(json);
            return file ?? new RecordFile();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BrushingRecord] JSON 읽기 실패({_filePath}): {e.Message} — 새 파일로 시작.");
            return new RecordFile();
        }
    }
}

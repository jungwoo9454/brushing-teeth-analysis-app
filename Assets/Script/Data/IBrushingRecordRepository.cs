using System.Collections.Generic;

/// <summary>
/// 양치 기록 저장소 추상화. 지금은 JSON 파일(JsonBrushingRecordRepository) 구현을 쓰지만,
/// 향후 DB로 교체할 때 이 인터페이스만 다시 구현하면 호출부(BrushingRecordWriter)는 그대로 둘 수 있다.
/// </summary>
public interface IBrushingRecordRepository
{
    /// <summary>레코드 1건 저장(append).</summary>
    void Save(BrushingRecord record);

    /// <summary>저장된 모든 레코드를 시간순(오래된→최근)으로 반환. 없으면 빈 리스트.</summary>
    List<BrushingRecord> LoadAll();
}

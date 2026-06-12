using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사용자 데이터(설정·양치 기록 등)의 영속화 매니저.
/// 저장 포맷(PlayerPrefs / JSON 파일 / DB 등)은 아직 결정되지 않아,
/// 현재는 인메모리 캐시만 제공하여 외부 API 시그니처를 먼저 고정한다.
/// 추후 Save/Load 내부 구현만 교체하면 호출부 변경 없이 동작하도록 설계.
/// </summary>
public class DataMng : Singleton<DataMng>
{
    protected DataMng() { }

    // 키-값 인메모리 캐시. 영속 백엔드가 정해지면 Flush 시 디스크로 내려보낸다.
    // object 박싱을 감수한 이유: 다양한 타입(string/int/bool/구조체)을 한 컨테이너로 관리하기 위함.
    private readonly Dictionary<string, object> cache = new Dictionary<string, object>();

    protected override void Awake()
    {
        // 부모 Singleton의 DontDestroyOnLoad 등록을 위해 반드시 호출.
        base.Awake();

        // TODO: 저장 포맷 결정 후 LoadAll() 구현 — 디스크의 영속 데이터를 cache로 채워넣는다.
    }

    /// <summary>
    /// 지정한 키에 값을 저장한다. 디스크 반영은 Flush 시점에 한 번에 일어난다.
    /// </summary>
    /// <typeparam name="T">저장할 값의 타입. 박싱 가능한 모든 타입 허용.</typeparam>
    public void Set<T>(string key, T value)
    {
        // 같은 키가 있으면 덮어쓰고, 없으면 새로 삽입. Dictionary 인덱서가 두 케이스를 자동 처리.
        cache[key] = value;

        // TODO: dirty flag를 세팅하여 Flush가 변경된 항목만 디스크에 쓰도록 최적화.
    }

    /// <summary>
    /// 키에 해당하는 값을 반환. 없거나 타입이 다르면 defaultValue를 반환한다.
    /// </summary>
    public T Get<T>(string key, T defaultValue = default)
    {
        // TryGetValue + 타입 매칭 동시 체크 — 잘못된 타입으로 저장된 데이터는 안전하게 무시.
        if (cache.TryGetValue(key, out var value) && value is T typed) return typed;
        return defaultValue;
    }

    /// <summary>키 존재 여부 확인.</summary>
    public bool Has(string key) => cache.ContainsKey(key);

    /// <summary>키와 값을 캐시에서 삭제. 영속 저장된 데이터는 다음 Flush 시 반영된다.</summary>
    public void Delete(string key) => cache.Remove(key);

    /// <summary>
    /// 인메모리 캐시를 영속 저장소로 내려보낸다.
    /// 호출 시점: 앱 일시정지(OnApplicationPause)·중요 상태 변경 직후·명시적 저장 버튼.
    /// </summary>
    public void Flush()
    {
        // TODO: 저장 포맷 결정 후 실제 디스크 쓰기 구현.
        //       - PlayerPrefs: 키별로 SetString/SetInt 호출 후 PlayerPrefs.Save()
        //       - JSON 파일: Application.persistentDataPath + JsonUtility.ToJson
        Debug.Log("[DataMng] Flush 호출됨 — 영속화 백엔드 미구현 (TODO).");
    }
}

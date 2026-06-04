using UnityEngine;

/// <summary>
/// 유저 공유 데이터(이름 등)의 전역 접근점.
/// Singleton&lt;T&gt; 기반이라 .ins로 어디서나 접근 가능하고, DontDestroyOnLoad로
/// 씬 전환(타이틀→양치→결과)에도 인스턴스가 유지된다.
/// 실제 저장/복원은 DataMng에 위임한다 — 이 클래스는 메모리상의 단일 소스(source of truth)다.
///
/// 사용 예)
///   UserMng.ins.UserName = "민준";        // 설정하면 즉시 저장 + 변경 이벤트 발행
///   string name = UserMng.ins.UserName;   // 어디서나 현재 유저 이름 읽기
/// </summary>
public class UserMng : Singleton<UserMng>
{
    // 비싱글톤 생성 방지 (프로젝트 컨벤션 — .ins로만 접근).
    protected UserMng() { }

    // DataMng 저장 키. 변경하면 기존 저장 데이터를 못 읽으니 신중히.
    private const string PROFILE_KEY = "user.profile";

    // 메모리상의 실제 데이터. Awake 또는 첫 접근(Profile getter) 시 Load로 채운다.
    private UserProfile profile;

    // ─────────────────────────────────────────────────────────────
    // 공개 접근자
    // ─────────────────────────────────────────────────────────────

    /// <summary>표시용 유저 이름. set 하면 즉시 저장 + UserProfileChangedEvent 발행.</summary>
    public string UserName
    {
        get => Profile.userName;
        set
        {
            if (Profile.userName == value) return;
            profile.userName = value;
            Save();
            PublishChanged();
        }
    }

    /// <summary>
    /// 프로필 전체 읽기. 외부에서 필드를 직접 바꾸지 말 것 — 저장이 누락된다.
    /// 값을 바꿀 땐 전용 프로퍼티(UserName 등)나 SetProfile을 쓸 것.
    /// </summary>
    public UserProfile Profile
    {
        get
        {
            if (profile == null) Load();
            return profile;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();   // Singleton의 DontDestroyOnLoad 등록.
        Load();         // 시작 시 프로필 복원(저장된 게 없으면 빈 프로필).
    }

    // ─────────────────────────────────────────────────────────────
    // 저장 / 복원 — DataMng에 위임
    // ─────────────────────────────────────────────────────────────

    /// <summary>DataMng에서 프로필을 불러온다. 저장된 게 없으면 빈 프로필을 새로 만든다.</summary>
    public void Load()
    {
        string json = DataMng.ins.Get<string>(PROFILE_KEY, null);

        profile = string.IsNullOrEmpty(json)
            ? new UserProfile()
            : (JsonUtility.FromJson<UserProfile>(json) ?? new UserProfile());
    }

    /// <summary>현재 프로필을 DataMng에 저장한다(디스크 반영은 DataMng.Flush 구현에 따름).</summary>
    public void Save()
    {
        if (profile == null) return;
        DataMng.ins.Set(PROFILE_KEY, JsonUtility.ToJson(profile));
        DataMng.ins.Flush();
    }

    // ─────────────────────────────────────────────────────────────
    // 변경 API
    // ─────────────────────────────────────────────────────────────

    /// <summary>프로필을 통째로 교체하고 저장한다(서버 동기화·복수 필드 일괄 변경 등).</summary>
    public void SetProfile(UserProfile next)
    {
        if (next == null) return;
        profile = next;
        Save();
        PublishChanged();
    }

    /// <summary>프로필 초기화(로그아웃/테스트).</summary>
    public void ResetProfile()
    {
        profile = new UserProfile();
        Save();
        PublishChanged();
    }

    // ─────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────────────────────────

    private void PublishChanged() => EventBus.Publish(new UserProfileChangedEvent { profile = profile });
}

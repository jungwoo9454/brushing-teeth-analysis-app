using System;
using UnityEngine;

/// <summary>
/// 유저 공유 데이터(이름·UUID 등)의 전역 접근점.
/// Singleton&lt;T&gt; 기반이라 .ins로 어디서나 접근 가능하고, DontDestroyOnLoad로
/// 씬 전환(타이틀→양치→결과)에도 인스턴스가 유지된다.
/// 실제 저장/복원은 DataMng에 위임한다 — 이 클래스는 메모리상의 단일 소스(source of truth)다.
///
/// 사용 예)
///   string id = UserMng.ins.Uuid;        // 최초 접근 시 UUID 자동 생성·저장
///   UserMng.ins.UserName = "민준";        // 설정하면 즉시 저장 + 변경 이벤트 발행
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

    /// <summary>유저 고유 식별자(읽기 전용). 최초 접근 시 자동 생성·저장된다.</summary>
    public string Uuid => Profile.uuid;

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
        Load();         // 시작 시 프로필 복원(없으면 생성 + UUID 발급).
    }

    // ─────────────────────────────────────────────────────────────
    // 저장 / 복원 — DataMng에 위임
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// DataMng에서 프로필을 불러온다. 저장된 게 없으면 새로 만들고,
    /// UUID가 비어 있으면 발급한 뒤 즉시 저장한다.
    /// </summary>
    public void Load()
    {
        string json = DataMng.ins.Get<string>(PROFILE_KEY, null);

        profile = string.IsNullOrEmpty(json)
            ? new UserProfile()
            : (JsonUtility.FromJson<UserProfile>(json) ?? new UserProfile());

        // UUID 보장 — 최초 실행 시 한 번만 생성된다.
        if (string.IsNullOrEmpty(profile.uuid))
        {
            profile.uuid = Guid.NewGuid().ToString("N");
            Save();
        }
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

    /// <summary>
    /// 프로필을 통째로 교체하고 저장한다(서버 동기화·복수 필드 일괄 변경 등).
    /// uuid가 비어 오면 기존 uuid를 유지해 식별자 분실을 막는다.
    /// </summary>
    public void SetProfile(UserProfile next)
    {
        if (next == null) return;
        if (string.IsNullOrEmpty(next.uuid)) next.uuid = Profile.uuid;
        profile = next;
        Save();
        PublishChanged();
    }

    /// <summary>프로필 초기화(로그아웃/테스트). UUID도 새로 발급된다.</summary>
    public void ResetProfile()
    {
        profile = new UserProfile { uuid = Guid.NewGuid().ToString("N") };
        Save();
        PublishChanged();
    }

    // ─────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────────────────────────

    private void PublishChanged() => EventBus.Publish(new UserProfileChangedEvent { profile = profile });
}

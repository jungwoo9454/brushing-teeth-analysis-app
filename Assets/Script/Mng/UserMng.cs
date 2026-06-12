using System;
using System.Collections.Generic;
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

        MigrateChildren();
    }

    /// <summary>구버전 단일 아이 필드를 children 목록으로 1회 이관하고, 활성 아이를 보정한다.</summary>
    private void MigrateChildren()
    {
        if (profile.children == null) profile.children = new List<ChildProfile>();

        // 과거 단일 아이(childName 등) → children[0]으로 이관 후 비운다.
        if (profile.children.Count == 0 && !string.IsNullOrEmpty(profile.childName))
        {
            var c = new ChildProfile(NewChildId(), profile.childName, profile.childAge, profile.childGender);
            profile.children.Add(c);
            profile.activeChildId = c.id;
            profile.childName = ""; profile.childAge = 0; profile.childGender = "";
            Save();
        }

        // 활성 아이 id가 비었거나 유효하지 않으면 첫 아이로 보정.
        if (profile.children.Count > 0 &&
            !profile.children.Exists(c => c.id == profile.activeChildId))
        {
            profile.activeChildId = profile.children[0].id;
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
    // 온보딩(회원가입) 설정 헬퍼 — 각 단계 화면이 호출. 즉시 저장한다.
    // (Profile getter는 live 객체라 필드를 직접 바꾼 뒤 Save()해야 저장된다.)
    // ─────────────────────────────────────────────────────────────

    /// <summary>약관 동의 상태 저장.</summary>
    public void SetTermsAgreed(bool agreed)
    {
        Profile.termsAgreed = agreed; Save();
    }

    /// <summary>보호자 이름·호칭 저장.</summary>
    public void SetGuardian(string name, string prefix)
    {
        Profile.guardianName = name ?? ""; Profile.guardianPrefix = prefix ?? "";
        Save(); PublishChanged();
    }

    /// <summary>보호자 모드 PIN(4자리) 저장.</summary>
    public void SetPin(string pin)
    {
        Profile.guardianPin = pin ?? ""; Save();
    }

    // ─────────────────────────────────────────────────────────────
    // 아이 프로필(여러 명) — 한 계정 공유, 아이별로 보상(스티커) 관리
    // ─────────────────────────────────────────────────────────────

    /// <summary>등록된 아이 프로필 목록(읽기 전용).</summary>
    public IReadOnlyList<ChildProfile> Children => Profile.children;

    /// <summary>현재 활성 아이 프로필. 없으면 null. (activeChildId가 가리키는 아이, 없으면 첫 아이)</summary>
    public ChildProfile ActiveChild
    {
        get
        {
            var list = Profile.children;
            if (list == null || list.Count == 0) return null;
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == profile.activeChildId) return list[i];
            return list[0];
        }
    }

    /// <summary>아이 프로필을 추가하고(첫 아이면 활성으로 지정) 그 프로필을 반환한다.</summary>
    public ChildProfile AddChild(string name, int age, string gender)
    {
        var child = new ChildProfile(NewChildId(), name ?? "", age, gender ?? "");
        Profile.children.Add(child);
        if (string.IsNullOrEmpty(profile.activeChildId)) profile.activeChildId = child.id;
        Save(); PublishChanged();
        return child;
    }

    /// <summary>활성 아이를 바꾼다(프로필 선택). 존재하는 id일 때만 적용.</summary>
    public void SetActiveChild(string childId)
    {
        if (string.IsNullOrEmpty(childId)) return;
        if (!Profile.children.Exists(c => c.id == childId)) return;
        profile.activeChildId = childId;
        Save(); PublishChanged();
    }

    /// <summary>로컬 고유 아이 id 생성(백엔드 없는 단계의 로컬 키 — 시각+순번).</summary>
    private string NewChildId()
        => "c" + DateTimeOffset.Now.ToUnixTimeMilliseconds() + "_" + (Profile.children?.Count ?? 0);

    /// <summary>온보딩 완료 처리 — 표시 이름을 활성 아이 이름으로 맞추고 저장. 다음 실행부터 메인으로 바로 시작.</summary>
    public void CompleteOnboarding()
    {
        Profile.onboardingComplete = true;
        var active = ActiveChild;
        if (active != null && !string.IsNullOrEmpty(active.name)) Profile.userName = active.name;
        Save(); PublishChanged();
    }

    /// <summary>온보딩을 이미 마쳤는지(시작 화면 분기에 사용).</summary>
    public bool OnboardingComplete => Profile.onboardingComplete;

    // ─────────────────────────────────────────────────────────────
    // 앱 모드(보호자/아이) — 세션 상태(저장 안 함). 기본은 아이 모드.
    // 보호자 모드 진입은 PIN 대조로만 허용한다(아이가 임의로 못 바꾸게).
    // ─────────────────────────────────────────────────────────────

    private AppMode currentMode = AppMode.Child;

    /// <summary>현재 앱 모드(기본 아이).</summary>
    public AppMode CurrentMode => currentMode;

    /// <summary>현재 보호자 모드인지.</summary>
    public bool IsGuardian => currentMode == AppMode.Guardian;

    /// <summary>보호자 모드 기본 PIN — PIN 미설정(온보딩 건너뜀 등) 시 이 값으로 검증한다.</summary>
    public const string DefaultGuardianPin = "0000";

    /// <summary>검증에 쓰는 실제 PIN — 저장된 PIN이 있으면 그것, 없으면 기본 0000.</summary>
    private string EffectiveGuardianPin
        => string.IsNullOrEmpty(Profile.guardianPin) ? DefaultGuardianPin : Profile.guardianPin;

    /// <summary>입력 PIN이 보호자 PIN(미설정 시 기본 0000)과 일치하는지. (모드 전환은 하지 않음)</summary>
    public bool VerifyGuardianPin(string pin) => pin == EffectiveGuardianPin;

    /// <summary>PIN이 일치하면 보호자 모드로 전환하고 true 반환(미설정 시 기본 0000과 대조). 불일치면 false.</summary>
    public bool TryEnterGuardianMode(string pin)
    {
        if (!VerifyGuardianPin(pin)) return false;
        SetMode(AppMode.Guardian);
        return true;
    }

    /// <summary>아이 모드로 되돌린다(보호자 모드 종료).</summary>
    public void ExitToChildMode() => SetMode(AppMode.Child);

    /// <summary>
    /// 앱 모드를 직접 지정한다(PIN 검증 없이). 설정 화면의 모드 전환 버튼처럼
    /// 이미 화면 흐름상 허용된 곳에서 사용한다. 보안이 필요한 진입은 <see cref="TryEnterGuardianMode"/>를 쓸 것.
    /// </summary>
    public void SetAppMode(AppMode mode) => SetMode(mode);

    private void SetMode(AppMode mode)
    {
        if (currentMode == mode) return;
        currentMode = mode;
        EventBus.Publish(new AppModeChangedEvent { mode = mode });
    }

    // ─────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────────────────────────

    private void PublishChanged() => EventBus.Publish(new UserProfileChangedEvent { profile = profile });
}

/// <summary>앱 사용 모드 — 아이(기본)와 보호자. 보호자 모드는 PIN으로만 진입한다.</summary>
public enum AppMode
{
    Child,
    Guardian,
}

/// <summary>앱 모드가 바뀌었을 때 EventBus로 발행되는 이벤트(설정/UI가 구독해 표시를 바꿀 수 있다).</summary>
public struct AppModeChangedEvent
{
    public AppMode mode;
}

using System;

/// <summary>
/// 앱 전역에서 공유하는 유저 정보를 담는 직렬화 가능 데이터 컨테이너.
/// JsonUtility로 직렬화되어 DataMng를 통해 저장/복원된다.
/// 순수 데이터(POCO)라 MonoBehaviour와 무관 — 어디서나 복사·전달할 수 있다.
/// 실제 전역 접근·저장은 UserMng가 담당한다.
/// </summary>
[Serializable]
public class UserProfile
{
    /// <summary>표시용 유저 이름. 기본값은 "하준"(온보딩에서 아이 이름으로 갱신).</summary>
    public string userName = "하준";

    // ── 온보딩(회원가입) 정보 ──

    /// <summary>약관 동의 완료 여부.</summary>
    public bool termsAgreed;

    /// <summary>보호자 이름.</summary>
    public string guardianName = "";

    /// <summary>보호자 호칭 — "엄마"/"아빠"/"조부모"/"선생님" 중 하나.</summary>
    public string guardianPrefix = "";

    /// <summary>보호자 모드 PIN(4자리). 보호자 모드 진입 시 대조한다.</summary>
    public string guardianPin = "";

    /// <summary>아이 이름(현재 1명 기준).</summary>
    public string childName = "";

    /// <summary>아이 나이(만).</summary>
    public int childAge;

    /// <summary>아이 성별 — "남"/"여".</summary>
    public string childGender = "";

    /// <summary>온보딩(약관→보호자정보→PIN→아이정보) 전부 완료했는지. 완료면 다음 실행부터 메인으로 바로 시작.</summary>
    public bool onboardingComplete;

    // ── 확장 지점 ──
    // 여기에 공유하고 싶은 유저 데이터를 필드로 추가하면 자동으로 저장/복원된다.
    //   예) public int totalBrushCount;          // 누적 양치 횟수
    // 주의: 필드 추가/이름 변경 시 기존 저장 데이터와의 호환을 고려할 것.
}

/// <summary>
/// 유저 프로필이 변경됐을 때 EventBus로 발행되는 이벤트.
/// UI(이름 표시 등)는 이걸 구독해 화면을 갱신할 수 있다.
/// </summary>
public struct UserProfileChangedEvent
{
    /// <summary>변경 후의 프로필(읽기 전용으로만 사용할 것).</summary>
    public UserProfile profile;
}

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
    /// <summary>유저 고유 식별자. 최초 1회 자동 생성 후 변경하지 않는다.</summary>
    public string uuid;

    /// <summary>표시용 유저 이름. 기본은 빈 문자열, 온보딩/설정에서 채운다.</summary>
    public string userName = "";

    // ── 확장 지점 ──
    // 여기에 공유하고 싶은 유저 데이터를 필드로 추가하면 자동으로 저장/복원된다.
    //   예) public int totalBrushCount;          // 누적 양치 횟수
    //       public string lastPlayedIsoDate;     // 마지막 플레이 시각(ISO 8601)
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

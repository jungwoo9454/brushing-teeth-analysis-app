using System;
using System.Collections.Generic;

/// <summary>
/// 앱 전역에서 공유하는 유저 정보를 담는 직렬화 가능 데이터 컨테이너.
/// JsonUtility로 직렬화되어 DataMng를 통해 저장/복원된다.
/// 순수 데이터(POCO)라 MonoBehaviour와 무관 — 어디서나 복사·전달할 수 있다.
/// 실제 전역 접근·저장은 UserMng가 담당한다.
///
/// 계정 구조: 넷플릭스처럼 한 계정(보호자)을 공유하고, 그 아래 여러 '아이 프로필'이 존재한다.
/// </summary>
[Serializable]
public class UserProfile
{
    /// <summary>표시용 유저 이름. 기본값은 "하준"(온보딩에서 활성 아이 이름으로 갱신).</summary>
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

    /// <summary>온보딩(약관→보호자정보→PIN→아이정보) 전부 완료했는지.</summary>
    public bool onboardingComplete;

    // ── 아이 프로필 (여러 명 — 한 계정 공유) ──

    /// <summary>등록된 아이 프로필 목록. 보상(스티커) 등은 아이별로 관리된다.</summary>
    public List<ChildProfile> children = new List<ChildProfile>();

    /// <summary>현재 선택된(활성) 아이 프로필 id. 양치/보상은 이 아이 기준으로 처리된다.</summary>
    public string activeChildId = "";

    // ── 구버전 단일 아이 필드 (마이그레이션 전용 — 신규 코드는 children 사용) ──
    // 과거 저장본에 값이 있으면 UserMng가 children[0]으로 1회 이관한 뒤 비운다.
    public string childName = "";
    public int childAge;
    public string childGender = "";
}

/// <summary>
/// 아이 한 명의 프로필. 한 계정(보호자) 아래 여러 명이 존재할 수 있다.
/// 보상(획득 스티커)은 아이마다 따로 보관한다.
/// </summary>
[Serializable]
public class ChildProfile
{
    /// <summary>로컬 고유 id(생성 시 부여). 스티커 등 아이별 데이터의 키.</summary>
    public string id = "";

    public string name = "";
    public int age;

    /// <summary>"남"/"여".</summary>
    public string gender = "";

    /// <summary>이 아이가 획득한 스티커 id 목록(버디 보상). 미획득은 카탈로그와의 차집합으로 구한다.</summary>
    public List<string> earnedStickerIds = new List<string>();

    public ChildProfile() { }

    public ChildProfile(string id, string name, int age, string gender)
    {
        this.id = id;
        this.name = name;
        this.age = age;
        this.gender = gender;
    }
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

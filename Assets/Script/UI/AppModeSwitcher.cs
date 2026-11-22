using UnityEngine;

/// <summary>
/// 설정(Setting) 화면의 '아이 모드 / 보호자 모드' 전환 버튼에 연결하는 헬퍼.
///
/// 사용법(추후 UI 작업): 설정 화면의 아무 오브젝트에 이 컴포넌트를 붙이고,
/// 모드 전환 버튼의 OnClick에 아래 공개 메서드 중 하나를 드래그해 연결한다.
///   - <see cref="SwitchToChildMode"/>   : 아이 모드(홈·버디·설정)로 전환.
///   - <see cref="SwitchToGuardianMode"/>: 보호자 모드(홈·리포트·설정)로 전환(PIN 없이 즉시).
///   - <see cref="ToggleMode"/>          : 현재 모드의 반대로 토글.
///
/// 전환되면 UserMng가 AppModeChangedEvent를 발행하고, AppRouter가 현재 탭 화면을 다시 띄워
/// 탭 구성(버디↔리포트)을 새 모드에 맞춰 갱신한다 — 별도 화면 새로고침 코드는 필요 없다.
///
/// 보안 진입(아이가 임의로 보호자 모드에 못 들어오게 PIN 검증)이 필요하면,
/// 이 컴포넌트 대신 PIN 입력 후 <see cref="UserMng.TryEnterGuardianMode"/>를 호출하는 흐름을 쓰면 된다.
/// </summary>
public class AppModeSwitcher : MonoBehaviour
{
    /// <summary>아이 모드로 전환.</summary>
    public void SwitchToChildMode() => UserMng.ins.SetAppMode(AppMode.Child);

    /// <summary>보호자 모드로 전환(PIN 검증 없이 즉시 — 화면 흐름상 허용된 버튼용).</summary>
    public void SwitchToGuardianMode() => UserMng.ins.SetAppMode(AppMode.Guardian);

    /// <summary>현재 모드의 반대로 토글.</summary>
    public void ToggleMode()
        => UserMng.ins.SetAppMode(UserMng.ins.IsGuardian ? AppMode.Child : AppMode.Guardian);

    /// <summary>PIN으로 보호자 모드 진입(일치해야 전환). 성공 여부를 반환.</summary>
    public bool TryEnterGuardianMode(string pin) => UserMng.ins.TryEnterGuardianMode(pin);
}

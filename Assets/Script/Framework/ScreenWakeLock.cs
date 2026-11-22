using UnityEngine;

/// <summary>
/// 앱 실행 동안 화면이 자동으로 꺼지지 않게 한다(특히 양치 중 손을 안 대도 카메라가 계속 보여야 함).
///
/// <see cref="Screen.sleepTimeout"/>을 <see cref="SleepTimeout.NeverSleep"/>로 두면 OS의 화면 자동 잠금이
/// 비활성화된다. 씬에 오브젝트를 둘 필요 없이 앱 시작 시(씬 로드 전) 1회 설정한다.
/// 일부 플랫폼은 포커스 복귀 시 값이 초기화될 수 있어, 안전하게 부팅 시 한 번만 박아둔다.
/// </summary>
public static class ScreenWakeLock
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}

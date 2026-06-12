using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 양치(Brushing) 화면의 Close 버튼. 누르면 진행 중인 양치를 "즉시" 종료한다.
///
/// 동작: <see cref="BrushingSession.EndSession"/>을 호출하면, 지금까지 누적된 양치 데이터로
/// BrushingSessionCompletedEvent가 발행되고 — 그 이벤트를
///   · BrushingRecordWriter 가 받아 기록을 저장(JSON)하고,
///   · AppRouter 가 받아 BrushingFinish 결과 화면으로 전환한다.
/// 즉 자동 종료(시간 만료)와 완전히 동일한 경로를 수동으로 트리거할 뿐이다(별도 저장/전환 코드 불필요).
///
/// Close 버튼 GameObject에 부착한다(Button 필요). onClick은 Awake에서 코드로 연결한다.
/// </summary>
[RequireComponent(typeof(Button))]
public class BrushingCloseButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(EndBrushing);
    }

    private void EndBrushing()
    {
        // 씬에 떠 있는 BrushingSession을 찾아 종료. EndSession은 이미 종료된 세션이면 no-op이라 중복 클릭도 안전.
        var session = FindFirstObjectByType<BrushingSession>();
        if (session == null)
        {
            Debug.LogWarning("[BrushingCloseButton] 씬에서 BrushingSession을 찾지 못했다 — 종료할 세션이 없다.");
            return;
        }
        session.EndSession();
    }
}

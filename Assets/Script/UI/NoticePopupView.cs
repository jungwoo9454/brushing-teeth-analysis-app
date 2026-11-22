using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// '확인' 버튼 하나만 있는 안내 팝업(NoticePopup). NoticePopup 프리팹 루트(Canvas)에 부착한다.
///
/// 사용: <c>NoticePopupView.Show(noticePopupPrefab, "보호자 모드로 전환되었습니다.", onConfirm);</c>
///   - 독립 루트 Canvas로 생성되므로, 띄운 화면이 그 사이 교체·파괴돼도(예: 모드 전환 시 AppRouter가
///     현재 탭 화면을 다시 생성) 팝업은 그대로 남는다.
///   - '확인'을 누르면 팝업을 닫고 onConfirm을 호출한다. 반투명 배경은 뒤 화면 클릭을 막기만 한다.
///
/// 자식 참조는 경로로 자동 탐색한다 — 프리팹 구조: Dim / Panel / (Message, Confirm/Text).
/// </summary>
[DisallowMultipleComponent]
public class NoticePopupView : MonoBehaviour
{
    private const string PathMessage = "Panel/Message";
    private const string PathConfirm = "Panel/Confirm";

    /// <summary>다른 화면·팝업(설정 팝업 100)보다 위에 그려지도록 쓰는 정렬 순서.</summary>
    private const int SortingOrder = 200;

    private TMP_Text _message;
    private Button _confirm;
    private Action _onConfirm;
    private bool _closed;

    /// <summary>
    /// prefab으로 팝업을 띄우고 메시지를 표시한다. prefab이 비었으면 경고만 남기고 onConfirm을 즉시 호출해
    /// 화면 흐름(예: 홈 이동)이 막히지 않게 한다.
    /// </summary>
    public static NoticePopupView Show(GameObject prefab, string message, Action onConfirm = null)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[NoticePopupView] 팝업 프리팹이 연결되지 않아 안내를 생략한다: {message}");
            onConfirm?.Invoke();
            return null;
        }

        var go = Instantiate(prefab);
        go.name = prefab.name;
        go.SetActive(true);

        var view = go.GetComponent<NoticePopupView>();
        if (view == null)
        {
            Debug.LogError("[NoticePopupView] 프리팹 루트에 NoticePopupView가 없다.");
            Destroy(go);
            onConfirm?.Invoke();
            return null;
        }

        view.Bind(message, onConfirm);
        return view;
    }

    private void Awake()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null) { canvas.overrideSorting = true; canvas.sortingOrder = SortingOrder; }

        _message = transform.Find(PathMessage)?.GetComponent<TMP_Text>();
        _confirm = OnboardingUtil.EnsureButton(transform.Find(PathConfirm));
        if (_confirm != null) _confirm.onClick.AddListener(OnConfirm);
    }

    /// <summary>메시지와 '확인' 콜백을 지정한다.</summary>
    public void Bind(string message, Action onConfirm)
    {
        if (_message != null) _message.text = message ?? "";
        _onConfirm = onConfirm;
    }

    private void OnConfirm()
    {
        if (_closed) return;   // 연타로 콜백이 두 번 불리지 않게.
        _closed = true;

        var callback = _onConfirm;
        _onConfirm = null;
        Destroy(gameObject);
        callback?.Invoke();
    }
}

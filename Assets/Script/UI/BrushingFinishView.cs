using System;

using TMPro;
using UnityEngine;

/// <summary>
/// 양치 결과(BrushingFinish) 화면에 한 세션 결과를 바인딩하는 뷰.
/// AppRouter가 결과 프리팹을 생성한 직후 Bind(...)로 BrushingScoredEvent 값을 주입한다
/// (이벤트를 직접 구독하지 않는 수동 뷰 — 테스트/재사용이 쉽다).
///
/// TMP 참조는 인스펙터에서 프리팹의 각 텍스트 오브젝트에 연결한다.
/// </summary>
public class BrushingFinishView : MonoBehaviour
{
    [Header("결과 표시 텍스트 (프리팹의 TMP에 연결)")]
    [Tooltip("점수 — Slot/xp/Text (TMP)")]
    [SerializeField] private TMP_Text scoreText;

    [Tooltip("제목/이름 — brushing finish title")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("점수 메시지(큰 줄) — Short report/ert")]
    [SerializeField] private TMP_Text messageLine1;

    [Tooltip("구역 메시지(작은 줄) — Short report/ert (1)")]
    [SerializeField] private TMP_Text messageLine2;

    [Tooltip("세션 날짜/시간대 — brushing info")]
    [SerializeField] private TMP_Text sessionInfoText;

    [Tooltip("연속 양치일수 — Slot/day/Text (TMP)")]
    [SerializeField] private TMP_Text streakText;

    /// <summary>한 세션의 점수 결과를 화면 각 텍스트에 채운다. 참조가 비어 있는 항목은 건너뛴다.</summary>
    public void Bind(BrushingScoredEvent r)
    {
        if (scoreText != null)       scoreText.text       = $"{r.score}점";
        if (titleText != null)       titleText.text       = BuildTitle();
        if (messageLine1 != null)    messageLine1.text    = ScoreMessage(r.score);
        if (messageLine2 != null)    messageLine2.text    = ZoneMessage(r.zoneSeconds, r.targetPerZoneSeconds);
        if (sessionInfoText != null) sessionInfoText.text = SessionInfo();
        if (streakText != null)      streakText.text      = $"{Mathf.Max(0, r.streakDays)}일 연속";
    }

    /// <summary>제목 — "잘했어, {이름}!". 이름이 없으면 이름 없이 표시.</summary>
    private static string BuildTitle()
    {
        var name = UserMng.ins != null ? UserMng.ins.UserName : null;
        return string.IsNullOrWhiteSpace(name) ? "잘했어!" : $"잘했어, {name}!";
    }

    /// <summary>점수 구간별 칭찬(큰 줄).</summary>
    private static string ScoreMessage(int score)
    {
        if (score >= 70) return "세균을 많이 물리쳤어요!";
        if (score >= 40) return "조금 더 꼼꼼히 닦아볼까요?";
        return "다음엔 더 오래 닦아봐요!";
    }

    /// <summary>
    /// 구역 데이터 기반 메시지(작은 줄). 모든 구역이 목표를 채웠으면 칭찬,
    /// 아니면 가장 적게 닦은 구역을 콕 집어 안내한다.
    /// </summary>
    private static string ZoneMessage(float[] zoneSeconds, float targetPerZone)
    {
        if (zoneSeconds == null || zoneSeconds.Length == 0)
            return "다음엔 모든 구역을 닦아봐요";

        int weakest = 0;
        float weakestSec = float.MaxValue;
        bool allMet = targetPerZone > 0f;
        for (int i = 0; i < zoneSeconds.Length; i++)
        {
            float sec = zoneSeconds[i];
            if (sec < targetPerZone) allMet = false;
            if (sec < weakestSec) { weakestSec = sec; weakest = i; }
        }

        if (allMet) return $"{zoneSeconds.Length}구역을 모두 잘 닦았어요";

        string zoneName = ZoneLabels.Korean((UiZone)weakest);
        return $"{zoneName}를 조금 더 닦아볼까요?";
    }

    /// <summary>
    /// 세션 정보 — "2026년 6월 1일 아침 양치 완료!" (바인딩 시각 기준).
    /// 시간대는 저장 기록과 동일하게 <see cref="BrushingTimeSlot"/>(아침 06~10 / 점심 10~16 / 그 외 저녁)을 따른다.
    /// </summary>
    private static string SessionInfo()
    {
        var now = DateTime.Now;
        return $"{now.Year}년 {now.Month}월 {now.Day}일 {BrushingTimeSlot.Korean(now.Hour)} 양치 완료!";
    }
}

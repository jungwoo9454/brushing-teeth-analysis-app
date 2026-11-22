using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 브러싱 리포트(BrushingReport) 화면에 '방금 양치한 한 세션' 결과를 바인딩하는 뷰.
/// AppRouter가 화면 프리팹을 생성한 직후 Bind(BrushingScoredEvent)로 값을 주입한다
/// (BrushingFinishView와 동일한 수동 바인딩 방식).
///
/// 표시:
///   - 점수원(Circle score): 점수 텍스트 + 원형 fill(점수/100)
///   - 5개 구역 섹션(Section_LU/LB/Center/RU/RB)의 Indicator를 구역 충족도에 따라 Good/Normal/Bad 색으로
///   - Alert report: 가장 부족한 구역 안내
///
/// 자식은 경로로 자동 탐색한다(BrushingReport 프리팹 루트에 부착).
/// </summary>
[DisallowMultipleComponent]
public class BrushingReportView : MonoBehaviour
{
    [Header("구역 충족 색")]
    [SerializeField] private Color goodColor   = new Color(0.298f, 0.686f, 0.314f, 1f); // 초록
    [SerializeField] private Color normalColor = new Color(0.961f, 0.651f, 0.137f, 1f); // 주황
    [SerializeField] private Color badColor    = new Color(0.898f, 0.325f, 0.239f, 1f); // 빨강
    [SerializeField, Range(0f, 1f)] private float goodThreshold   = 0.66f;
    [SerializeField, Range(0f, 1f)] private float normalThreshold = 0.33f;

    private const string PathScore = "RealSpace/Score report/Circle score/Score";
    private const string PathFill  = "RealSpace/Score report/Circle score/Circle score/fill";
    private const string PathHello = "RealSpace/Top/Profile/TextInfo/hello ment";
    private const string PathAlertZone = "RealSpace/Alert report/TextArea/Teeth location";
    private const string PathAlertText = "RealSpace/Alert report/TextArea/report";

    // 섹션 경로와 그 섹션이 나타내는 UiZone(0=좌상,1=우상,2=좌하,3=우하,4=앞니).
    private static readonly (string path, UiZone zone)[] Sections =
    {
        ("RealSpace/Section Area/Left/Section_LU",    UiZone.UpperLeft),
        ("RealSpace/Section Area/Left/Section_LB",    UiZone.LowerLeft),
        ("RealSpace/Section Area/Section_Center",     UiZone.Front),
        ("RealSpace/Section Area/Right/Section_RU",   UiZone.UpperRight),
        ("RealSpace/Section Area/Right/Section_RB",   UiZone.LowerRight),
    };

    /// <summary>한 세션 점수 결과를 화면에 채운다. 참조가 없는 항목은 건너뛴다.</summary>
    public void Bind(BrushingScoredEvent r)
    {
        // 점수 — 숫자 + 원형 fill.
        var scoreText = FindComp<TMP_Text>(PathScore);
        if (scoreText != null) scoreText.text = r.score.ToString();

        var fill = FindComp<Image>(PathFill);
        if (fill != null)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Bottom;
            fill.fillAmount = Mathf.Clamp01(r.score / 100f);
        }

        // 구역 섹션 — 충족도(zoneSeconds/perZone)에 따라 Indicator 색.
        float per = r.targetPerZoneSeconds > 0f ? r.targetPerZoneSeconds : 6f;
        foreach (var (path, zone) in Sections)
        {
            var ind = FindComp<Image>(path + "/Indicator");
            if (ind == null) continue;
            float cov = Coverage(r.zoneSeconds, (int)zone, per);
            ind.color = cov >= goodThreshold ? goodColor : (cov >= normalThreshold ? normalColor : badColor);
        }

        // Alert — 가장 부족한 구역 안내.
        BindAlert(r, per);

        // 프로필 한 줄 — 점수 칭찬.
        var hello = FindComp<TMP_Text>(PathHello);
        if (hello != null) hello.text = Praise(r.score);
    }

    private void BindAlert(BrushingScoredEvent r, float per)
    {
        var zoneText = FindComp<TMP_Text>(PathAlertZone);
        var msgText  = FindComp<TMP_Text>(PathAlertText);
        if (zoneText == null && msgText == null) return;

        // 충족도가 가장 낮은 구역.
        int worst = 0;
        float worstCov = float.MaxValue;
        bool hasZones = r.zoneSeconds != null && r.zoneSeconds.Length >= 5;
        if (hasZones)
        {
            for (int z = 0; z < 5; z++)
            {
                float cov = Coverage(r.zoneSeconds, z, per);
                if (cov < worstCov) { worstCov = cov; worst = z; }
            }
        }

        string zoneName = hasZones ? ZoneLabels.Korean((UiZone)worst) : "전체";
        if (zoneText != null) zoneText.text = zoneName;
        if (msgText != null)
            msgText.text = (hasZones && worstCov < goodThreshold)
                ? $"{zoneName}를 조금 더 꼼꼼히 닦아볼까요?"
                : "모든 구역을 골고루 잘 닦았어요!";
    }

    private static float Coverage(float[] zoneSeconds, int zone, float per)
    {
        if (zoneSeconds == null || zone < 0 || zone >= zoneSeconds.Length || per <= 0f) return 0f;
        return Mathf.Clamp01(zoneSeconds[zone] / per);
    }

    private static string Praise(int score)
    {
        if (score >= 70) return "정말 잘했어요!";
        if (score >= 40) return "잘했어요! 다음엔 더 꼼꼼히";
        return "다음엔 더 오래 닦아봐요";
    }

    private T FindComp<T>(string path) where T : Component
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }
}

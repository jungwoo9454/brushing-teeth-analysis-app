using UnityEngine;
using UnityEngine.UI;

namespace Sesalbeoreut.UI
{
    // 양치 중 화면 (UGUI)
    // 디자인 참조: design/hifi/screens.jsx HiBrush
    //   상단: 타이머 + "다음 닦을 곳" 칩 + 일시정지/닫기
    //   중앙: 진행률 바 + 큰 카메라 뷰(250px) — 입 영역 점선 박스 + 칫솔 트래커 + AI 분석 라벨
    //   하단: 캐릭터(120px) + 말풍선(현재 닦을 곳)
    //
    // 이 컴포넌트를 빈 GameObject에 붙이면 Awake 시 전체 레이아웃을 생성한다.
    // 카메라 RawImage와 트래커/입 박스는 외부에서 BrushingScreenController가 갱신한다.
    public class BrushingScreenBuilder : MonoBehaviour
    {
        // 디자인 토큰 (라이트 테마 기준) — 다크 테마 전환은 ThemeManager에서 처리
        public static readonly Color BG          = Hex("#F5F2EC");
        public static readonly Color BG_ELEV     = Hex("#FFFFFF");
        public static readonly Color BG_SOFT     = Hex("#ECE7DA");
        public static readonly Color INK         = Hex("#14181D");
        public static readonly Color INK_2       = Hex("#4A5159");
        public static readonly Color INK_3       = Hex("#8B8F94");
        public static readonly Color LINE        = new Color(20f/255f, 24f/255f, 29f/255f, 0.08f);
        public static readonly Color PRIMARY     = Hex("#3B7FE8");
        public static readonly Color ACCENT      = Hex("#F5C94B");
        public static readonly Color CAM_TOP     = Hex("#21283A");
        public static readonly Color CAM_BOTTOM  = Hex("#0E1320");
        public static readonly Color BUBBLE_ACCENT = Hex("#3B7FE8");

        [Header("외부 노출 참조")]
        public RawImage cameraFeed;     // 카메라 피드 (WebCamTexture 등 외부 주입)
        public RectTransform mouthBox;  // 인식된 입 영역 점선 박스
        public RectTransform tracker;   // 칫솔 위치 도트
        public Text timerText;          // 0:48 / 2:00
        public Text nextZoneText;       // "아래 왼쪽 어금니"
        public Text currentZoneText;    // 카메라 하단 라벨
        public Text bubbleZoneText;     // 말풍선 메인 텍스트
        public Image progressFill;      // 상단 진행률 바 fill
        public RectTransform character; // 캐릭터 슬롯 (스프라이트는 외부 주입)

        void Awake()
        {
            BuildLayout();
        }

        void BuildLayout()
        {
            // 최상위 Canvas — 이미 있다면 생성 생략
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("BrushingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(360, 780);
                scaler.matchWidthOrHeight = 0.5f;
                transform.SetParent(canvasGO.transform, false);
            }

            // 화면 루트 (세로 흐름)
            var screen = CreateRect("Screen", transform);
            Stretch(screen);
            AddImage(screen.gameObject, BG);

            float padX = 18f;

            // ───── 상단 헤더 영역 (타이머 + 다음 칩 + 일시정지 + 닫기) ─────
            var header = CreateRect("Header", screen);
            AnchorTop(header, height: 64, padding: new Vector4(padX, 6, padX, 0));

            // 타이머 블록 (좌)
            var timerBlock = CreateRect("TimerBlock", header);
            AnchorLeft(timerBlock, width: 130);
            var capLabel = AddText(timerBlock.gameObject, "양치 중", 11, INK_3);
            AnchorTopLeft(capLabel.rectTransform, new Vector2(0, -2), new Vector2(80, 14));
            timerText = AddText(timerBlock.gameObject, "0:48 / 2:00", 17, INK, FontStyle.Bold);
            AnchorTopLeft(timerText.rectTransform, new Vector2(0, -16), new Vector2(120, 22));

            // 다음 칩 (타이머 옆)
            var nextChip = CreateRect("NextChip", header);
            AnchorLeft(nextChip, width: 110, offset: 130);
            AddImage(nextChip.gameObject, BG_ELEV).type = Image.Type.Sliced;
            AddOutline(nextChip.gameObject, LINE);
            var nextCap = AddText(nextChip.gameObject, "다음", 9, INK_3);
            AnchorTopLeft(nextCap.rectTransform, new Vector2(10, -6), new Vector2(40, 12));
            nextZoneText = AddText(nextChip.gameObject, "아래 왼쪽 어금니", 12, INK, FontStyle.Bold);
            AnchorTopLeft(nextZoneText.rectTransform, new Vector2(10, -20), new Vector2(95, 16));

            // 우측 버튼: 일시정지 + 닫기
            var rightBlock = CreateRect("RightBlock", header);
            AnchorRight(rightBlock, width: 130);
            var pauseBtn = CreatePillButton(rightBlock, "일시정지");
            AnchorTopLeft(pauseBtn, new Vector2(0, -10), new Vector2(80, 32));
            var closeBtn = CreateIconButton(rightBlock, "✕");
            AnchorTopLeft(closeBtn, new Vector2(90, -10), new Vector2(36, 36));

            // ───── 진행률 바 ─────
            var progress = CreateRect("Progress", screen);
            AnchorTop(progress, height: 10, padding: new Vector4(padX, 70, padX, 0));
            AddImage(progress.gameObject, BG_SOFT);
            var fill = CreateRect("Fill", progress);
            AnchorLeft(fill, width: 0); // 폭은 controller가 채움
            fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0.4f, 1);
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            progressFill = AddImage(fill.gameObject, PRIMARY);

            // ───── 카메라 뷰 (250px 높이) ─────
            var cam = CreateRect("Camera", screen);
            AnchorTop(cam, height: 250, padding: new Vector4(padX, 90, padX, 0));
            AddRoundedImage(cam.gameObject, CAM_BOTTOM, 20);

            // 카메라 피드 RawImage (외부 주입 가능)
            var feed = CreateRect("Feed", cam);
            Stretch(feed);
            cameraFeed = feed.gameObject.AddComponent<RawImage>();
            cameraFeed.color = new Color(1, 1, 1, 0.6f); // 임시 — 실제 WebCamTexture 주입 전까지

            // 인식된 입 영역 점선 박스 (32% top, 16% left/right, 22% bottom)
            mouthBox = CreateRect("MouthBox", cam);
            mouthBox.anchorMin = new Vector2(0.16f, 0.22f);
            mouthBox.anchorMax = new Vector2(0.84f, 0.68f);
            mouthBox.offsetMin = Vector2.zero; mouthBox.offsetMax = Vector2.zero;
            AddOutline(mouthBox.gameObject, new Color(1, 1, 1, 0.85f), thickness: 2);

            // 칫솔 트래커 도트
            tracker = CreateRect("Tracker", cam);
            tracker.anchorMin = new Vector2(0.68f, 0.54f);
            tracker.anchorMax = new Vector2(0.68f, 0.54f);
            tracker.sizeDelta = new Vector2(14, 14);
            tracker.anchoredPosition = Vector2.zero;
            AddCircleImage(tracker.gameObject, ACCENT);

            // 카메라 좌상단: AI 분석 라벨
            var aiLabel = CreateRect("AILabel", cam);
            AnchorTopLeft(aiLabel, new Vector2(12, -12), new Vector2(100, 24));
            AddRoundedImage(aiLabel.gameObject, new Color(0, 0, 0, 0.45f), 999);
            var aiText = AddText(aiLabel.gameObject, "● AI 분석 중", 11, Color.white, FontStyle.Bold);
            AnchorCenter(aiText.rectTransform);

            // 카메라 하단: 현재 구역 라벨
            var curZoneLabel = CreateRect("CurZoneLabel", cam);
            curZoneLabel.anchorMin = new Vector2(0, 0); curZoneLabel.anchorMax = new Vector2(0, 0);
            curZoneLabel.pivot = new Vector2(0, 0);
            curZoneLabel.anchoredPosition = new Vector2(12, 12);
            curZoneLabel.sizeDelta = new Vector2(220, 32);
            AddRoundedImage(curZoneLabel.gameObject, new Color(1, 1, 1, 0.96f), 12);
            currentZoneText = AddText(curZoneLabel.gameObject, "● 현재 구역 · 위 오른쪽 어금니", 13, INK, FontStyle.Bold);
            AnchorCenter(currentZoneText.rectTransform);

            // ───── 하단: 캐릭터 + 말풍선 ─────
            var bottom = CreateRect("Bottom", screen);
            AnchorTop(bottom, height: 140, padding: new Vector4(padX, 350, padX, 0));

            // 캐릭터 슬롯 (120px) — 실제 스프라이트는 외부 주입
            character = CreateRect("Character", bottom);
            AnchorLeft(character, width: 120);
            character.sizeDelta = new Vector2(120, 120);
            var charBg = AddImage(character.gameObject, BG_SOFT);
            AddCircleImage(character.gameObject, BG_SOFT); // placeholder
            var charLabel = AddText(character.gameObject, "🐻", 80);
            AnchorCenter(charLabel.rectTransform);

            // 말풍선
            var bubble = CreateRect("Bubble", bottom);
            AnchorLeft(bubble, width: 0, offset: 130);
            bubble.anchorMin = new Vector2(0, 0); bubble.anchorMax = new Vector2(1, 1);
            bubble.offsetMin = new Vector2(130, 8); bubble.offsetMax = new Vector2(0, -16);
            AddRoundedImage(bubble.gameObject, BG_ELEV, 18);
            AddOutline(bubble.gameObject, LINE);

            var bubCap = AddText(bubble.gameObject, "지금 닦을 곳", 10, PRIMARY, FontStyle.Bold);
            AnchorTopLeft(bubCap.rectTransform, new Vector2(14, -10), new Vector2(100, 14));
            bubbleZoneText = AddText(bubble.gameObject, "위 오른쪽 어금니", 15, INK, FontStyle.Bold);
            AnchorTopLeft(bubbleZoneText.rectTransform, new Vector2(14, -26), new Vector2(180, 18));
            var bubHint = AddText(bubble.gameObject, "위에서 아래로 살살 ~", 12, INK_2);
            AnchorTopLeft(bubHint.rectTransform, new Vector2(14, -46), new Vector2(180, 16));
        }

        // ───── UGUI 헬퍼 ─────
        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static Image AddImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static Image AddRoundedImage(GameObject go, Color color, float cornerRadius)
        {
            // Unity 기본 UI에는 둥근 모서리가 직접 지원되지 않으므로 단색으로 대체.
            // 실제 둥근 모서리는 9-slice 스프라이트 또는 셰이더로 구현.
            return AddImage(go, color);
        }

        static Image AddCircleImage(GameObject go, Color color)
        {
            // 정확한 원 표현은 마스크/스프라이트 필요. 일단 단색 채움.
            return AddImage(go, color);
        }

        static Outline AddOutline(GameObject go, Color color, float thickness = 1)
        {
            var ol = go.AddComponent<Outline>();
            ol.effectColor = color;
            ol.effectDistance = new Vector2(thickness, thickness);
            return ol;
        }

        static Text AddText(GameObject go, string text, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var tGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tGo.transform.SetParent(go.transform, false);
            var t = tGo.GetComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            // Pretendard 폰트는 Resources/Font/ 하위에 추가될 예정.
            // 폰트 적용은 후속 작업에서 일괄 처리한다.
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }

        static Text AddText(GameObject go, string text, int size, Color color)
            => AddText(go, text, size, color, FontStyle.Normal);

        static Text AddText(GameObject go, string text, int size)
            => AddText(go, text, size, INK, FontStyle.Normal);

        static RectTransform CreatePillButton(Transform parent, string label)
        {
            var rt = CreateRect("Btn-" + label, parent);
            AddImage(rt.gameObject, BG_ELEV);
            AddOutline(rt.gameObject, LINE);
            var t = AddText(rt.gameObject, label, 12, INK_2, FontStyle.Bold);
            AnchorCenter(t.rectTransform);
            rt.gameObject.AddComponent<Button>();
            return rt;
        }

        static RectTransform CreateIconButton(Transform parent, string icon)
        {
            var rt = CreateRect("IconBtn-" + icon, parent);
            AddImage(rt.gameObject, BG_ELEV);
            AddOutline(rt.gameObject, LINE);
            var t = AddText(rt.gameObject, icon, 18, INK, FontStyle.Bold);
            AnchorCenter(t.rectTransform);
            rt.gameObject.AddComponent<Button>();
            return rt;
        }

        // ───── 앵커 헬퍼 ─────
        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void AnchorTop(RectTransform rt, float height, Vector4 padding)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -padding.y);
            rt.offsetMin = new Vector2(padding.x, -height - padding.y);
            rt.offsetMax = new Vector2(-padding.z, -padding.y);
        }

        static void AnchorLeft(RectTransform rt, float width, float offset = 0)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(offset, 0);
            rt.sizeDelta = new Vector2(width, 0);
        }

        static void AnchorRight(RectTransform rt, float width)
        {
            rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, 0);
        }

        static void AnchorTopLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void AnchorCenter(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}

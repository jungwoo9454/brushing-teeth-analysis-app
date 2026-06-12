// 카메라 텍스처를 센서 방향(videoRotationAngle)에 맞춰 회전 + (전면 셀카) 가로 반전해
// RenderTexture에 굽는 전용 블릿 셰이더. Graphics.Blit(src, dst, material)로 사용하며,
// Blit이 플랫폼별 Y 뒤집힘을 처리하므로 GL 직접 렌더보다 이식성이 안전하다.
//
// _AngleDeg: 표시 이미지를 "시계방향"으로 돌려야 하는 각도(0/90/180/270 = WebCamTexture.videoRotationAngle).
// _FlipX   : 1이면 소스(webcam) 좌표계에서 가로 반전 — 전면 카메라 unmirror용.
//            (기존 BrushingPipeline.PrepareSource의 Graphics.Blit scale(-1,1) offset(1,0)과 동일 의미)
Shader "Hidden/CameraOrient"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AngleDeg ("Rotate CW degrees", Float) = 0
        _FlipX ("Flip X (source space)", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            float _AngleDeg;
            float _FlipX;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 출력 UV(i.uv)를 소스 UV로 역매핑한다.
                // 표시 이미지를 시계방향 _AngleDeg 만큼 돌리려면 샘플 좌표를 그 반대 방향으로 회전한다.
                float a = radians(-_AngleDeg);
                float s = sin(a);
                float co = cos(a);
                float2 c = i.uv - 0.5;
                float2 r = float2(c.x * co - c.y * s, c.x * s + c.y * co) + 0.5;

                // 소스 좌표계에서의 가로 반전(unmirror) — 회전 매핑 이후 적용해 기존 동작과 일치.
                if (_FlipX > 0.5) r.x = 1.0 - r.x;

                return tex2D(_MainTex, r);
            }
            ENDCG
        }
    }
    Fallback Off
}

using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 파티클 프리팹 생성 에디터 유틸. Tools/세살버릇/Build Particles 로 실행한다.
///   - ConfettiBurst : 스티커 획득 축하(색종이 버스트, 일회성)
///   - BrushingBubbles : 양치 중 아래→위로 올라오는 버블(루프, BrushingBubblesController로 on/off)
/// 결과물은 Assets/Resources/Particle/ 에 저장된다(머티리얼·텍스처 포함).
/// </summary>
public static class ParticleFactory
{
    private const string Dir = "Assets/Resources/Particle";

    [MenuItem("Tools/세살버릇/Build Particles")]
    public static void BuildAll()
    {
        EnsureFolder();
        string circle = CreateCircleTexture();
        BuildConfetti();
        BuildBubbles(circle);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ParticleFactory] 파티클 프리팹 생성 완료 → " + Dir + " (ConfettiBurst, BrushingBubbles)");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Resources", "Particle");
    }

    private static Material MakeMaterial(string name, Texture2D tex)
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

        string path = Dir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = shader;
        if (tex != null) mat.mainTexture = tex;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>버블용 부드러운 원형 텍스처(가운데 밝고 가장자리 투명) 생성.</summary>
    private static string CreateCircleTexture()
    {
        string path = Dir + "/CircleSoft.png";
        int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2((s - 1) / 2f, (s - 1) / 2f);
        float rad = s / 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / rad;     // 0(중심)~1(가장자리)
                float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d));        // 가장자리로 갈수록 투명
                // 비눗방울 느낌: 살짝 링(테두리) 강조
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.12f) * 0.35f;
                a = Mathf.Clamp01(a * 0.55f + ring);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null) { imp.textureType = TextureImporterType.Default; imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.SaveAndReimport(); }
        return path;
    }

    private static void BuildConfetti()
    {
        var mat = MakeMaterial("ConfettiParticle", null);

        var go = new GameObject("ConfettiBurst");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.30f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.1f;
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;

        var palette = new Gradient();
        palette.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.957f, 0.263f, 0.212f), 0.00f),
                new GradientColorKey(new Color(1.000f, 0.757f, 0.027f), 0.25f),
                new GradientColorKey(new Color(0.298f, 0.686f, 0.314f), 0.50f),
                new GradientColorKey(new Color(0.129f, 0.588f, 0.953f), 0.75f),
                new GradientColorKey(new Color(0.611f, 0.153f, 0.690f), 1.00f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(palette) { mode = ParticleSystemGradientMode.RandomColor };

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)90) });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 0.25f; sh.arc = 360f; sh.radiusThickness = 1f;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-3.5f, 3.5f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(0f, 0.75f, 1f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.sortingOrder = 100;

        PrefabUtility.SaveAsPrefabAsset(go, Dir + "/ConfettiBurst.prefab");
        Object.DestroyImmediate(go);
    }

    private static void BuildBubbles(string circleTexPath)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(circleTexPath);
        var mat = MakeMaterial("BubbleParticle", tex);

        var go = new GameObject("BrushingBubbles");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 3f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.40f);
        main.gravityModifier = -0.05f;     // 살짝 부력(위로)
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.92f, 1f, 0.6f), new Color(1f, 1f, 1f, 0.5f));

        var em = ps.emission;
        em.rateOverTime = 14f;

        var sh = ps.shape;                  // 하단을 가로로 넓게 — 화면 밑에서 올라오게
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(6f, 0.2f, 0.1f);

        var vel = ps.velocityOverLifetime;  // 아래→위 이동
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        // x·y·z 모두 같은 모드(TwoConstants)여야 한다 — z를 안 주면 z만 Constant라 경고가 난다.
        vel.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        vel.y = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = ps.noise;               // 좌우로 살랑살랑
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.3f;

        var col = ps.colorOverLifetime;     // 떠오르며 나타났다 사라짐
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;      // 살짝 커졌다 작아짐
        sol.enabled = true;
        var sc = new AnimationCurve();
        sc.AddKey(0f, 0.6f); sc.AddKey(0.3f, 1f); sc.AddKey(1f, 0.9f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sc);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = mat;
        r.sortingOrder = 50;

        // 양치 중에만 켜지도록 컨트롤러 부착(프리팹 자체가 양치 상태에 반응).
        go.AddComponent<BrushingBubblesController>();

        PrefabUtility.SaveAsPrefabAsset(go, Dir + "/BrushingBubbles.prefab");
        Object.DestroyImmediate(go);
    }

    private static ParticleSystem.MinMaxGradient Fade(float aStart, float holdEnd, float aEndAt)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, aStart), new GradientAlphaKey(1f, holdEnd), new GradientAlphaKey(0f, aEndAt) });
        return new ParticleSystem.MinMaxGradient(g);
    }
}

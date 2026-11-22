using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 버디 스티커 카탈로그(Resources/StickerCatalog.asset)를 아이콘 폴더에서 자동으로 채우는 에디터 도구.
///
/// 메뉴: Tools/세살버릇/Build Sticker Catalog
///   - Assets/Texture/Icons/Images/Icons/&lt;카테고리&gt; 들에서 PNG 아이콘을 모아 무작위로 50개를 고른다.
///   - 고른 아이콘을 StickerDef(id/이름/스프라이트)로 만들어 카탈로그에 기록한다.
///   - 선택은 고정 시드라 다시 빌드해도 같은 50개·같은 id가 나와 획득 기록이 깨지지 않는다.
///
/// 아이콘 에셋(Texture/Icons)은 유료라 공개 저장소에서 제외되지만, 카탈로그(.asset)는 GUID 참조만
/// 담으므로 커밋해도 된다. 아이콘이 없는 환경에선 스프라이트가 비어도 해금 로직은 그대로 동작한다.
/// </summary>
public static class StickerCatalogBuilder
{
    private const string IconsRoot = "Assets/Texture/Icons/Images/Icons";
    private const string CatalogPath = "Assets/Resources/StickerCatalog.asset";
    private const int TargetCount = 50;
    private const int Seed = 20260612;   // 고정 시드 — 재빌드 시 동일한 선택/ id 유지.

    // 사용할 카테고리(요청 목록 중 실제 존재하는 폴더).
    private static readonly string[] Categories =
    {
        "Animals", "Coins", "Dinosaurs", "Drinks", "Electronics",
        "Food", "Music", "Nature", "Sport",
    };

    [MenuItem("Tools/세살버릇/Build Sticker Catalog")]
    public static void Build()
    {
        // 1) 카테고리별 PNG 경로 수집.
        var paths = new List<string>();
        foreach (var cat in Categories)
        {
            var dir = $"{IconsRoot}/{cat}";
            if (!Directory.Exists(dir)) { Debug.LogWarning($"[StickerCatalogBuilder] 폴더 없음: {dir}"); continue; }
            foreach (var f in Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly))
                paths.Add(f.Replace('\\', '/'));
        }
        if (paths.Count == 0)
        {
            Debug.LogError("[StickerCatalogBuilder] 아이콘을 찾지 못했다 — 유료 아이콘 에셋(Texture/Icons)이 있는지 확인하라.");
            return;
        }

        // 2) 고정 시드로 셔플 후 50개 선택.
        var rng = new System.Random(Seed);
        var chosen = paths.OrderBy(_ => rng.Next()).Take(Mathf.Min(TargetCount, paths.Count)).ToList();

        // 3) 선택 아이콘을 Sprite로 보장(이미 Sprite면 그대로).
        foreach (var p in chosen)
        {
            if (AssetImporter.GetAtPath(p) is not TextureImporter imp) continue;
            bool dirty = false;
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
            if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (dirty) imp.SaveAndReimport();
        }

        // 4) 카탈로그 에셋 준비(없으면 생성).
        var catalog = AssetDatabase.LoadAssetAtPath<StickerCatalog>(CatalogPath);
        if (catalog == null)
        {
            Directory.CreateDirectory("Assets/Resources");
            catalog = ScriptableObject.CreateInstance<StickerCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        // 5) StickerDef 채우기.
        var defs = new List<StickerDef>(chosen.Count);
        for (int i = 0; i < chosen.Count; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(chosen[i]);
            string nice = NiceName(chosen[i]);
            defs.Add(new StickerDef
            {
                id = $"sticker_{i:00}",
                displayName = nice,
                sprite = sprite,
                description = $"{nice} 스티커",
            });
        }
        catalog.stickers = defs.ToArray();

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[StickerCatalogBuilder] 스티커 {defs.Count}개 카탈로그 작성 완료 → {CatalogPath}");
    }

    /// <summary>파일명을 보기 좋은 표시 이름으로 — "Icon_Bear_Brown_LP" → "Bear Brown".</summary>
    private static string NiceName(string path)
    {
        var f = Path.GetFileNameWithoutExtension(path);
        f = f.Replace("Icon_", "").Replace("_LP", "").Replace('_', ' ').Trim();
        return string.IsNullOrEmpty(f) ? "스티커" : f;
    }
}

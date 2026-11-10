// 시연 화면 재료를 만든다 — 한글 TMP 글꼴(동적) · 기존 글꼴의 한글 대체 · ShowcaseSkin 묶음. 몇 번 돌려도 같은 결과
using System.Linq;
using TMPro;
using Unity.BossRoom.Gameplay.Showcase;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class ShowcaseSetup
{
    const string FontDir = "Assets/Showcase/Fonts/";
    const string SkinPath = "Assets/Showcase/Resources/ShowcaseSkin.asset";
    const string UI = "Assets/Textures/UI/";

    public static void Run()
    {
        var body = FontAsset("GothicA1-Bold");
        var title = FontAsset("BlackHanSans-Regular");

        // Boss Room 원래 글꼴(Bungee · Roboto)에는 한글이 없다 — 대체 글꼴로 한글을 그리게 한다
        AddFallback(TMP_Settings.instance, body);
        foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts", "Assets/TextMesh Pro" }))
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (fa == null || fa == body || fa == title) continue;
            fa.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (!fa.fallbackFontAssetTable.Contains(body)) { fa.fallbackFontAssetTable.Add(body); EditorUtility.SetDirty(fa); }
        }

        var skin = AssetDatabase.LoadAssetAtPath<ShowcaseSkin>(SkinPath);
        if (skin == null) { skin = ScriptableObject.CreateInstance<ShowcaseSkin>(); AssetDatabase.CreateAsset(skin, SkinPath); }
        skin.Panel = Sprite("ui_dialog");
        skin.TitleBar = Sprite("ui_scroll_frame");
        skin.Button = Sprite("ui_btn_blank");
        skin.ButtonDisabled = Sprite("ui_btn_disabled");
        skin.Input = Sprite("inputfield_Blank");
        skin.Soft = Sprite("ui_blurred_square");
        skin.HealthBar = Sprite("ui_healthbar");
        skin.HealthBarBg = Sprite("ui_healthbar_bg");
        skin.Logo = Sprite("ui_title_logo");
        skin.ClassIcons = new[] { "tank", "archer", "mage", "rogue" }.Select(c => Sprite($"ui_{c}_symbol_active")).ToArray();
        skin.Body = body;
        skin.Title = title;
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        Debug.Log("[setup] 시연 재료 준비 끝");
    }

    static Sprite Sprite(string name)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(UI + name + ".png");
        if (s == null) throw new System.InvalidOperationException("UI 그림이 없습니다: " + name);
        return s;
    }

    static void AddFallback(TMP_Settings settings, TMP_FontAsset font)
    {
        var list = TMP_Settings.fallbackFontAssets;
        if (list.Contains(font)) return;
        list.Add(font);
        EditorUtility.SetDirty(settings);
    }

    static TMP_FontAsset FontAsset(string file)
    {
        var path = FontDir + file + " SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + file + ".ttf");
        if (font == null) throw new System.InvalidOperationException("글꼴 파일이 없습니다: " + file);
        // 한글은 글자가 많아 미리 굽지 않고 쓰는 글자만 그때그때 아틀라스에 그린다
        var fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        fa.name = file + " SDF";
        AssetDatabase.CreateAsset(fa, path);
        fa.atlasTextures[0].name = file + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = file + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }
}

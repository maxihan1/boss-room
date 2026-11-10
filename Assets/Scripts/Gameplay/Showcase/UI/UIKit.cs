// 시연 화면을 코드로 짓는 도구 — 장면 파일을 고치지 않고 Boss Room 그림체(갈색 창 · 금색 제목 띠 · 남색 버튼)로 창과 버튼을 만든다
using System;
using Object = UnityEngine.Object;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Unity.BossRoom.Gameplay.Showcase
{
    public static class UIKit
    {
        public static readonly Color Gold = new Color(1f, 0.82f, 0.32f);
        public static readonly Color Cream = new Color(0.98f, 0.94f, 0.86f);
        public static readonly Color Dim = new Color(0.72f, 0.68f, 0.6f);
        public static readonly Color Red = new Color(1f, 0.42f, 0.36f);
        public static readonly Color Green = new Color(0.5f, 0.95f, 0.5f);
        public static readonly Color ShadeColor = new Color(0f, 0f, 0f, 0.72f);

        static ShowcaseSkin Skin => ShowcaseSkin.I;

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            return img;
        }

        // 갈색 테두리 창 + 위쪽 금색 제목 띠
        public static RectTransform Panel(Transform parent, string title, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var panel = Image(parent, "Panel " + title, Skin.Panel, Color.white, anchor, pos, size).rectTransform;
            if (!string.IsNullOrEmpty(title))
            {
                var bar = Image(panel, "TitleBar", Skin.TitleBar, Color.white, new Vector2(0.5f, 1f), new Vector2(0, 4), new Vector2(Mathf.Min(size.x + 60, 720), 128));
                bar.preserveAspect = true;
                // 제목 띠 그림의 어두운 줄(아래쪽 절반) 안에 글자를 둔다
                var t = Text(bar.transform, title, 38, Gold, TextAlignmentOptions.Center, title: true);
                t.rectTransform.offsetMin = new Vector2(120, 36);
                t.rectTransform.offsetMax = new Vector2(-120, -40);
            }
            return panel;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool title = false)
        {
            var rt = Stretch(Rect(parent, "Text", Vector2.zero, Vector2.zero, Vector2.zero));
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = title ? Skin.Title : Skin.Body;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, Vector2 anchor, Vector2 pos, Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool title = false)
        {
            var holder = Rect(parent, "Label", anchor, pos, box);
            return Text(holder, text, size, color, align, title);
        }

        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, float fontSize = 34)
        {
            var img = Image(parent, "Button " + label, Skin.Button, Color.white, anchor, pos, size);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.8f);
            colors.pressedColor = new Color(0.8f, 0.75f, 0.6f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            btn.colors = colors;
            Text(img.transform, label, fontSize, Gold, TextAlignmentOptions.Center, title: true);
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static TMP_InputField Input(Transform parent, string placeholder, Vector2 anchor, Vector2 pos, Vector2 size, bool password = false)
        {
            var bg = Image(parent, "Input " + placeholder, Skin.Input, Color.white, anchor, pos, size);
            var area = Stretch(Rect(bg.transform, "Text Area", Vector2.zero, Vector2.zero, Vector2.zero), 0);
            area.offsetMin = new Vector2(28, 8);
            area.offsetMax = new Vector2(-28, -8);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Text(area, placeholder, 30, Dim);
            var txt = Text(area, "", 30, Cream);
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.fontAsset = Skin.Body;
            input.pointSize = 30;
            input.customCaretColor = true;
            input.caretColor = Gold;
            if (password)
            {
                input.contentType = TMP_InputField.ContentType.Password;
                input.inputType = TMP_InputField.InputType.Password;
            }
            return input;
        }

        // 세로로 쌓이는 목록 — 줄은 Row 로 더한다
        public static RectTransform ScrollList(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var view = Rect(parent, "Scroll", anchor, pos, size);
            view.gameObject.AddComponent<RectMask2D>();
            var content = Rect(view, "Content", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, 0));
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0, content.offsetMin.y);
            content.offsetMax = new Vector2(0, 0);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            // 빈 곳을 끌어도 스크롤되게 투명한 판을 깐다
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            return content;
        }

        public static RectTransform Row(Transform list, float height, Color? tint = null)
        {
            var img = Image(list, "Row", null, tint ?? new Color(0.55f, 0.4f, 0.22f, 0.22f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, height));
            img.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return img.rectTransform;
        }

        public static void Clear(Transform t)
        {
            for (var i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }

        // 막대(경험치 · 체력) — 채움 비율은 돌려준 Image 의 fillAmount
        public static Image Bar(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color fill)
        {
            var bg = Image(parent, "Bar", null, new Color(0f, 0f, 0f, 0.65f), anchor, pos, size);
            var f = Image(bg.transform, "Fill", null, fill, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(f.rectTransform, 3);
            f.type = UnityEngine.UI.Image.Type.Filled;
            f.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            f.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            return f;
        }

        public static string Number(int n) => n.ToString("N0");

        // 화면 전체를 어둡게 덮는 판 — 창 뒤를 눌러도 닫히게 할 수 있다
        public static Image Shade(Transform parent, Action onClick = null)
        {
            var img = Image(parent, "Shade", null, ShadeColor, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(img.rectTransform);
            if (onClick != null) img.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
            return img;
        }
    }
}

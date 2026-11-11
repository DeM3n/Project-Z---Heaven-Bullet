using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Helper chuyên trách khởi tạo Procedural UI chuẩn Hi-Bit Pixel cho toàn bộ hệ thống UI,
/// triệt tiêu mã lệnh lặp lại cho RectTransform, Image, Text, Outline, Button.
/// </summary>
public static class UIFactory
{
    public static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 size,
        Color bgColor,
        Color? outlineColor = null,
        Vector2? outlineDist = null,
        bool raycastTarget = false)
    {
        var panelObj = new GameObject(name);
        panelObj.transform.SetParent(parent, false);

        var rect = panelObj.AddComponent<RectTransform>();
        rect.sizeDelta = size;

        var img = panelObj.AddComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = raycastTarget;

        if (outlineColor.HasValue)
        {
            var outline = panelObj.AddComponent<Outline>();
            outline.effectColor = outlineColor.Value;
            outline.effectDistance = outlineDist ?? new Vector2(2f, -2f);
        }

        return panelObj;
    }

    public static GameObject CreateFullScreenDim(Transform parent, string name, Color dimColor)
    {
        var dimObj = new GameObject(name);
        dimObj.transform.SetParent(parent, false);

        var rect = dimObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var img = dimObj.AddComponent<Image>();
        img.color = dimColor;
        img.raycastTarget = true;

        return dimObj;
    }

    public static Text CreateText(
        Transform parent,
        string name,
        string content,
        int fontSize,
        Color color,
        FontStyle style = FontStyle.Normal,
        TextAnchor alignment = TextAnchor.MiddleCenter,
        Color? outlineColor = null,
        Vector2? outlineDist = null,
        bool raycastTarget = false)
    {
        var textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        var rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var txt = textObj.AddComponent<Text>();
        txt.font = HUDTheme.PixelFont;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.alignment = alignment;
        txt.color = color;
        txt.text = content;
        txt.raycastTarget = raycastTarget;

        if (outlineColor.HasValue)
        {
            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = outlineColor.Value;
            outline.effectDistance = outlineDist ?? new Vector2(1f, -1f);
        }

        return txt;
    }

    public static Button CreateButton(
        Transform parent,
        string name,
        Vector2 size,
        Color bgColor,
        Color? outlineColor = null,
        UnityAction onClick = null,
        Vector2? outlineDist = null)
    {
        var btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        var rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = size;

        var img = btnObj.AddComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        if (outlineColor.HasValue)
        {
            var outline = btnObj.AddComponent<Outline>();
            outline.effectColor = outlineColor.Value;
            outline.effectDistance = outlineDist ?? new Vector2(2f, -2f);
        }

        var btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        btn.colors = colors;

        if (onClick != null)
        {
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlayUI(UIType.Click);
                onClick();
            });
        }

        return btn;
    }

    public static (GameObject barRoot, Image fillImage, Text text) CreateFillBar(
        Transform parent,
        string name,
        Color bgColor,
        Color fillColor,
        Color outlineColor,
        int fontSize = 12,
        Vector2? padding = null)
    {
        var barRoot = new GameObject(name);
        barRoot.transform.SetParent(parent, false);

        var rootRect = barRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var bg = barRoot.AddComponent<Image>();
        bg.color = bgColor;

        var outline = barRoot.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(1f, -1f);

        // Fill Image
        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(barRoot.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        var pad = padding ?? new Vector2(2f, 2f);
        fillRect.offsetMin = pad;
        fillRect.offsetMax = -pad;

        var fillImg = fillObj.AddComponent<Image>();
        fillImg.sprite = WhiteSprite;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.color = fillColor;

        // Text
        var txt = CreateText(barRoot.transform, "Text", "", fontSize, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter, Color.black);

        return (barRoot, fillImg, txt);
    }

    private static Sprite _whiteSprite;
    /// <summary>
    /// Sprite đơn sắc trắng 4x4 dùng làm nền cho Image.Type.Filled để Unity UI hỗ trợ fillAmount chính xác.
    /// (Lưu ý quan trọng: Unity UI mặc định sẽ bỏ qua fillAmount và vẽ 100% full quad nếu Image.sprite == null).
    /// </summary>
    public static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
            {
                _whiteSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 4f, 4f),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                _whiteSprite.name = "UI_WhitePixelSprite";
            }
            return _whiteSprite;
        }
    }
}


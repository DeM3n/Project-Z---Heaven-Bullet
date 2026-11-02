using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện đếm ngược 3 giây (3-Second Countdown) trước khi tiếp tục trận đấu:
/// - Hiển thị số 3... 2... 1... GO! cực lớn, nổi bật ở giữa màn hình.
/// - Hiệu ứng nảy phóng đại (Scale Punch) mỗi nhịp giây.
/// </summary>
public class ResumeCountdownView
{
    private readonly GameObject _panelObj;
    private readonly Text _countdownText;
    private readonly Text _subText;
    private readonly RectTransform _textRect;

    public bool IsActive => _panelObj != null && _panelObj.activeSelf;

    public ResumeCountdownView(Transform canvasParent)
    {
        _panelObj = UIFactory.CreateFullScreenDim(
            canvasParent,
            "ResumeCountdown_Panel",
            new Color(0.02f, 0.03f, 0.05f, 0.45f)
        );

        // Container text ở trung tâm
        var centerObj = new GameObject("CenterContainer");
        centerObj.transform.SetParent(_panelObj.transform, false);
        var cRect = centerObj.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0.5f);
        cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.pivot = new Vector2(0.5f, 0.5f);
        cRect.sizeDelta = new Vector2(500f, 300f);

        // Text đếm ngược chính
        var numObj = new GameObject("NumberText");
        numObj.transform.SetParent(centerObj.transform, false);
        _textRect = numObj.AddComponent<RectTransform>();
        _textRect.anchorMin = new Vector2(0.5f, 0.55f);
        _textRect.anchorMax = new Vector2(0.5f, 0.55f);
        _textRect.pivot = new Vector2(0.5f, 0.5f);
        _textRect.sizeDelta = new Vector2(400f, 160f);

        _countdownText = numObj.AddComponent<Text>();
        _countdownText.font = HUDTheme.PixelFont;
        _countdownText.fontSize = 110;
        _countdownText.fontStyle = FontStyle.Bold;
        _countdownText.alignment = TextAnchor.MiddleCenter;
        _countdownText.color = new Color(1f, 0.88f, 0.25f, 1f);

        var outline = numObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.05f, 0.02f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);

        var shadow = numObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(6f, -6f);

        // Subtitle phía dưới
        var subObj = new GameObject("SubText");
        subObj.transform.SetParent(centerObj.transform, false);
        var sRect = subObj.AddComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.5f, 0.25f);
        sRect.anchorMax = new Vector2(0.5f, 0.25f);
        sRect.pivot = new Vector2(0.5f, 0.5f);
        sRect.sizeDelta = new Vector2(500f, 40f);

        _subText = subObj.AddComponent<Text>();
        _subText.font = HUDTheme.PixelFont;
        _subText.fontSize = 20;
        _subText.fontStyle = FontStyle.Bold;
        _subText.alignment = TextAnchor.MiddleCenter;
        _subText.color = new Color(0.9f, 0.95f, 1f, 0.9f);
        _subText.text = "✦ GET READY / CHUẨN BỊ ✦";

        var sOutline = subObj.AddComponent<Outline>();
        sOutline.effectColor = Color.black;
        sOutline.effectDistance = new Vector2(2f, -2f);

        _panelObj.SetActive(false);
    }

    public void Show()
    {
        if (_panelObj != null) _panelObj.SetActive(true);
    }

    public void Hide()
    {
        if (_panelObj != null) _panelObj.SetActive(false);
    }

    public void SetCount(string text, Color color)
    {
        if (_countdownText != null)
        {
            _countdownText.text = text;
            _countdownText.color = color;
        }
    }

    public void SetScale(float scale)
    {
        if (_textRect != null)
        {
            _textRect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}


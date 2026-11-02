using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý hiển thị Banner thông báo tạm thời ở phía trên màn hình (vd: Treasure Goblin Alert).
/// </summary>
public class BannerNotificationView
{
    private readonly GameObject _bannerObj;
    private readonly Text _bannerText;
    private float _timer;

    public BannerNotificationView(Transform canvasParent)
    {
        _bannerObj = UIFactory.CreatePanel(
            canvasParent,
            "Banner_Notification",
            new Vector2(560f, 42f),
            new Color(0.1f, 0.08f, 0.02f, 0.94f),
            HUDTheme.GoldBorder,
            new Vector2(2f, -2f)
        );

        var rect = _bannerObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -80f);

        _bannerText = UIFactory.CreateText(
            _bannerObj.transform,
            "Text",
            "",
            16,
            new Color(1f, 0.9f, 0.35f),
            FontStyle.Bold,
            TextAnchor.MiddleCenter
        );

        _bannerObj.SetActive(false);
    }

    public void Trigger(string msg, float duration = 3.5f)
    {
        if (_bannerObj != null && _bannerText != null)
        {
            _bannerText.text = msg;
            _bannerObj.SetActive(true);
            _timer = duration;
        }
    }

    public void UpdateTimer(float unscaledDeltaTime)
    {
        if (_timer > 0f)
        {
            _timer -= unscaledDeltaTime;
            if (_timer <= 0f && _bannerObj != null)
            {
                _bannerObj.SetActive(false);
            }
        }
    }
}


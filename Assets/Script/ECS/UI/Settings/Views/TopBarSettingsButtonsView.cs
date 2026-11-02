using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý hiển thị và tương tác của 2 nút bấm ở góc trên bên phải màn hình:
/// - Nút Tạm dừng [⚙] (Pause Button)
/// - Nút Lập trình viên [🛠️ DEV] (Developer Button)
/// Hoàn toàn độc lập với ECS.
/// </summary>
public class TopBarSettingsButtonsView
{
    private readonly GameObject _pauseBtnObj;
    private readonly GameObject _devBtnObj;

    public event Action OnPauseClicked;
    public event Action OnDevClicked;

    public TopBarSettingsButtonsView(Transform canvasParent)
    {
        // 1. Nút Settings / Pause [⚙]
        _pauseBtnObj = UIFactory.CreatePanel(
            canvasParent,
            "Pause_Button",
            new Vector2(40f, 38f),
            new Color(0.08f, 0.12f, 0.18f, 0.95f),
            new Color(0.35f, 0.55f, 0.75f, 0.95f),
            new Vector2(2f, -2f),
            true
        );
        var pRect = _pauseBtnObj.GetComponent<RectTransform>();
        pRect.anchorMin = new Vector2(1f, 1f);
        pRect.anchorMax = new Vector2(1f, 1f);
        pRect.pivot = new Vector2(1f, 1f);
        pRect.anchoredPosition = new Vector2(-140f, -24f);

        var pBtn = _pauseBtnObj.AddComponent<Button>();
        pBtn.targetGraphic = _pauseBtnObj.GetComponent<Image>();
        pBtn.onClick.AddListener(() => OnPauseClicked?.Invoke());

        UIFactory.CreateText(
            _pauseBtnObj.transform,
            "Icon",
            "⚙",
            20,
            new Color(0.9f, 0.95f, 1f),
            FontStyle.Normal,
            TextAnchor.MiddleCenter
        );

        // 2. Nút Developer Mode [🛠️ DEV]
#if UNITY_EDITOR || DEBUG
        _devBtnObj = UIFactory.CreatePanel(
            canvasParent,
            "Dev_Button",
            new Vector2(44f, 38f),
            new Color(0.12f, 0.08f, 0.18f, 0.95f),
            new Color(0.75f, 0.35f, 1f, 0.95f),
            new Vector2(2f, -2f),
            true
        );
        var dRect = _devBtnObj.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(1f, 1f);
        dRect.anchorMax = new Vector2(1f, 1f);
        dRect.pivot = new Vector2(1f, 1f);
        dRect.anchoredPosition = new Vector2(-190f, -24f);

        var dBtn = _devBtnObj.AddComponent<Button>();
        dBtn.targetGraphic = _devBtnObj.GetComponent<Image>();
        dBtn.onClick.AddListener(() => OnDevClicked?.Invoke());

        UIFactory.CreateText(
            _devBtnObj.transform,
            "Icon",
            "🛠️",
            17,
            new Color(0.85f, 0.65f, 1f),
            FontStyle.Normal,
            TextAnchor.MiddleCenter
        );
#endif
    }

    public void SetVisible(bool visible)
    {
        if (_pauseBtnObj != null) _pauseBtnObj.SetActive(visible);
        if (_devBtnObj != null) _devBtnObj.SetActive(visible);
    }
}

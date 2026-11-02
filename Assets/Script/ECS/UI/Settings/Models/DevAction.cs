using System;
using UnityEngine;

/// <summary>
/// Định nghĩa cấu hình Data-Driven cho mỗi nút chức năng/cheat trên Dev Control Panel.
/// Cho phép DevControlPanelView sinh nút tự động mà không hardcode layout.
/// </summary>
public class DevAction
{
    public string ActionId { get; set; }
    public Func<string> LabelGetter { get; set; }
    public bool IsToggle { get; set; }
    public Color NormalColor { get; set; }
    public Color BorderColor { get; set; }
    public Action ExecuteAction { get; set; }
    public Func<string> StateGetter { get; set; }
}

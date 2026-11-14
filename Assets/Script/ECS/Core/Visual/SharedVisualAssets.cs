using UnityEngine;

/// <summary>
/// Kho tài nguyên thị giác dùng chung trung tâm (Centralized Shared Visual Assets & Materials).
/// Đóng vai trò Single Source of Truth (SSOT) cho toàn bộ Visual Bridges và VFX trong game:
/// 1. Tối ưu hóa cực hạn URP SRP Batching: Tất cả SpriteRenderer & LineRenderer (Kiếm, Lửa, Khiên, Phi tiêu, Bom,...)
///    dùng chung đúng 1 con trỏ Material duy nhất trong RAM/VRAM -> GPU gộp toàn bộ đòn đánh thành 1-2 Draw Calls.
/// 2. Triệt tiêu 100% code trùng lặp (DRY): Không còn tình trạng mỗi file tự viết GetCircleSprite(), GetRingSprite(), GetSharedMaterial().
/// 3. Zero Runtime GPU Allocation & Stall: Toàn bộ Texture procedural và Material được khởi tạo sẵn (Prewarm)
///    ngay trước khi Scene load (BeforeSceneLoad).
/// </summary>
public static class SharedVisualAssets
{
    private static Material _spriteUnlitMaterial;
    private static Sprite _circleSprite;
    private static Sprite _ringSprite;
    private static Sprite _radialGlowSprite;
    private static bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Prewarm()
    {
        if (_initialized) return;

        // 1. Khởi tạo Material dùng chung cho toàn bộ Game 2D
        GetSpriteUnlitMaterial();

        // 2. Khởi tạo các Procedural Sprite cơ bản
        GetCircleSprite();
        GetRingSprite();
        GetRadialGlowSprite();

        _initialized = true;
    }

    /// <summary>
    /// Material Sprite-Unlit duy nhất dùng chung cho toàn bộ SpriteRenderer và LineRenderer trong game.
    /// Cho phép URP 2D SRP Batching gộp toàn bộ hiệu ứng kỹ năng vào cùng một Draw Call.
    /// </summary>
    public static Material SpriteUnlitMaterial => GetSpriteUnlitMaterial();

    public static Material GetSpriteUnlitMaterial()
    {
        if (_spriteUnlitMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                        ?? Shader.Find("Sprites/Default")
                        ?? Shader.Find("Unlit/Color");

            if (shader != null)
            {
                _spriteUnlitMaterial = new Material(shader)
                {
                    name = "HB_Shared_SpriteUnlit_Mat",
                    hideFlags = HideFlags.DontSave
                };
            }
        }
        return _spriteUnlitMaterial;
    }

    /// <summary>
    /// Sprite hình tròn đặc màu trắng với viền khử răng cưa (Anti-Aliased Feathered Edge).
    /// Dùng cho shockwave discs, flash cores, glow centers, embers, hit particles.
    /// </summary>
    public static Sprite CircleSprite => GetCircleSprite();

    public static Sprite GetCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "HB_Shared_CircleTex",
            hideFlags = HideFlags.DontSave
        };

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - center) / center;
                float dy = (y + 0.5f - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > 1f)
                {
                    pixels[y * size + x] = Color.clear;
                }
                else
                {
                    // Viền mềm khử răng cưa mượt mà ở rìa ngoài 5%
                    float alpha = Mathf.Clamp01((1f - dist) / 0.05f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true); // makeNoLongerReadable = true để tối ưu VRAM

        _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _circleSprite.name = "HB_Shared_CircleSprite";
        return _circleSprite;
    }

    /// <summary>
    /// Sprite vành đai tròn rỗng màu trắng (Hollow Ring) với viền khử răng cưa 2 phía.
    /// Dùng cho sóng xung kích lan tỏa (Shockwave Rings), viền nổ, vòng thu hồi boomerang.
    /// </summary>
    public static Sprite RingSprite => GetRingSprite();

    public static Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "HB_Shared_RingTex",
            hideFlags = HideFlags.DontSave
        };

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];
        const float innerRadius = 0.70f;
        const float outerRadius = 1.0f;
        const float feather = 0.06f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - center) / center;
                float dy = (y + 0.5f - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist < innerRadius - feather || dist > outerRadius)
                {
                    pixels[y * size + x] = Color.clear;
                }
                else
                {
                    float innerAlpha = Mathf.Clamp01((dist - (innerRadius - feather)) / feather);
                    float outerAlpha = Mathf.Clamp01((outerRadius - dist) / feather);
                    float alpha = Mathf.Min(innerAlpha, outerAlpha);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _ringSprite.name = "HB_Shared_RingSprite";
        return _ringSprite;
    }

    /// <summary>
    /// Sprite vầng hào quang tỏa tròn (Radial Glow Falloff) giảm dần theo luật bình phương.
    /// Dùng cho chuôi kiếm, đốm sáng boomerang, tâm nổ ma pháp.
    /// </summary>
    public static Sprite RadialGlowSprite => GetRadialGlowSprite();

    public static Sprite GetRadialGlowSprite()
    {
        if (_radialGlowSprite != null) return _radialGlowSprite;

        const int size = 48;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "HB_Shared_RadialGlowTex",
            hideFlags = HideFlags.DontSave
        };

        float center = size * 0.5f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - center) / center;
                float dy = (y + 0.5f - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist >= 1f)
                {
                    pixels[y * size + x] = Color.clear;
                }
                else
                {
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha; // Quadratic falloff mềm mại
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        _radialGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _radialGlowSprite.name = "HB_Shared_RadialGlowSprite";
        return _radialGlowSprite;
    }
}

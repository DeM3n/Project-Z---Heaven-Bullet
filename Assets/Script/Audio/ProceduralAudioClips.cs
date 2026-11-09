using System;
using UnityEngine;

/// <summary>
/// Trình tổng hợp âm thanh thủ công (Procedural Audio Synthesizer) bằng toán học DSP.
/// Tự động sinh ra các AudioClip trong trẻo, mang âm hưởng Tiên Hiệp / Cổ Phong (Guzheng, Bell, War Horn, UI)
/// khi dự án chưa có file âm thanh bên ngoài (.wav/.mp3), hoặc làm âm thanh fallback zero-dependency.
/// </summary>
public static class ProceduralAudioClips
{
    private const int SampleRate = 44100;

    /// <summary>
    /// Tiếng click nút bấm: Gõ ngọc / mộc thanh thúy, dứt khoát (18ms).
    /// </summary>
    public static AudioClip CreateUIClick()
    {
        int length = (int)(SampleRate * 0.035f);
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / length;
            // Decay nhanh hình mũ
            float env = Mathf.Exp(-progress * 22f);
            // Tần số trượt từ 1300Hz xuống 650Hz
            float freq = Mathf.Lerp(1300f, 650f, progress);
            float sine = Mathf.Sin(2f * Mathf.PI * freq * t);
            float harmonic = 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t);
            samples[i] = (sine + harmonic) * env * 0.45f;
        }

        AudioClip clip = AudioClip.Create("Proc_UI_Click", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng hover chuột: Âm tick cực nhẹ, không gây mỏi tai (12ms).
    /// </summary>
    public static AudioClip CreateUIHover()
    {
        int length = (int)(SampleRate * 0.02f);
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / length;
            float env = Mathf.Exp(-progress * 30f);
            float sine = Mathf.Sin(2f * Mathf.PI * 920f * t);
            samples[i] = sine * env * 0.18f;
        }

        AudioClip clip = AudioClip.Create("Proc_UI_Hover", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng mở bảng Lên Cấp: Hợp âm Fanfare 4 nốt rạng rỡ (C5 - E5 - G5 - C6) ngân vang (0.85s).
    /// </summary>
    public static AudioClip CreateUILevelUp()
    {
        int length = (int)(SampleRate * 0.85f);
        float[] samples = new float[length];

        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
        float noteDuration = 0.12f;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float sampleSum = 0f;

            for (int n = 0; n < notes.Length; n++)
            {
                float noteStartTime = n * noteDuration;
                if (t >= noteStartTime)
                {
                    float noteT = t - noteStartTime;
                    float env = Mathf.Exp(-noteT * 5.5f);
                    float freq = notes[n];
                    float s = Mathf.Sin(2f * Mathf.PI * freq * noteT);
                    float h = 0.35f * Mathf.Sin(4f * Mathf.PI * freq * noteT);
                    sampleSum += (s + h) * env * 0.35f;
                }
            }

            samples[i] = Mathf.Clamp(sampleSum, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_UI_LevelUp", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng chọn thẻ Perk: Linh lực ngưng tụ, tiếng chuông đồng uy lực (0.4s).
    /// </summary>
    public static AudioClip CreateUIPerkSelect()
    {
        int length = (int)(SampleRate * 0.45f);
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / length;
            float env = Mathf.Exp(-progress * 9f);
            // Hợp âm root (440Hz A4) + fifth (659Hz E5) + octave (880Hz A5)
            float s1 = Mathf.Sin(2f * Mathf.PI * 440f * t);
            float s2 = 0.6f * Mathf.Sin(2f * Mathf.PI * 659f * t);
            float s3 = 0.35f * Mathf.Sin(2f * Mathf.PI * 880f * t);
            samples[i] = (s1 + s2 + s3) * env * 0.4f;
        }

        AudioClip clip = AudioClip.Create("Proc_UI_PerkSelect", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng Reroll: Tiếng lắc xúc xắc / xáo thẻ ngọc liên hoàn (0.28s).
    /// </summary>
    public static AudioClip CreateUIReroll()
    {
        int length = (int)(SampleRate * 0.3f);
        float[] samples = new float[length];

        float[] clickTimes = { 0.02f, 0.07f, 0.13f, 0.19f, 0.24f };
        float[] clickFreqs = { 900f, 1300f, 1100f, 1500f, 1200f };

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float sampleSum = 0f;

            for (int k = 0; k < clickTimes.Length; k++)
            {
                if (t >= clickTimes[k])
                {
                    float clickT = t - clickTimes[k];
                    float env = Mathf.Exp(-clickT * 40f);
                    float s = Mathf.Sin(2f * Mathf.PI * clickFreqs[k] * clickT);
                    sampleSum += s * env * 0.25f;
                }
            }

            samples[i] = Mathf.Clamp(sampleSum, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_UI_Reroll", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng ngọc EXP: Chuông tinh thể trong suốt (1760Hz - A6) ngân vang cực đã tai (0.16s).
    /// Khi kết hợp Pitch Escalation sẽ tự động tạo thành chuỗi thang âm lấp lánh!
    /// </summary>
    public static AudioClip CreateExpOrb()
    {
        int length = (int)(SampleRate * 0.18f);
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / length;
            // Decay tự nhiên của chuông tinh thể
            float env = Mathf.Exp(-progress * 16f);
            float fundamental = Mathf.Sin(2f * Mathf.PI * 1760f * t);
            float overtone1 = 0.35f * Mathf.Sin(2f * Mathf.PI * 3520f * t);
            float overtone2 = 0.15f * Mathf.Sin(2f * Mathf.PI * 5280f * t);
            samples[i] = (fundamental + overtone1 + overtone2) * env * 0.35f;
        }

        AudioClip clip = AudioClip.Create("Proc_SFX_ExpOrb", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng nhặt Trái Tim Hồi Máu: Cam lộ mát lành, lướt tần số êm ái đi lên (0.4s).
    /// </summary>
    public static AudioClip CreateHealthPickup()
    {
        int length = (int)(SampleRate * 0.42f);
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / length;
            float env = Mathf.Sin(progress * Mathf.PI) * Mathf.Exp(-progress * 3.5f);
            // Tần số trượt từ 480Hz lên 960Hz
            float freq = Mathf.Lerp(480f, 960f, Mathf.Sqrt(progress));
            float s = Mathf.Sin(2f * Mathf.PI * freq * t);
            float warmth = 0.3f * Mathf.Sin(2f * Mathf.PI * (freq * 1.5f) * t);
            samples[i] = (s + warmth) * env * 0.4f;
        }

        AudioClip clip = AudioClip.Create("Proc_SFX_HealthPickup", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng nhặt Nam Châm: Xung năng lượng từ trường hút chân không rền vang (0.55s).
    /// </summary>
    public static AudioClip CreateMagnetPickup()
    {
        int length = (int)(SampleRate * 0.55f);
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / length;
            float env = Mathf.Sin(progress * Mathf.PI);
            // Tần số cuộn xoáy từ 220Hz lên 880Hz kèm hiệu ứng tremolo
            float freq = Mathf.Lerp(220f, 880f, progress * progress);
            float tremolo = 1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 18f * t);
            float s = Mathf.Sin(2f * Mathf.PI * freq * t) * tremolo;
            samples[i] = s * env * 0.45f;
        }

        AudioClip clip = AudioClip.Create("Proc_SFX_MagnetPickup", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng mở Rương Cổ Vật: Chuỗi hợp âm kim quang vạn trượng (0.95s).
    /// </summary>
    public static AudioClip CreateChestOpen()
    {
        int length = (int)(SampleRate * 0.95f);
        float[] samples = new float[length];

        float[] chord = { 440f, 554.37f, 659.25f, 880f, 1108.73f, 1318.51f }; // A Major Arpeggio
        float interval = 0.08f;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float sum = 0f;

            for (int c = 0; c < chord.Length; c++)
            {
                float start = c * interval;
                if (t >= start)
                {
                    float noteT = t - start;
                    float env = Mathf.Exp(-noteT * 4f);
                    float s = Mathf.Sin(2f * Mathf.PI * chord[c] * noteT);
                    float h = 0.25f * Mathf.Sin(4f * Mathf.PI * chord[c] * noteT);
                    sum += (s + h) * env * 0.25f;
                }
            }

            samples[i] = Mathf.Clamp(sum, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_SFX_ChestOpen", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng Tù Và Báo Động Mega Wave: Trầm hùng, cổ xưa, cảnh báo nguy hiểm (1.8s).
    /// </summary>
    public static AudioClip CreateMegaWaveHorn()
    {
        int length = (int)(SampleRate * 1.8f);
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / length;
            // Attack êm, duy trì, decay nhẹ
            float env = Mathf.Sin(progress * Mathf.PI);
            // Sóng trầm 110Hz với các sóng hài kèn đồng (brass harmonics)
            float freq = 110f;
            float f1 = Mathf.Sin(2f * Mathf.PI * freq * t);
            float f2 = 0.7f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
            float f3 = 0.5f * Mathf.Sin(2f * Mathf.PI * freq * 3f * t);
            float f4 = 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 4f * t);
            // Thêm chút vibrato cổ xưa
            float vibrato = Mathf.Sin(2f * Mathf.PI * 4.5f * t) * 2f;
            float total = (f1 + f2 + f3 + f4) * env * 0.5f;
            samples[i] = Mathf.Clamp(total, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_SFX_MegaWaveHorn", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng người chơi nhận sát thương (Player Hurt): Va chạm trầm đục (0.16s).
    /// </summary>
    public static AudioClip CreatePlayerHurt()
    {
        int length = (int)(SampleRate * 0.16f);
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / length;
            float env = Mathf.Exp(-progress * 18f);
            float freq = Mathf.Lerp(140f, 45f, progress);
            float s = Mathf.Sin(2f * Mathf.PI * freq * t);
            // Pha chút noise nhẹ cho lực va chạm chân thực
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
            samples[i] = (s + noise) * env * 0.6f;
        }

        AudioClip clip = AudioClip.Create("Proc_SFX_PlayerHurt", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Tiếng nhịp tim khi máu yếu (Heartbeat Low HP): Tiếng thình thịch trầm (0.4s).
    /// </summary>
    public static AudioClip CreateHeartbeat()
    {
        int length = (int)(SampleRate * 0.4f);
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / length;
            // 2 nhịp tim liên tiếp: pulse 1 tại 0.05s, pulse 2 tại 0.18s
            float env = 0f;
            if (t >= 0.02f && t < 0.14f)
            {
                float p1 = (t - 0.02f) / 0.12f;
                env += Mathf.Sin(p1 * Mathf.PI);
            }
            if (t >= 0.16f && t < 0.32f)
            {
                float p2 = (t - 0.16f) / 0.16f;
                env += Mathf.Sin(p2 * Mathf.PI) * 0.75f;
            }
            float s = Mathf.Sin(2f * Mathf.PI * 60f * t);
            samples[i] = s * env * 0.5f;
        }

        AudioClip clip = AudioClip.Create("Proc_SFX_Heartbeat", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Nhạc nền Màn hình chính (BGM MainMenu Fallback): Giai điệu Ngũ Cung Tiên Hiệp thanh thoát lặp lại êm dịu (8.0s loop).
    /// </summary>
    public static AudioClip CreateMainMenuBGM()
    {
        float duration = 8.0f;
        int length = (int)(SampleRate * duration);
        float[] samples = new float[length];

        // Thang âm ngũ cung Trung Hoa / Tiên Hiệp: D4, F#4, G4, A4, C#5, D5 (D Cung diệu)
        float[] melodyNotes = { 293.66f, 369.99f, 440f, 554.37f, 587.33f, 440f, 369.99f, 293.66f };
        float noteTime = duration / melodyNotes.Length;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            int noteIndex = (int)(t / noteTime) % melodyNotes.Length;
            float noteT = t % noteTime;

            // Âm hưởng Cổ Tranh (Guzheng pluck): attack nhanh, decay ngân nga
            float env = Mathf.Exp(-noteT * 2.8f);
            float freq = melodyNotes[noteIndex];
            float s = Mathf.Sin(2f * Mathf.PI * freq * t);
            float h1 = 0.4f * Mathf.Sin(4f * Mathf.PI * freq * t);
            float h2 = 0.2f * Mathf.Sin(6f * Mathf.PI * freq * t);

            // Drone nền ấm áp trầm (D3 146.8Hz)
            float drone = 0.18f * Mathf.Sin(2f * Mathf.PI * 146.83f * t);

            samples[i] = ((s + h1 + h2) * env * 0.32f) + drone;
        }

        AudioClip clip = AudioClip.Create("Proc_BGM_MainMenu", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Nhạc nền Trận đấu Bình thường (BGM Battle Fallback): Nhịp điệu dồn dập, trống trận nhịp nhàng lặp lại (6.0s loop).
    /// </summary>
    public static AudioClip CreateBattleBGM()
    {
        float duration = 6.0f;
        int length = (int)(SampleRate * duration);
        float[] samples = new float[length];

        // Nhịp BPM = 130
        float beatInterval = 60f / 130f; // ~0.46s

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float beatT = t % beatInterval;
            float beatProgress = beatT / beatInterval;

            // Trống trận Taiko nhịp nhàng (Kick + Sub)
            float drumEnv = Mathf.Exp(-beatProgress * 8f);
            float drumFreq = Mathf.Lerp(120f, 45f, beatProgress);
            float drum = Mathf.Sin(2f * Mathf.PI * drumFreq * beatT) * drumEnv * 0.35f;

            // Tiếng gảy dây ngũ cung nhịp điệu
            float melodyT = t % (beatInterval * 2f);
            float melodyEnv = Mathf.Exp(-(melodyT / (beatInterval * 2f)) * 4f);
            float pluck = Mathf.Sin(2f * Mathf.PI * 329.63f * t) * melodyEnv * 0.22f; // E4

            samples[i] = drum + pluck;
        }

        AudioClip clip = AudioClip.Create("Proc_BGM_Battle", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Nhạc nền Mega Wave (BGM Mega Wave Fallback): Cực kỳ dồn dập, căng thẳng chiến trận (4.0s loop).
    /// </summary>
    public static AudioClip CreateMegaWaveBGM()
    {
        float duration = 4.0f;
        int length = (int)(SampleRate * duration);
        float[] samples = new float[length];

        // BPM = 160 dồn dập
        float beatInterval = 60f / 160f; // ~0.375s

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float beatT = t % beatInterval;
            float beatProgress = beatT / beatInterval;

            // Chiến cổ dồn dập liên tục
            float drumEnv = Mathf.Exp(-beatProgress * 12f);
            float drum = Mathf.Sin(2f * Mathf.PI * 80f * beatT) * drumEnv * 0.45f;

            // Nhạc nền căng thẳng (tremolo trượt)
            float tension = 0.15f * Mathf.Sin(2f * Mathf.PI * (220f + Mathf.Sin(t * 10f) * 15f) * t);

            samples[i] = drum + tension;
        }

        AudioClip clip = AudioClip.Create("Proc_BGM_MegaWave", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Nhạc nền Game Over (BGM GameOver Fallback): Tiếng tiêu trầm buồn, bi tráng (4.0s loop).
    /// </summary>
    public static AudioClip CreateGameOverBGM()
    {
        float duration = 4.0f;
        int length = (int)(SampleRate * duration);
        float[] samples = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float progress = t / duration;
            // Giai điệu tiêu trầm buồn hạ dần
            float freq = Mathf.Lerp(330f, 165f, progress);
            float breath = (UnityEngine.Random.value * 2f - 1f) * 0.05f;
            float s = Mathf.Sin(2f * Mathf.PI * freq * t);
            float env = Mathf.Sin(progress * Mathf.PI);
            samples[i] = (s + breath) * env * 0.35f;
        }

        AudioClip clip = AudioClip.Create("Proc_BGM_GameOver", length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}


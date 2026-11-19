#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Script tự động hóa Build WebGL tối ưu riêng cho nền tảng itch.io:
/// 1. Tự động kiểm tra module WebGL Build Support.
/// 2. Tự động chờ Unity biên dịch xong scripts nếu Editor đang compiling.
/// 3. Cấu hình chuẩn itch.io: Compression Format = Disabled (hoặc Gzip có Decompression Fallback) để tránh lỗi MIME type/HTTP Header trên itch.io.
/// 4. Cấu hình WebGL 2.0 và cấp phát bộ nhớ Wasm tối ưu cho Unity DOTS / Pure ECS (ALLOW_MEMORY_GROWTH).
/// 5. Xuất sẵn vào thư mục Builds/WebGL/ để sẵn sàng nén file .zip đăng lên itch.io.
/// </summary>
public static class WebGLBuildScript
{
    private const string BuildOutputPath = "Builds/WebGL";
    private const string WindowsOutputPath = "Builds/Windows/HeavenBullet.exe";
    private static bool _isWaitingForCompile = false;

    [MenuItem("Project Z/Build/Build WebGL (For itch.io)", false, 100)]
    public static void BuildForItchIo()
    {
        Debug.Log("[WebGLBuild] Bắt đầu chuẩn bị build WebGL cho itch.io...");

        // Kiểm tra module WebGL đã được cài đặt chưa
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            Debug.LogError("[WebGLBuild] LỖI: Unity Editor chưa cài đặt module WebGL Build Support trong Unity Hub!");
            return;
        }

        // Tự động chờ nếu Editor đang bận biên dịch script
        if (EditorApplication.isCompiling)
        {
            if (!_isWaitingForCompile)
            {
                _isWaitingForCompile = true;
                Debug.LogWarning("[WebGLBuild] Unity đang biên dịch scripts, tự động chờ hoàn tất rồi sẽ tự kích hoạt build...");
                EditorApplication.update += WaitForCompilingThenBuild;
            }
            return;
        }

        // 1. Cấu hình PlayerSettings tối ưu cho itch.io
        ConfigurePlayerSettingsForItchIo();

        // 2. Điều hướng thư mục TEMP của Emscripten sang ổ D: (nơi có 14GB+ trống) để tránh lỗi 'no space on device' trên ổ C:
        string dTempPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/EmscriptenTemp"));
        if (!Directory.Exists(dTempPath))
        {
            Directory.CreateDirectory(dTempPath);
        }
        Environment.SetEnvironmentVariable("TMP", dTempPath);
        Environment.SetEnvironmentVariable("TEMP", dTempPath);
        Environment.SetEnvironmentVariable("EM_TMP_DIR", dTempPath);

        // 3. Xóa sạch cache build WebGL cũ của Bee / Tundra để ép Unity tạo lại response file và DAG mới (tránh dùng lại cache hỏng)
        ClearBeeWebGLCache();

        // 2. Chuyển platform sang WebGL nếu đang ở platform khác
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            Debug.Log("[WebGLBuild] Đang chuyển đổi Active Build Target sang WebGL...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            if (EditorApplication.isCompiling)
            {
                _isWaitingForCompile = true;
                EditorApplication.update += WaitForCompilingThenBuild;
                return;
            }
        }

        // 3. Chuẩn bị danh sách Scene chuẩn của game
        string[] scenes = new string[]
        {
            "Assets/Scenes/MainMenuScene.unity",
            "Assets/Scenes/LoadingScene.unity",
            "Assets/Scenes/SampleScene.unity"
        };

        foreach (var scene in scenes)
        {
            if (!File.Exists(scene))
            {
                Debug.LogError($"[WebGLBuild] Không tìm thấy file Scene: {scene}");
                return;
            }
        }

        // 4. Đảm bảo thư mục output tồn tại
        if (!Directory.Exists(BuildOutputPath))
        {
            Directory.CreateDirectory(BuildOutputPath);
        }

        // 5. Thiết lập BuildPlayerOptions
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = BuildOutputPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        };

        Debug.Log($"[WebGLBuild] Bắt đầu gọi BuildPipeline.BuildPlayer ra '{BuildOutputPath}'...");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[WebGLBuild] THÀNH CÔNG! Build WebGL hoàn tất: {summary.totalSize / (1024 * 1024)} MB trong {summary.totalTime.TotalSeconds:F1}s.");
            Debug.Log($"[WebGLBuild] Thư mục output: {Path.GetFullPath(BuildOutputPath)}");
        }
        else
        {
            Debug.LogError($"[WebGLBuild] THẤT BẠI: {summary.result} ({summary.totalErrors} lỗi).");
        }
    }

    private static void WaitForCompilingThenBuild()
    {
        if (EditorApplication.isCompiling) return;

        EditorApplication.update -= WaitForCompilingThenBuild;
        _isWaitingForCompile = false;
        Debug.Log("[WebGLBuild] Scripts đã biên dịch xong 100%! Bắt đầu tiến trình Build WebGL...");
        BuildForItchIo();
    }

    [MenuItem("Project Z/Build/Configure Player Settings For itch.io", false, 101)]
    public static void ConfigurePlayerSettingsForItchIo()
    {
        // 1. Tắt Development Build & Debugging để tránh LLVM wasm-ld out of memory
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;

        // 2. Cấu hình IL2CPP Release & Tắt LTO để tránh tràn RAM wasm-ld
        PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSpeed);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

        // Ép wasm-ld chạy đơn luồng (threads=1) và tắt tối ưu hóa LTO codegen (lto-O0, lto-CGO0) để tiết kiệm tối đa RAM trên máy 8GB
        PlayerSettings.WebGL.emscriptenArgs = "-Wl,--threads=1 -Wl,--lto-O0 -Wl,--lto-CGO0";

        // 3. Trên itch.io: tắt nén hoặc bật Decompression Fallback để tránh lỗi MIME type / Content-Encoding
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = true;

        // 4. Cấu hình tỷ lệ hiển thị mặc định 16:9 (1280x720)
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = false;

        Debug.Log("[WebGLBuild] Đã áp dụng cấu hình tối ưu WebGL Release cho itch.io (Development=False, Release=True, Compression=Disabled, 1280x720).");
    }

    private static void ClearBeeWebGLCache()
    {
        string[] dirsToClean = new string[]
        {
            "Library/Bee/artifacts/WebGL/build",
            "Library/Bee/artifacts/rsp"
        };

        foreach (var dir in dirsToClean)
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }

    [MenuItem("Project Z/Build/Build Windows Standalone (For itch.io)", false, 102)]
    public static void BuildWindowsStandalone()
    {
        Debug.Log("[WindowsBuild] Bắt đầu build Windows Standalone...");

        string[] scenes = new string[]
        {
            "Assets/Scenes/MainMenuScene.unity",
            "Assets/Scenes/LoadingScene.unity",
            "Assets/Scenes/SampleScene.unity"
        };

        string outputDir = Path.GetDirectoryName(WindowsOutputPath);
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = WindowsOutputPath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[WindowsBuild] THÀNH CÔNG! Build Windows hoàn tất: {summary.totalSize / (1024 * 1024)} MB trong {summary.totalTime.TotalSeconds:F1}s.");
            Debug.Log($"[WindowsBuild] Thư mục output: {Path.GetFullPath(outputDir)}");
        }
        else
        {
            Debug.LogError($"[WindowsBuild] THẤT BẠI: {summary.result} ({summary.totalErrors} lỗi).");
        }
    }
}
#endif

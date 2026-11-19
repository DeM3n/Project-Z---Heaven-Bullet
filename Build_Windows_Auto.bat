@echo off
title Heaven Bullet - Windows Standalone Builder
echo =================================================================
echo   HEAVEN BULLET - WINDOWS STANDALONE BUILDER
echo   (Build ban cai dat Windows sieu nhanh trong 1 phut)
echo =================================================================
echo.
echo [1/2] Dang kiem tra...
echo Vui long DAM BAO ban da DONG phan mem Unity Editor truoc khi tiep tuc!
echo.
pause

echo.
echo [2/2] Dang build ban Windows Standalone 64-bit...
echo Xin vui long cho khoang 1 - 2 phut...
echo.

"D:\Unity\6000.6.0f1\Editor\Unity.exe" -batchmode -nographics -projectPath "%~dp0." -executeMethod WebGLBuildScript.BuildWindowsStandalone -logFile "build_windows.log" -quit

echo.
if exist "Builds\Windows\HeavenBullet.exe" (
    echo =================================================================
    echo   [THANH CONG] Build Windows hoan tat!
    echo   File game nam tai: %~dp0Builds\Windows\HeavenBullet.exe
    echo   Ban chi can nen ca thu muc Builds\Windows thanh .zip de up len itch.io!
    echo =================================================================
) else (
    echo =================================================================
    echo   [THAT BAI] Khong tim thay HeavenBullet.exe!
    echo   Vui long kiem tra file 'build_windows.log'.
    echo =================================================================
)
echo.
pause

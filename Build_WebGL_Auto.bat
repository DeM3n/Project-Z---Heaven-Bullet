@echo off
title Heaven Bullet - WebGL Auto Builder (Low RAM Safe)
echo =================================================================
echo   HEAVEN BULLET - BATCHMODE WEBGL BUILDER
echo   (Chay ngam khong mo do hoa de tiet kiem 3GB RAM cho may 8GB)
echo =================================================================
echo.
echo [1/3] Dang kiem tra tien trinh Unity...
echo Vui long DAM BAO ban da DONG phan mem Unity Editor truoc khi tiep tuc!
echo.
pause

echo.
echo [2/3] Dang bat dau tien trinh build WebGL...
echo Tien trinh nay mat khoang 3 - 8 phut tuy toc do CPU.
echo File log chi tiet duoc ghi tai: build_webgl.log
echo Xin vui long KHONG dong cua so nay...
echo.

"D:\Unity\6000.6.0f1\Editor\Unity.exe" -batchmode -nographics -projectPath "%~dp0." -executeMethod WebGLBuildScript.BuildForItchIo -logFile "build_webgl.log" -quit

echo.
if exist "Builds\WebGL\index.html" (
    echo =================================================================
    echo   [THANH CONG] Build WebGL hoan tat 100%!
    echo   Ket qua nam tai thu muc: %~dp0Builds\WebGL\
    echo =================================================================
) else (
    echo =================================================================
    echo   [THAT BAI] Khong tim thay file index.html!
    echo   Vui long mo file 'build_webgl.log' de xem chi tiet loi.
    echo =================================================================
)
echo.
pause

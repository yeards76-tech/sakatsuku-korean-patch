@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ===================================================
echo  사카츠쿠 2013 한글패치 GitHub 저장소 배포 (push)
echo  저장소: https://github.com/yeards76-tech/sakatsuku-korean-patch
echo ===================================================
echo.
echo GitHub 인증 창(브라우저)이 열리면 승인(Authorize)을 눌러주세요.
echo.
git push -u origin main
echo.
if %ERRORLEVEL% equ 0 (
    echo ===================================================
    echo  [성공] GitHub 배포가 성공적으로 완료되었습니다!
    echo ===================================================
) else (
    echo ===================================================
    echo  [오류] 배포 중 오류가 발생했습니다.
    echo  GitHub 로그인 상태 또는 권한을 확인해주세요.
    echo ===================================================
)
echo.
pause

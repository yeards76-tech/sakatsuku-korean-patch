# PowerShell UTF-8 with BOM
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
[System.Console]::Title = "사카츠쿠 2013 한글패치 GitHub 배포"

Clear-Host
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  사카츠쿠 2013 (PS3) 한글패치 GitHub 자동 배포 도구" -ForegroundColor Cyan
Write-Host "  저장소: https://github.com/yeards76-tech/sakatsuku-korean-patch" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "배포 방식을 선택하세요:" -ForegroundColor White
Write-Host " [1] 웹 브라우저 로그인으로 배포 (기본값 - 그냥 Enter)" -ForegroundColor Green
Write-Host " [2] GitHub Personal Access Token(PAT) 입력하여 배포" -ForegroundColor White
Write-Host ""
$choice = Read-Host "선택 [1/2] (기본값: 1)"

Set-Location $PSScriptRoot

if ($choice -eq '2') {
    $token = Read-Host "GitHub Personal Access Token (ghp_...)"
    if ([string]::IsNullOrWhiteSpace($token)) {
        Write-Host "토큰이 입력되지 않았습니다." -ForegroundColor Red
        exit 1
    }
    $remoteUrl = "https://yeards76-tech:$($token.Trim())@github.com/yeards76-tech/sakatsuku-korean-patch.git"
    git push -u $remoteUrl main
} else {
    Write-Host ""
    Write-Host "GitHub 로그인 창 또는 웹 브라우저가 열리면 [Authorize]를 클릭해주세요..." -ForegroundColor Yellow
    Write-Host ""
    git push -u origin main
}

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host " [성공] GitHub 배포가 성공적으로 완료되었습니다!" -ForegroundColor Green
    Write-Host " 저장소 확인: https://github.com/yeards76-tech/sakatsuku-korean-patch" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Red
    Write-Host " [안내] 푸시 작업이 완료되지 않았습니다." -ForegroundColor Red
    Write-Host " GitHub 로그인 또는 권한 상태를 확인해주세요." -ForegroundColor Red
    Write-Host "================================================================" -ForegroundColor Red
}

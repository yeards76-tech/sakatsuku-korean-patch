[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host " Sakatsuku 2013 Korean Patch - GitHub Deploy" -ForegroundColor Cyan
Write-Host " Repo: https://github.com/yeards76-tech/sakatsuku-korean-patch" -ForegroundColor DarkCyan
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Pushing to GitHub..." -ForegroundColor Yellow
Write-Host "If a browser or login popup appears, please click Authorize." -ForegroundColor Yellow
Write-Host ""

Set-Location $PSScriptRoot
git push -u origin main

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "===================================================" -ForegroundColor Green
    Write-Host " [SUCCESS] Push completed successfully!" -ForegroundColor Green
    Write-Host " https://github.com/yeards76-tech/sakatsuku-korean-patch" -ForegroundColor Green
    Write-Host "===================================================" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "===================================================" -ForegroundColor Red
    Write-Host " [FAILED] Push could not be completed." -ForegroundColor Red
    Write-Host " Please check GitHub authentication/permissions." -ForegroundColor Red
    Write-Host "===================================================" -ForegroundColor Red
}

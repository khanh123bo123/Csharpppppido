Write-Host "=== Cleaning old publish ===" -ForegroundColor Cyan
Remove-Item -Recurse -Force publish/api -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force publish/web -ErrorAction SilentlyContinue
Remove-Item -Force publish/api.zip -ErrorAction SilentlyContinue
Remove-Item -Force publish/web.zip -ErrorAction SilentlyContinue

Write-Host "=== Building API ===" -ForegroundColor Cyan
dotnet publish TourGuideApi -c Release -o publish/api
if ($LASTEXITCODE -ne 0) { Write-Host "API build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Building Web ===" -ForegroundColor Cyan
dotnet publish TouristGuideWeb -c Release -o publish/web
if ($LASTEXITCODE -ne 0) { Write-Host "Web build failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Zipping ===" -ForegroundColor Cyan
Compress-Archive -Path publish/api/* -DestinationPath publish/api.zip -Force
Compress-Archive -Path publish/web/* -DestinationPath publish/web.zip -Force

Write-Host "=== Deploying API ===" -ForegroundColor Cyan
az webapp deploy --resource-group rg-sharpppio-prod --name sharpppio-api --src-path publish/api.zip --type zip
if ($LASTEXITCODE -ne 0) { Write-Host "API deploy failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Deploying Web ===" -ForegroundColor Cyan
az webapp deploy --resource-group rg-sharpppio-prod --name sharpppio --src-path publish/web.zip --type zip
if ($LASTEXITCODE -ne 0) { Write-Host "Web deploy failed!" -ForegroundColor Red; exit 1 }

Write-Host "=== Done! ===" -ForegroundColor Green
Write-Host "API: https://sharpppio-api.azurewebsites.net" -ForegroundColor Green
Write-Host "Web: https://sharpppio.azurewebsites.net" -ForegroundColor Green

$locations = Invoke-RestMethod -Uri 'http://localhost:5214/api/locations'
foreach ($loc in $locations) {
    if ([string]::IsNullOrWhiteSpace($loc.description)) {
        Write-Host "Skipping $($loc.name) because it has no description"
        continue
    }

    Write-Host "Generating translation pack for $($loc.name)..."
    $body = @{
        LocationId = $loc.id
        VietnameseName = $loc.name
        VietnameseDescription = $loc.description
    } | ConvertTo-Json

    try {
        $result = Invoke-RestMethod -Uri 'http://localhost:5214/api/localizations/generate-pack' -Method Post -Body $body -ContentType 'application/json'
        Write-Host "Result: $($result.message)"
    } catch {
        Write-Host "Error translating $($loc.name): $_"
    }
}
Write-Host "Done setting up translations!"

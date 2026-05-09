param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$ApiAppName,

    [Parameter(Mandatory = $true)]
    [string]$WebAppName,

    [Parameter(Mandatory = $true)]
    [string]$PostgresServerName,

    [Parameter(Mandatory = $true)]
    [string]$PostgresAdminUser,

    [Parameter(Mandatory = $true)]
    [string]$PostgresAdminPassword,

    [Parameter(Mandatory = $true)]
    [string]$ApiPublicUrl,

    [Parameter(Mandatory = $true)]
    [string]$WebPublicUrl,

    [Parameter(Mandatory = $true)]
    [string]$JwtKey,

    [Parameter(Mandatory = $true)]
    [string]$AdminEmail,

    [Parameter(Mandatory = $true)]
    [string]$AdminPassword,

    [string]$GeminiApiKey
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Command {
    param([string]$Name)

    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $cmd) {
        throw "$Name is not installed or not in PATH."
    }
}

function Normalize-BaseUrl {
    param([string]$InputUrl)

    $trimmed = $InputUrl.Trim()
    $parsedUri = $null
    if (-not [Uri]::TryCreate($trimmed, [System.UriKind]::Absolute, [ref]$parsedUri)) {
        throw "Invalid absolute URL: $InputUrl"
    }

    $normalized = $parsedUri.AbsoluteUri.TrimEnd('/')
    return $normalized
}

function Ensure-TrailingSlash {
    param([string]$InputUrl)

    if ($InputUrl.EndsWith('/')) {
        return $InputUrl
    }

    return "$InputUrl/"
}

Assert-Command -Name "az"

Write-Host "Checking Azure login..." -ForegroundColor Cyan
az account show --output none

$apiBaseUrl = Normalize-BaseUrl -InputUrl $ApiPublicUrl
$webBaseUrl = Normalize-BaseUrl -InputUrl $WebPublicUrl

$postgresHost = "$PostgresServerName.postgres.database.azure.com"
# Azure Database for PostgreSQL Flexible Server uses the admin username as created
# (for example: pgadmin), not the legacy user@server format.
$postgresUser = $PostgresAdminUser

$apiConnection = "Host=$postgresHost;Port=5432;Database=tourguide;Username=$postgresUser;Password=$PostgresAdminPassword;Ssl Mode=Require;Trust Server Certificate=true"
$webConnection = "Host=$postgresHost;Port=5432;Database=tourguide_identity;Username=$postgresUser;Password=$PostgresAdminPassword;Ssl Mode=Require;Trust Server Certificate=true"

$apiSettings = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "ConnectionStrings__DefaultConnection=$apiConnection",
    "Jwt__Key=$JwtKey",
    "Jwt__Issuer=$apiBaseUrl",
    "Jwt__Audience=TourGuideApp",
    "AllowedOriginsCsv=$webBaseUrl"
)

if (-not [string]::IsNullOrWhiteSpace($GeminiApiKey)) {
    $apiSettings += "Gemini__ApiKey=$GeminiApiKey"
}

$protectedTerms = @(
    "Vĩnh Khánh",
    "Bánh mì",
    "Quán Ốc Oanh",
    "Ốc Oanh",
    "Cơm tấm",
    "Phở",
    "Bún bò",
    "Bánh xèo",
    "Bánh mì Huỳnh Hoa"
)
$apiSettings += "Gemini__ProtectedTerms__0=$($protectedTerms[0])"
$apiSettings += "Gemini__ProtectedTerms__1=$($protectedTerms[1])"
$apiSettings += "Gemini__ProtectedTerms__2=$($protectedTerms[2])"
$apiSettings += "Gemini__ProtectedTerms__3=$($protectedTerms[3])"
$apiSettings += "Gemini__ProtectedTerms__4=$($protectedTerms[4])"
$apiSettings += "Gemini__ProtectedTerms__5=$($protectedTerms[5])"
$apiSettings += "Gemini__ProtectedTerms__6=$($protectedTerms[6])"
$apiSettings += "Gemini__ProtectedTerms__7=$($protectedTerms[7])"
$apiSettings += "Gemini__ProtectedTerms__8=$($protectedTerms[8])"

$webSettings = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "ConnectionStrings__IdentityConnection=$webConnection",
    "ApiSettings__BaseUrl=$(Ensure-TrailingSlash -InputUrl $apiBaseUrl)",
    "ApiSettings__AdminEmail=$AdminEmail",
    "ApiSettings__AdminPassword=$AdminPassword",
    "DownloadSettings__PublicBaseUrl=$webBaseUrl"
)

Write-Host "Applying API app settings to $ApiAppName..." -ForegroundColor Cyan
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $ApiAppName `
    --settings $apiSettings `
    --output none

Write-Host "Applying Web app settings to $WebAppName..." -ForegroundColor Cyan
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --settings $webSettings `
    --output none

Write-Host "Done. Azure app settings configured for API and Web." -ForegroundColor Green

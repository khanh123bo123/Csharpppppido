# Azure Deployment Checklist (API + Web + Mobile)

This checklist is for the current codebase after local-only cleanup.

## 1) What You Deploy

1. `TourGuideApi` -> Azure App Service (API)
2. `TouristGuideWeb` -> Azure App Service (Admin/Owner web)
3. Azure Database for PostgreSQL Flexible Server -> two databases:
   1. `tourguide`
   2. `tourguide_identity`

## 2) Prerequisites

1. Azure subscription
2. Azure CLI installed (`az --version`)
3. GitHub repo connected
4. Unique app names prepared:
   1. `YOUR-API-APP-NAME`
   2. `YOUR-WEB-APP-NAME`

## 3) Create Azure Resources (CLI)

Run from any terminal (replace placeholders):

```powershell
az login
az account set --subscription "<YOUR_SUBSCRIPTION_NAME_OR_ID>"

az group create --name rg-tourguide-prod --location southeastasia

az appservice plan create \
  --resource-group rg-tourguide-prod \
  --name asp-tourguide-prod \
  --sku B1 \
  --is-linux

az webapp create \
  --resource-group rg-tourguide-prod \
  --plan asp-tourguide-prod \
  --name YOUR-API-APP-NAME \
  --runtime "DOTNETCORE|10.0"

az webapp create \
  --resource-group rg-tourguide-prod \
  --plan asp-tourguide-prod \
  --name YOUR-WEB-APP-NAME \
  --runtime "DOTNETCORE|10.0"

az postgres flexible-server create \
  --resource-group rg-tourguide-prod \
  --name pg-tourguide-prod \
  --location southeastasia \
  --admin-user pgadmin \
  --admin-password "<STRONG_DB_PASSWORD>" \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --version 16 \
  --storage-size 32

az postgres flexible-server db create --resource-group rg-tourguide-prod --server-name pg-tourguide-prod --database-name tourguide
az postgres flexible-server db create --resource-group rg-tourguide-prod --server-name pg-tourguide-prod --database-name tourguide_identity

# Allow Azure services to access PostgreSQL
az postgres flexible-server firewall-rule create \
  --resource-group rg-tourguide-prod \
  --name pg-tourguide-prod \
  --rule-name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

## 4) Configure App Settings (One Command Script)

Use the script created in this repo:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\azure\configure-appsettings.ps1 \
  -ResourceGroup "rg-tourguide-prod" \
  -ApiAppName "YOUR-API-APP-NAME" \
  -WebAppName "YOUR-WEB-APP-NAME" \
  -PostgresServerName "pg-tourguide-prod" \
  -PostgresAdminUser "pgadmin" \
  -PostgresAdminPassword "<STRONG_DB_PASSWORD>" \
  -ApiPublicUrl "https://YOUR-API-APP-NAME.azurewebsites.net" \
  -WebPublicUrl "https://YOUR-WEB-APP-NAME.azurewebsites.net" \
  -JwtKey "<LONG_RANDOM_JWT_KEY_MIN_32>" \
  -AdminEmail "admin@yourcompany.com" \
  -AdminPassword "<STRONG_ADMIN_PASSWORD>" \
  -GeminiApiKey "<OPTIONAL_GEMINI_KEY>"
```

## 5) Deploy Code from GitHub

Do this for both web apps in Azure Portal:

1. Open `YOUR-API-APP-NAME` -> Deployment Center -> GitHub
2. Select repository/branch
3. Save and trigger deployment
4. Repeat for `YOUR-WEB-APP-NAME`

## 6) Validate Production URLs

1. API health: `https://YOUR-API-APP-NAME.azurewebsites.net/api/health`
2. Web admin: `https://YOUR-WEB-APP-NAME.azurewebsites.net`
3. APK page: `https://YOUR-WEB-APP-NAME.azurewebsites.net/apk`
4. QR image: `https://YOUR-WEB-APP-NAME.azurewebsites.net/apk/qr.png`

## 7) Mobile App Setup

1. In app Settings, set API online URL to:
   1. `https://YOUR-API-APP-NAME.azurewebsites.net/`
2. Save
3. Test from 4G and WiFi

## 8) APK Publish Flow (Dynamic QR)

After each new Android build:

```powershell
powershell -ExecutionPolicy Bypass -File .\publish-apk.ps1
```

Then open:

1. `https://YOUR-WEB-APP-NAME.azurewebsites.net/apk`
2. Download or scan QR (QR points directly to current APK release path)

## 9) Important Notes

1. Keep secrets only in Azure App Settings, not in committed files.
2. If startup fails, check App Service logs first.
3. If DB connection fails, re-check firewall rule and PostgreSQL credentials.
4. If CORS fails, verify `AllowedOriginsCsv` includes the web app public URL.

<#
.SYNOPSIS
    Deploys the latest Skill Hub Backend container image to Azure Container Apps.

.DESCRIPTION
    Updates only the container image for 'ca-skillhub-backend' without touching
    existing environment variables, connection strings, or secrets.

.PARAMETER Tag
    The Docker image tag in GHCR to deploy (default: "dev-latest").
    Examples: "dev-latest", "latest", "sha-42dee34"

.EXAMPLE
    .\deploy.ps1
    .\deploy.ps1 -Tag latest
    .\deploy.ps1 -Tag dev-latest
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Tag = "dev-latest"
)

$ErrorActionPreference = "Stop"

# Configuration
$ResourceGroup = "TalentMatch-RG"
$AppName       = "ca-skillhub-backend"
$Image         = "ghcr.io/chamiya09/skill-hub-backend:$Tag"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "🚀 Skill Hub Backend: Azure Container App Deployment" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "• Resource Group : $ResourceGroup"
Write-Host "• Container App  : $AppName"
Write-Host "• Deploy Image   : $Image"
Write-Host "----------------------------------------------------------"

# 1. Verify Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI ('az') is not installed or not in PATH. Please install it from https://aka.ms/installazurecliwindows"
    exit 1
}

# 2. Check Azure Authentication status
Write-Host "🔍 Checking Azure authentication status..." -ForegroundColor Yellow
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "⚠️ Not logged in to Azure. Running 'az login'..." -ForegroundColor Yellow
    az login
    $account = az account show --output json | ConvertFrom-Json
}

Write-Host "✅ Logged in as: $($account.user.name) ($($account.name))" -ForegroundColor Green

# 3. Update the Container App Image
# IMPORTANT: 'az containerapp update --image' strictly updates ONLY the image.
# It preserves all existing environment variables, secrets, and configurations.
Write-Host "`n📦 Updating container image (preserving environment variables & secrets)..." -ForegroundColor Yellow
az containerapp update `
    --name $AppName `
    --resource-group $ResourceGroup `
    --image $Image `
    --output table

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed. Please check the error above."
    exit $LASTEXITCODE
}

# 4. Fetch and display deployed URL
$fqdn = az containerapp show `
    --name $AppName `
    --resource-group $ResourceGroup `
    --query "properties.configuration.ingress.fqdn" `
    -o tsv

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "🎉 DEPLOYMENT SUCCESSFUL!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "🌐 Live API URL : https://$fqdn" -ForegroundColor Cyan
Write-Host "📄 Swagger Docs : https://$fqdn/swagger" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Green

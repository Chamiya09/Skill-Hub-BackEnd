#!/usr/bin/env bash

# ==============================================================================
# Script Name: deploy-backend.sh
# Description: Production-ready Azure CLI deployment script for ASP.NET Core 8
#              backend targeting Azure Container Apps (Consumption Tier).
# Cost Target: Fully optimized for $100 Azure for Students Credit (Scale-to-Zero).
# ==============================================================================

set -euo pipefail

# ==========================================
# CONFIGURATION & ENVIRONMENT VARIABLES
# ==========================================
RESOURCE_GROUP="TalentMatch-RG"
LOCATION="eastasia"
ENVIRONMENT_NAME="cae-skillhub-prod"
APP_NAME="ca-skillhub-backend"

GITHUB_USERNAME="chamiya09"
IMAGE_TAG="${1:-${IMAGE_TAG:-latest}}"
IMAGE_NAME="ghcr.io/${GITHUB_USERNAME}/skill-hub-backend:${IMAGE_TAG}"

# Minimal resource footprint (Lowest possible cost tier)
CPU="0.25"
MEMORY="0.5Gi"
TARGET_PORT=8080
MIN_REPLICAS=0
MAX_REPLICAS=1

# ==========================================
# 1. PREREQUISITES & AUTHENTICATION CHECK
# ==========================================
echo "---------------------------------------------------------"
echo "🚀 Skill Hub Backend: Azure Container App Deployment"
echo "---------------------------------------------------------"

if ! command -v az &> /dev/null; then
    echo "❌ Error: Azure CLI ('az') is not installed. Please install it first:"
    echo "   https://learn.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

echo "🔍 Checking Azure authentication status..."
if ! az account show &> /dev/null; then
    echo "⚠️ Not logged in. Initiating Azure login..."
    az login --use-device-code
fi

CURRENT_SUB=$(az account show --query "name" -o tsv)
CURRENT_SUB_ID=$(az account show --query "id" -o tsv)
echo "✅ Authenticated to subscription: ${CURRENT_SUB} (${CURRENT_SUB_ID})"

# Ensure containerapp extension is registered and up-to-date
echo "📦 Ensuring Azure Container Apps CLI extension is ready..."
az extension add --name containerapp --upgrade --yes > /dev/null 2>&1 || true

# Register required resource providers if not already registered
az provider register --namespace Microsoft.App --wait > /dev/null 2>&1 || true
az provider register --namespace Microsoft.OperationalInsights --wait > /dev/null 2>&1 || true

# ==========================================
# 2. RESOURCE GROUP VERIFICATION
# ==========================================
echo "🔎 Checking Resource Group: ${RESOURCE_GROUP}..."
if ! az group exists --name "${RESOURCE_GROUP}" | grep -q "true"; then
    echo "📁 Resource group does not exist. Creating ${RESOURCE_GROUP} in ${LOCATION}..."
    az group create --name "${RESOURCE_GROUP}" --location "${LOCATION}" --output table
else
    echo "✅ Resource group ${RESOURCE_GROUP} already exists."
fi

# ==========================================
# 3. CONTAINER APP ENVIRONMENT (Consumption Tier)
# ==========================================
echo "🌐 Verifying Container Apps Managed Environment: ${ENVIRONMENT_NAME}..."
ENV_EXISTS=$(az containerapp env show \
    --name "${ENVIRONMENT_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --query "name" -o tsv 2>/dev/null || true)

if [ -z "${ENV_EXISTS}" ]; then
    echo "⚙️ Creating serverless Container Apps Environment (Consumption tier)..."
    az containerapp env create \
        --name "${ENVIRONMENT_NAME}" \
        --resource-group "${RESOURCE_GROUP}" \
        --location "${LOCATION}" \
        --output table
    echo "✅ Environment created."
else
    echo "✅ Environment ${ENVIRONMENT_NAME} already exists."
fi

# ==========================================
# 4. DEPLOY CONTAINER APP (Scale-to-Zero & Port 8080)
# ==========================================
echo "🚀 Deploying Container App: ${APP_NAME}..."
echo "   • Image:       ${IMAGE_NAME}"
echo "   • CPU/Memory:  ${CPU} vCPU / ${MEMORY}"
echo "   • Replicas:    Min ${MIN_REPLICAS} (Scale-to-Zero) / Max ${MAX_REPLICAS}"
echo "   • Ingress:     External HTTPS -> Port ${TARGET_PORT}"

# Check if GHCR_PAT or GITHUB_TOKEN is provided for private packages
GHCR_TOKEN="${GHCR_PAT:-${GITHUB_TOKEN:-}}"

APP_EXISTS=$(az containerapp show \
    --name "${APP_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --query "name" -o tsv 2>/dev/null || true)

if [ -n "${GHCR_TOKEN}" ]; then
    echo "🔑 Configuring GitHub Container Registry credentials..."
    if [ -n "${APP_EXISTS}" ]; then
        az containerapp registry set \
            --name "${APP_NAME}" \
            --resource-group "${RESOURCE_GROUP}" \
            --server "ghcr.io" \
            --username "${GITHUB_USERNAME}" \
            --password "${GHCR_TOKEN}" \
            --output none
    fi
fi

if [ -z "${APP_EXISTS}" ]; then
    REGISTRY_ARGS=()
    if [ -n "${GHCR_TOKEN}" ]; then
        REGISTRY_ARGS=(--registry-server "ghcr.io" --registry-username "${GITHUB_USERNAME}" --registry-password "${GHCR_TOKEN}")
    fi

    az containerapp create \
        --name "${APP_NAME}" \
        --resource-group "${RESOURCE_GROUP}" \
        --environment "${ENVIRONMENT_NAME}" \
        --image "${IMAGE_NAME}" \
        --target-port "${TARGET_PORT}" \
        --ingress external \
        --cpu "${CPU}" \
        --memory "${MEMORY}" \
        --min-replicas "${MIN_REPLICAS}" \
        --max-replicas "${MAX_REPLICAS}" \
        "${REGISTRY_ARGS[@]}" \
        --env-vars \
            ASPNETCORE_ENVIRONMENT="Production" \
            ASPNETCORE_HTTP_PORTS="${TARGET_PORT}" \
            ConnectionStrings__DefaultConnection="secretref:db-connection" \
            JwtSettings__SecretKey="secretref:jwt-secret" \
            JwtSettings__Issuer="SkillHubApi" \
            JwtSettings__Audience="SkillHubClients" \
            GoogleCalendar__ApiKey="secretref:calendar-key" \
            AiAgent__BaseUrl="http://127.0.0.1:8000/" \
            ExecutionEngine__Judge0Url="http://127.0.0.1:2358" \
        --output table
else
    echo "🔄 Updating existing Container App..."
    az containerapp update \
        --name "${APP_NAME}" \
        --resource-group "${RESOURCE_GROUP}" \
        --image "${IMAGE_NAME}" \
        --cpu "${CPU}" \
        --memory "${MEMORY}" \
        --min-replicas "${MIN_REPLICAS}" \
        --max-replicas "${MAX_REPLICAS}" \
        --set-env-vars \
            ASPNETCORE_ENVIRONMENT="Production" \
            ASPNETCORE_HTTP_PORTS="${TARGET_PORT}" \
            ConnectionStrings__DefaultConnection="secretref:db-connection" \
            JwtSettings__SecretKey="secretref:jwt-secret" \
            JwtSettings__Issuer="SkillHubApi" \
            JwtSettings__Audience="SkillHubClients" \
            GoogleCalendar__ApiKey="secretref:calendar-key" \
            AiAgent__BaseUrl="http://127.0.0.1:8000/" \
            ExecutionEngine__Judge0Url="http://127.0.0.1:2358" \
        --output table
fi

# ==========================================
# 5. RETRIEVE AND DISPLAY DEPLOYED URL
# ==========================================
BACKEND_FQDN=$(az containerapp show \
    --name "${APP_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --query "properties.configuration.ingress.fqdn" -o tsv)

echo "---------------------------------------------------------"
echo "🎉 DEPLOYMENT SUCCESSFUL!"
echo "---------------------------------------------------------"
echo "🌐 Live Backend URL: https://${BACKEND_FQDN}"
echo "📄 Swagger/API Test: https://${BACKEND_FQDN}/swagger (if enabled)"
echo "---------------------------------------------------------"

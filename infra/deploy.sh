#!/usr/bin/env bash
# =============================================================================
# DocPipeline — One-Shot Azure Deployment Script
# Prerequisites: az cli, docker, dotnet SDK 8, node 20
# Usage: cd DocPipeline && ./infra/deploy.sh
# =============================================================================
set -euo pipefail

# ── Config (edit these or export before running) ──────────────────────────────
APP_NAME="${APP_NAME:-docpipeline}"
ENVIRONMENT="${ENVIRONMENT:-prod}"
LOCATION="${LOCATION:-eastus2}"
RESOURCE_GROUP="${RESOURCE_GROUP:-${APP_NAME}-${ENVIRONMENT}-rg}"
IMAGE_TAG="${IMAGE_TAG:-$(git rev-parse --short HEAD 2>/dev/null || echo 'latest')}"

# ── Color helpers ─────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'
info()    { echo -e "${CYAN}[INFO]${NC} $*"; }
success() { echo -e "${GREEN}[OK]${NC}   $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC} $*"; }
error()   { echo -e "${RED}[ERR]${NC}  $*"; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# ── Prerequisite check ────────────────────────────────────────────────────────
info "Checking prerequisites..."
for cmd in az docker dotnet node; do
  command -v "$cmd" >/dev/null 2>&1 || error "'$cmd' is required but not installed."
done
az account show >/dev/null 2>&1 || error "Not logged in to Azure. Run: az login"
success "Prerequisites OK"

# ── Secrets — prompt if not set as env vars ───────────────────────────────────
while [[ -z "${SQL_ADMIN_PASSWORD:-}" || ${#SQL_ADMIN_PASSWORD} -lt 12 ]]; do
  [[ -n "${SQL_ADMIN_PASSWORD:-}" ]] && warn "Password too short (${#SQL_ADMIN_PASSWORD} chars) — must be at least 12."
  read -rsp "SQL admin password (min 12 chars, upper+lower+digit+special): " SQL_ADMIN_PASSWORD; echo
done
if [[ -z "${JWT_KEY:-}" ]]; then
  read -rsp "JWT key (min 32 chars, or press Enter to auto-generate): " JWT_KEY; echo
  if [[ -z "$JWT_KEY" ]]; then
    JWT_KEY="$(openssl rand -base64 48)"
    warn "Auto-generated JWT key — save this! ${JWT_KEY}"
  fi
fi
[[ ${#JWT_KEY} -lt 32 ]] && error "JWT key must be at least 32 characters."
export SQL_ADMIN_PASSWORD JWT_KEY

# ── Resource Group ────────────────────────────────────────────────────────────
RG_EXISTS=$(az group exists --name "$RESOURCE_GROUP")
if [[ "$RG_EXISTS" == "true" ]]; then
  warn "Resource group '${RESOURCE_GROUP}' already exists — checking for leftover resources..."
else
  info "Creating resource group '${RESOURCE_GROUP}' in '${LOCATION}'..."
  az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
  success "Resource group ready"
fi

# ── Purge soft-deleted Cognitive Services (blocks redeploy with same name) ────
info "Checking for soft-deleted Cognitive Services accounts..."
DELETED_ACCTS=$(az cognitiveservices account list-deleted \
  --query "[?starts_with(name, '${APP_NAME}-${ENVIRONMENT}')].{name:name,location:location}" \
  --output json 2>/dev/null || echo '[]')

if [[ "$DELETED_ACCTS" != "[]" && "$DELETED_ACCTS" != "null" && -n "$DELETED_ACCTS" ]]; then
  echo "$DELETED_ACCTS" | jq -c '.[]' | while read -r acct; do
    ACCT_NAME=$(echo "$acct" | jq -r '.name')
    ACCT_LOC=$(echo "$acct"  | jq -r '.location')
    warn "Purging soft-deleted account: ${ACCT_NAME} (${ACCT_LOC})"
    az cognitiveservices account purge \
      --name "$ACCT_NAME" --location "$ACCT_LOC" \
      --resource-group "$RESOURCE_GROUP" --output none \
      || warn "Purge failed for ${ACCT_NAME} — you may need Owner/Contributor rights on the subscription"
  done
  success "Soft-deleted Cognitive Services accounts purged"
else
  success "No soft-deleted Cognitive Services accounts found"
fi

# ── Deploy Infrastructure (Bicep) ─────────────────────────────────────────────
info "Deploying Bicep infrastructure (this takes ~8-12 minutes)..."
DEPLOY_OUTPUT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "${SCRIPT_DIR}/main.bicep" \
  --parameters "${SCRIPT_DIR}/main.bicepparam" \
  --parameters \
    appName="$APP_NAME" \
    environment="$ENVIRONMENT" \
    location="$LOCATION" \
  --query properties.outputs \
  --output json)

# Parse outputs
ACR_LOGIN_SERVER=$(echo "$DEPLOY_OUTPUT" | jq -r '.acrLoginServer.value')
CONTAINER_APP_NAME=$(echo "$DEPLOY_OUTPUT" | jq -r '.containerAppName.value')
STATIC_WEB_APP_NAME=$(echo "$DEPLOY_OUTPUT" | jq -r '.staticWebAppName.value')
API_URL=$(echo "$DEPLOY_OUTPUT" | jq -r '.apiUrl.value')
FRONTEND_URL=$(echo "$DEPLOY_OUTPUT" | jq -r '.frontendUrl.value')
SWA_DEPLOYMENT_TOKEN=$(echo "$DEPLOY_OUTPUT" | jq -r '.swaDeploymentToken.value')

success "Infrastructure deployed"
info "  API:      ${API_URL}"
info "  Frontend: ${FRONTEND_URL}"

# ── Build & Push Docker Image ─────────────────────────────────────────────────
info "Logging in to ACR '${ACR_LOGIN_SERVER}'..."
az acr login --name "${ACR_LOGIN_SERVER%%.*}"

info "Building API Docker image (tag: ${IMAGE_TAG})..."
docker build \
  --platform linux/amd64 \
  -t "${ACR_LOGIN_SERVER}/docpipeline-api:${IMAGE_TAG}" \
  -t "${ACR_LOGIN_SERVER}/docpipeline-api:latest" \
  -f "${PROJECT_ROOT}/backend/Dockerfile" \
  "${PROJECT_ROOT}/backend"

info "Pushing image to ACR..."
docker push "${ACR_LOGIN_SERVER}/docpipeline-api:${IMAGE_TAG}"
docker push "${ACR_LOGIN_SERVER}/docpipeline-api:latest"
success "API image pushed"

# ── Update Container App with new image ───────────────────────────────────────
info "Updating Container App '${CONTAINER_APP_NAME}' to image tag '${IMAGE_TAG}'..."
az containerapp update \
  --name "$CONTAINER_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --image "${ACR_LOGIN_SERVER}/docpipeline-api:${IMAGE_TAG}" \
  --output none
success "Container App updated"

# ── Build & Deploy Frontend ───────────────────────────────────────────────────
info "Installing frontend dependencies..."
cd "${PROJECT_ROOT}/frontend"
npm ci --silent

info "Building React app (API base URL: ${API_URL})..."
VITE_API_BASE_URL="$API_URL" npm run build

info "Deploying to Azure Static Web Apps..."
npx --yes @azure/static-web-apps-cli deploy \
  ./dist \
  --deployment-token "$SWA_DEPLOYMENT_TOKEN" \
  --env production

success "Frontend deployed"
cd "$PROJECT_ROOT"

# ── Health Check ──────────────────────────────────────────────────────────────
info "Waiting for API to become healthy..."
for i in {1..20}; do
  HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${API_URL}/health" 2>/dev/null || echo "000")
  if [[ "$HTTP_STATUS" == "200" ]]; then
    success "Health check passed (attempt $i)"
    break
  fi
  if [[ $i -eq 20 ]]; then
    warn "Health check did not pass after 20 attempts. Check Container App logs."
  fi
  sleep 10
done

# ── Summary ───────────────────────────────────────────────────────────────────
echo ""
echo -e "${GREEN}════════════════════════════════════════════════${NC}"
echo -e "${GREEN}  DocPipeline deployed successfully!${NC}"
echo -e "${GREEN}════════════════════════════════════════════════${NC}"
echo ""
echo -e "  Frontend:     ${CYAN}${FRONTEND_URL}${NC}"
echo -e "  API:          ${CYAN}${API_URL}${NC}"
echo -e "  Swagger:      ${CYAN}${API_URL}/swagger${NC}"
echo -e "  Health:       ${CYAN}${API_URL}/health${NC}"
echo ""
echo -e "  Resource Group: ${RESOURCE_GROUP}"
echo ""
echo -e "${YELLOW}Save your SWA deployment token as a GitHub secret:${NC}"
echo -e "  Secret name: AZURE_STATIC_WEB_APPS_API_TOKEN"
echo -e "  Value: ${SWA_DEPLOYMENT_TOKEN}"
echo ""

#!/usr/bin/env bash
# Provisions everything for AgriStream using the Azure CLI (az login first).
# Edit the variables, then run:  bash scripts/azure-setup.sh
# Autoscale needs the Standard (S1) tier or higher - Free/Basic cannot scale out automatically.
set -euo pipefail

SUFFIX="st10073072"                    # change - must make names globally unique
RG="rg-agristream"
LOC="southafricanorth"
STORAGE="agristream${SUFFIX}"          # lowercase letters/digits only, 3-24 chars
SQLSRV="sql-agristream-${SUFFIX}"
SQLDB="AgriStream"
SQLUSER="agriadmin"
SQLPASS="${SQLPASS:?export SQLPASS='a-strong-password' before running}"
PLAN="plan-agristream"
WEBAPP="app-agristream-${SUFFIX}"
FUNCAPP="func-agristream-${SUFFIX}"
INSIGHTS="ai-agristream"
INGEST_KEY="${INGEST_KEY:?export INGEST_KEY='some-long-random-string' before running}"

az group create -n $RG -l $LOC

# ---- Storage: Blob + Table + Queue live in one account (task c) ----
az storage account create -n $STORAGE -g $RG -l $LOC --sku Standard_LRS --allow-blob-public-access false
STORAGE_CS=$(az storage account show-connection-string -n $STORAGE -g $RG --query connectionString -o tsv)
az storage container create --name drone-images --connection-string "$STORAGE_CS"
az storage table create --name Telemetry --connection-string "$STORAGE_CS"
az storage table create --name ProcessingAudit --connection-string "$STORAGE_CS"
az storage queue create --name irrigation-commands --connection-string "$STORAGE_CS"

# ---- Azure SQL (task b) ----
az sql server create -n $SQLSRV -g $RG -l $LOC -u $SQLUSER -p "$SQLPASS"
az sql server firewall-rule create -g $RG -s $SQLSRV -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
MYIP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create -g $RG -s $SQLSRV -n MyIP --start-ip-address "$MYIP" --end-ip-address "$MYIP"
az sql db create -g $RG -s $SQLSRV -n $SQLDB --service-objective Basic
SQL_CS="Server=tcp:${SQLSRV}.database.windows.net,1433;Initial Catalog=${SQLDB};User ID=${SQLUSER};Password=${SQLPASS};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# ---- Application Insights (task f) ----
az extension add -n application-insights --only-show-errors || true
az monitor app-insights component create --app $INSIGHTS -g $RG -l $LOC --kind web
AI_CS=$(az monitor app-insights component show --app $INSIGHTS -g $RG --query connectionString -o tsv)

# ---- Function App (task d) - consumption plan ----
az functionapp create -n $FUNCAPP -g $RG --storage-account $STORAGE \
  --consumption-plan-location $LOC --runtime dotnet-isolated --runtime-version 8 --functions-version 4 \
  --app-insights $INSIGHTS
az functionapp config appsettings set -n $FUNCAPP -g $RG --settings \
  "SqlConnectionString=$SQL_CS" "APPLICATIONINSIGHTS_CONNECTION_STRING=$AI_CS"

# ---- App Service (task a) ----
az appservice plan create -n $PLAN -g $RG -l $LOC --sku S1
az webapp create -n $WEBAPP -g $RG -p $PLAN --runtime "dotnet:8"

FUNC_KEY=$(az functionapp keys list -n $FUNCAPP -g $RG --query functionKeys.default -o tsv 2>/dev/null || echo "")
az webapp config appsettings set -n $WEBAPP -g $RG --settings \
  "ConnectionStrings__AzureSql=$SQL_CS" \
  "ConnectionStrings__AzureBlobStorage=$STORAGE_CS" \
  "Functions__BaseUrl=https://${FUNCAPP}.azurewebsites.net/api" \
  "Functions__Key=$FUNC_KEY" \
  "Ingest__ApiKey=$INGEST_KEY" \
  "APPLICATIONINSIGHTS_CONNECTION_STRING=$AI_CS"

# ---- Auto-scaling: scale OUT on CPU or memory (task g) ----
az monitor autoscale create -g $RG --resource $PLAN --resource-type Microsoft.Web/serverfarms \
  --name agristream-autoscale --min-count 1 --max-count 5 --count 1
az monitor autoscale rule create -g $RG --autoscale-name agristream-autoscale \
  --condition "CpuPercentage > 60 avg 2m" --scale out 1 --cooldown 3
az monitor autoscale rule create -g $RG --autoscale-name agristream-autoscale \
  --condition "MemoryPercentage > 75 avg 2m" --scale out 1 --cooldown 3
az monitor autoscale rule create -g $RG --autoscale-name agristream-autoscale \
  --condition "CpuPercentage < 30 avg 10m" --scale in 1 --cooldown 10

echo
echo "Done."
echo "  Web app : https://${WEBAPP}.azurewebsites.net"
echo "  Deploy  : dotnet publish AgriStream/AgriStream.csproj -c Release -o publish/web  (zip + az webapp deploy)"
echo "            cd AgriStream.Functions && func azure functionapp publish ${FUNCAPP}"

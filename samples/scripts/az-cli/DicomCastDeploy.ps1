$RG = 'my-dicom-cast-rg'
$DeploymentTemplateUri = 'https://dcmcistorage.blob.core.windows.net/cibuild/dicom-cast/default-azuredeploy.json'
$DicomCastServiceName = 'my-dicom-cast-service'
$Location = 'WestUS2'

$DicomServiceName= "my-dicom-service"
$DicomWebEndpoint= "https://$DicomServiceName.azurewebsites.net"

$FhirServiceName = "my-fhir-service"
$FhirEndpoint = "https://$FhirServiceName.azurewebsites.net"

az group create -n $RG -l $Location
az deployment group create -g $RG --template-uri $DeploymentTemplateUri --parameters serviceName=$DicomCastServiceName dicomWebEndpoint=$DicomWebEndpoint fhirEndpoint=$FhirEndpoint

$azureTenantId = $(az account show --query "tenantId" -o tsv)
$tokenUri = "https://login.microsoftonline.com/$azureTenantId/oauth2/token"

echo "These settings should come from dicom web app.  See example access token or can also pull from AAD."
$resource = ""
$scope = ""
$clientId = ""
$clientSecret = ""

# at this point, we need to login as the dicom-cast-service?
# Notes mention being able to add KV settings but unable to because of access policy.

$currentUserObjectId = $(az ad signed-in-user show --query objectId -o tsv)
$accessPoliciesJson = $(az keyvault show -n $DicomCastServiceName -g $RG --query properties.accessPolicies)
$accessPoliciesObject = $accessPoliciesJson | ConvertFrom-Json

az keyvault set-policy -n $DicomCastServiceName -g $RG --object-id $currentUserObjectId --secret-permissions get list set

# https://github.com/microsoft/dicom-server/blob/master/docs/quickstarts/deploy-dicom-cast.md#authentication
az keyvault secret set --vault-name $DicomCastServiceName -n "DicomWeb--Authentication--Enabled" --value "True"
az keyvault secret set --vault-name $DicomCastServiceName -n "DicomWeb--Authentication--AuthenticationType" --value "OAuth2ClientCredential"
az keyvault secret set --vault-name $DicomCastServiceName -n "DicomWeb--Authentication--OAuth2ClientCredential--TokenUri" --value "$tokenUri"
az keyvault secret set --vault-name $DicomCastServiceName -n "DicomWeb--Authentication--OAuth2ClientCredential--Resource" --value "$resource"
az keyvault secret set --vault-name $DicomCastServiceName -n "DicomWeb--Authentication--OAuth2ClientCredential--Scope" --value "$scope"
az keyvault secret set --vault-name $DicomCastServiceName -n "DicomWeb--Authentication--OAuth2ClientCredential--ClientId" --value "$clientId"
az keyvault secret set --vault-name $DicomCastServiceName -n "DicomWeb--Authentication--OAuth2ClientCredential--ClientSecret" --value "$clientSecret"

# TODO: Check KV settings for FHIR Web as well?
# Add similar secrets to KeyVault for FHIR™ server.

# revert KV settings.
az keyvault delete-policy -n $DicomCastServiceName -g $RG --object-id $currentUserObjectId

# Restart ACI?
az container restart -n $DicomCastServiceName -g $RG

# check logs for ACI
az container logs -n $dicomCastServiceName -g $RG
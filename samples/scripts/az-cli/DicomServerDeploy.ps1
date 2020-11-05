# https://github.com/microsoft/dicom-server/blob/master/docs/how-to-guides/enable-authentication-with-tokens.md#get-access-token-using-azure-cli

$RG = 'my-dicom-rg'
$DeploymentTemplateUri = 'https://dcmcistorage.blob.core.windows.net/cibuild/default-azuredeploy.json'
$DicomServiceName = 'my-dicom-service'
$Location = 'WestUS2'
echo 'Please enter a SQL Admin Password'
$SqlAdminPassword = Read-Host -AsSecureString
az group create -n $RG -l $Location
az deployment group create -g $RG --template-uri $DeploymentTemplateUri --parameters sqlAdminPassword=$SqlAdminPassword serviceName=$DicomServiceName


###### Authentication ######

# get app service URL
### assumes that there's only one app service in the resource group.
### TODO: filter for app service name matching
$DicomServiceHostName = $(az webapp list -g $RG --query [].defaultHostName -o tsv)
$DicomServiceURL = "https://$DicomServiceHostName"

$DicomWebAppRegistration = $(az ad app create --display-name "$DicomServiceName-App-Registration" --reply-urls $DicomServiceURL)
$DicomWebAppRegistrationJsonObject = $DicomWebAppRegistration | ConvertFrom-json
$DicomWebAppId = $DicomWebAppRegistrationJsonObject.appId

az ad app update --id $DicomWebAppId --identifier-uris api://$DicomWebAppId
az ad app show --id $DicomWebAppId --query="oauth2Permissions" > scopes.json

$currentOauthPermissions = $(Get-Content scopes.json | ConvertFrom-Json)
$currentOauthPermissions[0].adminConsentDescription = "Allow Application Access to $DicomServiceName-App-Registration"
$currentOauthPermissions[0].adminConsentDisplayName = "Access $DicomServiceName-App-Registration"
$currentOauthPermissions[0].type = "Admin"
$currentOauthPermissions[0].userConsentDescription = ""
$currentOauthPermissions[0].userConsentDisplayName = ""
$currentOauthPermissions[0].value="user.impersonation"

# save for use later when tying delegated API permissions
$currentOauthPermissionsId = $currentOauthPermissions[0].id

# if we use pipeline we'll truncate array.
ConvertTo-Json -InputObject @($currentOauthPermissions) > scopes.json

az ad app update --id $DicomWebAppId --set oauth2Permissions=@scopes.json

# clean up scopes file
rm scopes.json

##### Set authentication for App Service #####

$azureTenantId = $(az account show --query "tenantId" -o tsv)
$logonUrl = "https://login.microsoftonline.com/$azureTenantId.onmicrosoft.com"
az webapp config appsettings set --settings DicomServer:Security:Authentication:Audience=$DicomWebAppId -n $DicomServiceName -g $RG
az webapp config appsettings set --settings DicomServer:Security:Authentication:Authority=$logonUrl -n $DicomServiceName -g $RG

$DicomServiceClientAppRegistration= $(az ad app create --display-name "$DicomServiceName-Service-Client-App-Registration")
$DicomServiceClientAppRegistrationJsonObject = $($DicomServiceClientAppRegistration | ConvertFrom-Json)
$DicomServiceClientAppRegistrationAppId = $DicomServiceClientAppRegistrationJsonObject.appId

# want to set the permission to look like the following:
az ad app permission add --id $DicomServiceClientAppRegistrationAppId --api $DicomWebAppId --api-permissions $currentOauthPermissionsId=Scope
# [{
#     "additionalProperties": null,
#     "expiryTime": "N/A",
#     "resourceAccess": [
#       {
#         "additionalProperties": null,
#         "id": "ab1c9472-b2d8-466b-bec0-cb296d3786ca",
#         "type": "Scope"
#       }
#     ],
#     "resourceAppId": "81e926be-93cf-48a0-af5a-e27115e1f96c"
# }]

az ad app permission list --id $DicomServiceClientAppRegistrationAppId

# Get Credential
$DicomServiceClientAppRegistrationCredential = $(az ad app credential reset --id $DicomServiceClientAppRegistrationAppId | ConvertFrom-Json)
$DicomServiceClientAppRegistrationCredentialPassword = $DicomServiceClientAppRegistrationCredential.password

az ad sp create --id $DicomWebAppId

# TODO: Recommended but missing a step here?
az ad app permission grant --id $DicomServiceClientAppRegistrationAppId --api $DicomWebAppId

az ad app show --id $DicomServiceClientAppRegistrationAppId

# https://github.com/microsoft/dicom-server/blob/master/docs/how-to-guides/enable-authentication-with-tokens.md#get-access-token-using-azure-cli
# https://github.com/Azure/azure-cli/blob/24e0b9ef8716e16b9e38c9bb123a734a6cf550eb/src/azure-cli-core/azure/cli/core/_profile.py#L65
$azCliAppId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46"

$requiredResourceAccessesManifest = @"
[
    {
      "additionalProperties": null,
      "resourceAccess": [
        {
          "additionalProperties": null,
          "id": "$currentOauthPermissionsId",
          "type": "Scope"
        }
      ],
      "resourceAppId": "$azCliAppId"
    }
]
"@

echo $requiredResourceAccessesManifest > manifest.json

az ad app update --id $DicomServiceClientAppRegistrationAppId --required-resource-accesses '@manifest.json'

# confirm we can retrieve the access token from az cli
az account get-access-token --resource=$azCliAppId

# Get Access Token Using Web-Request
$accessUrl = "https://login.microsoftonline.com/$azureTenantId/oauth2/token"
# note that we're using form url encoded
$body = @{grant_type='client_credentials'
      client_id=$DicomServiceClientAppRegistrationAppId
      client_secret=$DicomServiceClientAppRegistrationCredentialPassword
      resource=$DicomWebAppId}

# confirm that we can retrieve an access token from a web request
$accessTokenResult = $(Invoke-WebRequest -Uri $accessUrl -Method POST -ContentType 'application/x-www-form-urlencoded' -Body $body)
$accessToken = $($accessTokenResult.Content | ConvertFrom-Json).access_token

$settingsUsedJson = @{
    "Body" = $body;
    "AccessUrl" = $accessUrl;
    "ScopeId" = $currentOauthPermissionsId
} | ConvertTo-Json
echo "These are the settings used to request a token:" $settingsUsedJson

$dicomCastJson = @{
    "resource" = $body.resource;
    "scope" = $currentOauthPermissionsId;
    "clientId" = $body.client_id;
    "clientSecret" = $body.client_secret;
} | ConvertTo-Json
$outputNotes = [string]::Format("###### Use the following settings for dicom cast: {0}", $dicomCastJson)
echo $outputNotes
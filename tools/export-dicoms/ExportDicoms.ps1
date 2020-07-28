# This script is used to export dicom file or images for given instances
# Please update $BlobConnectionString, $BlobContainerName, $Instances, $ContentType, $Label before using this script

# BlobConnectionString: the connection string to connect storage account. Can be found on Access keys setting of Storage Account
[string]$BlobConnectionString = "<Storage Account Connection String>"

# BlobContainerName: the blob container name you want to export dicom file/images
[string]$BlobContainerName = "exported" 

# Instances: The instance id list for exporting dicom file/images
# You can specify multiple instances, the naming convention is studyinstanceUid/seriesInstanceUid/sopInstanceUid
[string[]]$Instances = @("1.2.276.0.7230010.3.1.2.8323329.13666.1517875247.117799/1.2.276.0.7230010.3.1.3.8323329.13666.1517875247.117798/1.2.276.0.7230010.3.1.4.8323329.13666.1517875247.117800")

# The folder you want to save the exported files
[string]$Label = "Pneumothorax"

# ContentType: if image/jpeg, download frames as jpeg file, otherwise just download dicom file
[string]$ContentType = "application/dicom" # could be image/jpeg or application/dicom

# The service host
[string]$Host = "<The URL>"

$ErrorActionPreference = "Stop"

[string]$instanceStr = [string]::Join("," ,($Instances | % {"`"$_`""}))

$body = @"
{
    
    "destinationBlobConnectionString":"$BlobConnectionString",
    "destinationBlobContainerName":"$BlobContainerName",
    "instances":[ $instanceStr ],
    "contentType":"$ContentType",
    "label":"$Label"
}
"@

Invoke-RestMethod -Uri "https://$Host/export1" -Method Post  -Body $body -ContentType 'application/json'
Write-Host "Successfully exported files"

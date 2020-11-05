#https://github.com/microsoft/dicom-server/blob/master/docs/tutorials/use-dicom-web-standard-apis-with-curl.md
serviceName='my-dicom-service'
pathToDcms="/path/to/my/dicom-server/docs/dcms"
studyId="1.2.826.0.1.3680043.8.498.13230779778012324449356534479549187420"
seriesId="1.2.826.0.1.3680043.8.498.45787841905473114233124723359129632652"
seriesInstanceId="1.2.826.0.1.3680043.8.498.47359123102728459884412887463296905395"

# Store-instances-using-multipart/related
curl --location --request POST "http://$serviceName.azurewebsites.net/studies" --header "Accept: application/dicom+json" --header "Content-Type: multipart/related; type=\"application/dicom\"" --form "file1=@$pathToDcms/red-triangle.dcm;type=application/dicom" --trace-ascii "trace.txt"

# Store-instances-for-a-specific-study
curl --request POST "http://$serviceName.azurewebsites.net/studies/$studyId" --header "Accept: application/dicom+json" --header "Content-Type: multipart/related; type=\"application/dicom\"" --form "file1=@$pathToDcms/blue-circle.dcm;type=application/dicom"

# Store-single-instance
curl --location --request POST "http://$serviceName.azurewebsites.net/studies" --header "Accept: application/dicom+json" --header "Content-Type: application/dicom" --data-binary "@$pathToDcms/green-square.dcm"

# Retrieve-all-instances-within-a-study
curl --request GET "http://$serviceName.azurewebsites.net/studies/$studyId" --header "Accept: multipart/related; type=\"application/dicom\"; transfer-syntax=*" --output "suppressWarnings.txt"
# if we want, we can review the results
cat suppressWarnings.txt

# Retrieve-metadata-of-all-instances-in-study
curl --request GET "http://$serviceName.azurewebsites.net/studies/$studyId/metadata" --header "Accept: application/dicom+json"

# Retrieve-all-instances-within-a-series
curl --request GET "http://$serviceName.azurewebsites.net/studies/$studyId/series/$seriesId" --header "Accept: multipart/related; type=\"application/dicom\"; transfer-syntax=*" --output "suppressWarnings.txt"
# if we want, we can review the results
cat suppressWarnings.txt

# Retrieve-metadata-of-all-instances-within-a-series
curl --request GET "http://$serviceName.azurewebsites.net/studies/$studyId/series/$seriesId/metadata" --header "Accept: application/dicom+json"

# Retrieve-a-single-instance-within-a-series-of-a-study
curl --request GET "http://$serviceName.azurewebsites.net/studies/$studyId/series/$seriesId/instances/$seriesInstanceId" --header "Accept: application/dicom; transfer-syntax=*" --output "suppressWarnings.txt"
# if we want, we can review the results
cat suppressWarnings.txt

# Retrieve-metadata-of-a-single-instance-within-a-series-of-a-study
curl --request GET "http://$serviceName.azurewebsites.net/studies/$studyId/series/$seriesId/instances/$seriesInstanceId/metadata" --header "Accept: application/dicom+json"

# Retrieve-one-or-more-frames-from-a-single-instance
curl --request GET "http://$serviceName.azurewebsites.net/studies/$studyId/series/$seriesId/instances/$seriesInstanceId/frames/1" --header "Accept: multipart/related; type=\"application/octet-stream\"; transfer-syntax=1.2.840.10008.1.2.1" --output "suppressWarnings.txt"
# if we want, we can review the results
cat suppressWarnings.txt

# Query DICOM
# Search-for-studies
curl --request GET "http://$serviceName.azurewebsites.net/studies?StudyInstanceUID=$studyId" --header "Accept: application/dicom+json"

# Search-for-series
curl --request GET "http://$serviceName.azurewebsites.net/series?SeriesInstanceUID=$seriesId" --header "Accept: application/dicom+json"

# Search-for-series-within-a-study
curl --request GET "http://$serviceName.azurewebsites.net/studies/$studyId/series?SeriesInstanceUID=$seriesId" --header "Accept: application/dicom+json"

# Search-for-instances
curl --request GET "http://$serviceName.azurewebsites.net/instances?SOPInstanceUID=$seriesInstanceId" --header "Accept: application/dicom+json"

# Search-for-instances-within-a-study
curl --request GET "http://$serviceName.azurewebsites.net/studies/$studyId/instances?SOPInstanceUID=$seriesInstanceId" --header "Accept: application/dicom+json"

# Search-for-instances-within-a-study-and-series
curl --request GET "http://$serviceName.azurewebsites.net/studies/$studyId/series/$seriesId/instances?SOPInstanceUID=$seriesInstanceId" --header "Accept: application/dicom+json"

# Delete-a-specific-instance-within-a-study-and-series
curl --request DELETE "http://$serviceName.azurewebsites.net/studies/$studyId/series/$seriesId/instances/$seriesInstanceId"

# Delete-a-specific-series-within-a-study
curl --request DELETE "http://$serviceName.azurewebsites.net/studies/$studyId/series/$seriesId"

# Delete-a-specific-study
curl --request DELETE "http://$serviceName.azurewebsites.net/studies/$studyId"

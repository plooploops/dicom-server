# Pixel export

Export images from your dicomWeb service as jpeg images to a blob storage container.

## API Design

The export exposes a POST endpoint.

Verb | Route              | Request     
:--- | :----------------- | :---------- 
POST | /export            | Json Object 

## Request Json Object
```
{
    "destinationBlobConnectionString": "",
    "destinationBlobContainerName": "",
    "instances": ["<studyInstanceUID>/<seriesInstanceUID>/<sopInstanceUID>", "", ""]
}
```

## Response

Code | Response
:--- | :---------
200  | OK
500  | Internal Server Error
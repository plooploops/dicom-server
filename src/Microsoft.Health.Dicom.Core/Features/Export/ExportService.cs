// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Dicom;
using Dicom.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Dicom.Core.Exceptions;
using Microsoft.Health.Dicom.Core.Features.Retrieve;
using Microsoft.Health.Dicom.Core.Messages.Retrieve;
using Microsoft.Health.Dicom.Core.Web;
using Microsoft.IO;

namespace Microsoft.Health.Dicom.Core.Features.Export
{
    public class ExportService : IExportService
    {
        private readonly IRetrieveResourceService _retrieveResourceService;
        private readonly ILogger<ExportService> _logger;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;
        private const string DefaultAcceptType = KnownContentTypes.ApplicationDicom;
        private const string JpegAcceptType = KnownContentTypes.ImageJpeg;

        public ExportService(IRetrieveResourceService retrieveResourceService, ILogger<ExportService> logger, RecyclableMemoryStreamManager recyclableMemoryStreamManager)
        {
            _retrieveResourceService = retrieveResourceService;
            _logger = logger;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
        }

        public async Task Export(
            IReadOnlyCollection<string> instances,
            string destinationBlobConnectionString,
            string destinationBlobContainerName,
            string label,
            string contentType = DefaultAcceptType,
            CancellationToken cancellationToken = default)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(destinationBlobConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(destinationBlobContainerName);

            foreach (string instance in instances)
            {
                Stream destinationStream = null;
                try
                {
                    string[] uids = instance.Split('/');
                    string studyInstanceUid = uids[0];
                    string seriesInstanceUid = uids[1];
                    string sopInstanceUid = uids[2];
                    RetrieveResourceRequest retrieve = new RetrieveResourceRequest("*", studyInstanceUid, seriesInstanceUid, sopInstanceUid);

                    var response = await _retrieveResourceService.GetInstanceResourceAsync(retrieve, cancellationToken);
                    destinationStream = response.ResponseStreams.First();

                    if (contentType == JpegAcceptType)
                    {
                        Stream imageStream = EncodeDicomFileAsImage(destinationStream);
                        await SaveAsync(GetFileName(label, studyInstanceUid, seriesInstanceUid, sopInstanceUid, "jpeg"), imageStream, containerClient, JpegAcceptType, cancellationToken);
                        imageStream?.DisposeAsync();
                    }
                    else
                    {
                        await SaveAsync(GetFileName(label, studyInstanceUid, seriesInstanceUid, sopInstanceUid, "dcm"), destinationStream, containerClient, DefaultAcceptType, cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to export instance {instance}, error: {e}");
                }
                finally
                {
                    destinationStream?.DisposeAsync();
                }
            }
        }

        private string GetFileName(
            string label,
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid,
            string extension)
        {
            string folder = string.IsNullOrWhiteSpace(label) ? string.Empty : label + "/";
            return $"{folder}{studyInstanceUid}-{seriesInstanceUid}-{sopInstanceUid}.{extension}";
        }

        private async Task SaveAsync(
            string blobName,
            Stream imageStream,
            BlobContainerClient containerClient,
            string contentType,
            CancellationToken cancellationToken)
        {
            var blobClient = containerClient.GetBlockBlobClient(blobName);

            imageStream.Seek(0, SeekOrigin.Begin);

            await blobClient.UploadAsync(
                    imageStream,
                    new BlobHttpHeaders()
                    {
                        ContentType = contentType,
                    },
                    metadata: null,
                    conditions: null,
                    accessTier: null,
                    progressHandler: null,
                    cancellationToken);
        }

        private Stream EncodeDicomFileAsImage(Stream stream)
        {
            var tempDicomFile = DicomFile.Open(stream);

            // All our test examples are JPEGProcess1, so just returns as it is.
            // We will handle with other transfersyntax later
            if (!tempDicomFile.Dataset.InternalTransferSyntax.Equals(DicomTransferSyntax.JPEGProcess1))
            {
                throw new TranscodingException();
            }

            var dicomPixelData = DicomPixelData.Create(tempDicomFile.Dataset);

            return ToRenderedMemoryStream(dicomPixelData);
        }

        private MemoryStream ToRenderedMemoryStream(DicomPixelData dicomPixelData, int frame = 0)
        {
            // All our test examples are JPEGProcess1, so just returns as it is.
            // We will handle with other transfersyntax later
            MemoryStream ms = _recyclableMemoryStreamManager.GetStream();
            byte[] frameData = dicomPixelData.GetFrame(frame).Data;
            ms.Write(frameData);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}

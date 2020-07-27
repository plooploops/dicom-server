// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Dicom;
using Dicom.Imaging;
using Microsoft.Extensions.Logging;
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
                        await SaveAsync(GetFileName(studyInstanceUid, seriesInstanceUid, sopInstanceUid, "jpeg"), imageStream, containerClient, JpegAcceptType, cancellationToken);
                        imageStream?.DisposeAsync();
                    }
                    else
                    {
                        await SaveAsync(GetFileName(studyInstanceUid, seriesInstanceUid, sopInstanceUid, "dcm"), destinationStream, containerClient, DefaultAcceptType, cancellationToken);
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
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid,
            string extension)
        {
            return $"{studyInstanceUid}-{seriesInstanceUid}-{sopInstanceUid}.{extension}";
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

            var dicomImage = new DicomImage(tempDicomFile.Dataset);

            return ToRenderedMemoryStream(dicomImage);
        }

        private MemoryStream ToRenderedMemoryStream(DicomImage dicomImage, int frame = 0)
        {
            // hardcoded to jpeg for now
            ImageCodecInfo codecInfo = ImageCodecInfo.GetImageEncoders().First(x => x.MimeType == "image/jpeg");
            EncoderParameters encoderParameters = new EncoderParameters(1) { Param = { [0] = new EncoderParameter(Encoder.Quality, 90L) } };

            Bitmap bmp = null;
            MemoryStream ms = _recyclableMemoryStreamManager.GetStream();
            try
            {
                bmp = ToBitmap(dicomImage, frame);
                bmp.Save(ms, codecInfo, encoderParameters);
                ms.Seek(0, SeekOrigin.Begin);
            }
            finally
            {
                bmp?.Dispose();
            }

            return ms;
        }

        private static Bitmap ToBitmap(DicomImage image, int frame = 0)
        {
            byte[] bytes = image.RenderImage(frame).AsBytes();
            var w = image.Width;
            var h = image.Height;
            var ch = 4;

            var bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);

            BitmapData bmData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
            IntPtr pNative = bmData.Scan0;
            Marshal.Copy(bytes, 0, pNative, w * h * ch);
            bmp.UnlockBits(bmData);

            return bmp;
        }
    }
}

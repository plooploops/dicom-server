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
using Microsoft.Health.Dicom.Core.Features.Common;
using Microsoft.Health.Dicom.Core.Features.Model;
using Microsoft.Health.Dicom.Core.Features.Query;
using Microsoft.Health.Dicom.Core.Features.Query.Model;
using Microsoft.Health.Dicom.Core.Web;
using Microsoft.IO;

namespace Microsoft.Health.Dicom.Core.Features.Export
{
    public class ExportService : IExportService
    {
        private readonly IQueryService _queryService;
        private readonly IFileStore _blobDataStore;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;

        public ExportService(IQueryService queryService, IFileStore blobDataStore, RecyclableMemoryStreamManager recyclableMemoryStreamManager)
        {
            _queryService = queryService;
            _blobDataStore = blobDataStore;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
        }

        public async Task Export(string destinationBlobConnectionString, string destinationBlobContainerName, CancellationToken cancellationToken)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(destinationBlobConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(destinationBlobContainerName);

            QueryExpression queryExpression = new QueryExpression(
                QueryResource.AllInstances,
                new QueryIncludeField(false, new List<DicomTag>(0)),
                fuzzyMatching: false,
                limit: 10,
                offset: 0,
                new List<QueryFilterCondition>(0));

            QueryResult queryResult = await _queryService.QueryAsync(queryExpression, cancellationToken);

            foreach (VersionedInstanceIdentifier instance in queryResult.DicomInstances)
            {
                Stream dcmStream = await _blobDataStore.GetFileAsync(instance, cancellationToken);

                Stream imageStream = EncodeDicomFileAsImage(dcmStream);

                await SaveAsync(instance, imageStream, containerClient, cancellationToken);

                // todo append metadata to csv

                // log process information
            }
        }

        private async Task SaveAsync(
            VersionedInstanceIdentifier versionedInstanceIdentifier,
            Stream imageStream,
            BlobContainerClient containerClient,
            CancellationToken cancellationToken)
        {
            string blobName = $"{versionedInstanceIdentifier.StudyInstanceUid}-{versionedInstanceIdentifier.SeriesInstanceUid}-{versionedInstanceIdentifier.SopInstanceUid}.jpeg";

            var blobClient = containerClient.GetBlockBlobClient(blobName);

            imageStream.Seek(0, SeekOrigin.Begin);

            await blobClient.UploadAsync(
                    imageStream,
                    new BlobHttpHeaders()
                    {
                        ContentType = KnownContentTypes.ImageJpeg,
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
    }
}

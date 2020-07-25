// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Health.Dicom.Core.Features.Export
{
    public interface IExportService
    {
        Task Export(string destinationBlobConnectionString, string destinationBlobContainerName, CancellationToken cancellationToken);
    }
}

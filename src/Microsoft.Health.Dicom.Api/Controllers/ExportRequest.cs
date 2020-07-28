// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Health.Dicom.Api.Controllers
{
    public class ExportRequest
    {
        public string DestinationBlobConnectionString { get; set; }

        public string DestinationBlobContainerName { get; set; }

#pragma warning disable CA2227 // Collection properties should be read only
        public List<string> Instances { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        public string ContentType { get; set; }

        public string Label { get; set; }
    }
}

// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Dicom.Core.Features.Export;

namespace Microsoft.Health.Dicom.Api.Controllers
{
    public partial class ExportController : Controller
    {
        private readonly IExportService _exportService;
        private readonly ILogger<ExportController> _logger;

        public ExportController(IExportService exportService, ILogger<ExportController> logger)
        {
            _exportService = exportService;
            _logger = logger;
        }

        [HttpPost]
        [Route("export")]
        public async Task<IActionResult> Export([FromBody] BlobAccessInformation destinationBlob)
        {
            await _exportService.Export(destinationBlob.DestinationBlobConnectionString, destinationBlob.DestinationBlobContainerName, cancellationToken: HttpContext.RequestAborted);

            return StatusCode((int)HttpStatusCode.OK);
        }
    }
}

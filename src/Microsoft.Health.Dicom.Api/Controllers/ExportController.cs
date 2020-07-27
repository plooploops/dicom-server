// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Dicom.Core.Features.Export;
using Microsoft.Health.Dicom.Core.Web;

namespace Microsoft.Health.Dicom.Api.Controllers
{
    public class ExportController : Controller
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
        public async Task<IActionResult> Export([FromBody] ExportRequest request)
        {
            await _exportService.Export(
                request.Instances,
                request.DestinationBlobConnectionString,
                request.DestinationBlobContainerName,
                KnownContentTypes.ApplicationDicom,
                cancellationToken: HttpContext.RequestAborted);

            return StatusCode((int)HttpStatusCode.OK);
        }
    }
}

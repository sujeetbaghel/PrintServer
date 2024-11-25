namespace PrintServer.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using PrintServer.Models;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Printing;

    [ApiController]
    [Route("api/[controller]")]
    public class PrintController : ControllerBase
    {
        private readonly ILogger<PrintController> _logger;

        // Dictionary to map document types to printers
        private readonly Dictionary<string, string> _printerMapping = new Dictionary<string, string>
        {
            { "Sticker", "Microsoft Print to PDF" },
            { "Invoice", "InvoicePrinterName" },
            { "Receipt", "ReceiptPrinterName" },
            { "Certificate", "CertificatePrinterName" }
        };

        public PrintController(ILogger<PrintController> logger)
        {
            _logger = logger;
        }

        [HttpPost("print")]
        public IActionResult PrintDocument([FromBody] PrintRequest request)
        {
            if (string.IsNullOrEmpty(request.DocumentType) || string.IsNullOrEmpty(request.DocumentData))
                return BadRequest("DocumentType and DocumentData are required.");

            if (!_printerMapping.TryGetValue(request.DocumentType, out var printerName))
                return BadRequest("Invalid DocumentType.");

            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("Printing is only supported on Windows.");
                }

                _logger.LogInformation($"Attempting to print '{request.DocumentType}' on printer '{printerName}'.");

                #pragma warning restore CA1416
                PrintToPrinter(printerName, request.DocumentData);
                #pragma warning disable CA1416

                _logger.LogInformation($"Document of type '{request.DocumentType}' sent to printer '{printerName}' successfully.");

                return Ok($"Document of type '{request.DocumentType}' sent to printer '{printerName}' successfully.");
            }
            catch (PlatformNotSupportedException ex)
            {
                _logger.LogError($"Platform error: {ex.Message}");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while processing print request: {ex.Message}");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private void PrintToPrinter(string printerName, string documentContent)
        {
            PrintDocument printDocument = new PrintDocument
            {
                PrinterSettings = new PrinterSettings
                {
                    PrinterName = printerName
                }
            };

            printDocument.PrintPage += (sender, e) =>
            {
                Font font = new Font("Arial", 12);
                Brush brush = Brushes.Black;
                PointF startPoint = new PointF(100, 100);

                e.Graphics.DrawString(documentContent, font, brush, startPoint);
            };

            if (!printDocument.PrinterSettings.IsValid)
            {
                throw new Exception($"The printer '{printerName}' is not available.");
            }

            printDocument.Print();
        }
    }
}

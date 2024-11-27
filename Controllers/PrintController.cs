namespace PrintServer.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using PrintServer.Models;
    using System;
    using System.Collections.Generic;
    using System.Drawing.Printing;
    using System.IO;


    [ApiController]
    [Route("api/[controller]")]
    public class PrintController : ControllerBase
    {
        private readonly ILogger<PrintController> _logger;

        // Dictionary to map document types to printers
        private readonly Dictionary<string, string> _printerMapping = new Dictionary<string, string>
        {
            { "Invoice", "Microsoft Print to PDF" },
            { "Letter", "LetterPrinter" },
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

            string tempFilePath = null;

            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("Printing is only supported on Windows.");
                }

                _logger.LogInformation($"Attempting to print '{request.DocumentType}' on printer '{printerName}'.");

                // Decode the Base64 PDF data
                byte[] pdfBytes = Convert.FromBase64String(request.DocumentData);

                // Save the PDF to a temporary file
                tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
                System.IO.File.WriteAllBytes(tempFilePath, pdfBytes);

                // Print the PDF file
                PrintPdf(printerName, tempFilePath);

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
            finally
            {
                // Ensure the temporary file is deleted
                if (tempFilePath != null && System.IO.File.Exists(tempFilePath))
                {
                    try
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning($"Failed to delete temporary file: {tempFilePath}. Exception: {deleteEx.Message}");
                    }
                }
            }
        }

        private void PrintPdf(string printerName, string filePath)
        {
            // Load the PDF document from the file
            using (var pdfDocument = PdfiumViewer.PdfDocument.Load(filePath))
            {
                // Setup the printer settings
                var printerSettings = new PrinterSettings
                {
                    PrinterName = printerName
                };

                // Setup the default page settings
                var pageSettings = new PageSettings(printerSettings)
                {
                    Margins = new Margins(0, 0, 0, 0)
                };

                // Use the PrintController to print the document
                using (var printDocument = pdfDocument.CreatePrintDocument())
                {
                    printDocument.PrinterSettings = printerSettings;
                    printDocument.DefaultPageSettings = pageSettings;

                    // Print the document
                    printDocument.Print();
                }
            }
        }
    }
}

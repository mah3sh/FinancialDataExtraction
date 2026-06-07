using DocPipeline.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DocPipeline.Infrastructure.Services;

/// <summary>
/// Used when USE_MOCK_AI=true. Returns realistic static JSON so the full
/// pipeline can be tested locally without Azure OpenAI credentials.
/// </summary>
public class MockExtractionService(ILogger<MockExtractionService> logger) : IAiExtractionService
{
    public async Task<string> ExtractAsync(Stream fileStream, string contentType, CancellationToken ct = default)
    {
        logger.LogWarning("MockExtractionService is active — not calling Azure OpenAI.");
        await Task.Delay(800, ct); // simulate realistic latency

        return """
            {
              "documentType": "Invoice",
              "vendorName": "Contoso Ltd.",
              "clientName": "Fabrikam Inc.",
              "invoiceNumber": "INV-2024-00042",
              "invoiceDate": "2024-06-15",
              "dueDate": "2024-07-15",
              "currency": "USD",
              "subtotalAmount": 4500.00,
              "taxRate": 0.10,
              "taxAmount": 450.00,
              "totalAmount": 4950.00,
              "paymentTerms": "Net 30",
              "lineItems": [
                {
                  "description": "Professional Services - Audit Q2",
                  "quantity": 3,
                  "unitPrice": 1500.00,
                  "totalPrice": 4500.00
                }
              ],
              "billingAddress": "123 Main St, Seattle, WA 98101",
              "notes": "Mock extraction — set USE_MOCK_AI=false and configure Azure OpenAI for real extraction."
            }
            """;
    }
}

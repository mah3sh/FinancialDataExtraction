using DocPipeline.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Azure.AI.OpenAI;
using System.ClientModel;
using UglyToad.PdfPig;
using Docnet.Core;
using Docnet.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DocPipeline.Infrastructure.Services;

public class AzureOpenAiExtractionService(
    IConfiguration config,
    ILogger<AzureOpenAiExtractionService> logger) : IAiExtractionService
{
    private const string SystemPrompt = """
        You are a financial document extraction AI. Analyze the provided document and extract ALL structured data you can identify.
        Return ONLY a valid JSON object — no markdown, no explanation, no code fences.
        Always include a "documentType" field (e.g., "Invoice", "Receipt", "Statement", "PurchaseOrder", "Unknown").
        For financial documents, extract any available fields such as:
        vendorName, clientName, invoiceNumber, invoiceDate, dueDate, totalAmount, subtotalAmount,
        taxAmount, taxRate, currency, paymentTerms, lineItems (array with description/quantity/unitPrice/totalPrice),
        billingAddress, shippingAddress, bankDetails, notes, and any other fields you can identify.
        Use null for fields you cannot determine. Amounts must be numeric (not strings).
        """;

    public async Task<string> ExtractAsync(Stream fileStream, string contentType, CancellationToken ct = default)
    {
        var endpoint = config["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured.");
        var apiKey = config["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not configured.");
        var deployment = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

        var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
        var chatClient = client.GetChatClient(deployment);

        List<ChatMessageContentPart> contentParts;

        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            var text = ExtractTextFromPdfStream(fileStream);
            logger.LogInformation("PDF text extraction yielded {CharCount} characters.", text.Length);

            if (text.Trim().Length > 50)
            {
                // Text-based PDF — send extracted text
                contentParts = [ChatMessageContentPart.CreateTextPart(
                    $"Financial document text content:\n\n{text}")];
            }
            else
            {
                // Scanned/image-based PDF — render first page as image and use vision API
                logger.LogInformation("PDF text too short; falling back to vision API.");
                fileStream.Position = 0;
                var imageData = await RenderPdfFirstPageAsync(fileStream, ct);
                contentParts = [
                    ChatMessageContentPart.CreateTextPart("Extract structured data from this financial document image:"),
                    ChatMessageContentPart.CreateImagePart(imageData, "image/png")
                ];
            }
        }
        else
        {
            // Image: PNG, JPEG, WebP — vision API
            var imageData = await BinaryData.FromStreamAsync(fileStream, ct);
            contentParts = [
                ChatMessageContentPart.CreateTextPart("Extract structured data from this financial document image:"),
                ChatMessageContentPart.CreateImagePart(imageData, contentType.ToLowerInvariant())
            ];
        }

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage(contentParts)
        };

        var options = new ChatCompletionOptions { MaxOutputTokenCount = 2000, Temperature = 0f };

        logger.LogInformation("Calling Azure OpenAI for extraction. Deployment: {Deployment}", deployment);

        var response = await chatClient.CompleteChatAsync(messages, options, ct);
        var rawJson = response.Value.Content[0].Text?.Trim() ?? "{}";
        logger.LogInformation("OpenAI raw response (first 500 chars): {Raw}", rawJson.Length > 500 ? rawJson[..500] : rawJson);

        // Strip markdown code fences if model adds them despite the prompt
        if (rawJson.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            var firstNewline = rawJson.IndexOf('\n');
            if (firstNewline >= 0) rawJson = rawJson[(firstNewline + 1)..];
            if (rawJson.EndsWith("```")) rawJson = rawJson[..^3].Trim();
        }

        return rawJson;
    }

    private static string ExtractTextFromPdfStream(Stream pdfStream)
    {
        using var pdf = PdfDocument.Open(pdfStream);
        var sb = new System.Text.StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private static async Task<BinaryData> RenderPdfFirstPageAsync(Stream pdfStream, CancellationToken ct)
    {
        var pdfBytes = new byte[pdfStream.Length - pdfStream.Position];
        await pdfStream.ReadExactlyAsync(pdfBytes, ct);

        using var lib = DocLib.Instance;
        using var docReader = lib.GetDocReader(pdfBytes, new PageDimensions(1280, 1600));
        using var pageReader = docReader.GetPageReader(0);

        var rawBytes = pageReader.GetImage();
        var width = pageReader.GetPageWidth();
        var height = pageReader.GetPageHeight();

        // Docnet returns BGRA; convert to PNG via ImageSharp
        using var image = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(rawBytes, width, height);
        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms, cancellationToken: ct);
        ms.Position = 0;
        return await BinaryData.FromStreamAsync(ms, ct);
    }
}

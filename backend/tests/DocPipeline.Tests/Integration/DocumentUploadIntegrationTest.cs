using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DocPipeline.Infrastructure.Data;
using FluentAssertions;

namespace DocPipeline.Tests.Integration;

/// <summary>
/// Integration test: registers → logs in → uploads PDF → polls status.
/// Uses in-memory EF and MockExtractionService (via UseMockAI=true).
/// </summary>
public class DocumentUploadIntegrationTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public DocumentUploadIntegrationTest(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace SQL Server with in-memory DB
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(opts =>
                    opts.UseInMemoryDatabase("IntegrationTestDb_" + Guid.NewGuid()));
            });

            builder.UseSetting("UseMockAI", "true");
            builder.UseSetting("Storage:UseAzureBlob", "false");
            builder.UseSetting("Storage:LocalBasePath", Path.GetTempPath());
            builder.UseSetting("Jwt:Key", "integration-test-secret-key-minimum-32-chars!!");
            builder.UseSetting("Jwt:Issuer", "docpipeline");
            builder.UseSetting("Jwt:Audience", "docpipeline");
        });
    }

    [Fact]
    public async Task FullFlow_RegisterLoginUploadPoll_ReturnsCompleted()
    {
        var client = _factory.CreateClient();

        // 1. Register
        var registerBody = JsonSerializer.Serialize(new { email = "test@test.com", password = "Password1!", role = "Uploader" });
        var registerRes = await client.PostAsync("/api/auth/register",
            new StringContent(registerBody, Encoding.UTF8, "application/json"));
        registerRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = JsonSerializer.Deserialize<JsonElement>(await registerRes.Content.ReadAsStringAsync(), JsonOpts);
        var token = auth.GetProperty("token").GetString()!;

        // 2. Upload a minimal PDF (1% of real PDF — just the header)
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF");
        var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        formContent.Add(fileContent, "file", "invoice.pdf");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadRes = await client.PostAsync("/api/documents", formContent);
        uploadRes.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var uploadBody = JsonSerializer.Deserialize<JsonElement>(await uploadRes.Content.ReadAsStringAsync(), JsonOpts);
        var docId = uploadBody.GetProperty("id").GetString()!;
        docId.Should().NotBeNullOrEmpty();

        // 3. Poll status — with MockExtractionService this completes quickly
        var finalStatus = "Pending";
        for (var i = 0; i < 20 && finalStatus is "Pending" or "Processing"; i++)
        {
            await Task.Delay(300);
            var statusRes = await client.GetAsync($"/api/documents/{docId}/status");
            statusRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var statusBody = JsonSerializer.Deserialize<JsonElement>(await statusRes.Content.ReadAsStringAsync(), JsonOpts);
            finalStatus = statusBody.GetProperty("status").GetString()!;
        }

        finalStatus.Should().Be("Completed");

        // 4. Fetch result
        var resultRes = await client.GetAsync($"/api/documents/{docId}/result");
        resultRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = JsonSerializer.Deserialize<JsonElement>(await resultRes.Content.ReadAsStringAsync(), JsonOpts);
        result.GetProperty("extractedData").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Upload_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        formContent.Add(fileContent, "file", "test.pdf");

        var res = await client.PostAsync("/api/documents", formContent);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_ReviewerRole_Returns403()
    {
        var client = _factory.CreateClient();

        // Register as Reviewer
        var body = JsonSerializer.Serialize(new { email = "reviewer@test.com", password = "Password1!", role = "Reviewer" });
        var authRes = await client.PostAsync("/api/auth/register",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var auth = JsonSerializer.Deserialize<JsonElement>(await authRes.Content.ReadAsStringAsync(), JsonOpts);
        var token = auth.GetProperty("token").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var formContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        formContent.Add(fileContent, "file", "f.pdf");

        var res = await client.PostAsync("/api/documents", formContent);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

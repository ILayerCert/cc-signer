using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CC.Signer.Models;
using CC.Signer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CC.Signer.Api;

/// <summary>
/// Lightweight HTTP API that lets iLayerCert request CC signatures programmatically.
/// Start with: cc-signer --api [--port 8085]
/// </summary>
public static class CcSignerApi
{
    public static async Task Run(string[] args, int port = 8085)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Silence ASP.NET Core logs (we use our own logging)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Register CC services (same as GUI mode)
        builder.Services.AddSingleton<CCReaderService>();
        builder.Services.AddSingleton<EncryptionService>();

        var app = builder.Build();
        app.Urls.Add($"http://0.0.0.0:{port}");

        // --- POST /sign ---
        // Body: { "pin": "1234", "hash": "hex..." }
        // Returns: { "success": true, "signature": "base64...", "certificate_pem": "-----BEGIN CERTIFICATE-----...", "mechanism": "SHA256-RSA-PKCS" }
        app.MapPost("/sign", async (SignRequest req, CCReaderService cc) =>
        {
            if (!cc.IsAvailable)
                return Results.Json(new SignResponse { Error = "Cartão de Cidadão não disponível." }, statusCode: 503);

            if (string.IsNullOrWhiteSpace(req.Hash))
                return Results.Json(new SignResponse { Error = "hash is required" }, statusCode: 400);

            if (string.IsNullOrWhiteSpace(req.Pin))
                return Results.Json(new SignResponse { Error = "pin is required" }, statusCode: 400);

            // 1. Sign the hash with Sign PIN (slot 1)
            var signResult = await Task.Run(() => cc.Sign(req.Hash, req.Pin));

            if (!signResult.Success)
                return Results.Json(new SignResponse { Error = $"Signing failed: {signResult.Error}" }, statusCode: 422);

            // 2. Extract certificate with Auth PIN (slot 0) — same PIN
            var cert = await Task.Run(() =>
                cc.GetCertificate(pin: req.Pin, label: "CITIZEN AUTHENTICATION CERTIFICATE"));

            var certificatePem = cert.Success ? cert.Pem : "";

            return Results.Ok(new SignResponse
            {
                Success = true,
                Signature = signResult.Signature,
                CertificatePem = certificatePem,
                Mechanism = signResult.Mechanism,
                Timestamp = DateTimeOffset.Now.ToString("o")
            });
        });

        // --- GET /health ---
        app.MapGet("/health", (CCReaderService cc) =>
        {
            var status = cc.GetStatus();
            return Results.Ok(new
            {
                available = status.Available,
                error = status.Available ? null : status.Error,
                certificates = status.Certificates?.Count ?? 0
            });
        });

        Console.WriteLine($"CC Signer API listening on http://localhost:{port}");
        Console.WriteLine("Endpoints: POST /sign | GET /health");
        await app.RunAsync();
    }
}

// --- Request/Response DTOs ---

public class SignRequest
{
    [JsonPropertyName("pin")]
    public string Pin { get; set; } = string.Empty;

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;
}

public class SignResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("certificate_pem")]
    public string CertificatePem { get; set; } = string.Empty;

    [JsonPropertyName("mechanism")]
    public string Mechanism { get; set; } = "SHA256-RSA-PKCS";

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}

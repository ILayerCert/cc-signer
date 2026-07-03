using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CC.Signer.Models;

namespace CC.Signer.Services;

public class CCReaderService
{
    private const string Pkcs11Tool = "pkcs11-tool";
    private string? _modulePath;

    private static readonly string[] ModulePathsLinux =
    [
        "/usr/lib/x86_64-linux-gnu/libpteidpkcs11.so",
        "/usr/local/lib/libpteidpkcs11.so",
        "/usr/lib/libpteidpkcs11.so"
    ];

    private static readonly string[] ModulePathsMac =
    [
        "/usr/local/lib/libpteidpkcs11.dylib"
    ];

    public CCReaderService()
    {
        _modulePath = FindModule();
    }

    public bool IsAvailable => _modulePath != null;

    private static string? FindModule()
    {
        string[] paths = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? ModulePathsMac
            : ModulePathsLinux;

        foreach (var p in paths)
        {
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private (string stdout, string stderr, int exitCode) Run(string[] args, int timeoutMs = 15000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Pkcs11Tool,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var allArgs = new List<string> { "--module", _modulePath! };
        allArgs.AddRange(args);
        foreach (var a in allArgs) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(timeoutMs);
        if (!proc.HasExited)
        {
            proc.Kill();
            return ("", "Timeout", -1);
        }
        return (stdout, stderr, proc.ExitCode);
    }

    public CCStatus GetStatus()
    {
        if (!IsAvailable)
            return new CCStatus { Available = false, Error = "Middleware do Cartão de Cidadão não encontrado. Instale: sudo apt install pteid-mw" };

        var (stdout, stderr, rc) = Run(["-O"], 10000);
        if (rc != 0)
            return new CCStatus { Available = false, Error = stderr.Trim().Length > 0 ? stderr.Trim() : "Cartão de Cidadão não detectado" };

        var certs = new List<CCCertificate>();
        foreach (var line in stdout.Split('\n'))
        {
            var idMatch = Regex.Match(line, @"ID:\s*(\S+)");
            if (idMatch.Success)
            {
                certs.Add(new CCCertificate
                {
                    Id = idMatch.Groups[1].Value,
                    Label = line[..line.IndexOf("ID:")].Trim()
                });
            }
        }

        return new CCStatus { Available = true, Certificates = certs };
    }

    public CCTokenResult GetCertificate(string? certId = null, string label = "CITIZEN AUTHENTICATION CERTIFICATE")
    {
        if (!IsAvailable)
            return new CCTokenResult { Error = "Middleware não encontrado" };

        if (string.IsNullOrEmpty(certId))
        {
            var (stdout, _, _) = Run(["-O"]);
            foreach (var line in stdout.Split('\n'))
            {
                if (line.Contains(label))
                {
                    var m = Regex.Match(line, @"ID:\s*(\S+)");
                    if (m.Success) { certId = m.Groups[1].Value; break; }
                }
            }
        }

        if (string.IsNullOrEmpty(certId))
            return new CCTokenResult { Error = $"Certificado '{label}' não encontrado" };

        var tmpDer = Path.GetTempFileName();
        var (_, stderr, rc) = Run(["-r", "-y", "cert", "--id", certId, "-o", tmpDer]);

        if (rc != 0)
        {
            try { File.Delete(tmpDer); } catch { }
            return new CCTokenResult { Error = stderr.Trim() };
        }

        // Convert DER to PEM via openssl
        var psi = new ProcessStartInfo
        {
            FileName = "openssl",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("x509");
        psi.ArgumentList.Add("-inform");
        psi.ArgumentList.Add("DER");
        psi.ArgumentList.Add("-in");
        psi.ArgumentList.Add(tmpDer);
        psi.ArgumentList.Add("-outform");
        psi.ArgumentList.Add("PEM");

        using var openssl = Process.Start(psi)!;
        var pem = openssl.StandardOutput.ReadToEnd();
        openssl.WaitForExit(5000);

        try { File.Delete(tmpDer); } catch { }

        return pem.Length > 0
            ? new CCTokenResult { Success = true, CertId = certId, Pem = pem, Label = label }
            : new CCTokenResult { Error = "Falha ao converter certificado para PEM" };
    }

    public CCSignResult Sign(string data, string mechanism = "SHA256-RSA-PKCS")
    {
        if (!IsAvailable)
            return new CCSignResult { Error = "Middleware não encontrado" };

        var infile = Path.GetTempFileName();
        var outfile = Path.GetTempFileName();
        File.WriteAllText(infile, data);

        var (_, stderr, rc) = Run(["--sign", "-m", mechanism, "--input", infile, "-o", outfile], 30000);

        string signature = "";
        if (rc == 0)
        {
            var sigBytes = File.ReadAllBytes(outfile);
            signature = Convert.ToBase64String(sigBytes);
        }

        try { File.Delete(infile); } catch { }
        try { File.Delete(outfile); } catch { }

        return rc == 0
            ? new CCSignResult { Success = true, Signature = signature, Mechanism = mechanism }
            : new CCSignResult { Error = stderr.Trim().Length > 0 ? stderr.Trim() : "Falha ao assinar" };
    }
}

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CC.Signer.Models;

namespace CC.Signer.Services;

/// <summary>
/// Cross-platform Cartão de Cidadão reader using pkcs11-tool.
/// Supports Linux (libpteidpkcs11.so), macOS (libpteidpkcs11.dylib), Windows (pteidpkcs11.dll).
/// </summary>
public class CCReaderService
{
    private readonly string _pkcs11Tool;
    private readonly string _openssl;
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

    private static readonly string[] ModulePathsWindows =
    [
        @"C:\Program Files\Portugal Identity Card\pteidpkcs11.dll",
        @"C:\Program Files (x86)\Portugal Identity Card\pteidpkcs11.dll",
        @"C:\Windows\System32\pteidpkcs11.dll",
        // OpenSC bundled pkcs11 module
        @"C:\Program Files\OpenSC Project\OpenSC\pkcs11\opensc-pkcs11.dll"
    ];

    private static readonly string[] WindowsPkcs11ToolPaths =
    [
        @"C:\Program Files\OpenSC Project\OpenSC\tools\pkcs11-tool.exe",
        @"C:\Program Files (x86)\OpenSC Project\OpenSC\tools\pkcs11-tool.exe",
        "pkcs11-tool.exe"
    ];

    private static readonly string[] WindowsOpenSslPaths =
    [
        @"C:\Program Files\OpenSSL\bin\openssl.exe",
        @"C:\Program Files (x86)\OpenSSL\bin\openssl.exe",
        "openssl.exe"
    ];

    public CCReaderService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _pkcs11Tool = FindWindowsExe(WindowsPkcs11ToolPaths, "pkcs11-tool.exe");
            _openssl = FindWindowsExe(WindowsOpenSslPaths, "openssl.exe");
        }
        else
        {
            _pkcs11Tool = "pkcs11-tool"; // on PATH for Linux/macOS
            _openssl = "openssl";         // on PATH for Linux/macOS
        }

        _modulePath = FindModule();
    }

    public bool IsAvailable => _modulePath != null;

    /// <summary>
    /// Human-readable install instructions based on current OS.
    /// </summary>
    public string GetInstallInstructions()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Instale o Cartão de Cidadão a partir de https://www.autenticacao.gov.pt/cc-aplicacao\n" +
                   "e instale também OpenSC (https://github.com/OpenSC/OpenSC/releases) para o pkcs11-tool.";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "Instale o middleware do CC via App Store ou https://www.autenticacao.gov.pt";
        return "Instale: sudo apt install pteid-mw opensc";
    }

    private static string? FindModule()
    {
        string[] paths = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ModulePathsWindows
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? ModulePathsMac
                : ModulePathsLinux;

        foreach (var p in paths)
        {
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static string FindWindowsExe(string[] paths, string fallback)
    {
        foreach (var p in paths)
        {
            if (File.Exists(p)) return p;
        }
        // Return fallback name — Process.Start will search PATH
        return fallback;
    }

    private (string stdout, string stderr, int exitCode) Run(string[] args, int timeoutMs = 15000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pkcs11Tool,
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
            return new CCStatus { Available = false, Error = GetInstallInstructions() };

        var (stdout, stderr, rc) = Run(["-O"], 10000);
        if (rc != 0)
            return new CCStatus
            {
                Available = false,
                Error = stderr.Trim().Length > 0
                    ? stderr.Trim()
                    : "Cartão de Cidadão não detectado. Verifique se o cartão está inserido no leitor."
            };

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
            return new CCTokenResult { Error = GetInstallInstructions() };

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
            FileName = _openssl,
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

        using var opensslProc = Process.Start(psi)!;
        var pem = opensslProc.StandardOutput.ReadToEnd();
        opensslProc.WaitForExit(5000);

        try { File.Delete(tmpDer); } catch { }

        return pem.Length > 0
            ? new CCTokenResult { Success = true, CertId = certId, Pem = pem, Label = label }
            : new CCTokenResult { Error = "Falha ao converter certificado para PEM. Verifique se o OpenSSL está instalado." };
    }

    public CCSignResult Sign(string data, string mechanism = "SHA256-RSA-PKCS")
    {
        if (!IsAvailable)
            return new CCSignResult { Error = GetInstallInstructions() };

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

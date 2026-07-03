using System.Collections.Generic;

namespace CC.Signer.Models;

public class CCStatus
{
    public bool Available { get; set; }
    public string Error { get; set; } = string.Empty;
    public List<CCCertificate> Certificates { get; set; } = new();
}

public class CCCertificate
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class CCSignResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string Mechanism { get; set; } = "SHA256-RSA-PKCS";
}

public class CCTokenResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public string CertId { get; set; } = string.Empty;
    public string Pem { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class SaveResult
{
    public bool Success { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string KeyFilePath { get; set; } = string.Empty;
    public string EncryptionKey { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

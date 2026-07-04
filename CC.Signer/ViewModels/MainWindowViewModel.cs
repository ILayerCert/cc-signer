using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CC.Signer.Models;
using CC.Signer.Services;

namespace CC.Signer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly CCReaderService _ccReader;
    private readonly EncryptionService _encryption;

    [ObservableProperty]
    private string _statusText = "A verificar...";

    [ObservableProperty]
    private string _statusColor = "#888888";

    [ObservableProperty]
    private bool _isCCAvailable;

    [ObservableProperty]
    private string _dataToSign = string.Empty;

    [ObservableProperty]
    private string _signaturePin = string.Empty;

    [ObservableProperty]
    private string _authenticationPin = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ilayercert", "signatures");

    [ObservableProperty]
    private string _signedFileName = string.Empty;

    [ObservableProperty]
    private string _encryptionKey = string.Empty;

    [ObservableProperty]
    private string _keyFilePath = string.Empty;

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = string.Empty;

    public ObservableCollection<CCCertificate> Certificates { get; } = new();

    public MainWindowViewModel() : this(new CCReaderService(), new EncryptionService()) { }

    public MainWindowViewModel(CCReaderService ccReader, EncryptionService encryption)
    {
        _ccReader = ccReader;
        _encryption = encryption;
        RefreshStatus();
    }

    [RelayCommand]
    private void RefreshStatus()
    {
        var status = _ccReader.GetStatus(AuthenticationPin);
        IsCCAvailable = status.Available;
        Certificates.Clear();

        if (status.Available)
        {
            StatusText = "Cartão de Cidadão detectado ✓";
            StatusColor = "#2ECC71";
            foreach (var c in status.Certificates)
                Certificates.Add(c);
        }
        else
        {
            StatusText = status.Error;
            StatusColor = "#E74C3C";
        }
    }

    [RelayCommand]
    private async Task SignAndSave()
    {
        if (!IsCCAvailable)
        {
            AppendLog("ERRO: Cartão de Cidadão não disponível.");
            return;
        }

        if (string.IsNullOrWhiteSpace(DataToSign))
        {
            AppendLog("ERRO: Introduza os dados para assinar.");
            return;
        }

        IsBusy = true;
        BusyText = "A assinar com Cartão de Cidadão...";
        AppendLog($"> A assinar dados ({DataToSign.Length} bytes)...");

        var signResult = _ccReader.Sign(DataToSign, SignaturePin);

        if (!signResult.Success)
        {
            AppendLog($"ERRO ao assinar: {signResult.Error}");
            IsBusy = false;
            return;
        }

        AppendLog("✓ Assinatura gerada com sucesso.");

        BusyText = "A encriptar assinatura...";
        AppendLog("> A encriptar assinatura com AES-256-GCM...");

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            signature = signResult.Signature,
            mechanism = signResult.Mechanism,
            timestamp = DateTimeOffset.UtcNow.ToString("o"),
            data_hash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(DataToSign)))
        });

        var saveResult = _encryption.EncryptAndSave(payload, OutputDirectory);

        if (!saveResult.Success)
        {
            AppendLog($"ERRO ao encriptar/gravar: {saveResult.Error}");
            IsBusy = false;
            return;
        }

        SignedFileName = saveResult.FilePath;
        EncryptionKey = saveResult.EncryptionKey;
        KeyFilePath = saveResult.KeyFilePath;

        AppendLog("✓ Assinatura encriptada e gravada.");
        AppendLog($"  Ficheiro: {saveResult.FilePath}");
        AppendLog($"  Chave:    {saveResult.EncryptionKey}");
        AppendLog($"  Key file: {saveResult.KeyFilePath}");
        AppendLog("");
        AppendLog("--- CHAVE DE ENCRIPTAÇÃO (copiar para iLayerCert) ---");
        AppendLog(saveResult.EncryptionKey);
        AppendLog("------------------------------------------------------");

        IsBusy = false;
        BusyText = "";
    }

    [RelayCommand]
    private async Task ExtractCertificate()
    {
        if (!IsCCAvailable)
        {
            AppendLog("ERRO: Cartão de Cidadão não disponível.");
            return;
        }

        IsBusy = true;
        BusyText = "A extrair certificado...";
        AppendLog("> A extrair certificado de autenticação...");

        var cert = _ccReader.GetCertificate(pin: AuthenticationPin, label: "CITIZEN AUTHENTICATION CERTIFICATE");

        if (!cert.Success)
        {
            AppendLog($"ERRO: {cert.Error}");
            IsBusy = false;
            return;
        }

        // Save certificate PEM
        var certPath = Path.Combine(OutputDirectory, $"cc-cert-{cert.CertId}.pem");
        Directory.CreateDirectory(OutputDirectory);
        await File.WriteAllTextAsync(certPath, cert.Pem);

        AppendLog($"✓ Certificado extraído: {certPath}");
        AppendLog($"  ID: {cert.CertId}");
        AppendLog($"  Label: {cert.Label}");

        IsBusy = false;
        BusyText = "";
    }

    private void AppendLog(string text)
    {
        LogText += text + Environment.NewLine;
    }
}

# CC Signer — Cartão de Cidadão Signing App (.NET/Avalonia)

Aplicação desktop para assinatura digital com Cartão de Cidadão com encriptação AES-256-GCM.
Integra-se com a iLayerCert APP para assinatura digital de documentos.

## Arquitectura

```
┌──────────────┐     ┌──────────────┐     ┌──────────────────┐
│  CC Signer   │────▶│  ~/.ilayer-  │◀────│  iLayerCert APP  │
│  (.NET GUI)  │     │  cert/signa- │     │  (Laravel)       │
│              │     │  tures/*.enc │     │                  │
│ pkcs11-tool  │     │  + *.key     │     │  CC_SIGNER_KEY=  │
│ AES-256-GCM  │     │              │     │  CC_SIGNER_DIR=  │
└──────────────┘     └──────────────┘     └──────────────────┘
```

## Requisitos

### Linux
- .NET 10.0 Runtime (ou publicar self-contained)
- `sudo apt install pteid-mw opensc` (middleware CC + pkcs11-tool)
- Leitor de smart cards + Cartão de Cidadão

### Windows 11
- .NET 10.0 SDK (para compilar) ou .NET 10.0 Runtime (para executar)
- [Cartão de Cidadão middleware](https://www.autenticacao.gov.pt/cc-aplicacao)
- [OpenSC](https://github.com/OpenSC/OpenSC/releases) (fornece pkcs11-tool.exe)
- [OpenSSL for Windows](https://slproweb.com/products/Win32OpenSSL.html) (Win64 OpenSSL)
- Leitor de smart cards + Cartão de Cidadão

## Instalação

### Linux
```bash
cd cc-signer/
chmod +x install.sh
./install.sh
```

### Windows 11
```bash
# Compilar (requer .NET 10.0 SDK)
publish-win.bat

# A pasta publish\win-x64\ contém o executável CC.Signer.exe
# Copiar para o computador destino e executar
```

Ou fazer cross-compile a partir de Linux:
```bash
dotnet publish CC.Signer/CC.Signer.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```

## Fluxo de utilização

1. **Inserir Cartão de Cidadão** no leitor
2. **Abrir CC Signer** → estado "Cartão de Cidadão detectado ✓"
3. **Inserir dados** a assinar (hash SHA-256 do documento)
4. **Clicar "Assinar com Cartão de Cidadão e Gravar"**
5. A app:
   - Assina com o CC via pkcs11-tool (SHA256-RSA-PKCS)
   - Gera chave AES-256 aleatória
   - Encripta a assinatura com AES-256-GCM
   - Guarda em `~/.ilayercert/signatures/cc-sign-YYYYMMDD-HHmmss.enc`
   - Guarda a chave em `~/.ilayercert/signatures/cc-sign-YYYYMMDD-HHmmss.key`
6. **Copiar a chave** (base64) e configurar na iLayerCert APP

## Configuração na iLayerCert APP

Adicionar ao `.env`:

```env
CC_SIGNER_KEY=<chave base64 do ficheiro .key>
CC_SIGNER_DIR=~/.ilayercert/signatures
```

### Leitura da assinatura no Laravel (PHP)

```php
// Decrypt signature file
$encFile = '~/.ilayercert/signatures/cc-sign-20260703-120000.enc';
$key = env('CC_SIGNER_KEY');

$encrypted = file_get_contents($encFile);
$keyBytes = base64_decode($key);

// Extract IV (12 bytes), Tag (16 bytes), Ciphertext
$iv = substr($encrypted, 0, 12);
$tag = substr($encrypted, 12, 16);
$ciphertext = substr($encrypted, 28);

$plaintext = openssl_decrypt($ciphertext, 'aes-256-gcm', $keyBytes, OPENSSL_RAW_DATA, $iv, $tag);
$payload = json_decode($plaintext, true);

// $payload['signature'] — base64 signature
// $payload['data_hash'] — SHA-256 hash of signed data
// $payload['mechanism'] — 'SHA256-RSA-PKCS'
// $payload['timestamp'] — ISO 8601 timestamp
```

## Estrutura do projecto

```
cc-signer/
├── CC.Signer/
│   ├── CC.Signer.csproj
│   ├── Program.cs
│   ├── App.axaml / App.axaml.cs
│   ├── ViewLocator.cs
│   ├── appsettings.json
│   ├── Models/
│   │   └── Models.cs          (CCStatus, CCSignResult, SaveResult, etc.)
│   ├── Services/
│   │   ├── CCReaderService.cs  (wrapper pkcs11-tool)
│   │   └── EncryptionService.cs (AES-256-GCM encrypt/decrypt)
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs
│   │   └── MainWindowViewModel.cs
│   └── Views/
│       └── MainWindow.axaml / .axaml.cs
├── CC.Signer.slnx
├── install.sh
└── README.md
```

## Troubleshooting

### Linux
**"Middleware não encontrado"**
```bash
sudo apt install pteid-mw opensc
```

**"Cartão não detectado"**
```bash
pkcs11-tool --module /usr/lib/x86_64-linux-gnu/libpteidpkcs11.so -O
```

### Windows 11
**"Middleware não encontrado"**
- Instalar [Cartão de Cidadão Software](https://www.autenticacao.gov.pt/cc-aplicacao)
- Instalar [OpenSC](https://github.com/OpenSC/OpenSC/releases) (versão .msi)
- Verificar que `C:\Program Files\Portugal Identity Card\pteidpkcs11.dll` existe
- Verificar que `C:\Program Files\OpenSC Project\OpenSC\tools\pkcs11-tool.exe` existe

**"Cartão não detectado"**
```cmd
"C:\Program Files\OpenSC Project\OpenSC\tools\pkcs11-tool.exe" --module "C:\Program Files\Portugal Identity Card\pteidpkcs11.dll" -O
```

**Erro ao assinar**
- Verificar que o CC está inserido correctamente
- Tentar `pkcs11-tool --module ... --test` para verificar PIN

#!/bin/bash
# CC Signer — Instalador
# Instala a aplicação de assinatura com Cartão de Cidadão em /opt/cc-signer/

set -e

APP_NAME="cc-signer"
INSTALL_DIR="/opt/$APP_NAME"
DESKTOP_FILE="/usr/share/applications/$APP_NAME.desktop"
PUBLISH_DIR="$(dirname "$0")/publish"

echo "=== CC Signer Installer ==="
echo ""

# Check dependencies
echo "[1/4] A verificar dependências..."

if ! command -v pkcs11-tool &>/dev/null; then
    echo "  ⚠ pkcs11-tool não encontrado. A instalar pteid-mw..."
    sudo apt-get update -qq && sudo apt-get install -y pteid-mw
fi

if ! command -v openssl &>/dev/null; then
    echo "  ⚠ openssl não encontrado. A instalar..."
    sudo apt-get install -y openssl
fi

echo "  ✓ Dependências OK"

# Install binaries
echo "[2/4] A instalar ficheiros..."
sudo mkdir -p "$INSTALL_DIR"
sudo cp -r "$PUBLISH_DIR"/* "$INSTALL_DIR/"
sudo chmod +x "$INSTALL_DIR/CC.Signer"
echo "  ✓ Ficheiros copiados para $INSTALL_DIR"

# Create data directory
echo "[3/4] A criar diretório de assinaturas..."
mkdir -p ~/.ilayercert/signatures
echo "  ✓ ~/.ilayercert/signatures/ criado"

# Create desktop entry
echo "[4/4] A criar atalho no menu..."
sudo tee "$DESKTOP_FILE" > /dev/null <<EOF
[Desktop Entry]
Name=CC Signer
Comment=Assinatura digital com Cartão de Cidadão
Exec=$INSTALL_DIR/CC.Signer
Icon=$INSTALL_DIR/Assets/avalonia-logo.ico
Terminal=false
Type=Application
Categories=Utility;Security;
EOF
sudo update-desktop-database /usr/share/applications/ 2>/dev/null || true
echo "  ✓ Atalho criado"

echo ""
echo "=== Instalação concluída ==="
echo "  Executar: $INSTALL_DIR/CC.Signer"
echo "  Ou procurar 'CC Signer' no menu de aplicações"
echo "  Assinaturas guardadas em: ~/.ilayercert/signatures/"
echo ""
echo "  Integração com iLayerCert:"
echo "  Configurar a chave de encriptação no ficheiro .env da APP:"
echo "    CC_SIGNER_KEY=<chave base64>"
echo "    CC_SIGNER_DIR=~/.ilayercert/signatures"

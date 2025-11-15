#!/bin/bash

# Script to generate development SSL certificates for HTTPS
# This script creates self-signed certificates for local development

set -e

echo "🔐 Generating Development SSL Certificates"
echo "==========================================="
echo ""

# Create certificates directory if it doesn't exist
CERT_DIR="src/HotSwap.Distributed.Api/certificates"
mkdir -p "$CERT_DIR"

# Certificate details
CERT_NAME="aspnetapp"
CERT_PASSWORD=""  # Empty password for development
CERT_DAYS=365

echo "📁 Certificate directory: $CERT_DIR"
echo "📄 Certificate name: $CERT_NAME"
echo "⏰ Valid for: $CERT_DAYS days"
echo ""

# Check if OpenSSL is available
if ! command -v openssl &> /dev/null; then
    echo "❌ Error: OpenSSL is not installed"
    echo "   Install it with:"
    echo "   - Ubuntu/Debian: sudo apt-get install openssl"
    echo "   - macOS: brew install openssl"
    echo "   - Windows: Download from https://slproweb.com/products/Win32OpenSSL.html"
    exit 1
fi

# Check if dotnet is available (for pfx export)
if ! command -v dotnet &> /dev/null; then
    echo "⚠️  Warning: .NET SDK not found - will generate OpenSSL certificates only"
    USE_DOTNET=false
else
    USE_DOTNET=true
fi

# Method 1: Use dotnet dev-certs (preferred if .NET SDK available)
if [ "$USE_DOTNET" = true ]; then
    echo "📦 Using .NET dev-certs tool..."

    # Clean existing dev certificates
    dotnet dev-certs https --clean

    # Generate new certificate
    dotnet dev-certs https --export-path "$CERT_DIR/$CERT_NAME.pfx" --format Pfx --no-password

    # Trust the certificate (optional, requires sudo on Linux)
    if [ "$(uname)" = "Darwin" ]; then
        # macOS - automatically trusted
        dotnet dev-certs https --trust
        echo "✅ Certificate trusted on macOS"
    elif [ "$(uname)" = "Linux" ]; then
        echo "ℹ️  On Linux, you may need to manually trust the certificate:"
        echo "   sudo dotnet dev-certs https --trust"
    fi

    # Also export as PEM for other uses
    openssl pkcs12 -in "$CERT_DIR/$CERT_NAME.pfx" -out "$CERT_DIR/$CERT_NAME.pem" -nodes -passin pass:""
    openssl pkcs12 -in "$CERT_DIR/$CERT_NAME.pfx" -nokeys -out "$CERT_DIR/$CERT_NAME.crt" -passin pass:""
    openssl pkcs12 -in "$CERT_DIR/$CERT_NAME.pfx" -nocerts -out "$CERT_DIR/$CERT_NAME.key" -nodes -passin pass:""

    echo "✅ Certificates generated successfully using .NET dev-certs"
else
    # Method 2: Use OpenSSL only (fallback)
    echo "📦 Using OpenSSL to generate certificates..."

    # Generate private key
    openssl genrsa -out "$CERT_DIR/$CERT_NAME.key" 2048

    # Generate certificate signing request (CSR)
    openssl req -new -key "$CERT_DIR/$CERT_NAME.key" -out "$CERT_DIR/$CERT_NAME.csr" -subj "/C=US/ST=Development/L=Local/O=DistributedKernel/OU=Development/CN=localhost"

    # Generate self-signed certificate
    openssl x509 -req -days $CERT_DAYS -in "$CERT_DIR/$CERT_NAME.csr" -signkey "$CERT_DIR/$CERT_NAME.key" -out "$CERT_DIR/$CERT_NAME.crt" \
        -extfile <(printf "subjectAltName=DNS:localhost,DNS:*.localhost,IP:127.0.0.1")

    # Combine into PEM format
    cat "$CERT_DIR/$CERT_NAME.crt" "$CERT_DIR/$CERT_NAME.key" > "$CERT_DIR/$CERT_NAME.pem"

    # Convert to PFX format (required by Kestrel)
    openssl pkcs12 -export -out "$CERT_DIR/$CERT_NAME.pfx" -inkey "$CERT_DIR/$CERT_NAME.key" -in "$CERT_DIR/$CERT_NAME.crt" -passout pass:""

    # Clean up intermediate files
    rm "$CERT_DIR/$CERT_NAME.csr"

    echo "✅ Certificates generated successfully using OpenSSL"
    echo "⚠️  You may need to manually trust the certificate in your browser"
fi

echo ""
echo "📋 Generated files:"
echo "   - $CERT_DIR/$CERT_NAME.pfx  (Kestrel certificate)"
echo "   - $CERT_DIR/$CERT_NAME.pem  (PEM format)"
echo "   - $CERT_DIR/$CERT_NAME.crt  (Certificate)"
echo "   - $CERT_DIR/$CERT_NAME.key  (Private key)"
echo ""
echo "🚀 You can now run the application with HTTPS:"
echo "   cd src/HotSwap.Distributed.Api"
echo "   dotnet run"
echo ""
echo "   The API will be available at:"
echo "   - HTTP:  http://localhost:5000"
echo "   - HTTPS: https://localhost:5001"
echo ""
echo "🔒 Note: This is a self-signed certificate for DEVELOPMENT ONLY"
echo "   Do NOT use in production. For production, use a certificate from a trusted CA."
echo ""
echo "✅ Done!"

#!/bin/bash

# Backend deployment script for AWS VM

echo "🚀 Deploying Backend..."

# Update system packages
echo "📦 Updating system packages..."
sudo apt update

# Install .NET 8 SDK if not installed
if ! command -v dotnet &> /dev/null; then
    echo "📥 Installing .NET 8 SDK..."
    # Add Microsoft package signing key
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    
    # Install .NET SDK
    sudo apt update
    sudo apt install -y dotnet-sdk-8.0
fi

# Install git if not installed
if ! command -v git &> /dev/null; then
    echo "📥 Installing Git..."
    sudo apt install -y git
fi

# Build the application
echo "🔨 Building application..."
dotnet build

# Run the application
echo "✅ Starting backend server on port 5050..."
echo "Press Ctrl+C to stop the server"
dotnet run --urls="http://0.0.0.0:5050" 
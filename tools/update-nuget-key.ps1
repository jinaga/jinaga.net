#!/usr/bin/env pwsh

# Function to check if gh CLI is installed
function Test-GHInstalled {
    try {
        gh --version > $null
        return $true
    }
    catch {
        return $false
    }
}

# Function to check if user is logged into gh CLI
function Test-GHLoggedIn {
    try {
        gh auth status > $null
        return $true
    }
    catch {
        return $false
    }
}

Write-Host "NuGet API Key Update Script" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Open NuGet API key page
Write-Host "Step 1: Generate new NuGet API key" -ForegroundColor Green
Write-Host "Opening NuGet API key management page..."
Write-Host "Please follow these steps:" -ForegroundColor Yellow
Write-Host "1. Click '+ Create' to create a new API key"
Write-Host "2. Set the following values:"
Write-Host "   - Key name: Jinaga.NET"
Write-Host "   - Glob pattern: Jinaga.*"
Write-Host "   - Scopes: Select 'Push'"
Write-Host "3. Click 'Create'"
Write-Host "4. Copy the generated API key"
Write-Host ""

Start-Process "https://www.nuget.org/account/apikeys"

# Prompt user to confirm they have the key
do {
    $confirmation = Read-Host "Have you generated and copied the new API key? (yes/no)"
} while ($confirmation -ne "yes")

# Step 2: Update GitHub secret
Write-Host ""
Write-Host "Step 2: Update GitHub secret" -ForegroundColor Green

# Check if gh CLI is installed
if (-not (Test-GHInstalled)) {
    Write-Host "GitHub CLI (gh) is not installed. Please install it first:" -ForegroundColor Red
    Write-Host "Windows: winget install GitHub.cli"
    Write-Host "macOS: brew install gh"
    Write-Host "Linux: See https://github.com/cli/cli#installation"
    exit 1
}

# Check if logged into gh CLI
if (-not (Test-GHLoggedIn)) {
    Write-Host "Please log in to GitHub CLI first:" -ForegroundColor Yellow
    Write-Host "Run: gh auth login"
    exit 1
}

# Get the API key securely
Write-Host "Please enter the new NuGet API key (input will be hidden):"
$apiKey = Read-Host -AsSecureString
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($apiKey)
$apiKeyPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)

# Update the GitHub secret
try {
    $repoPath = git rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0) {
        throw "Not in a git repository"
    }
    
    # Get the repo owner and name from the remote URL
    $remoteUrl = git config --get remote.origin.url
    if ($remoteUrl -match "github\.com[:/]([^/]+)/([^/]+)(\.git)?$") {
        $owner = $matches[1]
        $repo = $matches[2] -replace '\.git$', ''
        
        Write-Host "Updating NUGET_API_KEY secret for $owner/$repo..."
        echo $apiKeyPlain | gh secret set NUGET_API_KEY --repo="$owner/$repo"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Successfully updated GitHub secret NUGET_API_KEY" -ForegroundColor Green
        } else {
            throw "Failed to update secret"
        }
    } else {
        throw "Could not determine GitHub repository from git remote URL"
    }
} catch {
    Write-Host "❌ Error updating GitHub secret: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Process completed successfully!" -ForegroundColor Green
Write-Host "You can now run the Release workflow to publish packages with the new API key."

#!/bin/bash

# Colors for output
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to check if gh CLI is installed
check_gh_installed() {
    if ! command -v gh &> /dev/null; then
        echo -e "${RED}GitHub CLI (gh) is not installed. Please install it first:${NC}"
        echo "brew install gh"
        exit 1
    fi
}

# Function to check if user is logged into gh CLI
check_gh_login() {
    if ! gh auth status &> /dev/null; then
        echo -e "${YELLOW}Please log in to GitHub CLI first:${NC}"
        echo "Run: gh auth login"
        exit 1
    fi
}

echo -e "${CYAN}NuGet API Key Update Script${NC}"
echo -e "${CYAN}=========================${NC}"
echo ""

# Step 1: Open NuGet API key page
echo -e "${GREEN}Step 1: Generate new NuGet API key${NC}"
echo "Opening NuGet API key management page..."
echo -e "${YELLOW}Please follow these steps:${NC}"
echo "1. Click '+ Create' to create a new API key"
echo "2. Set the following values:"
echo "   - Key name: Jinaga.NET"
echo "   - Glob pattern: Jinaga.*"
echo "   - Scopes: Select 'Push'"
echo "3. Click 'Create'"
echo "4. Copy the generated API key"
echo ""

# Open the URL in the default browser
open "https://www.nuget.org/account/apikeys"

# Prompt user to confirm they have the key
while true; do
    read -p "Have you generated and copied the new API key? (yes/no) " confirmation
    if [ "$confirmation" = "yes" ]; then
        break
    fi
done

# Step 2: Update GitHub secret
echo ""
echo -e "${GREEN}Step 2: Update GitHub secret${NC}"

# Check prerequisites
check_gh_installed
check_gh_login

# Get the API key securely
echo "Please enter the new NuGet API key (input will be hidden):"
read -s api_key
echo ""

# Update the GitHub secret
echo "Updating GitHub secret..."

# Get repository information
repo_path=$(git rev-parse --show-toplevel 2>/dev/null)
if [ $? -ne 0 ]; then
    echo -e "${RED}Error: Not in a git repository${NC}"
    exit 1
fi

# Get the repo owner and name from the remote URL
remote_url=$(git config --get remote.origin.url)
if [[ $remote_url =~ github\.com[:/]([^/]+)/([^/]+)(\.git)?$ ]]; then
    owner="${BASH_REMATCH[1]}"
    repo="${BASH_REMATCH[2]}"
    repo="${repo%.git}"
    
    echo "Updating NUGET_API_KEY secret for $owner/$repo..."
    echo "$api_key" | gh secret set NUGET_API_KEY --repo="$owner/$repo"
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ Successfully updated GitHub secret NUGET_API_KEY${NC}"
    else
        echo -e "${RED}❌ Failed to update secret${NC}"
        exit 1
    fi
else
    echo -e "${RED}❌ Could not determine GitHub repository from git remote URL${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}Process completed successfully!${NC}"
echo "You can now run the Release workflow to publish packages with the new API key."

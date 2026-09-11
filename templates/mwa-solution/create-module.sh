#!/usr/bin/env bash
set -euo pipefail

MODULE_NAME="${1:-}"

if [ -z "$MODULE_NAME" ]; then
    read -rp "Enter the module name (e.g. Catalog, Orders, Identity): " MODULE_NAME
fi

if [ -z "$MODULE_NAME" ]; then
    echo "Error: Module name cannot be empty." >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODULE_DIR="$SCRIPT_DIR/Modules/$MODULE_NAME"
MODULE_CSPROJ="$MODULE_DIR/$MODULE_NAME.csproj"
CONTRACTS_DIR="$SCRIPT_DIR/Modules/$MODULE_NAME.Contracts"
CONTRACTS_CSPROJ="$CONTRACTS_DIR/$MODULE_NAME.Contracts.csproj"
HOST_CSPROJ="$SCRIPT_DIR/Host/Host.csproj"

if [ -f "$MODULE_CSPROJ" ]; then
    echo "Error: Module '$MODULE_NAME' already exists at '$MODULE_DIR'." >&2
    exit 1
fi

echo "Creating module '$MODULE_NAME' (with companion '$MODULE_NAME.Contracts')..."
(
    cd "$SCRIPT_DIR"
    case "$MODULE_NAME" in
        Identity)
            dotnet new mwa-module-identity
            ;;
        MultiTenancy)
            dotnet new mwa-module-multitenancy
            ;;
        Scheduling)
            dotnet new mwa-module-scheduling
            ;;
        Cms)
            dotnet new mwa-module-cms
            ;;
        Webhooks)
            dotnet new mwa-module-webhooks
            ;;
        *)
            dotnet new mwa-module -n "$MODULE_NAME"
            ;;
    esac

    SLN_FILE=$(find "$SCRIPT_DIR" -maxdepth 1 \( -name "*.slnx" -o -name "*.sln" \) | head -n 1)

    echo "Adding '$MODULE_NAME' and '$MODULE_NAME.Contracts' to solution under Modules folder..."
    if [ -n "$SLN_FILE" ]; then
        dotnet sln "$SLN_FILE" add "$MODULE_CSPROJ" -s "Modules"
        if [ -f "$CONTRACTS_CSPROJ" ]; then
            dotnet sln "$SLN_FILE" add "$CONTRACTS_CSPROJ" -s "Modules"
        fi
    else
        dotnet sln add "$MODULE_CSPROJ" -s "Modules"
        if [ -f "$CONTRACTS_CSPROJ" ]; then
            dotnet sln add "$CONTRACTS_CSPROJ" -s "Modules"
        fi
    fi

    TEST_CSPROJ="$SCRIPT_DIR/Tests/$MODULE_NAME.Tests/$MODULE_NAME.Tests.csproj"
    if [ -f "$TEST_CSPROJ" ]; then
        echo "Adding '$MODULE_NAME.Tests' to solution under Tests folder..."
        if [ -n "$SLN_FILE" ]; then
            dotnet sln "$SLN_FILE" add "$TEST_CSPROJ" -s "Tests"
        else
            dotnet sln add "$TEST_CSPROJ" -s "Tests"
        fi
    fi

    if [ -f "$HOST_CSPROJ" ]; then
        echo "Adding project reference to Host..."
        dotnet add "$HOST_CSPROJ" reference "$MODULE_CSPROJ"
    else
        echo "Warning: Host project not found at '$HOST_CSPROJ'. Skipping reference addition."
    fi

    INSTANCE_SETTINGS="$SCRIPT_DIR/Host/appsettings.Instance.json"
    if [ -f "$INSTANCE_SETTINGS" ]; then
        if command -v python3 >/dev/null 2>&1; then
            python3 -c "import json; p='$INSTANCE_SETTINGS'; f=open(p,'r'); d=json.load(f); f.close(); d.setdefault('ActiveModules',{})['$MODULE_NAME']=True; f=open(p,'w'); json.dump(d,f,indent=2); f.close()"
            echo "Enabled '$MODULE_NAME' in Host/appsettings.Instance.json."
        fi
    fi
)

echo "Module '$MODULE_NAME' (and '$MODULE_NAME.Contracts') created and configured successfully!"

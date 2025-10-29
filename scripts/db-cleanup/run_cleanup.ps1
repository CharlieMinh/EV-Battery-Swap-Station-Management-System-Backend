<#
  run_cleanup.ps1
  PowerShell helper to run scripts/db-cleanup/cleanup_migrations.sql against a SQL Server.

  Usage examples:
  # Windows Auth (current user must have DB access):
  .\run_cleanup.ps1 -Server 'localhost,1433' -Database 'EVBSS_Dev' -Integrated

  # SQL Auth (provide username and password interactively):
  .\run_cleanup.ps1 -Server 'localhost,1433' -Database 'EVBSS_Dev' -Username 'sa' -PromptForPassword

  Notes:
  - This script calls sqlcmd. Ensure sqlcmd is installed and available in PATH.
  - Review scripts/db-cleanup/cleanup_migrations.sql before running. The DELETE from __EFMigrationsHistory is intentionally commented out.
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Server,

    [Parameter(Mandatory=$true)]
    [string]$Database,

    [switch]$Integrated,

    [string]$Username,

    [switch]$PromptForPassword
)

$scriptPath = Join-Path -Path $PSScriptRoot -ChildPath 'cleanup_migrations.sql'
if (-not (Test-Path $scriptPath)) {
    Write-Error "SQL script not found: $scriptPath"
    exit 2
}

# Build sqlcmd arguments
if ($Integrated) {
    $authArgs = "-E"
} else {
    if (-not $Username) {
        Write-Error "For SQL auth, pass -Username and -PromptForPassword (or set -Integrated)."
        exit 2
    }
    if ($PromptForPassword) {
        $secure = Read-Host -AsSecureString "Enter SQL password for $Username"
        # convert secure string to plain text for sqlcmd -P (PowerShell has to hold plain text briefly)
        $ptr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        $plain = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
        $authArgs = "-U `"$Username`" -P `"$plain`""
    } else {
        Write-Error "Use -PromptForPassword to supply the SQL password interactively.";
        exit 2
    }
}

# Run sqlcmd
$cmd = "sqlcmd -S $Server -d $Database $authArgs -i `"$scriptPath`" -b"
Write-Host "Running: $cmd"
# Use Start-Process to preserve exit code
$proc = Start-Process -FilePath sqlcmd -ArgumentList "-S", $Server, "-d", $Database, $authArgs, "-i", $scriptPath -NoNewWindow -Wait -PassThru
if ($proc.ExitCode -ne 0) {
    Write-Error "sqlcmd exited with code $($proc.ExitCode). Check connectivity and credentials."
    exit $proc.ExitCode
}
Write-Host "Done. Review output above and then run 'dotnet ef database update' from the project folder."
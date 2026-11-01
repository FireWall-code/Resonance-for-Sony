# Publishes Resonance.exe (app) and resonance-cli.exe as self-contained single files
# (no .NET installation needed).
#   ./publish.ps1                 install to %LOCALAPPDATA%\Programs\Resonance + Start menu shortcut
#   ./publish.ps1 -Zip            also build publish\Resonance-win-x64.zip (release asset)
param([string]$Destination = "$env:LOCALAPPDATA\Programs\Resonance", [switch]$Zip, [switch]$NoShortcut)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Get-Process Resonance -ErrorAction SilentlyContinue | Stop-Process
$common = @("-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true",
            "-p:DebugType=none", "-o", $Destination, "-nologo", "-v", "q")
foreach ($project in "Resonance.App", "Resonance.Cli") {
    dotnet publish "$root\src\$project" @common
    if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed" }
}
if (-not $NoShortcut) {
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Resonance.lnk")
    $lnk.TargetPath = "$Destination\Resonance.exe"; $lnk.WorkingDirectory = $Destination
    $lnk.Description = "Control Sony headphones"; $lnk.Save()
}
if ($Zip) {
    New-Item -ItemType Directory -Force "$root\publish" | Out-Null
    Compress-Archive -Path "$Destination\Resonance.exe", "$Destination\resonance-cli.exe", "$root\LICENSE" `
        -DestinationPath "$root\publish\Resonance-win-x64.zip" -Force
    "Release asset: $root\publish\Resonance-win-x64.zip"
}
Get-ChildItem $Destination -Filter *.exe | Select-Object Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } }
"Installed to $Destination"

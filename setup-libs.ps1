# setup-libs.ps1
# Copies reference DLLs from your SPT install into the Libs/ folder for building.
# Usage: .\setup-libs.ps1 -SptPath "C:\SPT"

param(
    [Parameter(Mandatory=$false)]
    [string]$SptPath = "C:\SPT"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SptPath)) {
    Write-Error "SPT install not found at '$SptPath'. Pass -SptPath to specify the correct location."
    exit 1
}

$libsDir = Join-Path $PSScriptRoot "Libs"
if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir | Out-Null
}

$sources = @{
    # Game managed DLLs
    "Assembly-CSharp.dll"                  = "EscapeFromTarkov_Data\Managed"
    "bsg.console.core.dll"                 = "EscapeFromTarkov_Data\Managed"
    "Comfort.dll"                          = "EscapeFromTarkov_Data\Managed"
    "DissonanceVoip.dll"                   = "EscapeFromTarkov_Data\Managed"
    "Newtonsoft.Json.dll"                  = "EscapeFromTarkov_Data\Managed"
    "Sirenix.Serialization.dll"            = "EscapeFromTarkov_Data\Managed"
    "Unity.Burst.dll"                      = "EscapeFromTarkov_Data\Managed"
    "Unity.Mathematics.dll"                = "EscapeFromTarkov_Data\Managed"
    "Unity.TextMeshPro.dll"                = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.dll"                      = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.AIModule.dll"             = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.AssetBundleModule.dll"    = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.CoreModule.dll"           = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.ImageConversionModule.dll"= "EscapeFromTarkov_Data\Managed"
    "UnityEngine.IMGUIModule.dll"          = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.InputLegacyModule.dll"    = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.PhysicsModule.dll"        = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.TextRenderingModule.dll"  = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.UI.dll"                   = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.UIModule.dll"             = "EscapeFromTarkov_Data\Managed"
    "UnityEngine.UnityWebRequestModule.dll"= "EscapeFromTarkov_Data\Managed"
    # BepInEx core
    "0Harmony.dll"                         = "BepInEx\core"
    "BepInEx.dll"                          = "BepInEx\core"
    # SPT plugins
    "spt-common.dll"                       = "BepInEx\plugins\spt"
    "spt-core.dll"                         = "BepInEx\plugins\spt"
    "spt-reflection.dll"                   = "BepInEx\plugins\spt"
    "spt-singleplayer.dll"                 = "BepInEx\plugins\spt"
    # SPT patchers
    "spt-prepatch.dll"                     = "BepInEx\patchers"
}

$copied = 0
$missing = 0

foreach ($entry in $sources.GetEnumerator()) {
    $src = Join-Path $SptPath (Join-Path $entry.Value $entry.Key)
    $dst = Join-Path $libsDir $entry.Key

    if (Test-Path $src) {
        Copy-Item -Force $src $dst
        $copied++
    } else {
        Write-Warning "Not found: $src"
        $missing++
    }
}

Write-Host ""
Write-Host "Done: $copied DLLs copied to Libs/, $missing missing." -ForegroundColor $(if ($missing -gt 0) { "Yellow" } else { "Green" })

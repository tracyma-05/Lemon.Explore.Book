<#
.SYNOPSIS
    Publishes the book's Windows desktop app with native AOT to a local folder.

.DESCRIPTION
    The same build as scripts/package-desktop.sh and the release workflow: Release, native AOT, debug symbols left out,
    version from Directory.Build.props. The default update feed is empty, so the local copy does not look for updates.

    Native AOT turns the whole app into one ExploreBook.exe. The native libraries that Avalonia draws with
    (libSkiaSharp.dll, libHarfBuzzSharp.dll and av_libglesv2.dll) cannot go inside it, so they stay next to the exe.
    Narration clips go in a narration folder next to it too, if they have been rendered on this machine.

    The output folder is mirrored from the new publish, so files the app no longer ships are deleted there too.
    update-feed.txt and update.log next to the exe are kept.

    A copy running from the output folder is closed after the build succeeds, so a failed build leaves it running. It
    is started again from the new files once they are copied. With -NoRestart, a running copy stops the script before
    anything is built.

    Native AOT on Windows needs the Visual Studio C++ build tools ("Desktop development with C++").

.EXAMPLE
    ./scripts/publish-local.ps1

.EXAMPLE
    ./scripts/publish-local.ps1 -Zip

.EXAMPLE
    ./scripts/publish-local.ps1 -Clean

.EXAMPLE
    ./scripts/publish-local.ps1 -Runtime win-arm64 -Output C:\Temp\explore-book-arm64 -NoRestart
#>
[CmdletBinding()]
param(
    [string] $Output = 'C:\Data\publishes\explore-book',
    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = 'win-x64',
    # Built into the exe as its update address (see docs/UPDATES.md), e.g. github:owner/repo. Empty: no updates.
    [string] $UpdateFeed = '',
    # Also write explore-book-<version>-<rid>.zip next to the output folder, named the way the updater recognises it.
    [switch] $Zip,
    # Leave a copy running from the output folder alone: stop with an error instead of closing it.
    [switch] $NoRestart,
    # Delete the Release build output of both projects first, so nothing stale is linked into the exe.
    [switch] $Clean
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src\Lemon.Explore.Book.Desktop\Lemon.Explore.Book.Desktop.csproj'
$staging = Join-Path $repo "artifacts\publish-local\$Runtime"
$appName = 'explore-book'
$exeName = 'ExploreBook'

$version = ([xml](Get-Content (Join-Path $repo 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'No <Version> found in Directory.Build.props.' }

# The AOT compiler links with the Visual C++ toolset. Check up front instead of failing after the managed build.
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path $vswhere) {
    # (-prerelease: a Preview or Insiders install is as good, and the AOT compiler finds it the same way)
    $vc = & $vswhere -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (-not $vc) {
        throw 'Native AOT needs the Visual Studio C++ build tools ("Desktop development with C++"); none were found.'
    }

    # The AOT compiler's linker lookup runs vswhere.exe by name, so its folder has to be on PATH.
    $env:PATH = "$(Split-Path -Parent $vswhere);$env:PATH"
}
else {
    Write-Warning 'vswhere.exe not found; the AOT link step needs the Visual Studio C++ build tools.'
}

# A running copy from the output folder locks its files and makes the mirror fail halfway.
$targetExe = Join-Path $Output "$exeName.exe"
function Get-RunningCopy {
    Get-Process -Name $exeName -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and ($_.Path -ieq $targetExe) }
}
if ((Get-RunningCopy) -and $NoRestart) {
    throw "$exeName is running from $Output (PID $((Get-RunningCopy).Id -join ', ')). Close it, or run again without -NoRestart."
}

$audio = Join-Path $repo 'assets\narration\audio'
if (-not (Test-Path $audio)) {
    Write-Warning 'No narration clips rendered here (assets\narration\audio); the app will show subtitles without sound.'
}

Write-Host "Publishing $exeName $version ($Runtime, native AOT) ..." -ForegroundColor Cyan

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }

$library = Join-Path $repo 'src\Lemon.Explore.Book'
if ($Clean) {
    foreach ($folder in $library, (Split-Path -Parent $project)) {
        Remove-Item (Join-Path $folder 'bin\Release'), (Join-Path $folder 'obj\Release') -Recurse -Force -ErrorAction SilentlyContinue
    }
}

dotnet publish $project -c Release -r $Runtime `
    -p:PublishAot=true -p:DebugType=none "-p:UpdateFeed=$UpdateFeed" -p:Version=$version -warnaserror `
    -o $staging
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)." }

# The pages are XAML compiled into the shared assembly by Avalonia's build step. A Release build once came out without
# it and was then reused as up to date: the exe started and died at once ("No precompiled XAML found"). Nothing of that
# shows in the exe, so check the assembly it was linked from.
$built = Join-Path $library 'obj\Release\net10.0\Lemon.Explore.Book.dll'
if (-not [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($built)).Contains('CompiledAvaloniaXaml')) {
    throw "Lemon.Explore.Book.dll was built without its compiled XAML; the app would not start. Run again with -Clean."
}

# Debug symbols of the native libraries are not wanted: most of the size, no use to readers.
Get-ChildItem $staging -Recurse -Include *.pdb, *.dbg | Remove-Item -Force

New-Item -ItemType Directory -Force -Path $Output | Out-Null

# Checked again: the app may have been started or closed during the build.
$stopped = $false
if (-not $NoRestart -and ($running = Get-RunningCopy)) {
    Write-Host "Closing $exeName (PID $($running.Id -join ', ')) ..."
    foreach ($process in $running) {
        # Ask politely first (the book saves the reading position as it closes), then end the process.
        [void] $process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit(10000) | Out-Null
        }
    }
    if (Get-RunningCopy) { throw "$exeName is still running from $Output; nothing was copied." }
    $stopped = $true
}

# /MIR mirrors staging into the output; /XF keeps the install folder's own update address and the updater's log.
robocopy $staging $Output /MIR /XF update-feed.txt update.log /NFL /NDL /NJH /NJS /NP | Out-Null
# robocopy exit codes 0-7 are success (files copied / extras removed); 8+ are failures.
if ($LASTEXITCODE -ge 8) {
    $note = if ($stopped) { " $exeName was closed and not started again: the folder may be half updated." } else { '' }
    throw "robocopy failed (exit code $LASTEXITCODE).$note"
}
$global:LASTEXITCODE = 0

# Started again as soon as the new files are in place, so a failure making the zip below does not leave it closed.
if ($stopped) {
    Start-Process -FilePath $targetExe -WorkingDirectory $Output
    Write-Host "Started $exeName $version again."
}

if ($Zip) {
    # The folder's contents at the zip's root: the updater finds ExploreBook.exe there (see package-desktop.sh).
    $zipPath = Join-Path (Split-Path -Parent $Output) "$appName-$version-$Runtime.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath
    Write-Host "Package: $zipPath"
}

$exeSize = (Get-Item $targetExe).Length / 1MB
$files = Get-ChildItem $Output -File | Where-Object { $_.Name -notin 'update-feed.txt', 'update.log' }
Write-Host ("Done: {0} ({1:N1} MB), with {2}" -f $targetExe, $exeSize,
    (($files | Where-Object Name -ne "$exeName.exe" | ForEach-Object Name) -join ', ')) -ForegroundColor Green

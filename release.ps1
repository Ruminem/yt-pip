# Release: the single self-contained exe (about 50 MB), tag v<Version>, and the GitHub release with that version's
# CHANGELOG entry. Bump <Version> in yt-pip.csproj and add the CHANGELOG entry, commit, then run this.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$v = ([xml](Get-Content yt-pip.csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (git status --porcelain) { throw 'Uncommitted changes: commit the version bump first.' }
$notes = [regex]::Match((Get-Content CHANGELOG.md -Raw -Encoding utf8), "(?s)## $([regex]::Escape($v)) [^\n]*\n(.*?)(\n## |$)").Groups[1].Value.Trim()
if (!$notes) { throw "No CHANGELOG entry for $v." }

$out = Join-Path $env:TEMP "yt-pip-$v"
dotnet publish -c Release -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:GenerateDocumentationFile=false -o $out -nologo
if ($LASTEXITCODE) { throw 'Publish failed.' }
$nf = Join-Path $out 'notes.md'
[IO.File]::WriteAllText($nf, $notes)

git tag "v$v"
git push origin main "v$v"
if ($LASTEXITCODE) { throw 'Push failed.' }
gh release create "v$v" --title "YouTube PIP $v" --notes-file $nf "$out\yt-pip.exe" LICENSE THIRD_PARTY_NOTICES.md
if ($LASTEXITCODE) { throw 'Release failed.' }
"released v${v}: $out\yt-pip.exe"

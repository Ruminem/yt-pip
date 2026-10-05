# Release: the single self-contained exe (about 50 MB), tag v<Version>, the GitHub release with that version's
# CHANGELOG entry, and the same exe over Apps/yt-pip/yt-pip.exe in Google Drive (rclone remote "gdrive"; overwriting keeps
# the file and so its share link). Bump <Version> in yt-pip.csproj and add the CHANGELOG entry, commit, then run this.
# Native commands are checked by exit code: with $ErrorActionPreference = 'Stop', PowerShell 5.1 stops at git's progress on stderr.
Set-Location $PSScriptRoot
$v = ([xml](Get-Content yt-pip.csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (git status --porcelain) { throw 'Uncommitted changes: commit the version bump first.' }
$notes = [regex]::Match((Get-Content CHANGELOG.md -Raw -Encoding utf8), "(?s)## $([regex]::Escape($v)) [^\n]*\n(.*?)(\n## |$)").Groups[1].Value.Trim()
if (!$notes) { throw "No CHANGELOG entry for $v." }

$out = Join-Path $env:TEMP "yt-pip-$v"
dotnet publish -c Release -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:GenerateDocumentationFile=false -o $out -nologo
if ($LASTEXITCODE) { throw 'Publish failed.' }
$exe = Join-Path $out 'yt-pip.exe'
$nf = Join-Path $out 'notes.md'
[IO.File]::WriteAllText($nf, $notes)

git tag "v$v"
if ($LASTEXITCODE) { throw 'Tag failed.' }
git push origin main "v$v"
if ($LASTEXITCODE) { throw 'Push failed.' }
gh release create "v$v" --title "YouTube PIP $v" --notes-file $nf $exe LICENSE THIRD_PARTY_NOTICES.md
if ($LASTEXITCODE) { throw 'Release failed.' }

rclone copyto $exe gdrive:Apps/yt-pip/yt-pip.exe
if ($LASTEXITCODE) { throw 'Drive upload failed (the GitHub release is out).' }
"released v${v}: $exe, Drive link $(rclone link gdrive:Apps/yt-pip/yt-pip.exe)"

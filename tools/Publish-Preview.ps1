[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$config = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Release.psd1')
if ($config.Tag -notmatch '^v\d+\.\d+\.\d+-preview\.\d+$') { throw 'Only preview release tags are supported.' }
$branch = (& git -C $project branch --show-current).Trim()
if ($branch -ne $config.Branch) { throw "Publish from $($config.Branch), not $branch." }
$remote = (& git -C $project remote get-url origin).Trim()
if ($remote -ne ('https://github.com/' + $config.Repository + '.git')) { throw 'Unexpected origin repository.' }
$changes = & git -C $project status --porcelain
if ($changes) { throw 'Commit or preserve local source changes before publishing.' }
$commit = (& git -C $project rev-parse HEAD).Trim()
$output = Join-Path $project ('dist/releases/' + $config.Tag)
$names = @(('DuneRev-' + $config.Tag.TrimStart('v') + '.pthm'), ('DuneMaterials-' + $config.ExtensionVersion + '.pext'))
$buildInfo = [IO.File]::ReadAllText((Join-Path $output 'build-info.json')) | ConvertFrom-Json
if ($buildInfo.Commit -ne $commit -or $buildInfo.Tag -ne $config.Tag -or $buildInfo.ThemeVersion -ne $config.ThemeVersion -or $buildInfo.ExtensionVersion -ne $config.ExtensionVersion) { throw 'Build packages from the committed release source before publishing.' }
foreach ($name in $names) {
    if (-not (Test-Path -LiteralPath (Join-Path $output $name) -PathType Leaf)) { throw "Missing package: $name" }
}
$notes = [IO.File]::ReadAllText((Join-Path $project $config.Notes))
$oldPrompt = $env:GIT_TERMINAL_PROMPT
$oldInteractive = $env:GCM_INTERACTIVE
try {
    $env:GIT_TERMINAL_PROMPT = '0'; $env:GCM_INTERACTIVE = 'Never'
    # Git's configured credential helper is the only source of credentials.
    # Capture its output in memory; never print it or write it to a file.
    $credential = "protocol=https`nhost=github.com`n`n" | & git -C $project -c credential.interactive=false credential fill
    if ($LASTEXITCODE -ne 0) { throw 'GitHub credentials are unavailable. Sign in using Git Credential Manager first.' }
    $passwordLine = $credential | Where-Object { $_.StartsWith('password=') } | Select-Object -First 1
    if (-not $passwordLine) { throw 'Git credential helper did not provide a GitHub credential.' }
    $headers = @{ Authorization = 'Bearer ' + $passwordLine.Substring(9); Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
    $credential = $passwordLine = $null
    $api = 'https://api.github.com/repos/' + $config.Repository
    $release = $null
    try { $release = Invoke-RestMethod -Uri ($api + '/releases/tags/' + $config.Tag) -Headers $headers }
    catch { if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404) { throw } }
    if ($release -and -not $release.draft) { throw 'This release is already published; create a new preview version instead of overwriting it.' }
    if ($release -and $release.target_commitish -ne $commit) { throw 'Existing draft targets a different commit.' }
    & git -C $project show-ref --verify --quiet ('refs/tags/' + $config.Tag)
    if ($LASTEXITCODE -eq 0) {
        $tagCommit = (& git -C $project rev-list -n 1 $config.Tag).Trim()
        if ($tagCommit -ne $commit) { throw 'Existing local tag targets a different commit.' }
    } else {
        & git -C $project tag -a $config.Tag -m ('Dune Rev ' + $config.Tag + ' with Dune Materials ' + $config.ExtensionVersion)
        if ($LASTEXITCODE -ne 0) { throw 'Creating the preview tag failed.' }
    }
    & git -C $project push --quiet --atomic origin ('HEAD:refs/heads/' + $branch) ('refs/tags/' + $config.Tag)
    if ($LASTEXITCODE -ne 0) { throw 'Pushing the preview branch/tag failed; no force push was attempted.' }
    if (-not $release) {
        $previewNumber = $config.Tag.Split('.')[-1]
        $payload = @{ tag_name = $config.Tag; target_commitish = $commit; name = ('Dune Rev ' + $config.ThemeVersion + ' Preview ' + $previewNumber + ' + Dune Materials ' + $config.ExtensionVersion); body = $notes; draft = $true; prerelease = $true; make_latest = 'false' } | ConvertTo-Json
        $release = Invoke-RestMethod -Method Post -Uri ($api + '/releases') -Headers $headers -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($payload))
    }
    foreach ($name in $names) {
        $file = Join-Path $output $name
        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        $existing = $release.assets | Where-Object { $_.name -eq $name } | Select-Object -First 1
        if ($existing) {
            if ($existing.digest -ne ('sha256:' + $hash)) { throw "Draft contains a different asset: $name" }
            continue
        }
        $upload = $release.upload_url.Split('{')[0] + '?name=' + [Uri]::EscapeDataString($name)
        $asset = Invoke-RestMethod -Method Post -Uri $upload -Headers $headers -ContentType 'application/octet-stream' -InFile $file
        if ($asset.size -ne (Get-Item -LiteralPath $file).Length -or ($asset.digest -and $asset.digest -ne ('sha256:' + $hash))) { throw "Uploaded asset verification failed: $name" }
        Write-Output "Uploaded: $name"
    }
    $payload = @{ draft = $false; prerelease = $true; make_latest = 'false'; body = $notes } | ConvertTo-Json
    $published = Invoke-RestMethod -Method Patch -Uri ($api + '/releases/' + $release.id) -Headers $headers -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($payload))
    if ($published.draft -or -not $published.prerelease) { throw 'Release did not enter the requested prerelease state.' }
    Write-Output ('Published prerelease: ' + $published.html_url)
} finally {
    $headers = $null
    $env:GIT_TERMINAL_PROMPT = $oldPrompt; $env:GCM_INTERACTIVE = $oldInteractive
}

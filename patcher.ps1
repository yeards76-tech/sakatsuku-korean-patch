param([string]$GamePath, [ValidateSet('Apply','Verify')][string]$Mode='Apply', [switch]$NonInteractive)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Hash([byte[]]$Bytes) {
  $h=[Security.Cryptography.SHA256]::Create()
  try { return ([BitConverter]::ToString($h.ComputeHash($Bytes))).Replace('-','').ToLowerInvariant() } finally { $h.Dispose() }
}
$archive=$null
$stage=$null
try {
  if (!$GamePath) {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog=New-Object Windows.Forms.FolderBrowserDialog
    $dialog.Description='Select the game folder containing PS3_GAME (or USRDIR/data).'
    if ($dialog.ShowDialog() -ne 'OK') { exit 0 }
    $GamePath=$dialog.SelectedPath
    $dialog.Dispose()
  }
  $root=(Resolve-Path -LiteralPath $GamePath).Path
  $data=$null
  foreach ($rel in @('PS3_GAME\USRDIR\data','USRDIR\data','data','')) {
    $candidate=if ($rel) { Join-Path $root $rel } else { $root }
    if (Test-Path -LiteralPath (Join-Path $candidate 'font.par')) { $data=$candidate; break }
  }
  if (!$data) { throw 'Game data folder not found.' }
  if (Get-Process rpcs3 -ErrorAction SilentlyContinue) { throw 'Close RPCS3 before patching.' }
  $archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $PSScriptRoot 'korean.patch'))
  $reader=New-Object IO.StreamReader($archive.GetEntry('manifest.json').Open())
  try { $manifest=$reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
  if ($manifest.format -ne 'sakatsuku-xor-v1') { throw 'Unsupported patch format.' }
  $stage=Join-Path $env:TEMP ('sakatsuku-stage-'+[guid]::NewGuid().ToString('N'))
  [IO.Directory]::CreateDirectory($stage) | Out-Null
  $pending=@()
  $seen=@{}
  $already=0
  foreach ($item in $manifest.files) {
    $rel=[string]$item.path
    if ($rel -notmatch '^[A-Za-z0-9_./-]+$' -or $rel -match '(^|/)\.\.(/|$)' -or [IO.Path]::IsPathRooted($rel) -or $seen.ContainsKey($rel)) { throw 'Invalid patch path.' }
    $seen[$rel]=$true
    $dest=Join-Path $data $rel
    if (!(Test-Path -LiteralPath $dest -PathType Leaf)) { throw "Missing original: $rel" }
    $bytes=[IO.File]::ReadAllBytes($dest)
    $current=Hash $bytes
    if ($current -eq $item.target_sha256) { $already++; continue }
    if ($current -ne $item.original_sha256 -or $bytes.Length -ne $item.size) { throw "Original version mismatch: $rel. Use an unmodified original game folder." }
    $entry=$archive.GetEntry([string]$item.payload)
    if (!$entry) { throw "Missing payload: $rel" }
    $stream=$entry.Open()
    $memory=New-Object IO.MemoryStream
    try { $stream.CopyTo($memory); $delta=$memory.ToArray() } finally { $stream.Dispose(); $memory.Dispose() }
    if ($delta.Length -ne $bytes.Length) { throw "Invalid payload length: $rel" }
    for ($i=0; $i -lt $bytes.Length; $i++) { $bytes[$i]=$bytes[$i] -bxor $delta[$i] }
    if ((Hash $bytes) -ne $item.target_sha256) { throw "Patched checksum mismatch: $rel" }
    $prepared=Join-Path $stage $rel
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($prepared)) | Out-Null
    [IO.File]::WriteAllBytes($prepared,$bytes)
    $pending+=@{item=$item; dest=$dest; prepared=$prepared}
  }
  Write-Host "Verified: $($pending.Count) files to patch, $already already patched."
  if ($Mode -eq 'Apply' -and $pending.Count -gt 0) {
    $backup=Join-Path (Split-Path $data -Parent) ('KoreanPatch_Backup_'+[guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($backup) | Out-Null
    foreach ($p in $pending) {
      $saved=Join-Path $backup $p.item.path
      [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
      [IO.File]::Copy($p.dest,$saved,$false)
      if ((Hash ([IO.File]::ReadAllBytes($saved))) -ne $p.item.original_sha256) { throw 'Backup verification failed.' }
    }
    $written=@()
    try {
      foreach ($p in $pending) {
        $written+=$p
        [IO.File]::Copy($p.prepared,$p.dest,$true)
        if ((Hash ([IO.File]::ReadAllBytes($p.dest))) -ne $p.item.target_sha256) { throw 'Write verification failed.' }
      }
    } catch {
      foreach ($p in $written) { [IO.File]::Copy((Join-Path $backup $p.item.path),$p.dest,$true) }
      throw
    }
    Write-Host "PATCH COMPLETE. Backup: $backup"
  }
  Write-Host 'SUCCESS'
} catch {
  Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
  exit 1
} finally {
  if ($archive) { $archive.Dispose() }
  if ($stage -and (Test-Path -LiteralPath $stage)) {
    $resolved=[IO.Path]::GetFullPath($stage)
    $tempRoot=[IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\'
    if ($resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -match '^sakatsuku-stage-[a-f0-9]{32}$') { Remove-Item -LiteralPath $resolved -Recurse -Force }
  }
}

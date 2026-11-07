param([string]$GamePath,[ValidateSet('Apply','Verify')][string]$Mode='Apply',[switch]$NonInteractive)
$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot 'Sakatsuku_Korean_Patch.exe'
if(!$GamePath) {
  if($Mode -eq 'Verify'){throw 'Specify -GamePath for Verify.'}
  $p=Start-Process -FilePath $exe -Wait -PassThru
} else {
  $verb=if($Mode -eq 'Verify'){'--verify'}else{'--apply'}
  $p=Start-Process -FilePath $exe -ArgumentList @($verb,('"'+$GamePath+'"')) -Wait -PassThru
}
if($p.ExitCode -ne 0){Write-Error 'Patch failed. Verify game version and close RPCS3.';exit $p.ExitCode}
Write-Host 'SUCCESS (v0.9)'

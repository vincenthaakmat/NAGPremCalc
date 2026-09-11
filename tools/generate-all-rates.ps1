$ErrorActionPreference = 'Stop'

$territories = @('ARU', 'BON', 'BVI', 'DOM', 'EUX', 'GRE', 'MON', 'SAB', 'SLU', 'SMD', 'SVC', 'TCI')
foreach ($territory in $territories) {
  $workbook = Join-Path (Resolve-Path '.\data').Path "$territory-PremiumCalc.xlsx"
  if (-not (Test-Path $workbook)) {
    Write-Host "Skipping $territory; workbook not found: $workbook"
    continue
  }
  & "$PSScriptRoot\generate-territory-rates.ps1" -Code $territory -WorkbookPath $workbook
}

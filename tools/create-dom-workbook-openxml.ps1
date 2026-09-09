$ErrorActionPreference = 'Stop'

$root = (Resolve-Path '.').Path
$outputPath = Join-Path $root 'data\DOM-PremiumCalc.xlsx'
$tempPath = Join-Path $root 'data\.dom-xlsx-temp'

if (Test-Path $outputPath) {
  throw "Workbook already exists: $outputPath"
}
if ((Resolve-Path '.\data').Path -ne (Split-Path $tempPath -Parent)) {
  throw "Refusing to create temporary workbook outside the data folder."
}
if (Test-Path $tempPath) {
  Remove-Item -LiteralPath $tempPath -Recurse -Force
}

function Xml-Escape($value) {
  if ($null -eq $value) { return '' }
  return [System.Security.SecurityElement]::Escape([string]$value)
}

function Col-Name([int]$index) {
  $name = ''
  while ($index -gt 0) {
    $index--
    $name = [char](65 + ($index % 26)) + $name
    $index = [math]::Floor($index / 26)
  }
  return $name
}

function Cell-Xml([int]$rowIndex, [int]$colIndex, $value) {
  $ref = (Col-Name $colIndex) + $rowIndex
  if ($null -eq $value -or [string]$value -eq '') {
    return "<c r=""$ref""/>"
  }
  if ($value -is [int] -or $value -is [double] -or $value -is [decimal]) {
    return "<c r=""$ref""><v>$value</v></c>"
  }
  $text = Xml-Escape $value
  return "<c r=""$ref"" t=""inlineStr""><is><t>$text</t></is></c>"
}

function Sheet-Xml($rows) {
  $rowXml = @()
  for ($r = 0; $r -lt $rows.Count; $r++) {
    $cells = @()
    for ($c = 0; $c -lt $rows[$r].Count; $c++) {
      $cells += Cell-Xml ($r + 1) ($c + 1) $rows[$r][$c]
    }
    $rowXml += "<row r=""$($r + 1)"">$($cells -join '')</row>"
  }
  return @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <sheetData>
    $($rowXml -join "`n    ")
  </sheetData>
</worksheet>
"@
}

$sheets = [ordered]@{
  CompPercentage = @(
    @('Territory', 'Coverage', 'VehUse', 'VehType', 'Percentage', 'Premium', 'Deductible', 'DeductibleUnderAge', 'Notes')
  )
  CompRateUpdates = @(
    @('CountryCode', 'PolType', 'CoverageCode', 'Date_Effective', 'TrxCode', 'Rate', 'Notes')
  )
  Coverage = @(
    @('CoverageCode', 'Territory', 'CoverageName', 'MaxNCD', 'SubNCD', 'AddNCD', 'POLTYP', 'LoadPerc', 'Deduct', 'BonusMalus', 'SubCoverageCode', 'IndemnityValue', 'ReplacementValue', 'Notes'),
    @('C', 'DOM', 'Comprehensive', 60, 0, 0, 'V', 0, 0, 0, '', 0, 0, ''),
    @('T', 'DOM', 'Third Party', 50, 0, 0, 'V', 0, 0, 0, '', 0, 0, ''),
    @('TF', 'DOM', 'Third Party Fire & Theft', 50, 0, 0, 'V', 0, 0, 0, '', 0, 0, '')
  )
  Instructions = @(
    @('Section', 'Notes'),
    @('DOM - Dominica', 'Fill the lookup rows from the DOM rate tables. Keep sheet names and headers unchanged.'),
    @('CompPercentage', 'For T coverage, fill one row per Territory/Coverage/VehUse/VehType with Premium and deductibles. If DOM comprehensive percentages should come from Excel, fill Percentage by vehicle use/type.'),
    @('NVehUse', 'For DOM/SVC-style calculations, Premium, MinSumIns, TreshHold, AddCharge, ToolsCharge and Coverage are used for base-rating style rows.'),
    @('PremVar', 'Fill AALD, LicExp, ForLicense, FleetDisc, StaffDisc, PassLiab, period percentages, ActofGOD, and Windscreen by Vehicle Use + Coverage + Territory.'),
    @('Sys', 'GovTax should be entered as a decimal, for example 0.05 for 5%.')
  )
  LiabilityVar = @(
    @('ID', 'Amount', 'Value', 'Territory', 'Notes')
  )
  MinimumPremium = @(
    @('MinPremID', 'Premium', 'Territory', 'POLTYP', 'CurrCode', 'Description', 'string1', 'string2', 'string3', 'bit1', 'bit2', 'decimal1', 'decimal2', 'Notes'),
    @(1, 0, 'DOM', 'V', 'XCD', 'Vehicle minimum premium', '', '', '', '', '', '', '', '')
  )
  NcdScale = @(
    @('CountryCode', 'CoverageCode', 'NCD', 'NewNCD', '1stClaim', '2ndClaim', 'id', 'Notes')
  )
  NVehUse = @(
    @('id', 'VuseID', 'Vuse', 'LoadPerc', 'IncrNCD', 'PassLiab', 'MaxNCD', 'Territory', 'MinSumIns', 'Premium', 'TreshHold', 'AddCharge', 'MinNCD', 'ToolsCharge', 'Coverage', 'Notes'),
    @(1, 'PR', 'PRIVATE', 0, 0, 0, 0, 'DOM', 0, 0, 0, 0, 0, 0, '', ''),
    @(2, 'CP', 'COMMERCIAL', 0, 0, 0, 0, 'DOM', 0, 0, 0, 0, 0, 0, '', ''),
    @(3, 'RN', 'RENTAL', 0, 0, 0, 0, 'DOM', 0, 0, 0, 0, 0, 0, '', ''),
    @(4, 'BS', 'BUS', 0, 0, 0, 0, 'DOM', 0, 0, 0, 0, 0, 0, '', ''),
    @(5, 'TX', 'TAXI', 0, 0, 0, 0, 'DOM', 0, 0, 0, 0, 0, 0, '', ''),
    @(6, 'HD', 'HEAVY DUTY', 0, 0, 0, 0, 'DOM', 0, 0, 0, 0, 0, 0, '', ''),
    @(7, 'SB', 'SCHOOL BUS', 0, 0, 0, 0, 'DOM', 0, 0, 0, 0, 0, 0, '', ''),
    @(8, 'MB', 'MOTOR BIKE', 0, 0, 0, 0, 'DOM', 0, 0, 0, 0, 0, 0, '', ''),
    @(9, 'TT', 'TOOL OF TRADE', 0, 0, 0, 0, 'DOM', 0, 0, 0, 0, 0, 0, '', '')
  )
  PremVar = @(
    @('ID', 'Charge', 'Deductible', 'HalfYearlyPerc', 'HalfYearlyAdd', 'QuarterlyPerc', 'QuarterlyAdd', 'ThreeNinePerc', 'ThreeNineAdd', 'PassLiab', 'AALD', 'ForLicense', 'FleetDisc', 'StaffDisc', 'AddDriver', 'LicExp', 'RentalPerc', 'RentalValue', 'Coverage', 'Territory', 'UnderAgePerc', 'ActofGOD', 'Windscreen')
  )
  ShortPeriods = @(
    @('CountryCode', 'Label', 'Percent', 'Notes'),
    @('DOM', 'Up to 7 Days', 10, ''),
    @('DOM', 'Up to 1 Month', 15, ''),
    @('DOM', 'Up to 2 Months', 25, ''),
    @('DOM', 'Up to 3 Months', 30, ''),
    @('DOM', 'Up to 4 Months', 40, ''),
    @('DOM', 'Up to 5 Months', 50, ''),
    @('DOM', 'Up to 6 Months', 55, ''),
    @('DOM', 'Up to 7 Months', 65, ''),
    @('DOM', 'Up to 8 Months', 75, ''),
    @('DOM', 'More than 8 Months', 100, '')
  )
  Sys = @(
    @('CountryCode', 'Currency', 'CurrencyName', 'PolicyFee', 'NewVehicleFactor', 'GovTax', 'GovVAT', 'NRSA', 'RSSFee'),
    @('DOM', 'XCD', 'Eastern Caribbean Dollar', 0, 0, 0, 0, 0, 0)
  )
  VehicleTypes = @(
    @('CountryCode', 'VehicleType', 'RateUpYear', 'RateUpPerc', 'Notes'),
    @('DOM', 'Car', 0, 0, ''),
    @('DOM', 'Jeep', 0, 0, ''),
    @('DOM', 'Truck', 0, 0, ''),
    @('DOM', 'MotorCycle', 0, 0, ''),
    @('DOM', 'Bus', 0, 0, ''),
    @('DOM', 'Harley Davidson', 0, 0, ''),
    @('DOM', 'Pickup', 0, 0, ''),
    @('DOM', 'Van', 0, 0, ''),
    @('DOM', 'Backhoe', 0, 0, ''),
    @('DOM', 'Crane', 0, 0, ''),
    @('DOM', 'Tractor', 0, 0, ''),
    @('DOM', 'Scooter', 0, 0, ''),
    @('DOM', 'ATV', 0, 0, '')
  )
}

New-Item -ItemType Directory -Path $tempPath | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tempPath '_rels') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tempPath 'xl') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tempPath 'xl\_rels') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tempPath 'xl\worksheets') | Out-Null

$sheetEntries = @()
$relEntries = @()
$overrides = @()
$index = 1
foreach ($entry in $sheets.GetEnumerator()) {
  $sheetEntries += "<sheet name=""$(Xml-Escape $entry.Key)"" sheetId=""$index"" r:id=""rId$index""/>"
  $relEntries += "<Relationship Id=""rId$index"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet$index.xml""/>"
  $overrides += "<Override PartName=""/xl/worksheets/sheet$index.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>"
  Set-Content -LiteralPath (Join-Path $tempPath "xl\worksheets\sheet$index.xml") -Value (Sheet-Xml $entry.Value) -Encoding UTF8
  $index++
}

Set-Content -LiteralPath (Join-Path $tempPath '[Content_Types].xml') -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  $($overrides -join "`n  ")
</Types>
"@

Set-Content -LiteralPath (Join-Path $tempPath '_rels\.rels') -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>
"@

Set-Content -LiteralPath (Join-Path $tempPath 'xl\workbook.xml') -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    $($sheetEntries -join "`n    ")
  </sheets>
</workbook>
"@

Set-Content -LiteralPath (Join-Path $tempPath 'xl\_rels\workbook.xml.rels') -Value @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  $($relEntries -join "`n  ")
</Relationships>
"@

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($tempPath, $outputPath)
Remove-Item -LiteralPath $tempPath -Recurse -Force

Write-Host "Created $outputPath"

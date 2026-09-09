$ErrorActionPreference = 'Stop'

$outputPath = Join-Path (Resolve-Path '.').Path 'data\DOM-PremiumCalc.xlsx'

if (Test-Path $outputPath) {
  throw "Workbook already exists: $outputPath"
}

$sheets = [ordered]@{
  CompPercentage = @(
    'Territory', 'Coverage', 'VehUse', 'VehType', 'Percentage', 'Premium',
    'Deductible', 'DeductibleUnderAge', 'Notes'
  )
  CompRateUpdates = @(
    'CountryCode', 'PolType', 'CoverageCode', 'Date_Effective', 'TrxCode', 'Rate', 'Notes'
  )
  Coverage = @(
    'CoverageCode', 'Territory', 'CoverageName', 'MaxNCD', 'SubNCD', 'AddNCD',
    'POLTYP', 'LoadPerc', 'Deduct', 'BonusMalus', 'SubCoverageCode',
    'IndemnityValue', 'ReplacementValue', 'Notes'
  )
  Instructions = @('Section', 'Notes')
  LiabilityVar = @('ID', 'Amount', 'Value', 'Territory', 'Notes')
  MinimumPremium = @(
    'MinPremID', 'Premium', 'Territory', 'POLTYP', 'CurrCode', 'Description',
    'string1', 'string2', 'string3', 'bit1', 'bit2', 'decimal1', 'decimal2', 'Notes'
  )
  NcdScale = @('CountryCode', 'CoverageCode', 'NCD', 'NewNCD', '1stClaim', '2ndClaim', 'id', 'Notes')
  NVehUse = @(
    'id', 'VuseID', 'Vuse', 'LoadPerc', 'IncrNCD', 'PassLiab', 'MaxNCD',
    'Territory', 'MinSumIns', 'Premium', 'TreshHold', 'AddCharge',
    'MinNCD', 'ToolsCharge', 'Coverage', 'Notes'
  )
  PremVar = @(
    'ID', 'Charge', 'Deductible', 'HalfYearlyPerc', 'HalfYearlyAdd',
    'QuarterlyPerc', 'QuarterlyAdd', 'ThreeNinePerc', 'ThreeNineAdd',
    'PassLiab', 'AALD', 'ForLicense', 'FleetDisc', 'StaffDisc',
    'AddDriver', 'LicExp', 'RentalPerc', 'RentalValue', 'Coverage',
    'Territory', 'UnderAgePerc', 'ActofGOD', 'Windscreen'
  )
  ShortPeriods = @('CountryCode', 'Label', 'Percent', 'Notes')
  Sys = @('CountryCode', 'Currency', 'CurrencyName', 'PolicyFee', 'NewVehicleFactor', 'GovTax', 'GovVAT', 'NRSA', 'RSSFee')
  VehicleTypes = @('CountryCode', 'VehicleType', 'RateUpYear', 'RateUpPerc', 'Notes')
}

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
  $workbook = $excel.Workbooks.Add()
  while ($workbook.Worksheets.Count -gt 1) {
    $workbook.Worksheets.Item($workbook.Worksheets.Count).Delete()
  }

  $sheetIndex = 1
  foreach ($entry in $sheets.GetEnumerator()) {
    if ($sheetIndex -eq 1) {
      $sheet = $workbook.Worksheets.Item(1)
    } else {
      $sheet = $workbook.Worksheets.Add([System.Type]::Missing, $workbook.Worksheets.Item($workbook.Worksheets.Count))
    }
    $sheet.Name = $entry.Key
    Write-Host "Added sheet $($entry.Key)"

    for ($c = 0; $c -lt $entry.Value.Count; $c++) {
      $cell = $sheet.Cells.Item(1, $c + 1)
      $cell.Value2 = $entry.Value[$c]
    }
    $sheetIndex++
  }

  function Set-Row($sheetName, $row, [object[]]$values) {
    $sheet = $workbook.Worksheets.Item($sheetName)
    for ($c = 0; $c -lt $values.Count; $c++) {
      $sheet.Cells.Item($row, $c + 1).Value2 = $values[$c]
    }
  }

  Set-Row 'Instructions' 2 @('DOM - Dominica', 'Fill the lookup rows from the DOM rate tables. Keep sheet names and headers unchanged.')
  Set-Row 'Instructions' 3 @('CompPercentage', 'For T coverage, fill one row per Territory/Coverage/VehUse/VehType with Premium and deductibles. If DOM comprehensive percentages should come from Excel, fill Percentage by vehicle use/type.')
  Set-Row 'Instructions' 4 @('NVehUse', 'For DOM/SVC-style calculations, Premium, MinSumIns, TreshHold, AddCharge, ToolsCharge and Coverage are used for base-rating style rows.')
  Set-Row 'Instructions' 5 @('PremVar', 'Fill AALD, LicExp, ForLicense, FleetDisc, StaffDisc, PassLiab, period percentages, ActofGOD, and Windscreen by Vehicle Use + Coverage + Territory.')
  Set-Row 'Instructions' 6 @('Sys', 'GovTax should be entered as a decimal, for example 0.05 for 5%.')

  Set-Row 'Sys' 2 @('DOM', 'XCD', 'Eastern Caribbean Dollar', 0, 0, 0, 0, 0, 0)

  Set-Row 'Coverage' 2 @('C', 'DOM', 'Comprehensive', 60, 0, 0, 'V', 0, 0, 0, '', 0, 0, '')
  Set-Row 'Coverage' 3 @('T', 'DOM', 'Third Party', 50, 0, 0, 'V', 0, 0, 0, '', 0, 0, '')
  Set-Row 'Coverage' 4 @('TF', 'DOM', 'Third Party Fire & Theft', 50, 0, 0, 'V', 0, 0, 0, '', 0, 0, '')

  $vehicleUses = @(
    @('PR', 'PRIVATE'),
    @('CP', 'COMMERCIAL'),
    @('RN', 'RENTAL'),
    @('BS', 'BUS'),
    @('TX', 'TAXI'),
    @('HD', 'HEAVY DUTY'),
    @('SB', 'SCHOOL BUS'),
    @('MB', 'MOTOR BIKE'),
    @('TT', 'TOOL OF TRADE')
  )
  $row = 2
  $id = 1
  foreach ($use in $vehicleUses) {
    Set-Row 'NVehUse' $row @($id, $use[0], $use[1], 0, 0, 0, 0, 'DOM', 0, 0, 0, 0, 0, 0, '', '')
    $row++
    $id++
  }

  $vehicleTypes = @('Car', 'Jeep', 'Truck', 'MotorCycle', 'Bus', 'Harley Davidson', 'Pickup', 'Van', 'Backhoe', 'Crane', 'Tractor', 'Scooter', 'ATV')
  $row = 2
  foreach ($type in $vehicleTypes) {
    Set-Row 'VehicleTypes' $row @('DOM', $type, 0, 0, '')
    $row++
  }

  $shortPeriods = @(
    @('Up to 7 Days', 10),
    @('Up to 1 Month', 15),
    @('Up to 2 Months', 25),
    @('Up to 3 Months', 30),
    @('Up to 4 Months', 40),
    @('Up to 5 Months', 50),
    @('Up to 6 Months', 55),
    @('Up to 7 Months', 65),
    @('Up to 8 Months', 75),
    @('More than 8 Months', 100)
  )
  $row = 2
  foreach ($period in $shortPeriods) {
    Set-Row 'ShortPeriods' $row @('DOM', $period[0], $period[1], '')
    $row++
  }

  Set-Row 'MinimumPremium' 2 @(1, 0, 'DOM', 'V', 'XCD', 'Vehicle minimum premium', '', '', '', '', '', '', '', '')

  foreach ($sheet in $workbook.Worksheets) {
  }

  Write-Host "Saving $outputPath"
  $workbook.SaveAs($outputPath)
  Write-Host "Created $outputPath"
}
finally {
  if ($workbook) { $workbook.Close($true) | Out-Null }
  $excel.Quit() | Out-Null
  [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}

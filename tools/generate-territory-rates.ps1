param(
  [Parameter(Mandatory=$true)][string]$Code,
  [string]$WorkbookPath,
  [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$Code = $Code.ToUpperInvariant()
$root = (Resolve-Path '.').Path
if (-not $WorkbookPath) { $WorkbookPath = Join-Path $root "data\$Code-PremiumCalc.xlsx" }
if (-not $OutputPath) { $OutputPath = Join-Path $root ("data\" + $Code.ToLowerInvariant() + "-rates.js") }

$engineMap = @{
  ARU = $null
  BON = $null
  BVI = 'BVI'
  DOM = 'DOM'
  EUX = 'SEM_EUX'
  GRE = 'GRE'
  MON = 'SEM_MON'
  SAB = 'SEM_SAB'
  SLU = 'SLU'
  SMD = 'SMD'
}

function Normalize-Key($value) {
  if ($null -eq $value) { return '' }
  return ([string]$value).Trim().ToLowerInvariant() -replace '[^a-z0-9]', ''
}

function To-Number($value) {
  if ($null -eq $value -or $value -eq '') { return 0 }
  $text = ([string]$value).Trim()
  if ($text -eq '' -or $text -eq 'NULL') { return 0 }
  $num = 0.0
  if ([double]::TryParse($text, [Globalization.NumberStyles]::Any, [Globalization.CultureInfo]::InvariantCulture, [ref]$num)) { return $num }
  if ([double]::TryParse($text, [Globalization.NumberStyles]::Any, [Globalization.CultureInfo]::CurrentCulture, [ref]$num)) { return $num }
  return 0
}

function To-Text($value) {
  if ($null -eq $value) { return '' }
  $text = ([string]$value).Trim()
  if ($text -eq 'NULL') { return '' }
  return $text
}

function Has-Sheet($workbook, $sheetName) {
  foreach ($sheet in $workbook.Worksheets) {
    if ($sheet.Name -eq $sheetName) { return $true }
  }
  return $false
}

function Find-HeaderRow($used) {
  $known = @(
    'countrycode','territory','coveragecode','vuseid','tableid','rangeid','tprateid',
    'minpremid','vehicletype','vtype','ratetype','vehuse','amount','id','vehrate'
  )
  $maxRows = [Math]::Min($used.Rows.Count, 10)
  $bestRow = 1
  $bestScore = -1
  for ($r = 1; $r -le $maxRows; $r++) {
    $score = 0
    for ($c = 1; $c -le $used.Columns.Count; $c++) {
      $key = Normalize-Key $used.Cells.Item($r, $c).Text
      if ($known -contains $key) { $score++ }
    }
    if ($score -gt $bestScore) {
      $bestScore = $score
      $bestRow = $r
    }
  }
  return $bestRow
}

function Get-SheetRows($workbook, $sheetName) {
  if (-not (Has-Sheet $workbook $sheetName)) { return @() }
  $sheet = $workbook.Worksheets.Item($sheetName)
  $used = $sheet.UsedRange
  $rowCount = $used.Rows.Count
  $colCount = $used.Columns.Count
  $headerRow = Find-HeaderRow $used
  if ($rowCount -le $headerRow) { return @() }

  $headers = @{}
  for ($c = 1; $c -le $colCount; $c++) {
    $key = Normalize-Key $used.Cells.Item($headerRow, $c).Text
    if ($key -and -not $headers.ContainsKey($key)) { $headers[$key] = $c }
  }

  $rows = @()
  for ($r = $headerRow + 1; $r -le $rowCount; $r++) {
    $obj = [ordered]@{}
    $hasData = $false
    foreach ($key in $headers.Keys) {
      $value = $used.Cells.Item($r, $headers[$key]).Text
      if ((To-Text $value) -ne '') { $hasData = $true }
      $obj[$key] = $value
    }
    if ($hasData) { $rows += [pscustomobject]$obj }
  }
  return $rows
}

function Get-Prop($row, [string[]]$names) {
  foreach ($name in $names) {
    $key = Normalize-Key $name
    if ($row.PSObject.Properties.Name -contains $key) { return $row.$key }
  }
  return $null
}

function Territory-Matches($row, [switch]$AllowGlobal) {
  $territory = To-Text (Get-Prop $row @('CountryCode','Territory','Country'))
  if (-not $territory) { return [bool]$AllowGlobal }
  $escaped = [Regex]::Escape($Code)
  return $territory -match "(^|[^A-Z0-9])$escaped([^A-Z0-9]|$)"
}

function Add-CommonTables($workbook, $data) {
  $coverage = [ordered]@{}
  foreach ($row in (Get-SheetRows $workbook 'Coverage')) {
    if (-not (Territory-Matches $row)) { continue }
    $polType = To-Text (Get-Prop $row @('POLTYP','PolType'))
    if ($polType -and $polType -ne 'V') { continue }
    $codeValue = To-Text (Get-Prop $row @('CoverageCode','ID','Coverage'))
    if (-not $codeValue) { continue }
    if (@('BVI','SMD') -contains $Code -and @('C','T','TF') -notcontains $codeValue) { continue }
    $coverage[$codeValue] = [ordered]@{
      name = To-Text (Get-Prop $row @('CoverageName','Description','Descr','Name'))
      replacementvalue = To-Number (Get-Prop $row @('ReplacementValue','Replacement'))
      loadperc = To-Number (Get-Prop $row @('LoadPerc','LoadPercentage'))
      bonusmalus = To-Number (Get-Prop $row @('BonusMalus'))
      maxncd = To-Number (Get-Prop $row @('MaxNCD','MaxNoClaimDiscount'))
    }
  }
  $data.coverage = $coverage

  $nVehUse = [ordered]@{}
  foreach ($row in (Get-SheetRows $workbook 'NVehUse')) {
    if (-not (Territory-Matches $row)) { continue }
    $id = To-Text (Get-Prop $row @('VuseID','ID','Code'))
    if (-not $id -or $id -match '^\d+$') { continue }
    $nVehUse[$id] = [ordered]@{
      label = To-Text (Get-Prop $row @('Vuse','Description','Descr','Name'))
      loadperc = To-Number (Get-Prop $row @('LoadPerc'))
      maxncd = To-Number (Get-Prop $row @('MaxNCD'))
      passliab = To-Number (Get-Prop $row @('PassLiab','PassLiabAmt','PassLiability'))
    }
  }
  $data.nVehUse = $nVehUse

  $premVarTable = [ordered]@{}
  foreach ($row in (Get-SheetRows $workbook 'PremVar')) {
    if (-not (Territory-Matches $row -AllowGlobal)) { continue }
    $id = To-Text (Get-Prop $row @('ID','VehUse','VehicleUse'))
    $cov = To-Text (Get-Prop $row @('Coverage','CoverageCode'))
    if (-not $id -or -not $cov) { continue }
    $premVarTable["$($id)_$($cov)"] = [ordered]@{
      charge = To-Number (Get-Prop $row @('Charge'))
      passliab = To-Number (Get-Prop $row @('PassLiab','Passliab'))
      halfyearly = To-Number (Get-Prop $row @('HalfYearlyPerc','HalfYearly','Halfyearlyperc','Halfyearly'))
      staffdisc = To-Number (Get-Prop $row @('StaffDisc'))
      fleetdisc = To-Number (Get-Prop $row @('FleetDisc'))
      rentalperc = To-Number (Get-Prop $row @('RentalPerc'))
      quarterly = To-Number (Get-Prop $row @('QuarterlyPerc','Quarterly','Quarterlyperc'))
      forlicense = To-Number (Get-Prop $row @('ForLicense','Forlicense'))
      actofgod = To-Number (Get-Prop $row @('ActofGOD','AOG','ActOfGod','Actofgod'))
      windscreen = To-Number (Get-Prop $row @('WindscreenAmt','Windscreen'))
      licexp = To-Number (Get-Prop $row @('LicExp','LicenseExp'))
      aald = To-Number (Get-Prop $row @('AALD','Aald'))
      agentStaffdisc = To-Number (Get-Prop $row @('AgentStaffDisc'))
      rssFee = To-Number (Get-Prop $row @('RSSFee','RssFee'))
      underageperc = To-Number (Get-Prop $row @('UnderAgePerc','Underageperc'))
    }
  }
  $data.premVarTable = $premVarTable

  if ($nVehUse.Count -eq 0 -and $premVarTable.Count -gt 0) {
    $labels = @{ PR = 'PRIVATE'; CP = 'COMMERCIAL'; RN = 'RENTAL'; MB = 'MOTOR BIKE'; MC = 'MOTOR CYCLE'; HD = 'HEAVY DUTY'; BS = 'BUS'; TX = 'TAXI'; HB = 'HARLEY DAVIDSON'; SB = 'SCHOOL BUS'; GEN = 'GENERAL'; GS = 'GENERAL SPECIAL'; GL = 'GENERAL LIABILITY'; TT = 'TOOL OF TRADE'; PO = 'PERSONAL OCCUPANTS' }
    $ids = @($premVarTable.Keys | ForEach-Object { ($_ -split '_')[0] } | Where-Object { $_ -and $_ -ne 'GEN' } | Select-Object -Unique)
    foreach ($id in $ids) {
      $nVehUse[$id] = [ordered]@{ label = ($labels[$id] ?? $id); loadperc = 0; maxncd = 0; passliab = 0 }
    }
  }

  $liabilityVar = @()
  foreach ($row in (Get-SheetRows $workbook 'LiabilityVar')) {
    if (-not (Territory-Matches $row)) { continue }
    $amount = To-Text (Get-Prop $row @('Amount','LiabilityVar','Liability','Description'))
    $value = To-Number (Get-Prop $row @('Value','AmountValue','Premium'))
    $liabilityVar += [ordered]@{ label = $amount; amount = $amount; value = $value; flatAmount = $value }
  }
  $data.liabilityVar = $liabilityVar

  $ncdScale = [ordered]@{}
  foreach ($row in (Get-SheetRows $workbook 'NcdScale')) {
    if (-not (Territory-Matches $row)) { continue }
    $cov = To-Text (Get-Prop $row @('CoverageCode','Coverage'))
    $ncd = To-Number (Get-Prop $row @('NCD','NoClaimDiscount'))
    if (-not $cov) { continue }
    if (-not $ncdScale.Contains($cov)) { $ncdScale[$cov] = @() }
    $ncdScale[$cov] = @($ncdScale[$cov]) + $ncd
  }
  $data.ncdScale = $ncdScale

  $vehicleTypes = @()
  foreach ($row in (Get-SheetRows $workbook 'VehicleTypes')) {
    if (-not (Territory-Matches $row)) { continue }
    $type = To-Text (Get-Prop $row @('VehicleType','Vtype','Type','Description'))
    if (-not $type) { continue }
    $vehicleTypes += [ordered]@{
      type = $type
      rateUpYear = To-Number (Get-Prop $row @('RateUpYear'))
      rateUpPerc = To-Number (Get-Prop $row @('RateUpPerc'))
    }
  }
  $data.vehicleTypes = $vehicleTypes

  $minimumPremium = @()
  foreach ($row in (Get-SheetRows $workbook 'MinimumPremium')) {
    if (-not (Territory-Matches $row)) { continue }
    $premium = To-Number (Get-Prop $row @('MinimumPremium','Premium','MinPremium','Value'))
    $minimumPremium += [ordered]@{
      premium = $premium
      coverage = To-Text (Get-Prop $row @('Coverage','CoverageCode','String1'))
      vehicleUses = To-Text (Get-Prop $row @('VehicleUses','String2'))
      polType = To-Text (Get-Prop $row @('POLTYP','PolType'))
    }
  }
  $data.minimumPremium = $minimumPremium
  $vehicleMinimumPremium = $minimumPremium | Where-Object { $_.polType -eq 'V' -or $_.polType -match '(^|,\s*)V($|,)' } | Select-Object -First 1
  if ($vehicleMinimumPremium) { $data.minPremium = $vehicleMinimumPremium.premium }
  elseif ($minimumPremium.Count -gt 0) { $data.minPremium = ($minimumPremium | Select-Object -First 1).premium }
  else { $data.minPremium = 0 }

  $shortPeriods = @()
  foreach ($row in (Get-SheetRows $workbook 'ShortPeriods')) {
    if (-not (Territory-Matches $row -AllowGlobal)) { continue }
    $label = To-Text (Get-Prop $row @('Label','Description','Period'))
    $value = To-Number (Get-Prop $row @('Percent','Perc','Percentage','Value','Rate'))
    if ($label -or $value) { $shortPeriods += [ordered]@{ label = $label; value = $value } }
  }
  $data.shortPeriods = $shortPeriods

  $compRateUpdates = @()
  foreach ($row in (Get-SheetRows $workbook 'CompRateUpdates')) {
    if (-not (Territory-Matches $row)) { continue }
    $compRateUpdates += [ordered]@{
      polType = To-Text (Get-Prop $row @('PolType','POLTYP'))
      coverage = To-Text (Get-Prop $row @('CoverageCode','Coverage'))
      trxCode = To-Text (Get-Prop $row @('TrxCode'))
      rate = To-Number (Get-Prop $row @('Rate','RatePercent'))
      effectiveDate = To-Text (Get-Prop $row @('Date_Effective','EffectiveFrom'))
    }
  }
  $data.compRateUpdates = $compRateUpdates

  $sys = [ordered]@{ policyfee = 0; newvehdisc = 0; govTax = 0; govVAT = 0; nrsa = 0; rssFee = 0 }
  foreach ($row in (Get-SheetRows $workbook 'Sys')) {
    if (-not (Territory-Matches $row)) { continue }
    $sys.policyfee = To-Number (Get-Prop $row @('PolicyFee','Policyfee'))
    $sys.newvehdisc = To-Number (Get-Prop $row @('NewVehicleFactor','NewVehDisc','NewVehicleDiscount'))
    $sys.govTax = To-Number (Get-Prop $row @('GovTax','Tax','GovernmentTax'))
    $sys.govVAT = To-Number (Get-Prop $row @('GovVAT','Vat','GovernmentVAT'))
    $sys.nrsa = To-Number (Get-Prop $row @('NRSA'))
    $sys.rssFee = To-Number (Get-Prop $row @('RSSFee','RssFee'))
    break
  }
  $data.sys = $sys
}

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false

try {
  $workbook = $excel.Workbooks.Open((Resolve-Path $WorkbookPath).Path)
  $data = [ordered]@{}
  if ($engineMap.ContainsKey($Code) -and $engineMap[$Code]) { $data.engine = $engineMap[$Code] }

  if (Has-Sheet $workbook 'AbcTpRate') {
    $abcTpRate = @()
    foreach ($row in (Get-SheetRows $workbook 'AbcTpRate')) {
      if (-not (Territory-Matches $row)) { continue }
      $abcTpRate += [ordered]@{
        from = To-Number (Get-Prop $row @('FRSUMINS','FromSumIns'))
        to = To-Number (Get-Prop $row @('TOSUMINS','ToSumIns'))
        premium = To-Number (Get-Prop $row @('Premium'))
        vehCat = To-Text (Get-Prop $row @('VehCat'))
        currCode = To-Text (Get-Prop $row @('CurrCode'))
      }
    }
    $data.abcTpRate = $abcTpRate
  }

  if (Has-Sheet $workbook 'ComprehensiveRate') {
    $rows = @()
    foreach ($row in (Get-SheetRows $workbook 'ComprehensiveRate')) {
      if (-not (Territory-Matches $row)) { continue }
      $rows += [ordered]@{
        rangeFrom = To-Number (Get-Prop $row @('RangeFrom'))
        rangeTo = To-Number (Get-Prop $row @('RangeTo'))
        premiumAmount = To-Number (Get-Prop $row @('PremiumAmount'))
        objectCategory = To-Text (Get-Prop $row @('ObjectCategory'))
      }
    }
    $data.comprehensiveRate = $rows
  }

  if (Has-Sheet $workbook 'ComprehensiveRateStLucia') {
    $rows = @()
    foreach ($row in (Get-SheetRows $workbook 'ComprehensiveRateStLucia')) {
      $rows += [ordered]@{
        engineSize = To-Text (Get-Prop $row @('EngineSize'))
        year = To-Number (Get-Prop $row @('Year'))
        revBase = To-Number (Get-Prop $row @('RevBase'))
        plusCat = To-Number (Get-Prop $row @('PlusCat'))
        vehUse = To-Text (Get-Prop $row @('VehUse'))
      }
    }
    $data.comprehensiveRateStLucia = $rows
  }

  if (Has-Sheet $workbook 'ThirdPartyRates') {
    $rows = @()
    foreach ($row in (Get-SheetRows $workbook 'ThirdPartyRates')) {
      if (-not (Territory-Matches $row)) { continue }
      $rows += [ordered]@{
        amount = To-Number (Get-Prop $row @('Amount','Premium'))
        ratesCategorie = To-Text (Get-Prop $row @('RatesCategorie','VehUse'))
        objectCategorie = To-Text (Get-Prop $row @('ObjectCategorie','EngineSize','VehType'))
        age = To-Number (Get-Prop $row @('Age'))
      }
    }
    $data.thirdPartyRates = $rows
  }

  if (Has-Sheet $workbook 'ThirdPRateMONEUX') {
    $rows = @()
    foreach ($row in (Get-SheetRows $workbook 'ThirdPRateMONEUX')) {
      if (-not (Territory-Matches $row)) { continue }
      $rows += [ordered]@{
        amount = To-Number (Get-Prop $row @('Amount','Premium'))
        ratesCategorie = To-Text (Get-Prop $row @('VehUse','RatesCategorie','ID'))
        objectCategorie = To-Text (Get-Prop $row @('EngineSize','VehType','ObjectCategorie'))
        age = To-Number (Get-Prop $row @('Age'))
        compPerc = To-Number (Get-Prop $row @('CompPerc'))
      }
    }
    $data.thirdPartyRates = $rows
  }

  if (Has-Sheet $workbook 'ThirdPRateSABA') {
    $rows = @()
    foreach ($row in (Get-SheetRows $workbook 'ThirdPRateSABA')) {
      if (-not (Territory-Matches $row)) { continue }
      $rows += [ordered]@{
        amount = To-Number (Get-Prop $row @('Amount','Premium'))
        vehUse = To-Text (Get-Prop $row @('VehUse','RatesCategorie','ID'))
        vehType = To-Text (Get-Prop $row @('VehType','ObjectCategorie'))
        age = To-Number (Get-Prop $row @('Age'))
        loadPerc = To-Number (Get-Prop $row @('LoadPerc'))
      }
    }
    $data.thirdPartySaba = $rows
  }

  if (Has-Sheet $workbook 'CompRateSABEUX') {
    $rows = @()
    foreach ($row in (Get-SheetRows $workbook 'CompRateSABEUX')) {
      if (-not (Territory-Matches $row)) { continue }
      $rows += [ordered]@{
        countryCode = To-Text (Get-Prop $row @('CountryCode','Territory'))
        objectCategory = To-Text (Get-Prop $row @('ObjectCategory'))
        rangeFrom = To-Number (Get-Prop $row @('RangeFrom'))
        rangeTo = To-Number (Get-Prop $row @('RangeTo'))
        premiumAmount = To-Number (Get-Prop $row @('PremiumAmount'))
      }
    }
    $data.compRateSabEux = $rows
  }

  if (Has-Sheet $workbook 'CompRateMON') {
    $rows = @()
    foreach ($row in (Get-SheetRows $workbook 'CompRateMON')) {
      $rows += [ordered]@{
        rateType = To-Text (Get-Prop $row @('RateType'))
        compUnder30Perc = To-Number (Get-Prop $row @('CompUnder30Perc'))
        compOver30Perc = To-Number (Get-Prop $row @('CompOver30Perc'))
        passLiabPrice = To-Number (Get-Prop $row @('PassLiabPrice'))
        licLess6MPerc = To-Number (Get-Prop $row @('LicLess6MPerc'))
        licLess2YPerc = To-Number (Get-Prop $row @('LicLess2YPerc'))
        aaldOver30Perc = To-Number (Get-Prop $row @('AALDOver30Perc'))
        aaldUnder30Perc = To-Number (Get-Prop $row @('AALDUnder30Perc'))
        forLicPerc = To-Number (Get-Prop $row @('ForLicPerc'))
      }
    }
    $data.compRateMon = $rows
  }


  if (Has-Sheet $workbook 'CompPercentage') {
    $rows = @()
    foreach ($row in (Get-SheetRows $workbook 'CompPercentage')) {
      if (-not (Territory-Matches $row)) { continue }
      $rows += [ordered]@{
        territory = To-Text (Get-Prop $row @('Territory','CountryCode'))
        coverage = To-Text (Get-Prop $row @('Coverage','CoverageCode'))
        vehUse = To-Text (Get-Prop $row @('VehUse','ID','VuseID'))
        vehType = To-Text (Get-Prop $row @('VehType','VehicleType','Vtype'))
        engineSize = To-Text (Get-Prop $row @('EngineSize','Enginesize'))
        vehicleAge = To-Text (Get-Prop $row @('VehicleAge','Vehicleage','Age'))
        percentage = To-Number (Get-Prop $row @('Percentage','CompPerc','CompPercDOM'))
        premium = To-Number (Get-Prop $row @('Premium'))
        deductible = To-Number (Get-Prop $row @('Deductible'))
        deductibleUnderAge = To-Number (Get-Prop $row @('DeductibleUnderAge','Deductibleunderage'))
      }
    }
    $data.compPercentage = $rows
  }
  if (Has-Sheet $workbook 'CompRate') {
    $rows = @()
    foreach ($row in (Get-SheetRows $workbook 'CompRate')) {
      if (-not (Territory-Matches $row)) { continue }
      $rows += [ordered]@{
        id = To-Text (Get-Prop $row @('ID'))
        objectCategory = To-Text (Get-Prop $row @('ID','ObjectCategory','RateCategory'))
        rangeFrom = To-Number (Get-Prop $row @('FRSUMINS','FromSumIns','RangeFrom'))
        rangeTo = To-Number (Get-Prop $row @('TOSUMINS','ToSumIns','RangeTo'))
        sumins = To-Number (Get-Prop $row @('SUMINS','SumIns'))
        over30 = To-Number (Get-Prop $row @('Over30'))
        under30 = To-Number (Get-Prop $row @('Under30'))
        currCode = To-Text (Get-Prop $row @('CurrCode'))
        territory = To-Text (Get-Prop $row @('Territory','CountryCode'))
      }
    }
    $data.compRate = $rows
  }

  if (Has-Sheet $workbook 'ThirdPRate') {
    $rows = @()
    foreach ($row in (Get-SheetRows $workbook 'ThirdPRate')) {
      if (-not (Territory-Matches $row)) { continue }
      $rows += [ordered]@{
        id = To-Text (Get-Prop $row @('ID'))
        over30under1600cc = To-Number (Get-Prop $row @('Over30Under1600CC'))
        under30under1600cc = To-Number (Get-Prop $row @('Under30Under1600CC'))
        over30over1600cc = To-Number (Get-Prop $row @('Over30Over1600CC'))
        under30over1600cc = To-Number (Get-Prop $row @('Under30Over1600CC'))
        mcOver50cc = To-Number (Get-Prop $row @('MCOver50CC'))
        mcUnder50cc = To-Number (Get-Prop $row @('MCUnder50CC'))
        currCode = To-Text (Get-Prop $row @('CurrCode'))
        territory = To-Text (Get-Prop $row @('Territory','CountryCode'))
      }
    }
    $data.thirdPartyRates = $rows
  }

  if (Has-Sheet $workbook 'VehicleRateCriteria') {
    $vehicleRateCriteria = [ordered]@{}
    foreach ($row in (Get-SheetRows $workbook 'VehicleRateCriteria')) {
      $id = To-Text (Get-Prop $row @('VehRate','ID'))
      if (-not $id) { continue }
      $vehicleRateCriteria[$id] = [ordered]@{
        amountPer1000 = To-Number (Get-Prop $row @('AmountPer1000'))
        aaldPerc = To-Number (Get-Prop $row @('AALDPerc'))
        maxVehAge = To-Number (Get-Prop $row @('MaxVehAge'))
        incrLiabPerc = To-Number (Get-Prop $row @('IncrLiabPerc'))
        minComprPrem = To-Number (Get-Prop $row @('MinComprPrem'))
        passLiabAmt = To-Number (Get-Prop $row @('PassLiabAmt'))
        passLiabSeat = To-Number (Get-Prop $row @('PassLiabSeat'))
        aog = To-Number (Get-Prop $row @('AOG'))
        windscreenAmt = To-Number (Get-Prop $row @('WindscreenAmt'))
        maxNCD = To-Number (Get-Prop $row @('MaxNCD'))
        maleUnder22DeductAmt = To-Number (Get-Prop $row @('MaleUnder22DeductAmt'))
        maleUnder22IncrPerc = To-Number (Get-Prop $row @('MaleUnder22IncrPerc'))
        maleUnder25DeductAmt = To-Number (Get-Prop $row @('MaleUnder25DeductAmt'))
        maleUnder25IncrPerc = To-Number (Get-Prop $row @('MaleUnder25IncrPerc'))
        femaleUnder22DeductAmt = To-Number (Get-Prop $row @('FemaleUnder22DeductAmt'))
        femaleUnder22IncrPerc = To-Number (Get-Prop $row @('FemaleUnder22IncrPerc'))
        femaleUnder25DeductAmt = To-Number (Get-Prop $row @('FemaleUnder25DeductAmt'))
        femaleUnder25IncrPerc = To-Number (Get-Prop $row @('FemaleUnder25IncrPerc'))
        inexpUnder25DeductAmt = To-Number (Get-Prop $row @('InexpUnder25DeductAmt'))
        inexpUnder25IncrPerc = To-Number (Get-Prop $row @('InexpUnder25IncrPerc'))
      }
    }
    $data.vehicleRateCriteria = $vehicleRateCriteria
  }

  Add-CommonTables $workbook $data

  $json = $data | ConvertTo-Json -Depth 30 -Compress
  $varName = "window.$($Code)_RATES"
  Set-Content -Path $OutputPath -Value "$varName = $json;" -Encoding UTF8

  Write-Host "Wrote $OutputPath"
  Write-Host "Coverage: $($data.coverage.Count), Vehicle uses: $($data.nVehUse.Count), PremVar: $($data.premVarTable.Count), Types: $($data.vehicleTypes.Count)"
}
finally {
  if ($workbook) { $workbook.Close($false) | Out-Null }
  $excel.Quit() | Out-Null
  [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}



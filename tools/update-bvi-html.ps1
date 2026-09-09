$ErrorActionPreference = 'Stop'

$path = '.\index.html'
$text = Get-Content -Path $path -Raw

$text = $text.Replace('  .flag-sab::after { content: none; }', @'
  .flag-sab::after { content: none; }
  .flag-bvi {
    background-image: url("data/BVI_Flag.svg");
    background-position: center;
    background-size: cover;
    background-repeat: no-repeat;
    background-color: #012169;
  }
  .flag-bvi::after { content: none; }
'@)

$text = $text.Replace('      <option value="BON">BON · Bonaire</option>', @'
      <option value="BON">BON · Bonaire</option>
      <option value="BVI">BVI · Tortola</option>
'@)

$text = $text.Replace('<script src="data/bon-rates.js"></script>', @'
<script src="data/bon-rates.js"></script>
<script src="data/bvi-rates.js"></script>
'@)

$text = $text.Replace('function getSabEuxCompRate(vehUse, vehicleValue) {', @'
function getBviCompRate(vehUse, vehicleValue) {
  const rows = RATES.compRate || [];
  const lookupValue = Math.min(Number(vehicleValue) || 0, 10000);
  const matchesUse = rows.filter(r => String(r.objectCategory || r.id) === String(vehUse));
  return matchesUse.find(r => Number(r.rangeFrom || 0) <= lookupValue && Number(r.rangeTo || 0) >= lookupValue && Number(r.over30 || 0) > 0)
      || matchesUse.find(r => Number(r.rangeTo || 0) >= lookupValue && Number(r.over30 || 0) > 0)
      || matchesUse.find(r => Number(r.over30 || 0) > 0)
      || null;
}

function getBviThirdPartyRate(vehUse) {
  const rows = RATES.thirdPartyRates || [];
  return rows.find(r => String(r.id) === String(vehUse)) || null;
}

function getSabEuxCompRate(vehUse, vehicleValue) {
'@)

$text = $text.Replace("  } else if (currentTerritory.code === 'SAB') {", @"
  } else if (currentTerritory.code === 'BVI') {
    if (coverage === 'T') {
      const tpRate = getBviThirdPartyRate(vehUse);
      if (!tpRate || Number(tpRate.over30under1600cc || 0) <= 0) notices.missingCoverageRate = true;
      basicPremium = round2(tpRate ? Number(tpRate.over30under1600cc || 0) : 0);
      steps.push({ label: `Basic Premium - BVI third party table (`${vehUse})`, amount: basicPremium, running: basicPremium, type: 'neutral' });
    } else if (coverage === 'C' || coverage === 'TF') {
      const compRate = getBviCompRate(vehUse, vehValue);
      if (!compRate || Number(compRate.over30 || 0) <= 0) notices.missingCoverageRate = true;
      basicPremium = round2(compRate ? Number(compRate.over30 || 0) : 0);
      steps.push({ label: `Basic Premium - BVI comp table (`${vehUse}, capped lookup `${cur} `${fmt(Math.min(vehValue, 10000))})`, amount: basicPremium, running: basicPremium, type: 'neutral' });
      if (vehValue > 10000) {
        const roundedVehicleValue = Math.ceil(vehValue / 1000) * 1000;
        const overBaseUnits = Math.round((roundedVehicleValue - 10000) / 1000);
        const overBaseCharge = round2(overBaseUnits * Number(pv.charge || 0));
        basicPremium = round2(basicPremium + overBaseCharge);
        if (overBaseCharge <= 0) notices.missingPremVar = true;
        steps.push({ label: `BVI over `${cur} 10,000 charge (`${overBaseUnits} x `${cur} `${fmt(Number(pv.charge || 0))})`, amount: overBaseCharge, running: basicPremium, type: 'positive' });
      }
      if (coverage === 'TF') {
        basicPremium = round2(basicPremium * 0.75);
        steps.push({ label: 'BVI Third Party Fire/Theft factor (75%)', amount: null, running: basicPremium, type: 'subtotal' });
      }
    }
  } else if (currentTerritory.code === 'SAB') {
"@)

$text = $text.Replace("  { code: 'BVI', name: 'British Virgin Islands',engine:'GetPremRateBVI',   currency: 'USD', active: false },", "  { code: 'BVI', name: 'Tortola / British Virgin Islands', engine: 'GetPremRateBVI', currency: 'US$', currencyName: 'United States Dollar', active: true },")

$text = $text.Replace("          currentTerritory.code === 'SAB' && window.SAB_RATES ? window.SAB_RATES :", @"
          currentTerritory.code === 'SAB' && window.SAB_RATES ? window.SAB_RATES :
          currentTerritory.code === 'BVI' && window.BVI_RATES ? window.BVI_RATES :
"@)

$text = $text.Replace("  if (currentTerritory.code === 'MON' || currentTerritory.code === 'SAB' || currentTerritory.code === 'EUX') {", @"
  if (currentTerritory.code === 'BVI') {
    options = [
      { value: '26', label: '25 and over' },
      { value: '24', label: 'Under 25' },
      { value: '21', label: 'Under 22' }
    ];
  } else if (currentTerritory.code === 'MON' || currentTerritory.code === 'SAB' || currentTerritory.code === 'EUX') {
"@)

Set-Content -Path $path -Value $text -Encoding UTF8

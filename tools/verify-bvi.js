const fs = require('fs');
const vm = require('vm');

const html = fs.readFileSync('index.html', 'utf8');
const inlineScripts = [...html.matchAll(/<script(?![^>]*src=)[^>]*>([\s\S]*?)<\/script>/gi)].map(match => match[1]);
inlineScripts.forEach((script, index) => {
  new Function(script);
  console.log(`inline script ${index + 1}: ok (${script.length} chars)`);
});

const ctx = { window: {} };
vm.runInNewContext(fs.readFileSync('data/bvi-rates.js', 'utf8'), ctx);
const rates = ctx.window.BVI_RATES;
const comp = rates.compRate.find(row => row.id === 'PR' && row.rangeFrom <= 10000 && row.rangeTo >= 10000);
const premVar = rates.premVarTable.PR_C;
const vehicleValue = 30000;
const prCompAt30000 = comp.over30 + ((Math.ceil(vehicleValue / 1000) * 1000 - 10000) / 1000) * premVar.charge;
const tp = rates.thirdPartyRates.find(row => row.id === 'PR');

console.log(JSON.stringify({
  coverage: Object.keys(rates.coverage),
  vehicleUses: Object.keys(rates.nVehUse),
  vehicleTypes: rates.vehicleTypes.map(row => row.type),
  prCompAt30000,
  prThirdParty: tp.over30under1600cc,
  minPremium: rates.minPremium,
  shortPeriods: rates.shortPeriods.length
}, null, 2));

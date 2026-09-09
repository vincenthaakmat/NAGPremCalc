# DOM Premium Calculator Requirements

Source reviewed: data/source/PremCalculationRepository.cs
Engine: GetPremiumRate routes country code DOM to GetPremRateDOM.

## Required Workbook

Workbook: data/DOM-PremiumCalc.xlsx
Generated file: data/dom-rates.js (via tools/generate-dom-rates.ps1)

Use CountryCode/Territory value `DOM` on every Dominica row.
Currency: XCD / Eastern Caribbean Dollar (EC$).

## Required Sheets

### 1. Sys
Columns include: CountryCode, CurrCode, PolicyFee, NewVehDisc, Tax, VAT, CalcRounding

Used for policy fee, new vehicle discount factor and government tax. DOM currently
carries PolicyFee = 0, Tax = 0 and VAT = 0. DOM rounds NetPremium, PolicyFee, GovTax
and TotalPremium to the nearest 0.05 (CalcRounding = 2).

### 2. Coverage
Columns: CoverageCode, Territory, CoverageName, MaxNCD, SubNCD, AddNCD, POLTYP

DOM uses only two coverage codes:
- `C` = Comprehensive
- `T` = Third Party

There is no `TF` (Third Party Fire/Theft) for DOM. The C# maps `TF` to `C` only for the
PremVar lookup and otherwise leaves the basic premium unset, so the web UI does not offer TF.
Coverage rows drive the UI dropdown labels and the NCD cap (MaxNCD).

### 3. CompPercentage
Columns: COMPPERID, Coverage, VehType, VehUse, EngineSize, VehicleAge, Percentage,
Premium, Deductible, DeductibleUnderAge, Territory

This is the single rating table for DOM. Both coverages are read from it.

Comprehensive (`Coverage = C`):
- Match on VehType, EngineSize and VehicleAge (Territory = DOM).
- `Car` rows are split by EngineSize `1599` (up to 1600cc) and `1601` (over 1600cc).
  Pickup, Jeep, Bus, Taxi and Heavy Equipment rows leave EngineSize blank and are matched
  on VehType + VehicleAge only.
- VehicleAge is the calendar-year age of the vehicle, from 0 up to 9; ages of 9 or more use
  the age-9 row.
- BasicPremium = VehicleValue * Percentage / 100.

Third Party (`Coverage = T`):
- Match on VehUse + VehType (Territory = DOM).
- BasicPremium = Premium (a flat EC$ amount).
- Deductible = DeductibleUnderAge when the driver age category is 30 or under, otherwise Deductible.
  The deductible is informational in the web calculator (shown in the calculation step text).

### 4. PremVar
Columns include: ID (vehicle use), Coverage, Charge, PassLiab, HalfYearlyPerc, QuarterlyPerc,
ForLicense, FleetDisc, StaffDisc, LicExp, AALD, ActofGOD, Windscreen, UnderAgePerc

Keyed `<VehicleUse>_<Coverage>` (for example `PR_C`, `PR_T`). Used for:
- AALD percentage and inexperienced-driver (LicExp) percentage.
- Fleet and staff discount percentages.
- Passenger liability amount per seat (PassLiab).
- Quarterly and half-yearly period factors.
- Foreign-license percentage.
- Act of God and Windscreen flat charges (DOM/BVI add these as flat amounts when selected;
  the web UI does not currently expose Act of God or Windscreen inputs, matching the other
  active territories).

### 5. NVehUse
Columns: id, VuseID, Vuse, LoadPerc, IncrNCD, PassLiab, MaxNCD, Territory

Populates the vehicle-use dropdown. DOM does not apply NVehUse.LoadPerc in GetPremRateDOM.

### 6. VehicleTypes
Columns: VehicleType, RateUpYear, RateUpPerc

Populates the vehicle-type dropdown for DOM (Car, Jeep, Taxi, Mini Bus, Bus, 16 seat Bus,
Pickup, Large Bus, Small Trucks, Medium Trucks, Large Trucks, Heavy Equipment, Medium Bus,
Motorcycle). RateUpYear/RateUpPerc are 0 for DOM.

### 7. LiabilityVar
Columns: Amount, Value, ...

DOM additional-liability rows carry label text only (Value/FlatAmount 0), so additional TP
liability contributes 0 unless a flat amount is later configured.

### 8. MinimumPremium
Columns: MinimumPremium/Premium, POLTYP, Coverage, VehicleUses

Vehicle rows (POLTYP containing `V`) give the EC$ 50 minimum. The shared web engine applies
this as a floor on the net premium; GetPremRateDOM itself does not contain a minimum-premium step.

### 9. NcdScale
Columns: CountryCode, CoverageCode, NCD, ...

Populates the valid NCD dropdown values by coverage.

### 10. ShortPeriods
Columns: Label, Percent

Used when Short Period is selected: GrossPremium = YearPremium * ShortPeriodPercent / 100.

### 11. CompRateUpdates
GetPremRateDOM contains a Getdataset() rate-increase block. Treat as optional; the web engine
models it as 0 unless DOM rate increases are configured.

## DOM Calculation Summary

Base premium:
- C: CompPercentage(C).Percentage * VehicleValue / 100, matched on VehType, EngineSize
  (Car only) and VehicleAge.
- T: CompPercentage(T).Premium (flat), matched on VehUse and VehType.

Loadings and add-ons (shared web pipeline, order per GetPremRateDOM):
- New vehicle discount multiplies BasicPremium by Sys.NewVehDisc.
- RateUp adds BasicPremium * RateUp / 100.
- When not rental: commercial vehicle uses (BS, SB, CP, HD, TX, RN) or a ticked AALD with an
  experienced driver add running premium * PremVar.AALD / 100; a driver with under two years'
  experience adds running premium * PremVar.LicExp / 100.
- AddTPL and ToolsOfTrade are flat additions.
- Fleet discount = running premium * PremVar.FleetDisc / 100.
- Staff discount = running premium * PremVar.StaffDisc / 100.
- Act of God / Windscreen add PremVar.ActofGOD / PremVar.Windscreen as flat amounts (not
  currently surfaced in the web UI).
- Management discount = running premium * ManagDisc / 100.

Period / NCD / extras:
- YearPremium = running amount after loadings and discounts.
- 6M = YearPremium * PremVar.HalfYearlyPerc / 100.
- 1Q = YearPremium * PremVar.QuarterlyPerc / 100.
- 3M50 and 9M50 = YearPremium / 2 (50%). DOM does NOT use the 40% / 60% split that
  SLU / GRE / MON apply.
- Short period = YearPremium * ShortPeriodPercent / 100.
- NCD is applied after the period adjustment.
- Passenger liability = seats * PremVar.PassLiab, then adjusted by period (3M50 / 9M50 use 50%).
- NetPremium = DiscountPremium + PassLiab + ExtraCoverage1 + ExtraCoverage2 + CampaignAmt.

Taxes and total:
- NetPremium, PolicyFee, GovTax and TotalPremium are each rounded to the nearest 0.05.
- TotalPremium = NetPremium + PolicyFee + GovTax (+ VAT, both 0 for DOM).

## Screen Inputs Needed For DOM

Already present and used:
- Territory, Coverage, Period / short period percent
- Vehicle / catalog value, Vehicle use, Vehicle type, Vehicle year, Engine size
- Driver age category and driving experience
- NCD, Rate Up, AALD, Fleet / staff / management discounts
- Passenger liability seats, Tools of Trade, Extra Coverage 1 and 2, Campaigns / coupon, Rental

Not surfaced (consistent with the other active territories):
- Explicit Act of God and Windscreen checkboxes (PremVar carries the flat amounts if needed).

## Web Integration Points (index.html)

- Flag: `.flag-dom` CSS uses `data/DOM_Flag.svg`; `#territoryFlag` class is set from the
  territory code on selection.
- Header territory `<select>` option `DOM · Dominica` (alphabetical placement).
- `<script src="data/dom-rates.js">` include.
- `TERRITORIES` entry: `{ code: 'DOM', name: 'Dominica', engine: 'GetPremRateDOM',
  currency: 'XCD', currencyName: 'Eastern Caribbean Dollar', active: true }`.
- `updateTerritoryDisplay()` maps `DOM` to `window.DOM_RATES`.
- `isEcDollarRoundingTerritory()` includes `DOM` (0.05 rounding) but the 3M50 / 9M50 gross
  and passenger-liability branches exclude `DOM` so it uses 50%.
- `getDomCompPercent()` / `getDomThirdPartyRate()` helpers and the
  `else if (currentTerritory.code === 'DOM')` basic-premium branch in `calculate()`.
- Generator: `tools/generate-territory-rates.ps1` captures `engineSize` and `vehicleAge`
  from the CompPercentage sheet.

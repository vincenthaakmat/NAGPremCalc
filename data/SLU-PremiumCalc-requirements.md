# SLU Premium Calculator Requirements

Source reviewed: data/source/PremCalculationRepository.cs
Engine: GetPremiumRate routes country code SLU to GetPremRateSLU.

## Required Workbook

Workbook: data/SLU-PremiumCalc.xlsx
Future generated file: data/slu-rates.js

Use CountryCode/Territory value `SLU` on every St. Lucia row.
Currency: XCD / Eastern Caribbean Dollar.

## Required Sheets

### 1. Sys
Columns: CountryCode, Currency, CurrencyName, PolicyFee, NewVehicleFactor, GovTax, GovVAT, NRSA, RSSFee

Used for policy fee, new vehicle discount factor, government tax/VAT, NRSA and RSS fee.
For SLU, the C# taxes NetPremium directly when GovTax is non-zero: `GovTax = NetPremium * GovTax`.
SLU totals are rounded to the nearest 0.05.

### 2. Coverage
Columns: CountryCode, CoverageCode, CoverageName, LoadPerc, MaxNCD, BonusMalus, ReplacementValue

The SLU C# uses these coverage codes:
- `C` = Comprehensive
- `TF` = Basic Comprehensive
- `T` = Third Party

Coverage rows are still needed so the web UI can populate the coverage dropdown and NCD cap behavior.

### 3. ComprehensiveRateStLucia
Columns: CountryCode, VehUse, PlusCat, Year, EngineSize, RevBase

Used for Comprehensive (`C`) when Rental is false.
The current C# lookup uses only VehUse and then calculates:
`BasicPremium = PlusCat * VehicleValue / 100`.

The commented older lookup also referenced Year and EngineSize, so those columns are included for traceability if the old database has them.

### 4. ThirdPartyRates
Columns: CountryCode, RatesCategorie, ObjectCategorie, Amount, Age

Used for `TF` and `T`:
- `TF`: find RatesCategorie = VehicleUse and ObjectCategorie = EngineSize, then `BasicPremium = Age * VehicleValue / 100`.
- `T`: find RatesCategorie = VehicleUse and ObjectCategorie = EngineSize, then `BasicPremium = Amount`.

### 5. PremVar
Columns: CountryCode, VehicleUseCode, CoverageCode, AALD, LicExp, UnderAgePerc, ForLicense, FleetDisc, StaffDisc, AgentStaffDisc, PassLiab, QuarterlyPerc, HalfYearlyPerc, RentalPerc, ActOfGOD, Windscreen, RSS_Fee

Used for:
- Rental percentage when Comprehensive is selected and Rental is checked.
- AALD surcharge.
- Windscreen fixed charge.
- Act of God percentage when Comprehensive is selected.
- Fleet and staff discounts.
- Passenger liability amount per seat.
- Quarterly and half-yearly premium factors.

### 6. NVehUse
Columns: CountryCode, VehicleUseCode, Description, LoadPerc, MaxNCD

SLU does not use NVehUse.LoadPerc in the C# calculation, but this sheet is needed to populate the vehicle-use dropdown in the web calculator.

### 7. VehicleTypes
Columns: CountryCode, VehicleType, RateUpYear, RateUpPerc

Used by the web UI for the vehicle type dropdown and future auto rate-up support.
The SLU C# section does not appear to do a vehicle-age rate-up lookup directly.

### 8. LiabilityVar
Columns: CountryCode, Amount, LimitLabel, Value, FlatAmount

The SLU C# uses `request.AddTPL` as a flat amount, not as a percentage of basic premium.
Use FlatAmount for SLU additional liability values. Value can be left blank unless a percentage behavior is later confirmed.

### 9. MinimumPremium
Columns: CountryCode, PolTyp, CoverageCode, VehicleUseCodes, Premium

Used for the minimum premium floor when implemented in the web engine.
The shared C# minimum-premium code applies floors by period:
- 1Y: full minimum
- 6M / 3M50 / 9M50: half minimum
- 1Q: quarter minimum

### 10. NcdScale
Columns: CountryCode, CoverageCode, NCD, NewNCD, 1stClaim, 2ndClaim, id

Used to populate valid NCD dropdown values by coverage.

### 11. ShortPeriods
Columns: CountryCode, Label, Percent

Used when Short Period is selected:
`GrossPremium = YearPremium * ShortPeriodPercent / 100`.

### 12. CompRateUpdates
Columns: CountryCode, PolType, CoverageCode, Date_Effective, TrxCode, Rate

The SLU C# contains a Getdataset() rate-increase block, but it appears to add the increase to `response.BasicPremium` before that value is assigned from `tmpBasicPremium`. Treat this as optional/confirm before production use.

## SLU Calculation Summary

Base premium:
- C and not rental: ComprehensiveRateStLucia.PlusCat * VehicleValue / 100.
- C and rental: VehicleValue * PremVar.RentalPerc / 100.
- TF: ThirdPartyRates.Age * VehicleValue / 100.
- T: ThirdPartyRates.Amount.

Driver loading:
- If DriverExp = 0 and AgeCat <= 25: add 50% of basic premium.
- Else if DriverExp = 1 and AgeCat <= 25: add 35% of basic premium.
- Else if DriverExp = 0: add 35% of basic premium.

Other loadings:
- RateUp amount = BasicPremium * RateUp / 100.
- AALD is added when selected and not rental.
- Windscreen is a fixed PremVar.Windscreen amount when selected.
- Act of God is PremVar.ActOfGOD * VehicleValue for Comprehensive only.
- The old request.CompPercDOM field was used as a manual windscreen/extra charge for SLU Comprehensive.
- AddTPL and ToolsOfTrade are flat additions.

Discounts:
- Fleet discount = running premium * PremVar.FleetDisc / 100.
- Staff discount = running premium * PremVar.StaffDisc / 100.
- Management discount = running premium * ManagDisc / 100.

Period/NCD/extras:
- YearPremium = running amount after loadings and discounts.
- 1Q = YearPremium * PremVar.QuarterlyPerc / 100.
- 3M = YearPremium * 40%.
- 9M = YearPremium * 60% for gross premium.
- 6M1 = YearPremium * 70%.
- 6M2 = YearPremium * 30%.
- Short period = YearPremium * ShortPeriodPercent / 100.
- NCD is applied after period adjustment.
- Passenger liability is adjusted by period. Note: in the C# passenger liability for 9M is multiplied by 40%, while gross premium for 9M is multiplied by 60%; confirm if this is intentional.

Taxes and total:
- NetPremium = DiscountPremium + PassLiab + ExtraCoverage1 + ExtraCoverage2 + CampaignAmt.
- TotalPremium = NetPremium + PolicyFee + GovTax.
- EC dollar territories including SLU round NetPremium, PolicyFee, GovTax, GovVAT and TotalPremium to the nearest 0.05.

## Screen Inputs Needed For SLU

Already present:
- Territory
- Coverage
- Period and short period percent
- Vehicle/catalog value
- Vehicle use
- Vehicle type
- Vehicle year
- Engine size
- Driver age category and driving experience
- NCD
- Rate Up
- AALD
- Fleet/staff/management discounts
- Passenger liability seats
- Tools of Trade
- Extra Coverage 1 and 2
- Campaigns/coupon
- Rental

Still to confirm before activation:
- SLU coverage names and whether the web labels should show `T`/`TF` or user-friendly labels only.
- Whether SLU AddTPL should be selected as a flat amount from LiabilityVar.
- Whether SLU needs visible Windscreen and Act of God inputs/options.
- Whether the Getdataset rate-increase behavior should be ignored, fixed, or reproduced.
- Whether the passenger-liability 9M factor should remain 40% as in the C#.
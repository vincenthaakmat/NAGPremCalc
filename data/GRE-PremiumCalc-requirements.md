# GRE Premium Calculator Requirements

Source reviewed: data/source/PremCalculationRepository.cs
Engine: GetPremiumRate routes country code GRE to GetPremRateGRE.

## Required Workbook

Workbook: data/GRE-PremiumCalc.xlsx
Future generated file: data/gre-rates.js

Use CountryCode/Territory value `GRE` on every Grenada row.
Currency: XCD / Eastern Caribbean Dollar.

## Required Sheets

### 1. Sys
Columns: CountryCode, Currency, CurrencyName, PolicyFee, NewVehicleFactor, GovTax, GovVAT, NRSA, RSSFee

Used for policy fee, new vehicle discount factor, government tax and optional fees. GRE rounds NetPremium, PolicyFee, GovTax and TotalPremium to the nearest 0.05.

### 2. Coverage
Columns: CountryCode, CoverageCode, CoverageName, LoadPerc, MaxNCD, BonusMalus, ReplacementValue

The GRE C# uses these coverage codes:
- `C` = Comprehensive
- `TF` = Third Party Fire/Theft
- `T` = Third Party

Coverage rows are needed for the web UI dropdown labels and NCD cap behavior. GRE base premiums come from ComprehensiveRate or ThirdPartyRates, not Coverage.LoadPerc.

### 3. ComprehensiveRate
Columns: CountryCode, ObjectCategory, RangeFrom, RangeTo, PremiumAmount

Used for Comprehensive and related GRE add-ons:
- For C/TF base premium: find CountryCode = GRE, ObjectCategory = VehicleUse, and VehicleValue between RangeFrom and RangeTo. BasicPremium = PremiumAmount * VehicleValue / 100.
- For object category `WS`: windscreen fixed premium by vehicle value band.
- For object category `TR`: temporary replacement fixed premium by vehicle value band.
- For object category `SP`: special perils / Act of God percentage. PremiumAmount is percentage of vehicle value. RangeTo is used by the C# as the minimum special-perils amount.

Special GRE C# rule:
- If VehicleUse = `BS`, bus rates use request.BasicPremium as a percentage of vehicle value. Confirm whether the web UI should expose this as manual basic premium or whether BS rows should be configured directly in ComprehensiveRate.
- If Coverage = `TF`, the C# overrides the comprehensive-rate result: VehicleValue below 50,000 uses 4.5%; above 50,000 uses 5.0%.

### 4. ThirdPartyRates
Columns: CountryCode, RatesCategorie, ObjectCategorie, Amount

Used for Third Party (`T`):
- RatesCategorie = VehicleUse
- ObjectCategorie = EngineSizeText / selected engine size label
- BasicPremium = Amount

### 5. PremVar
Columns: CountryCode, VehicleUseCode, CoverageCode, Charge, AALD, LicExp, UnderAgePerc, ForLicense, FleetDisc, StaffDisc, AgentStaffDisc, PassLiab, QuarterlyPerc, HalfYearlyPerc, RentalPerc, ActOfGOD, Windscreen, RSS_Fee

Used for:
- Minimum premium via Charge.
- Rental surcharge percentage.
- AALD percentage.
- Fleet and staff discounts.
- Passenger liability amount per seat.
- Quarterly and half-yearly percentage factors.

### 6. NVehUse
Columns: CountryCode, VehicleUseCode, Description, LoadPerc, MaxNCD

GRE does not use NVehUse.LoadPerc in the C# calculation, but this sheet is needed to populate the vehicle-use dropdown in the web calculator.

### 7. VehicleTypes
Columns: CountryCode, VehicleType, RateUpYear, RateUpPerc

Used by the web UI for the vehicle type dropdown and future auto rate-up behavior.

### 8. LiabilityVar
Columns: CountryCode, Amount, LimitLabel, Value, FlatAmount

GRE C# uses request.AddTPL as a flat amount. Use FlatAmount for GRE additional liability values. Value can be left blank unless a percentage behavior is later confirmed.

### 9. MinimumPremium
Columns: CountryCode, PolTyp, CoverageCode, VehicleUseCodes, Premium

Optional for the web engine. The C# minimum comes from PremVar.Charge, but the existing web calculator also supports a shared MinimumPremium sheet.

### 10. NcdScale
Columns: CountryCode, CoverageCode, NCD, NewNCD, 1stClaim, 2ndClaim, id

Used to populate valid NCD dropdown values by coverage.

### 11. ShortPeriods
Columns: CountryCode, Label, Percent

Used when Short Period is selected:
GrossPremium = YearPremium * ShortPeriodPercent / 100.

### 12. CompRateUpdates
Columns: CountryCode, PolType, CoverageCode, Date_Effective, TrxCode, Rate

The GRE C# contains a Getdataset() rate-increase block. Treat this as optional unless GRE currently uses rate increases.

## GRE Calculation Summary

Base premium:
- C and not rental: ComprehensiveRate.PremiumAmount * VehicleValue / 100 by vehicle-use band.
- C and rental: base premium plus base premium * PremVar.RentalPerc / 100.
- TF: 4.5% of vehicle value if below 50,000; 5.0% if above 50,000.
- T: ThirdPartyRates.Amount by vehicle use and engine size.
- Minimum premium is applied from PremVar.Charge.

Loadings and add-ons:
- New vehicle discount multiplies BasicPremium by Sys.NewVehicleFactor.
- RateUp adds BasicPremium * RateUp / 100.
- AALD adds running premium * PremVar.AALD / 100 when selected and not rental.
- Windscreen adds ComprehensiveRate `WS` fixed amount when selected and coverage is not T.
- Temporary replacement adds ComprehensiveRate `TR` fixed amount when selected and coverage is not T.
- Act of God / Special Perils adds ComprehensiveRate `SP`.PremiumAmount * VehicleValue / 100, with a minimum equal to the SP row RangeTo.
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
- Passenger liability is adjusted by period.
- NetPremium = DiscountPremium + PassLiab + ExtraCoverage1 + ExtraCoverage2 + CampaignAmt.

Taxes and total:
- NetPremium is rounded to nearest 0.05.
- PolicyFee is rounded to nearest 0.05.
- GovTax = NetPremium * GovTax, rounded to nearest 0.05.
- TotalPremium = NetPremium + PolicyFee + GovTax, rounded to nearest 0.05.

## Screen Inputs Needed For GRE

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
- Whether GRE should expose Windscreen, Temporary Replacement, and Act of God as explicit checkboxes or keep using existing extra coverage inputs.
- Whether GRE AddTPL must be selected as a flat amount from LiabilityVar.
- How VehicleUse `BS` bus percentage should be entered in the web UI.
- Whether GRE currently uses CompRateUpdates.
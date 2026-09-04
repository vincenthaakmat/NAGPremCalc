# ARU Premium Calculator Requirements

Source: data/source/PremCalculationRepository.cs
Engine: GetPremiumRate routes country code ARU to GetPremRateABC, then CalcNoOverride(request, true).

## Required Workbook

Recommended workbook: data/ARU-PremiumCalc.xlsx
Recommended generated file: data/aru-rates.js

Use CountryCode/Territory value `ARU` on all ARU rows.

## Required Sheets

### 1. Sys
Columns: CountryCode, Currency, CurrencyName, PolicyFee, NewVehicleFactor, GovTax, GovVAT, NRSA, RSSFee

Notes:
- ARU currency should be AWG.
- PolicyFee is used in the final total.
- GovTax is normally applied on NetPremium + PolicyFee. The old EffectiveDate/year-specific ARU rate-change check is no longer required for this web calculator.
- NRSA is added to total premium unless suppressed by NoRSSFeeCharge.
- RSSFee in Sys is less important for ARU because the C# takes RSS_Fee from PremVar for ARU.

### 2. AbcTpRate
Columns: TableID, Territory, ID, FRSUMINS, TOSUMINS, SUMINS, Premium, CurrCode, VehCat, FRSUMINS1, TOSUMINS1, FRSUMINS2, RowVersion

Used for TP/LAR premium lookup.

Normal lookup:
- Select first row where Territory = ARU and TOSUMINS >= VehicleValue, ordered by TOSUMINS.

Special ARU/CUR motorcycle or Harley lookup:
- If Territory is ARU or CUR and VehicleUse = HB or VehicleType = MotorCycle, the C# changes the lookup to use VehCat = Coverage.
- If Coverage = LAR, it also requires TOSUMINS >= VehicleValue.

Include ARU rows for normal TP bands and any VehCat rows needed for HB/MotorCycle/LAR.

### 3. Coverage
Columns: CountryCode, CoverageCode, CoverageName, LoadPerc, MaxNCD, BonusMalus, ReplacementValue

Used for C, SC, and TPC base premium:
- BasicPremium = VehicleValue * Coverage.LoadPerc
- For SC: BasicPremium += BasicPremium * Coverage.ReplacementValue

ARU-specific rule:
- For TPC, if IsNew is true, BasicPremium is increased by 5%.

### 4. NVehUse
Columns: id, VuseID, Vuse, LoadPerc, IncrNCD, PassLiab, MaxNCD, Territory, RowVersion, MinSumIns, Premium, TreshHold, AddCharge, MinNCD, ToolsCharge, Coverage

Used after base premium:
- TP/LAR: BasicPremium starts from AbcTpRate.Premium, then adds BasicPremium * NVehUse.LoadPerc.
- C/SC/TPC: BasicPremium starts from vehicle value percentage, then adds BasicPremium * NVehUse.LoadPerc.

### 5. LiabilityVar
Columns: ID, Amount, Value, Territory, RowVersion

Used for additional TP liability.
For ABC territories including ARU:
- AddTPL = (BasicPremium + BasicPremium * RateUp% / 100) * LiabilityVar.Value

The UI needs to store/select the Amount text and use the Value percentage.

### 6. PremVar
Columns: ID, Charge, Deductible, HalfYearlyPerc, HalfYearlyAdd, QuarterlyPerc, QuarterlyAdd, ThreeNinePerc, ThreeNineAdd, PassLiab, AALD, ForLicense, FleetDisc, StaffDisc, AddDriver, LicExp, RentalPerc, RentalValue, Coverage, Territory, TableID, UnderAgePerc, UID, ActofGOD, Windscreen, RSS_Fee, RowVersion

Used for:
- AALD surcharge
- Foreign license surcharge
- Fleet discount
- Staff discount
- Passenger liability amount per seat
- Quarterly and half-yearly period factors
- ARU RSS_Fee from PremVar.RSS_Fee, if ARU should charge RSS in the web calculator

ARU-specific RSS rule:
- If Territory = ARU, RSS_Fee can be taken from PremVar.RSS_Fee when ARU RSS should be charged.
- If NoRSSFeeCharge is checked, RSS_Fee = 0 and NRSA is also set to 0.

### 7. VehicleTypes / Vtype
Columns: CountryCode, VehicleType, RateUpYear, RateUpPerc

Used to auto-set RateUp based on vehicle age:
- If current year - VehicleYear >= RateUpYear and RateUpPerc > 0, request.RateUp is set to RateUpPerc.

Important: The values must match the UI values exactly. The C# checks MotorCycle specifically in one ARU/CUR branch.

### 8. MinimumPremium
Columns: CountryCode, PolTyp, CoverageCode, VehicleUseCodes, Premium

Used after NetPremium calculation:
- 1Y: floor = Premium
- 6M, 3M50, 9M50: floor = Premium / 2
- 1Q: floor = Premium / 4

### 9. NcdScale
Columns: CountryCode, CoverageCode, NCD, NewNCD, 1stClaim, 2ndClaim, id, RowVersion

Used to populate valid NCD options per coverage.

### 10. ShortPeriods
Columns: CountryCode, Label, Percent

Used when Short Period is selected:
- GrossPremium = YearPremium * ShortPeriodPercent / 100

### 11. CompRateUpdates
Columns: CountryCode, PolType, CoverageCode, Date_Effective, TrxCode, Rate

The C# Getdataset() applies a rate increase to BasicPremium when a matching row exists:
- CountryCode = ARU
- PolType = request.PolType
- CoverageCode = request.Coverage
- Date_Effective <= request.EffectiveDate
- TrxCode = NEW when IsNew = true, otherwise REN

## Screen Inputs Needed For ARU

Already present in the calculator:
- Territory
- Coverage
- Period and Short Period percent
- Policy Type
- Vehicle value / catalog value
- Vehicle use
- Vehicle type
- Vehicle year
- NCD
- Rate Up
- Management discount
- Staff type
- Passenger liability seats
- Tools of Trade
- Extra Coverage 1
- Extra Coverage 2
- Liability tier
- Campaign amount/type
- AALD
- Foreign license
- Fleet discount
- Staff discount
- New vehicle
- Rental
- No RSS fee

Still needed or should be confirmed before implementing ARU:
- Transaction type / IsNew vs Renewal. ARU TPC has a 5% increase when IsNew is true.
- LAR coverage option if ARU sells Limited All Risk.
- Catalog conversion is confirmed as 1:1: XCG catalog value equals AWG insured value for ARU.

## ARU Calculation Summary

Base Premium:
- TP/LAR: lookup AbcTpRate by vehicle value band.
- C/SC/TPC: VehicleValue * Coverage.LoadPerc.
- Add NVehUse.LoadPerc surcharge.
- SC adds Coverage.ReplacementValue surcharge.
- ARU TPC new vehicles add 5% to BasicPremium.
- CompRateUpdates is optional for future use; the old 2023 effective-date check is no longer required.

Rate Up and AddTPL:
- Vehicle type may auto-set RateUp based on vehicle age.
- AddTPL = (BasicPremium + RateUpAmt) * LiabilityVar.Value.

Loadings and Discounts:
- Add AALD if selected.
- Add ForLicense if selected.
- Add ToolsOfTrade.
- Apply FleetDiscount.
- Apply StaffDiscount: StaffDisc for NON/EMP, otherwise 20%.
- Apply Management Discount.

Period/NCD/Extras:
- Apply period factor.
- Apply NCD.
- Apply new vehicle discount within MaxNCD cap.
- Add passenger liability.
- Add extra coverages.
- Add campaign amount or percentage.
- Apply minimum premium if configured.

Taxes and Final Total:
- PolicyFee from Sys.
- GovTax from Sys. The old EffectiveDate/year-specific ARU exception is no longer required.
- ARU RSS_Fee from PremVar.RSS_Fee if ARU should charge RSS in the web calculator.
- NoRSSFeeCharge suppresses RSS_Fee and NRSA.
- TotalPremium = NetPremium + PolicyFee + NRSA + GovTax + GovVAT + RSS_Fee.
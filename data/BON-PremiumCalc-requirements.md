# Bonaire (BON) Premium Calculation Requirements

Source reviewed: data/source/PremCalculationRepository.cs

BON routing:
- GetPremiumRate routes country code BON to GetPremRateABC.
- GetPremRateABC performs the base premium calculation, then calls CalcNoOverride(request, true).

Required Excel workbook: data/BON-PremiumCalc.xlsx

Required tables/sheets:

1. Sys
Columns: CountryCode, Currency, CurrencyName, PolicyFee, NewVehicleFactor, GovTax, GovVAT, NRSA, RSSFee
Use for policy fee, tax, VAT, NRSA/RSS fee, and new vehicle discount factor.

2. AbcTpRate
Columns: CountryCode, FromSumInsured, ToSumInsured, Premium, VehCat
Use for TP/LAR base premium. Query behavior: first row where ToSumInsured >= VehicleValue and CountryCode/Territory = BON, ordered by ToSumInsured.

3. Coverage
Columns: CountryCode, CoverageCode, CoverageName, LoadPerc, MaxNCD, BonusMalus, ReplacementValue
Use for C/TPC/SC base premium percentage, max NCD cap, and SC replacement value surcharge.

4. NVehUse
Columns: CountryCode, VehicleUseCode, Description, LoadPerc, MaxNCD
Use for vehicle-use load percentage added to base premium.

5. LiabilityVar
Columns: CountryCode, Amount, LimitLabel, Value
Use for additional TP liability. For ABC territories including BON: AddTPL = (BasicPremium + BasicPremium * RateUp% / 100) * LiabilityVar.Value.

6. PremVar
Columns: CountryCode, VehicleUseCode, CoverageCode, AALD, LicExp, UnderAgePerc, ForLicense, FleetDisc, StaffDisc, AgentStaffDisc, PassLiab, QuarterlyPerc, HalfYearlyPerc, RSS_Fee
Use for AALD, foreign license, fleet/staff discounts, passenger liability, period factors, and RSS fee if applicable.

7. Vtype / VehicleTypes
Columns: CountryCode, VehicleType, RateUpYear, RateUpPerc
Use for vehicle-age rate-up. If current year - VehicleYear >= RateUpYear, request.RateUp is set to RateUpPerc when not overridden.

8. MinimumPremium
Columns: CountryCode, PolTyp, CoverageCode, VehicleUseCodes, Premium
Use for minimum premium floor. Query behavior: PolTyp = request.PolType, CoverageCode = request.Coverage, VehicleUseCodes contains request.VehicleUse.

9. CompRateUpdates
Columns: CountryCode, PolType, EffectiveFrom, EffectiveTo, Rate
Use if BON should receive the same database-driven premium increase returned by Getdataset(). The C# GetPremRateABC applies this to BasicPremium when a row exists.

10. ShortPeriods
Columns: CountryCode, Description, Percent
Use for short period selections if these are maintained outside the UI.

Calculation sequence for BON:

A. Determine base premium when NoOverride is false.
- TP/LAR: BasicPremium = AbcTpRate.Premium.
- TPC/C/SC: BasicPremium = VehicleValue * Coverage.LoadPerc.
- Then add vehicle-use load: BasicPremium += BasicPremium * NVehUse.LoadPerc.
- For SC: BasicPremium += BasicPremium * Coverage.ReplacementValue.
- If CompRateUpdates row exists: BasicPremium += BasicPremium * Rate / 100.

B. Rate-up and additional TP liability.
- CalcNoOverride calculates RateUpAmt = BasicPremium * request.RateUp / 100.
- VehicleTypes can overwrite request.RateUp based on vehicle age, but the original C# calculates RateUpAmt before that lookup. Confirm whether this is intended before final production use.
- For BON/ABC, AddTPL = (BasicPremium + BasicPremium * RateUp% / 100) * LiabilityVar.Value.

C. Loadings.
- If not rental and AALD is checked: add AALD = running premium * PremVar.AALD / 100.
- If foreign license is checked: add ForLicense = running premium * PremVar.ForLicense / 100.
- The C# does not automatically force AALD for BON commercial vehicle uses; that automatic rule only appears for SMD/SMF.

D. Discounts.
- AddTPL and ToolsOfTrade are added to gross running amount before discounts.
- Fleet discount: subtract running amount * PremVar.FleetDisc / 100.
- Staff discount: if MasterAgentCode is NON or EMP, use PremVar.StaffDisc; otherwise use 20%.
- Management discount: subtract running amount * request.ManagDisc / 100.

E. Period premium.
- YearPremium = running amount after loadings/discounts.
- 1Y: GrossPremium = YearPremium.
- 1Q: GrossPremium = YearPremium * PremVar.QuarterlyPerc / 100.
- 6M: GrossPremium = YearPremium * PremVar.HalfYearlyPerc / 100.
- 3M50 or 9M50: GrossPremium = YearPremium / 2.
- Short period: GrossPremium = YearPremium * ShortPeriodPercent / 100.

F. NCD and new vehicle discount.
- NCD = GrossPremium * request.NCD / 100.
- DiscountPremium = GrossPremium - NCD.
- New vehicle discount is controlled by Sys.NewVehicleFactor and Coverage.MaxNCD, including fleet discount in the cap check.

G. Passenger liability, extras, campaigns.
- PassLiab = seats * PremVar.PassLiab, adjusted for premium period.
- For 6M/3M50/9M50 on BON, passenger liability is divided by 2.
- Campaign can be a flat amount or percentage depending on request.isPercentageCampaign.
- NetPremium = DiscountPremium - NewVehDiscValue + PassLiab + ExtraCoverage1 + ExtraCoverage2 + CampaignAmt.

H. Minimum premium.
- Apply MinimumPremium.Premium by period:
  - 1Y: full minimum.
  - 6M/3M50/9M50: half minimum.
  - 1Q: quarter minimum.

I. Taxes and total.
- GovTax = (NetPremium + PolicyFee) * GovTax.
- GovVAT = (NetPremium + PolicyFee + GovTax) * GovVAT.
- TotalPremium = NetPremium + PolicyFee + NRSA + GovTax + GovVAT + RSS_Fee.

Open questions before final production implementation:
- Provide actual BON rows from the database for all required tables.
- Confirm the C# rate-up ordering quirk: RateUpAmt is computed before VehicleTypes may overwrite request.RateUp.
- Confirm whether BON should use USD catalog values or another catalog source.

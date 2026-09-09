# EUX - Statia premium calculation setup

Territory code: `EUX`
Currency: `USD` / `US$`
Engine: `GetPremRateSEM`

## Statia-specific base premium rules

- Third Party (`T`) uses `ThirdPRateMONEUX`, matched by `CountryCode`, `VehUse`, `Age`, and `EngineSize`.
- Comprehensive (`C`) and Third Party Fire/Theft (`TF`) use `CompRateSABEUX` plus the over-20,000 charge from `PremVar.Charge`.

## Required sheets

- `CompRateSABEUX`: `CountryCode`, `ObjectCategory`, `RangeFrom`, `RangeTo`, `PremiumAmount`
- `Coverage`: `CoverageCode`, `Territory`, `CoverageName`, `MaxNCD`, `LoadPerc`, `BonusMalus`, `ReplacementValue`
- `LiabilityVar`: `Amount`, `Value`, `Territory`
- `MinimumPremium`: `Premium`, `Territory`, `POLTYP`, `CurrCode`, `string1`, `string2`
- `NcdScale`: `CountryCode`, `CoverageCode`, `NCD`
- `NVehUse`: `VuseID`, `Vuse`, `LoadPerc`, `MaxNCD`, `Territory`
- `PremVar`: `ID`, `Charge`, `HalfYearlyPerc`, `QuarterlyPerc`, `ThreeNinePerc`, `PassLiab`, `AALD`, `ForLicense`, `FleetDisc`, `StaffDisc`, `LicExp`, `RentalPerc`, `Coverage`, `Territory`, `UnderAgePerc`, `ActofGOD`, `Windscreen`
- `ShortPeriods`: `Description`, `Value`
- `Sys`: `CountryCode`, `CurrCode`, `PolicyFee`, `NewVehDisc`, `Tax`, `VAT`, `PolicyFeeRSS`
- `ThirdPRateMONEUX`: `CountryCode`, `VehUse`, `Age`, `EngineSize`, `Premium`
- `VehicleTypes`: `Vtype`, `CountryCode`, `RateUpYear`, `RateUpPerc`

## Shared SEM calculation notes

These workbooks follow the original C# `GetPremRateSEM` branch for Saba / Statia / Montserrat.

For Comprehensive (`C`) and Third Party Fire/Theft (`TF`) in Saba and Statia:

1. Find the `CompRateSABEUX` row where `CountryCode` is the territory, `ObjectCategory` is the vehicle-use code, and `RangeTo` is greater than or equal to the vehicle value used for the lookup.
2. If vehicle value is above 20,000, the lookup value is capped at 20,000.
3. Add an extra charge for the portion above 20,000: round the actual vehicle value up to the next 1,000, subtract 20,000, divide by 1,000, then multiply by `PremVar.Charge`.
4. If Rental is selected, add `BasicPremium * PremVar.RentalPerc / 100`.
5. Then the common `CalcNoOverride` logic applies loadings, discounts, liability, policy fee, tax, minimum premium, and period factors.

Saba and Statia also have special 2024+ tax behavior in the source: government tax is 0 before 2024, and 5% on `NetPremium + PolicyFee` from 2024 onward.

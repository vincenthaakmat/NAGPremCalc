# MON - Montserrat premium calculation setup

This workbook is for territory `MON` (Montserrat) and follows the original C# method `GetPremRateSEM`, the shared Saba / Statia / Montserrat calculation branch.

## Calculation source

- Territory code: `MON`
- Engine: `GetPremRateSEM`
- Currency: `XCD` / `EC$`
- Third Party (`T`) basic premium: lookup in `ThirdPRateMONEUX` by `CountryCode`, `VehUse`, `Age`, and `EngineSize`.
- Third Party Fire/Theft (`TF`) and Comprehensive (`C`) basic premium: starts with the same `ThirdPRateMONEUX` premium, then adds the `CompRateMON` percentage for the selected vehicle use.
- For rentals (`VehUse = RN`), the C# replaces the third-party base with only `VehicleValue * CompOver30Perc / 100`.
- For non-rentals, if `AgeCat = 31`, use `CompOver30Perc`; otherwise use `CompUnder30Perc`.
- After the basic premium is calculated, the common `CalcNoOverride` logic applies NCD, discounts, loadings, liability, fees, taxes, and EC-dollar rounding.

## Required workbook sheets

### `Sys`

One row for MON. Required columns:

`CountryCode`, `Currency`, `CurrencyName`, `PolicyFee`, `NewVehicleFactor`, `GovTax`, `GovVAT`, `NRSA`, `RSSFee`

Use decimal format for tax percentages. Example: `0.05` for 5%.

### `Coverage`

Coverage-level settings. Required columns:

`CountryCode`, `CoverageCode`, `CoverageName`, `LoadPerc`, `MaxNCD`, `BonusMalus`, `ReplacementValue`

Expected coverage rows:

- `C` - Comprehensive
- `TF` - Third Party Fire/Theft
- `T` - Third Party

### `ThirdPRateMONEUX`

Third-party base premium table for MON. This is mandatory for all MON coverages because `C` and `TF` also start from this table.

Required columns:

`CountryCode`, `VehUse`, `Age`, `EngineSize`, `Premium`

Important:

- `CountryCode` must be `MON`.
- `VehUse` must match the codes in `NVehUse`.
- `Age` must match the app age category values, for example `0`, `25`, `26`, `31`, depending on the MON tables.
- `EngineSize` must match the screen dropdown values exactly.
- `Premium` is the base third-party premium.

### `CompRateMON`

Comprehensive/fire-theft percentage table for MON. Required columns:

`CountryCode`, `RateType`, `CompUnder30Perc`, `CompOver30Perc`

Important:

- `RateType` must match the vehicle-use code, for example `PR`, `CP`, `RN`, `TX`.
- Enter percentages as full percentage numbers, not decimals. Example: enter `7.5` for 7.5%, not `0.075`.
- `CompOver30Perc` is used when `AgeCat = 31` and also for rentals.
- `CompUnder30Perc` is used for non-rental drivers not in age category `31`.

### `PremVar`

Common additional premium variables used by `CalcNoOverride`. Required columns:

`CountryCode`, `VehUse`, `CoverageCode`, `Charge`, `PassLiab`, `HalfYearly`, `StaffDisc`, `FleetDisc`, `RentalPerc`, `Quarterly`, `ForLicense`, `ActOfGod`, `Windscreen`, `LicExp`, `AALD`, `AgentStaffDisc`, `RSSFee`, `UnderAgePerc`

Important MON-specific behavior:

- If `AALD` is selected, MON applies the `AALD` percentage.
- If driver experience is under 2 years, MON applies the `LicExp` percentage.

### `NVehUse`

Populates the vehicle-use dropdown. Required columns:

`CountryCode`, `Code`, `Description`, `LoadPerc`, `MaxNCD`

### `VehicleTypes`

Populates the vehicle-type dropdown. Required columns:

`CountryCode`, `Type`, `RateUpYear`, `RateUpPerc`

### `LiabilityVar`

Additional third-party liability values. Required columns:

`CountryCode`, `Amount`, `Value`, `FlatAmount`

For MON and other non-ABC territories, the source uses `Value` as the additional liability amount/percentage setting from the table.

### `NcdScale`

No-claim discount options. Required columns:

`CountryCode`, `CoverageCode`, `NCD`

### `ShortPeriods`

Short-period percentages. Required columns:

`CountryCode`, `Label`, `Value`

### `MinimumPremium`

Optional minimum premium rules. Required columns:

`CountryCode`, `Coverage`, `VehicleUses`, `PolType`, `Premium`

## After filling the workbook

After this file is filled with MON rates, regenerate the MON JavaScript data file and activate `MON - Montserrat` in the territory dropdown.

# Drug reference data (egyptian-drugs.csv / .json)

Profiled 2026-11-09. The CSV and JSON contain identical content: 25,070 rows.

Columns: commercial_name_en, commercial_name_ar, scientific_name, manufacturer, drug_class, route, price_egp.

## What the data does NOT have
- No barcode (cannot be used for scanning at the POS).
- No registration number, no source date, no stable id (PharmaCore derives a SHA-256 SourceKey from NAME|MANUFACTURER|ROUTE).
- No strength, pack size or pharmaceutical form columns. Strength is parsed from the commercial name (heuristic). `route` is a coarse value (ORAL.SOLID, TOPICAL, INJECTION, UNKNOWN ...), not a form.
- No drug interactions.
- 9.2% of rows have no scientific name; ~1,900 rows are skin/hair care.
- Provenance and licence are unverified. It is not confirmed to be an official EDA export. Verify before commercial use.

## How PharmaCore uses it
- Shared catalogue table `DrugReference` (not tenant-scoped). Import goes through `DrugImportStaging`, one transaction, with a batch report.
- Re-import updates changed rows and never deletes rows missing from the file.
- Alternatives: same normalized ActiveKey + same parsed StrengthText + same route. Null ActiveKey or StrengthText means no suggestion. Suggestions only; a pharmacist must verify.
- `IsDrug` is a keyword heuristic on drug_class/route. Cosmetics are hidden from search by default.
- Prices are reference prices, not binding.

## Known limitations / risks
- Import permission `drugref.import` currently goes to every tenant's Administrator role, but the catalogue is shared. Before multi-tenant production, restrict import to a platform-admin role.
- Salt forms are not merged (DICLOFENAC vs DICLOFENAC POTASSIUM are different keys) to avoid unsafe substitutions.
- Strength parsing can misread names with several numbers (e.g. grams of cream vs strength).
- Products do not yet store a link to the catalogue entry (`DrugReferenceId`); prefill copies values only.

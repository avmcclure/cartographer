---
kind: component
name: AcmeBilling
type: library
summary: 'AcmeBilling: a library in acme-billing-com; not yet described.'
repository: repositories/acme-billing-com/repository
stack:
  - vb6
sources:
  invoice-cls: acme-billing-com:Invoice.cls@818f8ea
  moddata-bas: acme-billing-com:modData.bas@818f8ea
  proj: acme-billing-com:AcmeBilling.vbp@818f8ea
provides:
  - name: AcmeBilling.Invoice
    as: progid
    basis: extracted
    source: invoice-cls#L13
  - name: AcmeBilling.dll
    as: assembly
    basis: extracted
    source: proj#L11
links:
  - type: depends-on
    name: AcmeCommon.dll
    basis: extracted
    source: proj#L4
  - type: calls
    to: databases/acme-billing-db/routines/dbo/usp_getinvoice
    name: dbo.usp_GetInvoice
    how: sql
    basis: extracted
    source: invoice-cls#L37
  - type: uses
    name: AcmeBilling
    how: sql
    basis: extracted
    source: moddata-bas#L6
verified:
  commit: "818f8ea"
  date: "2026-01-01"
  cartographer: "0.1.0"
---

## Technical detail

<!-- extracted -->
- VB6 project `AcmeBilling.vbp`: ActiveX DLL (`Type=OleDll`), startup object `(None)`.
- 0 forms, 1 module, 2 classes.
- Public creatable classes: `Invoice`.
- Other classes: `InvoiceMath` (private).
- References:
  - `AcmeCommon.dll`: Acme Common Library; type library {7C1A4E52-93B0-4D1F-8E6A-5B2D0C9F3A17} version 1.0
- Platform references: `stdole2.tlb` (OLE Automation), `msado15.dll` (Microsoft ActiveX Data Objects 2.8 Library).
- Connection string in `modData.bas` line 6: server `ACMESQL01`, database `AcmeBilling`; integrated sign-in, no credential.
<!-- /extracted -->

## Open questions

### q1

- State: open
- Raised by: cli
- Attempts: 0
- Missing: Which component provides 'AcmeCommon.dll'. It matches an internal name prefix but no scanned repository provides it.
- Waiting on: name AcmeCommon.dll
- Answer looks like: A component whose provides list contains AcmeCommon.dll, or a person stating it is public.
- Answer:

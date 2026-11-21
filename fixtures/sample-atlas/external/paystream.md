---
kind: external
name: Paystream
summary: The card payment provider. Authorises cards at checkout and confirms settlement later.
sources:
  client: sales-order-service:src/Brackley.Orders.Api/Payments/PaystreamClient.cs@4c1e9a2
  said: person:j.petrov@2026-11-10
owner:
  name: Paystream Ltd
  basis: stated
  source: said
verified:
  commit: "4c1e9a2"
  date: "2026-11-12"
  cartographer: "0.1.0"
---

## Overview

An outside company that moves card money for Brackley. Order details and the customer's billing name leave Brackley for Paystream at checkout; a settlement notice comes back some hours later.

## Open questions

### q1

- State: needs-person
- Raised by: agent
- Attempts: 3
- Missing: Which version of the Paystream API Brackley is contracted to use; the client class names no version.
- Waiting on: person
- Answer looks like: A version number from the Paystream contract or portal.
- Answer:

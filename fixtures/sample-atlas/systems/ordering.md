---
kind: system
name: Ordering
summary: Takes customer orders from the web shop and the back office, prices them, takes payment and hands them on for invoicing and picking.
sources:
  said: person:m.okafor@2026-10-02
owner:
  name: Sales Systems team
  basis: stated
  source: said
---

## Overview

Ordering is how Brackley takes an order. A customer places it in the web shop, or a sales clerk keys it into the back office program. Either way the order ends up in the Sales database, payment is taken through Paystream, and an event tells Billing and Warehouse that there is something to invoice and pick.

Everything here changes together when the shape of an order changes.

---
kind: system
name: Warehouse
summary: Keeps stock levels in step with orders so that pickers know what to pull.
sources:
  said: person:d.lindqvist@2026-10-15
owner:
  name: Warehouse Systems team
  basis: stated
  source: said
---

## Overview

Warehouse listens for new orders and reserves stock for them. It has no database of its own in this atlas yet: its stock store has not been scanned.

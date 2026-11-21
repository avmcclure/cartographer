---
kind: channel
name: order-events
type: queue
summary: The queue on which Ordering announces each order that has been placed.
sources:
  pub: sales-order-service:src/Brackley.Orders.Api/Events/OrderPublisher.cs@4c1e9a2
verified:
  commit: "4c1e9a2"
  date: "2026-11-12"
  cartographer: "0.1.0"
---

## Overview

A Service Bus queue. The order API puts one "order placed" message on it per order; Warehouse and the payment worker take them off. Whether Billing listens too is not certain.

## Technical detail

<!-- extracted -->
Queue name `order-events`, from `OrderPublisher.cs`. The message type is `OrderPlaced` in Brackley.Orders.Contracts.
<!-- /extracted -->

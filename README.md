# CommerceCore

[![CommerceCore CI](https://github.com/MahirSafar/CommerceCore/actions/workflows/ci.yml/badge.svg)](https://github.com/MahirSafar/CommerceCore/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![C# 13/14](https://img.shields.io/badge/C%23-13%2F14-239120?style=flat&logo=csharp)](https://docs.microsoft.com/dotnet/csharp/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18.6-4169E1?style=flat&logo=postgresql)](https://www.postgresql.org/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-4.3-FF6600?style=flat&logo=rabbitmq)](https://www.rabbitmq.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-10.0-512BD4?style=flat&logo=dotnet)](https://docs.microsoft.com/ef/core/)
[![Architecture](https://img.shields.io/badge/Architecture-Modular%20Monolith%20%7C%20Clean%20%7C%20DDD%20%7C%20CQRS-blueviolet?style=flat)]()
[![Multi-Tenancy](https://img.shields.io/badge/Multi--Tenancy-Pool%20%2B%20PostgreSQL%20RLS-orange?style=flat)]()
[![Security](https://img.shields.io/badge/Security-Least--Privilege%20App%20Role%20%7C%20RLS%20%7C%20JWT-red?style=flat)]()
[![Tests](https://img.shields.io/badge/Tests-250%2B%20Passed%20%7C%20xUnit%20v3%20%7C%20Testcontainers-brightgreen?style=flat)]()

**CommerceCore** is an enterprise-grade, high-performance modular e-commerce backend platform built with **.NET 10**, engineered around the principles of **Modular Monolith**, **Clean Architecture**, **Domain-Driven Design (DDD)**, and **CQRS (Command Query Responsibility Segregation)**.

Engineered for extreme reliability, throughput, and multi-tenant isolation, the system provides native database-level **PostgreSQL Row-Level Security (RLS)**, least-privilege runtime database roles, compile-time source-generated mediation, an asynchronous **Distributed Outbox Worker with RabbitMQ topic messaging**, a dynamic attribute & schema compilation engine, explicit **Product Variants**, rich PostgreSQL JSONB specifications & localization, time-sortable **UUIDv7** identities, hierarchical taxonomy trees with PostgreSQL `ltree`, automated auditing interceptors, OpenTelemetry observability, rate limiting, and optimistic concurrency control.

---

## Table of Contents

- [Architectural Blueprint](#architectural-blueprint)
- [Multi-Tenancy & Row-Level Security (RLS)](#multi-tenancy--row-level-security-rls)
- [Least-Privilege Database Security](#least-privilege-database-security)
- [Distributed Outbox Worker & RabbitMQ Messaging](#distributed-outbox-worker--rabbitmq-messaging)
- [Key Engineering Highlights](#key-engineering-highlights)
- [Solution & Project Decomposition](#solution--project-decomposition)
- [Technology Matrix](#technology-matrix)
- [Domain Model & Invariants](#domain-model--invariants)
  - [1. Product & Product Variant Aggregates](#1-product--product-variant-aggregates)
  - [2. ProductType & Dynamic Attribute Schema Engine](#2-producttype--dynamic-attribute-schema-engine)
  - [3. Platform Control Plane & Multi-Tenancy](#3-platform-control-plane--multi-tenancy)
  - [4. Core Value Objects](#4-core-value-objects)
- [Persistence & Database Architecture](#persistence--database-architecture)
  - [Relational Schemas & Tables](#relational-schemas--tables)
  - [PostgreSQL Row-Level Security (RLS) Engine](#postgresql-row-level-security-rls-engine)
  - [Strategic Indexing & Query Optimizations](#strategic-indexing--query-optimizations)
- [Security, Resilience & Observability](#security-resilience--observability)
  - [Rate Limiting Before Tenant Resolution (DoS Mitigation)](#rate-limiting-before-tenant-resolution-dos-mitigation)
  - [Authentication & Scoped Authorization](#authentication--scoped-authorization)
  - [Security Headers & Kestrel Hardening](#security-headers--kestrel-hardening)
  - [OpenTelemetry & Health Probes](#opentelemetry--health-probes)
  - [High-Performance Logging](#high-performance-logging)
- [API Reference & Contracts](#api-reference--contracts)
  - [Product & Variant Endpoints](#product--variant-endpoints)
  - [Product Type & Attribute Endpoints](#product-type--attribute-endpoints)
  - [Health Check Endpoints](#health-check-endpoints)
  - [Sample Payloads & Responses](#sample-payloads--responses)
- [Error Handling & RFC 7807 Problem Details](#error-handling--rfc-7807-problem-details)
- [CLI Tooling: CommerceCore.Bootstrap](#cli-tooling-commercecorebootstrap)
- [Getting Started & Local Setup](#getting-started--local-setup)
- [CI/CD Automation & Quality Gates](#cicd-automation--quality-gates)
- [Testing Strategy & Quality Assurance](#testing-strategy--quality-assurance)
- [Engineering Practices & Design Patterns](#engineering-practices--design-patterns)

---

## Architectural Blueprint

CommerceCore employs a **Modular Monolith** architecture combined with **Clean Architecture** (Ports and Adapters / Onion Architecture). Layer boundaries and modular decoupling rules are strictly asserted by automated architectural tests (`NetArchTest`).

```
                              ┌──────────────────────────────────────────────┐
                              │              Presentation Layer              │
                              │             (CommerceCore.Api)               │
                              │  - Minimal API Route Endpoints (V1)          │
                              │  - Rate Limiter (Applied before Tenant Res)  │
                              │  - Multi-Tenant & Security Middleware        │
                              │  - OpenTelemetry Tracing & Metrics Export    │
                              │  - RFC 7807 Problem Details Exception Handler│
                              └──────────────────────┬───────────────────────┘
                                                     │
                    ┌────────────────────────────────┴────────────────────────────────┐
                    │                                                                 │
     ┌──────────────▼──────────────────────────────┐   ┌──────────────────────────────▼──────────────┐
     │              Platform Module                │   │                Catalog Module               │
     │  - CommerceCore.Platform.Contracts          │   │  - CommerceCore.Modules.Catalog.Domain      │
     │  - CommerceCore.Platform.ControlPlane       │   │  - CommerceCore.Modules.Catalog.Application │
     │  - CommerceCore.Platform.Identity           │   └──────────────────────┬──────────────────────┘
     └──────────────────────┬──────────────────────┘                          │
                            │                                                 │
                            └────────────────────────┬────────────────────────┘
                                                     │
                              ┌──────────────────────▼───────────────────────┐
                              │                  Core Layer                  │
                              │  - CommerceCore.Domain (Base Entities, VOs)  │
                              │  - CommerceCore.Application (CQRS Pipelines) │
                              │  - Source-Generated Mediator Handlers        │
                              └──────────────────────┬───────────────────────┘
                                                     │
                              ┌──────────────────────▼───────────────────────┐
                              │             Infrastructure Layer             │
                              │  - CommerceCore.Persistence (EF Core 10,     │
                              │    PostgreSQL RLS, Outbox, Interceptors)     │
                              │  - PlatformReadDbContext (Read-Only Context) │
                              │  - CommerceCore.Infrastructure (Clock)       │
                              └──────────────────────┬───────────────────────┘
                                                     │
                              ┌──────────────────────▼───────────────────────┐
                              │           Asynchronous Workers               │
                              │     (CommerceCore.Outbox.Worker)             │
                              │  - Distributed Outbox Leasing (SKIP LOCKED)  │
                              │  - Resilient RabbitMQ Event Publisher (7.x)  │
                              │  - Versioned Integration Event Contracts     │
                              └──────────────────────────────────────────────┘
```

### Layer Responsibilities & Dependency Rules

| Layer / Module | Scope & Responsibilities | Dependency Constraints |
|---|---|---|
| **Domain** (`Core.Domain`, `Catalog.Domain`) | Pure business models, aggregates, immutable value objects, domain events, domain exceptions, and invariant rules. | **Zero dependencies** on external frameworks, ORMs, or IO libraries. |
| **Application** (`Core.Application`, `Catalog.Application`) | CQRS use cases, commands, queries, mediator handlers, and validation pipeline behaviors (`ValidationBehavior`). | Depends only on **Domain**. No references to persistence, database drivers, or presentation frameworks. |
| **Platform** (`Contracts`, `ControlPlane`, `Identity`) | Multi-tenant isolation contracts (`ITenantContext`), tenant control plane entities, storefront resolution, and identity integration. | Shared cross-cutting foundation for functional modules. Read-only at runtime via `PlatformReadDbContext`. |
| **Persistence & Infra** | PostgreSQL EF Core 10 DbContext, connection interceptors (`TenantSessionInterceptor`, `AuditingSaveChangesInterceptor`, `OutboxSaveChangesInterceptor`), schema configurations, and migrations. | Implements Application abstractions using concrete PostgreSQL drivers and EF Core mappings. |
| **Presentation (API)** | Application composition root, Minimal APIs, rate limiting, security headers, authentication, and OpenTelemetry instrumentation. | References application and infrastructure modules to compose the runtime pipeline. |
| **Workers (`Outbox.Worker`)** | Background dispatcher consuming atomic outbox messages, managing distributed lease locks, and publishing to message brokers. | Independent runtime process consuming persistence and publishing via RabbitMQ. |

---

## Multi-Tenancy & Row-Level Security (RLS)

CommerceCore uses a **Pool-Based Multi-Tenancy** model where tenants share a single high-performance PostgreSQL database while enforcing isolation directly at the database engine level via **PostgreSQL Row-Level Security (RLS)**.

```
Incoming HTTP Request
          │
          ▼
┌────────────────────────────────────────────────────────────┐
│ 1. RateLimitingMiddleware (Global Sliding Window)          │
│    - Applied BEFORE tenant resolution to thwart DoS attacks│
└─────────────────────────────┬──────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────┐
│ 2. TenantResolutionMiddleware                              │
│    - Resolves Host from request header (case-insensitive)  │
│    - Queries PlatformReadDbContext (Read-Only Invariant)   │
│    - Matches Host against platform.storefronts             │
│    - Extracts user subject from JWT ('sub' / NameId)       │
│    - Verifies active membership in platform.tenant_members │
│    - Verifies parent tenant status is TenantStatuses.Active│
│    - Populates Scoped ITenantContext (TenantId, Storefront)│
└─────────────────────────────┬──────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────┐
│ 3. TenantSessionInterceptor (DbConnectionInterceptor)      │
│    - Intercepts EF Core database connection open           │
│    - Executes: SELECT set_config('app.tenant_id', @id, false)│
│    - Sets session variable for the connection lifetime     │
└─────────────────────────────┬──────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────┐
│ 4. PostgreSQL Native Row-Level Security (RLS) Engine       │
│    - Evaluates: tenant_id = NULLIF(current_setting(        │
│                 'app.tenant_id', true), '')::uuid          │
│    - Applied to ALL SELECT, INSERT, UPDATE, DELETE queries │
│    - Defense-in-depth: guarantees zero cross-tenant leak   │
└────────────────────────────────────────────────────────────┘
```

- **Defense in Depth**: Even if application-level query filters are bypassed, PostgreSQL RLS physically rejects any query or mutation attempting to touch another tenant's rows.
- **Parent Tenant Status Enforcement**: Tenant membership resolution verifies that both the membership *and* the parent tenant have active status (`TenantStatuses.Active`). Inactive or suspended tenants are immediately blocked.
- **PlatformReadDbContext Read-Only Guard**: Control plane lookups execute through a specialized DbContext that rejects all mutations (`SaveChanges` / `SaveChangesAsync` throw `NotSupportedException`).

---

## Least-Privilege Database Security

CommerceCore establishes a strict separation of database privileges between runtime application execution and administrative migrations:

```
                  ┌──────────────────────────────────────────────┐
                  │            PostgreSQL Database               │
                  └──────────────────────┬───────────────────────┘
                                         │
                 ┌───────────────────────┴───────────────────────┐
                 │                                               │
  ┌──────────────▼──────────────┐                 ┌──────────────▼──────────────┐
  │  Runtime Application Role   │                 │   Administrative Migration   │
  │     (commercecore_app)      │                 │            Role             │
  │ - NOSUPERUSER, NOCREATEDB   │                 │ - Schema creation & DDL     │
  │ - NOBYPASSRLS (enforces RLS)│                 │ - Migration execution       │
  │ - SELECT on 'platform'      │                 │ - Bootstrap CLI operations  │
  │ - CRUD on 'catalog', 'outbox'│                └─────────────────────────────┘
  └─────────────────────────────┘
```

1. **Runtime App Role (`commercecore_app`)**:
   - `NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS`
   - **`platform` Schema**: Read-only (`SELECT` only). The application cannot create, update, or delete tenants, storefronts, or memberships at runtime.
   - **`catalog` and `outbox` Schemas**: `SELECT, INSERT, UPDATE, DELETE` with RLS strictly enforced on every query.
2. **Migration Role Separation**:
   - `CommerceCoreDbContextFactory` reads `COMMERCECORE_MIGRATIONS_CONNECTION_STRING` for design-time migrations and DDL execution.
   - Prevents web application connection strings from possessing schema alteration (DDL) privileges in production.

---

## Distributed Outbox Worker & RabbitMQ Messaging

CommerceCore implements a production-grade, resilient **Transactional Outbox Worker** (`src/Workers/CommerceCore.Outbox.Worker`) capable of running as multiple horizontal worker nodes without duplicate event deliveries.

```
       Write Operation (API Request)
                     │
                     ▼
       ┌───────────────────────────┐
       │ Atomic DB Transaction     │
       │ 1. Catalog Mutation       │
       │ 2. Outbox Message Insert  │
       └─────────────┬─────────────┘
                     │
                     ▼
       ┌───────────────────────────┐
       │     outbox.messages       │
       │ (PostgreSQL Database)     │
       └─────────────┬─────────────┘
                     │
                     │  1. ClaimAsync() via FOR UPDATE SKIP LOCKED
                     │     Sets lease_id & lease_expires_on_utc
                     ▼
       ┌───────────────────────────┐
       │   OutboxDeliveryStore     │
       └─────────────┬─────────────┘
                     │
                     │  2. Map domain event to public contract
                     ▼
       ┌───────────────────────────┐
       │    OutboxEventMapper      │
       │ (catalog.product.*.v1)    │
       └─────────────┬─────────────┘
                     │
                     │  3. PublishAsync() with Publisher Confirmations
                     ▼
       ┌───────────────────────────┐
       │   RabbitMqEventPublisher  │
       └─────────────┬─────────────┘
                     │
                     ▼
       ┌───────────────────────────┐
       │ RabbitMQ Topic Exchange   │
       │ (Durable, AutoDelete: F)  │
       └─────────────┬─────────────┘
                     │
                     │  4. CompleteAsync() - Clears lease, sets processed_on_utc
                     ▼
       ┌───────────────────────────┐
       │   outbox.messages         │
       │ (Marked Processed)        │
       └───────────────────────────┘
```

### Key Outbox Engineering Capabilities:

1. **Lock-Free Distributed Leasing (`FOR UPDATE SKIP LOCKED`)**:
   - Outbox messages are claimed using PostgreSQL common table expressions (CTEs) with `FOR UPDATE SKIP LOCKED`. Multiple worker instances process messages concurrently without lock contention or deadlocks.
   - Each claim sets a unique `lease_id` and `lease_expires_on_utc`.
2. **Automatic Crash Recovery & Resilient Leases**:
   - If a worker crashes or encounters network partition while processing, the lease expires automatically (`lease_expires_on_utc <= statement_timestamp()`). Another worker claims and retries the message seamlessly.
3. **Exponential Backoff & Dead-Letter Queueing**:
   - Failed publications increment `attempt_count` and calculate `next_attempt_on_utc` using exponential backoff (from 5s up to 300s).
   - Poison messages exceeding `MaximumAttempts = 5` are automatically moved to terminal dead-letter state (`dead_lettered_on_utc = statement_timestamp()`).
4. **Resilient RabbitMQ Publisher (RabbitMQ.Client 7.x)**:
   - Built on modern asynchronous channel APIs with **Publisher Confirmations** (`CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true)`).
   - Publishes to durable Topic Exchanges declared by `RabbitMqTopologyInitializer`.
   - Strict 10-second confirmation timeouts with Semaphore-protected single-channel concurrency.
5. **Versioned Integration Event Contracts**:
   - `OutboxEventMapper` transforms internal domain events into public integration event contracts:
     - `ProductCreatedDomainEvent` $
ightarrow$ `catalog.product.created.v1`
     - `ProductArchivedDomainEvent` $
ightarrow$ `catalog.product.archived.v1`
   - Strict payload validation ensuring event ID equals outbox message ID, valid timestamps, and non-empty tenant IDs.

---

## Key Engineering Highlights

- **Compile-Time Source-Generated Mediator**: Utilizes `Mediator.SourceGenerator` for zero-reflection, high-throughput CQRS dispatching with compile-time pipeline behaviors (`ValidationBehavior`).
- **PostgreSQL Row-Level Security (RLS)**: Automatic tenant session binding (`set_config('app.tenant_id', ...)`) ensuring bulletproof multi-tenant isolation.
- **Distributed Outbox Worker**: High-throughput message dispatching using `FOR UPDATE SKIP LOCKED`, distributed leases, exponential backoff, dead-letter tracking, and RabbitMQ publisher confirmations.
- **Least-Privilege Database Role**: Hardened runtime app role (`commercecore_app`) with read-only access to control plane metadata and enforced RLS.
- **Read-Only Context Guard**: `PlatformReadDbContext` physically rejects write operations on control-plane metadata at runtime.
- **Explicit Product Variants**: Matrix variations with custom SKUs, variant pricing, currency parity validation, default variant assignment, and dynamic variant option bags.
- **Dynamic Attribute Schema & Versioning**: Strongly-typed attributes (`Text`, `Integer`, `Decimal`, `Boolean`, `SingleSelect`, `MultiSelect`, `Measurement`) with scopes, validation bounds, enforcement states (`Draft`, `Backfilling`, `Enforced`, `Deprecated`), and compiled JSONB effective schemas.
- **Native UUIDv7 Primary Keys**: Uses .NET 10's native `Guid.CreateVersion7()` for time-sortable sequential identifiers, eliminating B-Tree index fragmentation and boosting PostgreSQL write throughput.
- **Hierarchical Product Taxonomies (`ltree`)**: Dynamic category and product type hierarchies backed by PostgreSQL's native `ltree` extension with GiST indexing for fast subtree queries.
- **Multilingual Localization via PostgreSQL JSONB**: Localized fields (`LocalizedText`, `LanguageCode`) stored directly as native PostgreSQL `jsonb` with custom EF Core value converters, value comparers, and RFC language-tag validation.
- **Transactional Outbox Interceptor**: Domain events are automatically serialized into `outbox.messages` within the same atomic database transaction via EF Core interceptors (`OutboxSaveChangesInterceptor`).
- **Automated Auditing Interceptor**: EF Core `AuditingSaveChangesInterceptor` automatically stamps `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, and `UpdatedBy` across entities and nested owned entities without polluting command handlers.
- **Optimistic Concurrency via PostgreSQL `xmin`**: Uses PostgreSQL system column `xmin` (`IsRowVersion()`) to detect concurrent modifications and automatically return HTTP `409 Conflict` problem details.
- **Pre-Resolution Rate Limiting**: Built-in sliding-window rate limiter executing before tenant resolution to prevent tenant enumeration and denial of service.
- **Client-Aborted Request Suppression**: Global exception handler gracefully detects cancelled client requests, suppressing unnecessary error payloads and noisy logs.
- **OpenTelemetry & Observability**: Complete distributed tracing, metrics, and structured logging integrated with OTLP exporters.
- **High-Performance Logging**: Zero-allocation compile-time `[LoggerMessage]` source-generated logging in exception handlers.
- **Automated Verification**: **250+ tests** across architectural boundaries (`NetArchTest`), domain invariants (`xUnit v3`), API integration, RabbitMQ integration, and PostgreSQL integration tests (`Testcontainers`).

---

## Solution & Project Decomposition

```text
CommerceCore/
├── CommerceCore.slnx                                   # Modern solution manifest (.slnx)
├── docker-compose.yml                                  # Local infrastructure (PostgreSQL 18.6, RabbitMQ 4.3, pgAdmin 4)
├── Dockerfile                                          # Multi-stage production container build
├── Directory.Build.props                               # WarningsAsErrors & AnalysisLevel latest-recommended
├── .editorconfig                                       # Standardized code style & analyzer rules
├── dotnet-tools.json                                   # Local CLI tools (dotnet-ef)
├── global.json                                         # Microsoft Testing Platform configuration
│
├── src/
│   ├── Core/
│   │   ├── CommerceCore.Domain/                        # Shared AggregateRoot, BaseEntity, ValueObjects, Events
│   │   └── CommerceCore.Application/                   # Core CQRS Abstractions, ValidationBehavior, ValidationRules
│   │
│   ├── Platform/
│   │   ├── CommerceCore.Platform.Contracts/            # Multi-Tenancy abstractions (ITenantContext, TenantId, MarketId)
│   │   ├── CommerceCore.Platform.ControlPlane/         # Tenant, Storefront, Membership entities & store
│   │   └── CommerceCore.Platform.Identity/             # TenantResolutionMiddleware, Identity & Scope extensions
│   │
│   ├── Modules/
│   │   └── Catalog/
│   │       ├── CommerceCore.Modules.Catalog.Domain/    # Product, ProductVariant, ProductType, Attribute aggregates
│   │       └── CommerceCore.Modules.Catalog.Application/ # Catalog CQRS Commands, Queries, Handlers & Validators
│   │
│   ├── Infrastructure/
│   │   ├── CommerceCore.Infrastructure/                # System implementations (SystemClock)
│   │   └── CommerceCore.Persistence/                   # PostgreSQL EF Core 10 DbContext, PlatformReadDbContext, Outbox
│   │
│   ├── Presentation/
│   │   └── CommerceCore.Api/                           # Minimal APIs, RateLimiting, Security, Observability, Program.cs
│   │
│   └── Workers/
│       └── CommerceCore.Outbox.Worker/                 # Asynchronous Outbox Dispatcher & RabbitMQ Topic Publisher
│
├── tests/
│   ├── CommerceCore.Domain.UnitTests/                  # 150 Tests: Domain entities, invariants, value objects
│   ├── CommerceCore.Persistence.IntegrationTests/      # 47+ Tests: EF Core, PostgreSQL RLS, Outbox with Testcontainers
│   ├── CommerceCore.Api.UnitTests/                     # 37 Tests: Endpoint parsers, auth regression, entity validation
│   ├── CommerceCore.Outbox.Worker.IntegrationTests/    # 10+ Tests: Outbox dispatcher, event mapper, RabbitMQ publisher
│   └── CommerceCore.ArchitectureTests/                 # 10 Tests: Architecture & Layer boundary rules (NetArchTest)
│
└── tools/
    └── CommerceCore.Bootstrap/                         # CLI utility for tenant, storefront & admin provisioning
```

---

## Technology Matrix

| Technology / Library | Version | Purpose |
|---|---|---|
| **.NET SDK** | 10.0 (`net10.0`) | Primary runtime framework & C# 13/14 language features |
| **ASP.NET Core** | 10.0 | High-performance Minimal APIs & OpenAPI integration |
| **Entity Framework Core** | 10.0.11 | Modern ORM with PostgreSQL provider (`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3) |
| **PostgreSQL** | 18.6 | Relational database with native `ltree`, `jsonb`, Row-Level Security (RLS), and `xmin` |
| **RabbitMQ** | 4.3 (Management) | Message broker with AMQP 0-9-1 Topic Exchanges & publisher confirmations |
| **RabbitMQ.Client** | 7.1.1 | Official asynchronous .NET client for RabbitMQ messaging |
| **Mediator (Source Generator)** | 3.0.2 | Zero-reflection compile-time CQRS messaging & pipeline execution |
| **FluentValidation** | 12.1.1 | Strongly-typed request & business validation rules |
| **OpenTelemetry** | 1.11.2 | Distributed tracing, metrics, and structured logging export |
| **xUnit** | v3 (4.0.0) | Modern testing framework |
| **NetArchTest.eNhancedEdition**| 1.4.5 | Automated architecture rule enforcement |
| **Testcontainers** | 4.14.0 | Disposable PostgreSQL and RabbitMQ containers for integration tests |
| **Docker Compose** | - | Local development infrastructure (PostgreSQL, RabbitMQ & pgAdmin) |

---

## Domain Model & Invariants

### 1. Product & Product Variant Aggregates

#### `Product` Aggregate
Inherits `SoftDeletableAggregateRoot<ProductId>`:
- **Identity**: `ProductId` (wrapping UUIDv7 `Guid`).
- **Multi-Tenancy**: Scoped to `TenantId`.
- **Name**: `LocalizedText` stored as JSONB with default language guarantee.
- **Specifications**: Dynamic `AttributeValueBag` validated against the effective schema version of the assigned `ProductType`.
- **Variants**: Collection of `ProductVariant` entities with a single default variant.
- **Status**: `ProductStatus` (`Draft = 1`, `Active = 2`, `Inactive = 3`).
- **Audit & Soft-Delete**: `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy`, `IsDeleted`, `DeletedAtUtc`, `DeletedBy`.

#### `ProductVariant` Entity
Inherits `BaseEntity<ProductVariantId>`:
- **Identity**: `ProductVariantId` (UUIDv7).
- **SKU**: `VariantSku` (unique within tenant).
- **Pricing**: Base variant `Money` (positive amount, 3-letter currency).
- **Currency Parity**: Variant price currency is strictly validated to match the product's base price currency.
- **Variant Options**: `AttributeValueBag` storing variation attributes (e.g., `size`, `color`).
- **Status**: `ProductVariantStatus` (`Draft = 1`, `Active = 2`, `Inactive = 3`).
- **Default Flag**: `IsDefault` ensuring each product has exactly one primary variant.

#### Lifecycle Invariants:
1. **Creation**: Products are initialized in `Draft` status and emit `ProductCreatedDomainEvent`.
2. **Variants**: A product cannot be activated without an active default variant having a positive price.
3. **Currency Invariant**: Variant currency must strictly equal the product base price currency.
4. **Soft Deletion**: Idempotent archiving stamps UTC timestamp and actor, emitting `ProductArchivedDomainEvent`. Modifying archived products is forbidden.
5. **Restoration**: Restoring an archived product that was previously `Active` resets its status to `Inactive` to prevent accidental immediate exposure.

---

### 2. ProductType & Dynamic Attribute Schema Engine

Inherits `AggregateRoot<ProductTypeId>`:
- **Identity**: `ProductTypeId` (UUIDv7).
- **Multi-Tenancy**: Scoped to `TenantId`.
- **Code**: Unique normalized identifier `ProductTypeCode` (e.g., `apparel`, `shoes`).
- **Hierarchy & Taxonomies**: Modeled via `ParentProductTypeId` and PostgreSQL `ltree` `path`.
- **Assignment Control**: `IsAssignable` indicates whether concrete products can be assigned to this type.
- **Effective Schema**: `OwnSchemaVersion` increments on changes, compiling into `ProductTypeEffectiveSchema` for fast runtime validation.
- **Concurrency Coordinator**: Attribute definitions are coordinated within critical sections, preventing race conditions during concurrent additions.

#### Attribute Definitions (`AttributeDefinition`):
- **Key & Ordering**: Strongly-typed `AttributeKey` and unique `DisplayOrder`.
- **Data Types**: `Text`, `Integer`, `Decimal`, `Boolean`, `SingleSelect`, `MultiSelect`, `Measurement`.
- **Scope**: `ProductSpecification` (product-level property) or `VariantOption` (matrix variation axis).
- **Validation Bounds**: `MinimumValue`, `MaximumValue`, `MinimumLength`, `MaximumLength`, and `MeasurementUnitFamily`.
- **Enforcement Lifecycle**: `Draft` $
ightarrow$ `Backfilling` $
ightarrow$ `Enforced` $
ightarrow$ `Deprecated`.

---

### 3. Platform Control Plane & Multi-Tenancy

- **`Tenant`**: Encapsulated entity with factory method `Tenant.Create(id, slug, name)`. Normalized slug and lifecycle methods (`Activate()`, `Deactivate()`).
- **`Storefront`**: Encapsulated entity with factory method `Storefront.Create(id, tenantId, hostName, marketId)`. Validated host name (`Uri.CheckHostName`) and lifecycle methods.
- **`TenantMembership`**: Encapsulated entity with factory method `TenantMembership.Create(tenantId, userSubject, role)`.
- **Type-Safe Constants**: `TenantStatuses.Active`, `TenantStatuses.Inactive`, `TenantMembershipStatuses.Active`, `TenantMembershipStatuses.Inactive`, `TenantMembershipRoles.Admin`.

---

### 4. Core Value Objects

- **`Money`**: Non-negative amount, maximum scale of 4 decimal places, uppercase ISO 4217 currency code.
- **`LanguageCode`**: RFC-compliant language tags (`en`, `az`, `en-US`).
- **`LocalizedText`**: Immutable multilingual map ensuring default language presence and fallback resolution.
- **`VariantSku`**: Normalized alphanumeric SKU identifier.
- **`AttributeValueBag`**: Strongly-typed container for dynamic product specifications and variant options supporting JSONB serialization.
- **Strongly-Typed IDs**: `ProductId`, `ProductVariantId`, `ProductTypeId`, `TenantId`, `StorefrontId`, `MarketId` wrapping UUIDv7.

---

## Persistence & Database Architecture

### Relational Schemas & Tables

#### `platform.tenants`
```sql
CREATE TABLE platform.tenants (
    id uuid NOT NULL,
    name varchar(200) NOT NULL,
    slug varchar(100) NOT NULL,
    status varchar(50) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT pk_tenants PRIMARY KEY (id),
    CONSTRAINT ck_platform_tenants_status CHECK (status IN ('Active', 'Inactive'))
);
CREATE UNIQUE INDEX ix_platform_tenants_slug ON platform.tenants (slug);
```

#### `platform.storefronts`
```sql
CREATE TABLE platform.storefronts (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    host_name varchar(255) NOT NULL,
    market_code varchar(10) NOT NULL,
    default_locale varchar(20) NOT NULL,
    is_active boolean NOT NULL,
    CONSTRAINT pk_storefronts PRIMARY KEY (id),
    CONSTRAINT fk_storefronts_tenants FOREIGN KEY (tenant_id) REFERENCES platform.tenants (id) ON DELETE CASCADE,
    CONSTRAINT ck_platform_storefronts_host_name_lowercase CHECK (host_name = lower(host_name))
);
CREATE UNIQUE INDEX ix_platform_storefronts_host_name ON platform.storefronts (host_name);
```

#### `platform.tenant_memberships`
```sql
CREATE TABLE platform.tenant_memberships (
    tenant_id uuid NOT NULL,
    user_subject varchar(200) NOT NULL,
    role varchar(50) NOT NULL,
    status varchar(50) NOT NULL,
    CONSTRAINT pk_tenant_memberships PRIMARY KEY (tenant_id, user_subject),
    CONSTRAINT fk_tenant_memberships_tenants FOREIGN KEY (tenant_id) REFERENCES platform.tenants (id) ON DELETE CASCADE,
    CONSTRAINT ck_platform_tenant_memberships_status CHECK (status IN ('Active', 'Inactive'))
);
```

#### `catalog.products`
```sql
CREATE TABLE catalog.products (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    product_type_id uuid NOT NULL,
    status varchar(16) NOT NULL,
    name jsonb NOT NULL,
    specifications jsonb NOT NULL DEFAULT '{}'::jsonb,
    validated_against_version bigint NOT NULL DEFAULT 0,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    deleted_at_utc timestamp with time zone NULL,
    deleted_by varchar(200) NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by varchar(200) NULL,
    updated_at_utc timestamp with time zone NULL,
    updated_by varchar(200) NULL,
    xmin xid NOT NULL, -- PostgreSQL concurrency token
    CONSTRAINT pk_products PRIMARY KEY (id),
    CONSTRAINT ux_products_tenant_id_id UNIQUE (tenant_id, id)
);
```

#### `catalog.product_variants`
```sql
CREATE TABLE catalog.product_variants (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    product_id uuid NOT NULL,
    sku varchar(128) NOT NULL,
    status varchar(16) NOT NULL,
    price_amount numeric(18,4) NOT NULL,
    price_currency varchar(3) NOT NULL,
    options jsonb NOT NULL DEFAULT '{}'::jsonb,
    is_default boolean NOT NULL DEFAULT FALSE,
    xmin xid NOT NULL,
    CONSTRAINT pk_product_variants PRIMARY KEY (id),
    CONSTRAINT ux_product_variants_tenant_id_id UNIQUE (tenant_id, id),
    CONSTRAINT fk_product_variants_products FOREIGN KEY (tenant_id, product_id)
        REFERENCES catalog.products (tenant_id, id) ON DELETE CASCADE
);
```

#### `outbox.messages`
```sql
CREATE TABLE outbox.messages (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    occurred_on_utc timestamp with time zone NOT NULL,
    type varchar(500) NOT NULL,
    content jsonb NOT NULL,
    processed_on_utc timestamp with time zone NULL,
    attempt_count integer NOT NULL DEFAULT 0,
    last_error text NULL,
    lease_id uuid NULL,
    lease_expires_on_utc timestamp with time zone NULL,
    next_attempt_on_utc timestamp with time zone NULL,
    dead_lettered_on_utc timestamp with time zone NULL,
    CONSTRAINT pk_messages PRIMARY KEY (id),
    CONSTRAINT ck_outbox_messages_attempt_count CHECK (attempt_count >= 0),
    CONSTRAINT ck_outbox_messages_lease_pair CHECK ((lease_id IS NULL) = (lease_expires_on_utc IS NULL)),
    CONSTRAINT ck_outbox_messages_terminal_state CHECK (
        NOT (processed_on_utc IS NOT NULL AND dead_lettered_on_utc IS NOT NULL)
        AND (
            (processed_on_utc IS NULL AND dead_lettered_on_utc IS NULL)
            OR (lease_id IS NULL AND next_attempt_on_utc IS NULL)
        )
    )
);
```

---

### PostgreSQL Row-Level Security (RLS) Engine

Every tenant-partitioned table (`catalog.products`, `catalog.product_variants`, `catalog.product_types`, `catalog.attribute_definitions`, `catalog.attribute_options`, `catalog.product_type_effective_schema`, `outbox.messages`) has RLS enforced:

```sql
ALTER TABLE catalog.products ENABLE ROW LEVEL SECURITY;
ALTER TABLE catalog.products FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_policy ON catalog.products
    FOR ALL
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
```

---

### Strategic Indexing & Query Optimizations

- `ix_products_tenant_status_is_deleted` on `(tenant_id, status, is_deleted)`: Fast catalog filtering.
- `ux_product_variants_tenant_sku` on `(tenant_id, sku)` (Unique): Enforces SKU uniqueness per tenant.
- `ux_product_variants_tenant_default_per_product` on `(tenant_id, product_id) WHERE is_default = TRUE`: Ensures only one default variant per product.
- `ux_product_types_tenant_code` on `(tenant_id, code)` (Unique): Unique category codes per tenant.
- `ix_product_types_path_gist` on `path USING gist`: High-performance hierarchical subtree operations (`@>`, `<@`, `~`).
- `ix_outbox_messages_tenant_dispatch` on `(tenant_id, next_attempt_on_utc, occurred_on_utc, id) WHERE processed_on_utc IS NULL AND dead_lettered_on_utc IS NULL`: High-speed partial index for the outbox worker leasing engine.

---

## Security, Resilience & Observability

### Rate Limiting Before Tenant Resolution (DoS Mitigation)

The rate limiter is mounted **before** tenant resolution middleware in `Program.cs`:
- Unauthenticated or malicious actors sending high-volume traffic cannot trigger expensive storefront database lookups.
- **Partitioning**: Grouped by authenticated User ID (`sub`), Client ID (`client_id`), or client IP address.
- **Permit Limits**: 300 requests/minute for read operations (`GET`), 60 requests/minute for write operations (`POST`, `PUT`, `DELETE`).
- **Response**: Returns HTTP `429 Too Many Requests` with RFC 7807 problem details and `Retry-After` header.

### Authentication & Scoped Authorization

Endpoints enforce JWT Bearer authentication with scope-based authorization policies:
- `catalog.read`: Read-only access to catalog products, variants, and product types.
- `catalog.manage`: Write permissions for creating and modifying products, variants, prices, and specifications.
- `catalog.schema.manage`: Administrative permissions to define product types, attributes, and options.

### Security Headers & Kestrel Hardening

Configured via `SecurityHeadersMiddleware`:
- `Content-Security-Policy: default-src 'self'`
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Strict-Transport-Security: max-age=31536000; includeSubDomains`
- `Referrer-Policy: strict-origin-when-cross-origin`
- Server header stripped from Kestrel response (`AddServerHeader = false`).

### OpenTelemetry & Health Probes

- **Tracing & Metrics**: Integrated with ASP.NET Core, HttpClient, and Runtime meters exporting via OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`).
- **Liveness Probe**: `/health/live` returns HTTP 200 indicating the process is running.
- **Readiness Probe**: `/health/ready` evaluates the PostgreSQL database connection probe (`PostgreSqlHealthCheck`).

### High-Performance Logging

Global exception handling uses compile-time source-generated logging (`[LoggerMessage]`):
- Avoids boxing and heap allocations during error formatting.
- Automatically preserves `traceId` correlation tags across all logs.
- Suppresses error responses when the client disconnects or aborts the HTTP request.

---

## API Reference & Contracts

### Product & Variant Endpoints

Base route: `/api/products`

| Method | Endpoint | Authorization | Description |
|---|---|---|---|
| `POST` | `/api/products` | `catalog.manage` | Create a new product in draft status |
| `GET` | `/api/products/{productId}` | `catalog.read` | Get product details with dynamic specifications |
| `POST` | `/api/products/{productId}/activate` | `catalog.manage` | Activate product (requires active default variant) |
| `POST` | `/api/products/{productId}/deactivate` | `catalog.manage` | Deactivate active product |
| `POST` | `/api/products/{productId}/archive` | `catalog.manage` | Archive (soft delete) product |
| `POST` | `/api/products/{productId}/restore` | `catalog.manage` | Restore an archived product |
| `PUT` | `/api/products/{productId}/name` | `catalog.manage` | Update localized product name |
| `PUT` | `/api/products/{productId}/price` | `catalog.manage` | Update product base price |
| `PUT` | `/api/products/{productId}/specifications` | `catalog.manage` | Set dynamic product specifications |
| `GET` | `/api/products/{productId}/variants` | `catalog.read` | List all product variants |
| `POST` | `/api/products/{productId}/variants` | `catalog.manage` | Add a new product variant |
| `GET` | `/api/products/{productId}/variants/{variantId}` | `catalog.read` | Get variant details |
| `POST` | `/api/products/{productId}/variants/{variantId}/activate` | `catalog.manage` | Activate variant |
| `POST` | `/api/products/{productId}/variants/{variantId}/deactivate` | `catalog.manage` | Deactivate variant |
| `PUT` | `/api/products/{productId}/variants/{variantId}/default` | `catalog.manage` | Set variant as product default |

---

### Product Type & Attribute Endpoints

Base route: `/api/product-types`

| Method | Endpoint | Authorization | Description |
|---|---|---|---|
| `POST` | `/api/product-types` | `catalog.schema.manage` | Create root or child product type |
| `POST` | `/api/product-types/{id}/attributes` | `catalog.schema.manage` | Define an attribute on product type |
| `POST` | `/api/product-types/{id}/attributes/{attrId}/options` | `catalog.schema.manage` | Add predefined select option |

---

### Health Check Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/health/live` | Liveness health probe (returns 200 OK) |
| `GET` | `/health/ready` | Readiness probe (verifies PostgreSQL database connectivity) |

---

### Sample Payloads & Responses

#### 1. Create Product (`POST /api/products`)
```json
{
  "productTypeId": "019139f6-3c00-74a9-832c-9a489d28e751",
  "defaultLanguage": "en",
  "nameTranslations": {
    "en": "Mechanical Keyboard",
    "az": "Mexaniki Klaviatura"
  },
  "priceAmount": 149.99,
  "currency": "USD"
}
```
**Response (`201 Created`):**
```json
{
  "productId": "019139f4-1800-7521-97b7-5bfcfca91b92"
}
```

#### 2. Add Product Variant (`POST /api/products/{productId}/variants`)
```json
{
  "sku": "KB-RGB-RED",
  "priceAmount": 159.99,
  "currency": "USD",
  "isDefault": true,
  "options": {
    "switch_color": "red",
    "layout": "ansi"
  }
}
```
**Response (`201 Created`):**
```json
{
  "productId": "019139f4-1800-7521-97b7-5bfcfca91b92",
  "productVariantId": "019139f9-9a10-73ef-bc21-0a1982b1c411",
  "sku": "KB-RGB-RED",
  "status": "Draft",
  "isDefault": true
}
```

#### 3. Set Dynamic Product Specifications (`PUT /api/products/{productId}/specifications`)
```json
{
  "specifications": {
    "wireless": true,
    "battery_capacity": {
      "value": 4000,
      "unit": "mAh"
    },
    "weight_grams": 850
  }
}
```
**Response (`200 OK`):**
```json
{
  "productId": "019139f4-1800-7521-97b7-5bfcfca91b92",
  "validatedAgainstVersion": 2,
  "changed": true
}
```

---

## Error Handling & RFC 7807 Problem Details

All application errors strictly adhere to the **RFC 7807 Problem Details** specification with unique `traceId` correlation tags:

#### Validation Error (`400 Bad Request`)
```json
{
  "type": "/problems/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "instance": "/api/products",
  "errors": {
    "PriceAmount": [
      "Price cannot have more than 4 decimal places."
    ]
  },
  "traceId": "0HN0000000001:00000001"
}
```

#### Domain Business Rule Violation (`422 Unprocessable Entity`)
```json
{
  "type": "/problems/product.activation_requires_price",
  "title": "A business rule was violated.",
  "status": 422,
  "detail": "A product cannot be activated without an active default variant.",
  "instance": "/api/products/019139f4-1800-7521-97b7-5bfcfca91b92/activate",
  "code": "product.activation_requires_price",
  "traceId": "0HN0000000001:00000002"
}
```

#### Concurrency Conflict (`409 Conflict`)
```json
{
  "type": "/problems/concurrency-conflict",
  "title": "The resource was modified by another request.",
  "status": 409,
  "detail": "Reload the resource and try again.",
  "instance": "/api/products/019139f4-1800-7521-97b7-5bfcfca91b92/price",
  "traceId": "0HN0000000001:00000003"
}
```

---

## CLI Tooling: CommerceCore.Bootstrap

CommerceCore includes an administrative CLI tool (`tools/CommerceCore.Bootstrap`) to safely provision new tenants, storefronts, and admin memberships:

```bash
# Set environment variables for the bootstrap tool
export COMMERCECORE_BOOTSTRAP_ConnectionStrings__CommerceCoreDatabase="Host=localhost;Port=5432;Database=commercecore;Username=commercecore;Password=your_password"
export COMMERCECORE_BOOTSTRAP_TENANT_SLUG="acme-corp"
export COMMERCECORE_BOOTSTRAP_TENANT_NAME="Acme Corporation"
export COMMERCECORE_BOOTSTRAP_HOST_NAME="acme.store.local"
export COMMERCECORE_BOOTSTRAP_MARKET_CODE="US"
export COMMERCECORE_BOOTSTRAP_DEFAULT_LOCALE="en-US"
export COMMERCECORE_BOOTSTRAP_ADMIN_SUBJECT="auth0|64f1234567890abcdef"

# Run bootstrap
dotnet run --project tools/CommerceCore.Bootstrap
```

### Safety Invariants Enforced by Bootstrap CLI:
1. **Pending Migrations Check**: Halts immediately if unapplied migrations exist.
2. **Atomic Execution**: Wraps tenant, storefront, and membership creation in a single database transaction.
3. **Collision Prevention**: Prevents reusing existing slugs with differing tenant names or assigning an admin to multiple tenants.
4. **Normalized Hostnames**: Validates and normalizes storefront hostnames without protocol or port.

---

## Getting Started & Local Setup

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 1. Clone the Repository
```bash
git clone https://github.com/MahirSafar/CommerceCore.git
cd CommerceCore
```

### 2. Configure Environment & Start Infrastructure
Copy `.env.example` to `.env` and start PostgreSQL 18.6, RabbitMQ 4.3, and pgAdmin 4:
```bash
cp .env.example .env
docker compose up -d
```
- **PostgreSQL**: `localhost:5432` (User: `commercecore`, Database: `commercecore`)
- **RabbitMQ**: `localhost:5672` (AMQP) | `http://localhost:15672` (Management UI)
- **pgAdmin 4**: `http://localhost:5050` (Email: `admin@example.test`)

### 3. Apply EF Core Database Migrations
Restore tools and apply migrations using the administrative migration connection:
```bash
dotnet tool restore
dotnet ef database update --project src/Infrastructure/CommerceCore.Persistence --startup-project src/Presentation/CommerceCore.Api
```

### 4. Bootstrap Initial Tenant & Storefront
```bash
dotnet run --project tools/CommerceCore.Bootstrap
```

### 5. Run the API and Outbox Worker
Run the Minimal API web application:
```bash
dotnet run --project src/Presentation/CommerceCore.Api
```
The API will start at `https://localhost:7198` (or `http://localhost:5247`).
OpenAPI documentation is available at `/openapi/v1.json`.

In a separate terminal, launch the Outbox Dispatcher Worker:
```bash
dotnet run --project src/Workers/CommerceCore.Outbox.Worker
```

---

## CI/CD Automation & Quality Gates

The GitHub Actions workflow (`.github/workflows/ci.yml`) enforces automated quality gates on every push and pull request:

1. **Deterministic Build**: Compiles solution in `Release` configuration with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<AnalysisLevel>latest-recommended</AnalysisLevel>`.
2. **C# Formatting Gate**: Asserts zero formatting divergence:
   ```bash
   dotnet format CommerceCore.slnx --verify-no-changes --no-restore --severity warn
   ```
3. **Pending Model Changes Check**: Verifies the EF Core model is 100% in sync with code migrations:
   ```bash
   dotnet ef migrations has-pending-model-changes --no-build
   ```
4. **Idempotent Migration Script Generation**: Asserts migration SQL generation succeeds without errors:
   ```bash
   dotnet ef migrations script --idempotent --no-build
   ```
5. **Container Image Build**: Validates multi-stage production `Dockerfile` builds cleanly.
6. **Automated Test Suite**: Executes all unit, integration, and architecture tests in `Release` mode.

---

## Testing Strategy & Quality Assurance

The repository includes a comprehensive, multi-tiered testing suite with **250+ passing automated tests**:

```text
tests/
├── CommerceCore.Domain.UnitTests/             # 150 Tests: Domain entities, invariants, value objects
├── CommerceCore.Persistence.IntegrationTests/ # 47+ Tests: EF Core, PostgreSQL RLS, Outbox with Testcontainers
├── CommerceCore.Api.UnitTests/                # 37 Tests: Endpoint parsers, auth regression, entity validation
├── CommerceCore.Outbox.Worker.IntegrationTests/# 10+ Tests: Outbox dispatcher, event mapper, RabbitMQ publisher
└── CommerceCore.ArchitectureTests/            # 10 Tests: Architecture & Layer boundary rules (NetArchTest)
```

### Run Architecture Tests
Verifies Clean Architecture rules, ensuring Domain and Application layers maintain zero forbidden dependencies:
```bash
dotnet test tests/CommerceCore.ArchitectureTests/CommerceCore.ArchitectureTests.csproj
```

### Run Domain Unit Tests
Tests domain aggregates, value objects (`Money`, `LocalizedText`, `LanguageCode`, `AttributeValueBag`), variant currency parity, and invariant validations:
```bash
dotnet test tests/CommerceCore.Domain.UnitTests/CommerceCore.Domain.UnitTests.csproj
```

### Run API Unit Tests
Verifies endpoint request/response parsers, rate limiting, and authorization policies:
```bash
dotnet test tests/CommerceCore.Api.UnitTests/CommerceCore.Api.UnitTests.csproj
```

### Run Outbox Worker Integration Tests (Requires Docker)
Spawns RabbitMQ Testcontainers, initializes topology, and verifies resilient publisher confirmations and event mapping:
```bash
dotnet test tests/CommerceCore.Outbox.Worker.IntegrationTests/CommerceCore.Outbox.Worker.IntegrationTests.csproj
```

### Run Persistence Integration Tests (Requires Docker)
Spawns isolated PostgreSQL containers via Testcontainers, applies migrations, and verifies RLS tenant isolation, outbox transactions, and least-privilege security permissions:
```bash
dotnet test tests/CommerceCore.Persistence.IntegrationTests/CommerceCore.Persistence.IntegrationTests.csproj
```

### Run All Tests
```bash
dotnet test CommerceCore.slnx
```

---

## Engineering Practices & Design Patterns

- **Modular Monolith**: Clear module boundaries allowing independent evolution and straightforward future microservice extraction.
- **Clean Architecture & DDD**: Pure domain model, explicit Aggregate Roots, encapsulated business invariants, and immutable Value Objects.
- **CQRS (Command Query Responsibility Segregation)**: Distinct write commands and read queries with optimized query pipelines.
- **Compile-Time Source Generation**: Zero-reflection CQRS dispatching with `Mediator.SourceGenerator`.
- **Distributed Outbox Pattern**: PostgreSQL `FOR UPDATE SKIP LOCKED` distributed leasing paired with RabbitMQ topic messaging.
- **Pool Multi-Tenancy with RLS**: PostgreSQL Row-Level Security enforcing tenant boundaries directly at the database engine.
- **Least-Privilege Security**: Hardened runtime app role preventing unauthorized administrative access to platform metadata.
- **Read-Only Context Guard**: Invariant protection ensuring platform metadata is never mutated during API request dispatch.
- **Hierarchical Taxonomies (`ltree`)**: Native PostgreSQL path indexing for high-speed taxonomy trees.
- **Optimistic Concurrency Control**: Automatic conflict detection and 409 handling via PostgreSQL `xmin`.
- **Automated Auditing**: Created and Updated timestamps/actors automatically injected via EF Core Interceptors.
- **UUIDv7 Primary Keys**: Time-ordered UUIDs for optimal database index locality and clustered index performance.
- **RFC 7807 Standard Error Responses**: Uniform problem details contract across all endpoints.

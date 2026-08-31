# CommerceCore

[![CommerceCore CI](https://github.com/MahirSafar/CommerceCore/actions/workflows/ci.yml/badge.svg)](https://github.com/MahirSafar/CommerceCore/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![C# 13/14](https://img.shields.io/badge/C%23-13%2F14-239120?style=flat&logo=csharp)](https://docs.microsoft.com/dotnet/csharp/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18.6-4169E1?style=flat&logo=postgresql)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF%20Core-10.0-512BD4?style=flat&logo=dotnet)](https://docs.microsoft.com/ef/core/)
[![Architecture](https://img.shields.io/badge/Architecture-Modular%20Monolith%20%7C%20Clean%20%7C%20DDD%20%7C%20CQRS-blueviolet?style=flat)]()
[![Multi-Tenancy](https://img.shields.io/badge/Multi--Tenancy-Pool%20%2B%20PostgreSQL%20RLS-orange?style=flat)]()
[![Tests](https://img.shields.io/badge/Tests-225%20Passed%20%7C%20xUnit%20v3%20%7C%20Testcontainers-brightgreen?style=flat)]()

**CommerceCore** is an enterprise-grade, high-performance modular e-commerce backend platform built with **.NET 10**, engineered around the principles of **Modular Monolith**, **Clean Architecture**, **Domain-Driven Design (DDD)**, and **CQRS (Command Query Responsibility Segregation)**.

Engineered for extreme reliability, throughput, and multi-tenant isolation, the system provides native database-level **PostgreSQL Row-Level Security (RLS)**, compile-time source-generated mediation, a dynamic attribute & schema compilation engine, explicit **Product Variants**, rich PostgreSQL JSONB specifications & localization, time-sortable **UUIDv7** identities, hierarchical taxonomy trees with PostgreSQL `ltree`, transactional outbox messaging, automated auditing interceptors, OpenTelemetry observability, rate limiting, and optimistic concurrency control.

---

## Table of Contents

- [Architectural Blueprint](#architectural-blueprint)
- [Multi-Tenancy & Row-Level Security (RLS)](#multi-tenancy--row-level-security-rls)
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
  - [Authentication & Scoped Authorization](#authentication--scoped-authorization)
  - [Partitioned Rate Limiting](#partitioned-rate-limiting)
  - [Security Headers & Hardening](#security-headers--hardening)
  - [OpenTelemetry & Health Probes](#opentelemetry--health-probes)
- [API Reference & Contracts](#api-reference--contracts)
  - [Product & Variant Endpoints](#product--variant-endpoints)
  - [Product Type & Attribute Endpoints](#product-type--attribute-endpoints)
  - [Health Check Endpoints](#health-check-endpoints)
  - [Sample Payloads & Responses](#sample-payloads--responses)
- [Error Handling & RFC 7807 Problem Details](#error-handling--rfc-7807-problem-details)
- [Getting Started & Local Setup](#getting-started--local-setup)
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
                              │  - Multi-Tenant & Security Middleware        │
                              │  - Rate Limiter & Observability Instrumentation│
                              │  - RFC 7807 Problem Details Exception Handler│
                              └──────────────────────┬───────────────────────┘
                                                     │
                    ┌────────────────────────────────┴────────────────────────────────┐
                    │                                                                 │
     ┌──────────────▼──────────────────────────────┐   ┌──────────────────────────────▼──────────────┐
     │              Platform Module                │   │                Catalog Module               │
     │  - CommerceCore.Platform.Contracts          │   │  - CommerceCore.Modules.Catalog.Contracts   │
     │  - CommerceCore.Platform.ControlPlane       │   │  - CommerceCore.Modules.Catalog.Domain      │
     │  - CommerceCore.Platform.Identity           │   │  - CommerceCore.Modules.Catalog.Application │
     └──────────────────────┬──────────────────────┘   │  - CommerceCore.Modules.Catalog.Infrastr... │
                            │                          └──────────────────────┬──────────────────────┘
                            └────────────────────────┬────────────────────────┘
                                                     │
                              ┌──────────────────────▼───────────────────────┐
                              │                  Core Layer                  │
                              │  - CommerceCore.Domain (Base Entities, VOs)  │
                              │  - CommerceCore.Application (CQRS Behaviors) │
                              │  - Source-Generated Mediator Pipeline        │
                              └──────────────────────┬───────────────────────┘
                                                     │
                              ┌──────────────────────▼───────────────────────┐
                              │             Infrastructure Layer             │
                              │  - CommerceCore.Persistence (EF Core 10,     │
                              │    PostgreSQL RLS, Outbox, Interceptors)     │
                              │  - CommerceCore.Infrastructure (Clock, System)│
                              └──────────────────────────────────────────────┘
```

### Layer Responsibilities & Dependency Rules

| Layer / Module | Scope & Responsibilities | Dependency Constraints |
|---|---|---|
| **Domain** | Pure business models, aggregates, immutable value objects, domain events, domain exceptions, and invariant rules. | **Zero dependencies** on external frameworks, ORMs, or IO libraries. |
| **Application** | CQRS use cases, commands, queries, mediator handlers, and validation pipeline behaviors (`ValidationBehavior`). | Depends only on **Domain**. No references to persistence, database drivers, or presentation frameworks. |
| **Platform** | Multi-tenant isolation contracts (`ITenantContext`), tenant control plane entities, storefront resolution, and identity integration. | Shared cross-cutting foundation for all functional modules. |
| **Catalog Module** | Full product catalog, variants, dynamic attribute definitions, option sets, taxonomy hierarchies, and schema compilation. | Modular domain and application boundaries, encapsulating catalog-specific business logic. |
| **Persistence & Infra** | PostgreSQL EF Core 10 DbContext, connection interceptors (`TenantSessionInterceptor`, `AuditingSaveChangesInterceptor`, `OutboxSaveChangesInterceptor`), schema configurations, and migrations. | Implements Application abstractions using concrete PostgreSQL drivers and EF Core mappings. |
| **Presentation (API)** | Application composition root, Minimal APIs, rate limiting, security headers, authentication, and OpenTelemetry instrumentation. | References application and infrastructure modules to compose the runtime pipeline. |

---

## Multi-Tenancy & Row-Level Security (RLS)

CommerceCore uses a **Pool-Based Multi-Tenancy** model where tenants share a single high-performance PostgreSQL database while enforcing isolation directly at the database engine level via **PostgreSQL Row-Level Security (RLS)**.

```
Incoming HTTP Request
          │
          ▼
┌────────────────────────────────────────────────────────────┐
│ 1. TenantResolutionMiddleware                              │
│    - Resolves Host from request header                     │
│    - Matches Host against platform.storefronts (cached)    │
│    - Extracts user subject from JWT ('sub' / NameIdentifier)│
│    - Verifies active membership in platform.tenant_members │
│    - Populates Scoped ITenantContext (TenantId, Storefront)│
└─────────────────────────────┬──────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────┐
│ 2. TenantSessionInterceptor (DbConnectionInterceptor)      │
│    - Intercepts EF Core database connection open           │
│    - Executes: SELECT set_config('app.tenant_id', @id, false)│
│    - Sets session variable for the connection lifetime     │
└─────────────────────────────┬──────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────┐
│ 3. PostgreSQL Native Row-Level Security (RLS) Engine       │
│    - Evaluates: tenant_id = NULLIF(current_setting(        │
│                 'app.tenant_id', true), '')::uuid          │
│    - Applied to ALL SELECT, INSERT, UPDATE, DELETE queries │
│    - Defense-in-depth: guarantees zero cross-tenant leak   │
└────────────────────────────────────────────────────────────┘
```

- **Fail-Safe Isolation**: Even if application-level filters are bypassed, PostgreSQL RLS physically prevents any query or command from accessing another tenant's rows.
- **Dynamic Host Routing**: Storefronts dynamically map domain names (e.g., `us.store.com`, `eu.store.com`) to specific tenants, markets, and default locales.

---

## Key Engineering Highlights

- **Compile-Time Source-Generated Mediator**: Utilizes `Mediator.SourceGenerator` for zero-reflection, high-throughput CQRS dispatching with compile-time pipeline behaviors (`ValidationBehavior`).
- **PostgreSQL Row-Level Security (RLS)**: Automatic tenant session binding (`set_config('app.tenant_id', ...)`) ensuring bulletproof multi-tenant isolation.
- **Explicit Product Variants**: Full support for matrix variations with custom SKUs, variant pricing, default variant assignment, and dynamic variant option bags.
- **Dynamic Attribute Schema & Versioning**: Strongly-typed attributes (`Text`, `Integer`, `Decimal`, `Boolean`, `SingleSelect`, `MultiSelect`, `Measurement`) with scopes, validation bounds, enforcement states (`Draft`, `Backfilling`, `Enforced`, `Deprecated`), and compiled JSONB effective schemas.
- **Native UUIDv7 Primary Keys**: Uses .NET 10's native `Guid.CreateVersion7()` for time-sortable sequential identifiers, eliminating B-Tree index fragmentation and boosting PostgreSQL write throughput.
- **Hierarchical Product Taxonomies (`ltree`)**: Dynamic category and product type hierarchies backed by PostgreSQL's native `ltree` extension with GiST indexing for fast subtree queries.
- **Multilingual Localization via PostgreSQL JSONB**: Localized fields (`LocalizedText`, `LanguageCode`) stored directly as native PostgreSQL `jsonb` with custom EF Core value converters, value comparers, and RFC language-tag validation.
- **Transactional Outbox Pattern**: Domain events (`ProductCreatedDomainEvent`, `ProductArchivedDomainEvent`) are automatically serialized into the `outbox.messages` table within the same atomic database transaction via EF Core interceptors (`OutboxSaveChangesInterceptor`).
- **Automated Auditing Interceptor**: EF Core `AuditingSaveChangesInterceptor` automatically stamps `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, and `UpdatedBy` across entities and nested owned entities without polluting command handlers.
- **Optimistic Concurrency via PostgreSQL `xmin`**: Uses PostgreSQL system column `xmin` (`IsRowVersion()`) to detect concurrent modifications and automatically return HTTP `409 Conflict` problem details.
- **Sliding-Window Rate Limiting**: Built-in partition-based rate limiter partitioning by authenticated user ID / client ID or client IP, returning RFC 7807 `429 Too Many Requests` with `Retry-After` headers.
- **OpenTelemetry & Observability**: Complete distributed tracing, metrics, and structured logging integrated with OTLP exporters.
- **Standardized RFC 7807 Error Responses**: Custom `GlobalExceptionHandler` produces structured JSON problem details with correlation trace IDs for validation errors (400), domain invariant violations (422), concurrency conflicts (409), not found (404), and unhandled server faults (500).
- **Automated Verification**: **225 tests** across architectural integrity (`NetArchTest`), domain invariants (`xUnit v3`), API behavior, and real PostgreSQL integration tests (`Testcontainers.PostgreSql`).

---

## Solution & Project Decomposition

```text
CommerceCore/
├── CommerceCore.slnx                                   # Modern solution manifest (.slnx)
├── docker-compose.yml                                  # Local development infrastructure (PostgreSQL 18.6 & pgAdmin 4)
├── Dockerfile                                          # Multi-stage production container build
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
│   │   ├── CommerceCore.Platform.ControlPlane/         # Tenant, Storefront, TenantMembership entities & store
│   │   └── CommerceCore.Platform.Identity/             # TenantResolutionMiddleware, Identity & Scope extensions
│   │
│   ├── Modules/
│   │   └── Catalog/
│   │       ├── CommerceCore.Modules.Catalog.Contracts/ # Inter-module catalog contracts
│   │       ├── CommerceCore.Modules.Catalog.Domain/    # Product, ProductVariant, ProductType, Attribute aggregates
│   │       ├── CommerceCore.Modules.Catalog.Application/ # Catalog CQRS Commands, Queries, Handlers & Validators
│   │       └── CommerceCore.Modules.Catalog.Infrastructure/ # Catalog-specific infrastructure implementations
│   │
│   ├── Infrastructure/
│   │   ├── CommerceCore.Infrastructure/                # System implementations (SystemClock)
│   │   └── CommerceCore.Persistence/                   # PostgreSQL EF Core 10 DbContext, RLS Migrations, Interceptors
│   │
│   └── Presentation/
│       └── CommerceCore.Api/                           # Minimal APIs, RateLimiting, Security, Observability, Program.cs
│
└── tests/
    ├── CommerceCore.Domain.UnitTests/                  # 149 Tests: Domain entities, invariants, value objects
    ├── CommerceCore.Persistence.IntegrationTests/      # 42 Tests: EF Core, PostgreSQL RLS, Outbox with Testcontainers
    ├── CommerceCore.Api.UnitTests/                     # 28 Tests: Endpoint parsers, auth regression, middleware
    └── CommerceCore.ArchitectureTests/                 # 6 Tests: Architecture & Layer boundary rules (NetArchTest)
```

---

## Technology Matrix

| Technology / Library | Version | Purpose |
|---|---|---|
| **.NET SDK** | 10.0 (`net10.0`) | Primary runtime framework & C# 13/14 language features |
| **ASP.NET Core** | 10.0 | High-performance Minimal APIs & OpenAPI integration |
| **Entity Framework Core** | 10.0.11 | Modern ORM with PostgreSQL provider (`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3) |
| **PostgreSQL** | 18.6 | Relational database with native `ltree`, `jsonb`, Row-Level Security (RLS), and `xmin` |
| **Mediator (Source Generator)** | 3.0.2 | Zero-reflection compile-time CQRS messaging & pipeline execution |
| **FluentValidation** | 12.1.1 | Strongly-typed request & business validation rules |
| **OpenTelemetry** | 1.11.2 | Distributed tracing, metrics, and structured logging export |
| **xUnit** | v3 (4.0.0) | Unit & integration testing framework |
| **NetArchTest.eNhancedEdition**| 1.4.5 | Automated architecture rule enforcement |
| **Testcontainers.PostgreSql** | 4.14.0 | Disposable PostgreSQL containers for integration tests |
| **Docker Compose** | - | Local development infrastructure (PostgreSQL & pgAdmin) |

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
- **Variant Options**: `AttributeValueBag` storing variation attributes (e.g., `size`, `color`).
- **Status**: `ProductVariantStatus` (`Draft = 1`, `Active = 2`, `Inactive = 3`).
- **Default Flag**: `IsDefault` ensuring each product has exactly one primary variant.

#### Lifecycle Invariants:
1. **Creation**: Products are initialized in `Draft` status and emit `ProductCreatedDomainEvent`.
2. **Variants**: A product cannot be activated without an active default variant having a positive price.
3. **Currency Invariant**: Currency must be consistent across product variants.
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

#### Attribute Definitions (`AttributeDefinition`):
- **Key & Ordering**: Strongly-typed `AttributeKey` and unique `DisplayOrder`.
- **Data Types**: `Text`, `Integer`, `Decimal`, `Boolean`, `SingleSelect`, `MultiSelect`, `Measurement`.
- **Scope**: `ProductSpecification` (product-level property) or `VariantOption` (matrix variation axis).
- **Validation Bounds**: `MinimumValue`, `MaximumValue`, `MinimumLength`, `MaximumLength`, and `MeasurementUnitFamily`.
- **Enforcement Lifecycle**: `Draft` $ightarrow$ `Backfilling` $ightarrow$ `Enforced` $ightarrow$ `Deprecated`.

---

### 3. Platform Control Plane & Multi-Tenancy

- **`Tenant`**: Represents the isolated organization (`Id`, `Name`, `Slug`, `Status`, `CreatedAtUtc`).
- **`Storefront`**: E-commerce sales channel mapped to a tenant (`Id`, `TenantId`, `HostName`, `MarketCode`, `DefaultLocale`, `IsActive`).
- **`TenantMembership`**: Maps user subjects to tenants with roles (`TenantId`, `UserSubject`, `Role`, `Status`).

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
    CONSTRAINT pk_tenants PRIMARY KEY (id)
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

#### `catalog.product_types`
```sql
CREATE TABLE catalog.product_types (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    code varchar(64) NOT NULL,
    parent_product_type_id uuid NULL,
    path ltree NOT NULL,
    is_assignable boolean NOT NULL DEFAULT FALSE,
    own_schema_version bigint NOT NULL DEFAULT 0,
    created_at_utc timestamp with time zone NOT NULL,
    created_by varchar(200) NULL,
    updated_at_utc timestamp with time zone NULL,
    updated_by varchar(200) NULL,
    xmin xid NOT NULL,
    CONSTRAINT pk_product_types PRIMARY KEY (id),
    CONSTRAINT ux_product_types_tenant_id_id UNIQUE (tenant_id, id)
);
```

#### `catalog.attribute_definitions` & `catalog.attribute_options`
```sql
CREATE TABLE catalog.attribute_definitions (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    product_type_id uuid NOT NULL,
    key varchar(64) NOT NULL,
    data_type varchar(32) NOT NULL,
    scope varchar(32) NOT NULL,
    is_required boolean NOT NULL,
    enforcement_status varchar(32) NOT NULL,
    is_deprecated boolean NOT NULL DEFAULT FALSE,
    display_order integer NOT NULL,
    minimum_value numeric(18,4) NULL,
    maximum_value numeric(18,4) NULL,
    minimum_length integer NULL,
    maximum_length integer NULL,
    measurement_unit_family varchar(64) NULL,
    CONSTRAINT pk_attribute_definitions PRIMARY KEY (id),
    CONSTRAINT ux_attribute_definitions_tenant_id_id UNIQUE (tenant_id, id)
);

CREATE TABLE catalog.attribute_options (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    attribute_definition_id uuid NOT NULL,
    code varchar(64) NOT NULL,
    display_order integer NOT NULL,
    is_deprecated boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT pk_attribute_options PRIMARY KEY (id),
    CONSTRAINT ux_attribute_options_tenant_id_id UNIQUE (tenant_id, id)
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
    CONSTRAINT pk_messages PRIMARY KEY (id)
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
- `ix_outbox_messages_tenant_pending_occurred_on_utc` on `(tenant_id, occurred_on_utc) WHERE processed_on_utc IS NULL`: Partial index for high-speed outbox processing.

---

## Security, Resilience & Observability

### Authentication & Scoped Authorization

Endpoints enforce JWT Bearer authentication with scope-based authorization policies:
- `catalog.read`: Read-only access to catalog products, variants, and product types.
- `catalog.manage`: Write permissions for creating and modifying products, variants, prices, and specifications.
- `catalog.schema.manage`: Administrative permissions to define product types, attributes, and options.

### Partitioned Rate Limiting

The API includes sliding-window rate limiting configured via ASP.NET Core:
- **Partitioning**: Grouped by authenticated User ID (`sub`), Client ID (`client_id`), or remote IP address.
- **Permit Limits**: 300 requests/minute for read operations (`GET`), 60 requests/minute for write operations (`POST`, `PUT`, `DELETE`).
- **Response**: Returns HTTP `429 Too Many Requests` with RFC 7807 problem details and `Retry-After` header.

### Security Headers & Hardening

Configured via `SecurityHeadersMiddleware`:
- `Content-Security-Policy: default-src 'self'`
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Strict-Transport-Security: max-age=31536000; includeSubDomains`
- `Referrer-Policy: strict-origin-when-cross-origin`
- Server Header stripped from Kestrel response (`AddServerHeader = false`).

### OpenTelemetry & Health Probes

- **Tracing & Metrics**: Integrated with ASP.NET Core, HttpClient, and Runtime meters exporting via OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`).
- **Liveness Probe**: `/health/live` returns HTTP 200 indicating the process is running.
- **Readiness Probe**: `/health/ready` evaluates the PostgreSQL connection probe (`PostgreSqlHealthCheck`).

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
| `GET` | `/health/ready` | Readiness probe (verifies PostgreSQL database connection) |

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

## Getting Started & Local Setup

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### 1. Clone the Repository
```bash
git clone https://github.com/MahirSafar/CommerceCore.git
cd CommerceCore
```

### 2. Start PostgreSQL & pgAdmin
Launch containerized PostgreSQL 18.6 and pgAdmin 4:
```bash
docker compose up -d
```
- **PostgreSQL**: `localhost:5432` (User: `CommerceCore`, Password: `Commerce123!`, DB: `CommerceCoreDb`)
- **pgAdmin 4**: `http://localhost:5050` (Email: `admin@commercecore.com`, Password: `Admin123!`)

### 3. Apply EF Core Database Migrations
Restore local tools and apply code-first migrations:
```bash
dotnet tool restore
dotnet ef database update --project src/Infrastructure/CommerceCore.Persistence --startup-project src/Presentation/CommerceCore.Api
```

### 4. Run the API
```bash
dotnet run --project src/Presentation/CommerceCore.Api
```
The API will start at `https://localhost:7198` (or `http://localhost:5247`).
OpenAPI documentation is available at `/openapi/v1.json`.

---

## Testing Strategy & Quality Assurance

The repository includes a comprehensive, multi-tiered testing suite with **225 passing automated tests**:

```text
tests/
├── CommerceCore.Domain.UnitTests/             # 149 Tests: Domain entities, invariants, value objects
├── CommerceCore.Persistence.IntegrationTests/ # 42 Tests: EF Core, PostgreSQL RLS, Outbox with Testcontainers
├── CommerceCore.Api.UnitTests/                # 28 Tests: Endpoint parsers, auth regression, middleware
└── CommerceCore.ArchitectureTests/            # 6 Tests: Architecture & Layer boundary rules (NetArchTest)
```

### Run Architecture Tests
```bash
dotnet test tests/CommerceCore.ArchitectureTests/CommerceCore.ArchitectureTests.csproj
```

### Run Domain Unit Tests
```bash
dotnet test tests/CommerceCore.Domain.UnitTests/CommerceCore.Domain.UnitTests.csproj
```

### Run API Unit Tests
```bash
dotnet test tests/CommerceCore.Api.UnitTests/CommerceCore.Api.UnitTests.csproj
```

### Run Integration Tests (Requires Docker)
Spawns isolated PostgreSQL containers via Testcontainers, applies migrations, and verifies RLS tenant isolation, outbox transactions, and concurrency tokens:
```bash
dotnet test tests/CommerceCore.Persistence.IntegrationTests/CommerceCore.Persistence.IntegrationTests.csproj
```

### Run All Tests
```bash
dotnet test
```

---

## Engineering Practices & Design Patterns

- **Modular Monolith**: Clear module and package boundaries allowing independent module evolution and straightforward future microservice extraction.
- **Clean Architecture & DDD**: Pure domain model, explicit Aggregate Roots, encapsulated business invariants, and immutable Value Objects.
- **CQRS (Command Query Responsibility Segregation)**: Distinct write commands and read queries with optimized query pipelines.
- **Compile-Time Source Generation**: Zero-reflection CQRS dispatching with `Mediator.SourceGenerator`.
- **Pool Multi-Tenancy with RLS**: PostgreSQL Row-Level Security enforcing tenant boundary directly at the database engine.
- **Transactional Outbox**: Guaranteed at-least-once domain event persistence within relational transactions.
- **Hierarchical Schemas (`ltree`)**: Native PostgreSQL path indexing for high-speed taxonomy trees.
- **Optimistic Concurrency Control**: Automatic conflict detection and 409 handling via PostgreSQL `xmin`.
- **Automated Auditing**: Created and Updated timestamps/actors automatically injected via EF Core Interceptors.
- **UUIDv7 Primary Keys**: Time-ordered UUIDs for optimal database index locality and clustered index performance.
- **RFC 7807 Standard Error Responses**: Uniform problem details contract across all endpoints.

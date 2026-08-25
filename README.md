# CommerceCore

[![CommerceCore CI](https://github.com/MahirSafar/CommerceCore/actions/workflows/ci.yml/badge.svg)](https://github.com/MahirSafar/CommerceCore/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![C# 13/14](https://img.shields.io/badge/C%23-13%2F14-239120?style=flat&logo=csharp)](https://docs.microsoft.com/dotnet/csharp/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18.6-4169E1?style=flat&logo=postgresql)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF%20Core-10.0-512BD4?style=flat&logo=dotnet)](https://docs.microsoft.com/ef/core/)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20DDD%20%2F%20CQRS-blueviolet?style=flat)]()
[![Tests](https://img.shields.io/badge/Tests-195%20Passed%20%7C%20xUnit%20v3%20%7C%20Testcontainers-brightgreen?style=flat)]()

**CommerceCore** is an enterprise-grade, high-performance modular e-commerce backend built with **.NET 10**, adhering strictly to the principles of **Clean Architecture**, **Domain-Driven Design (DDD)**, and **CQRS (Command Query Responsibility Segregation)**.

The platform is designed with production-readiness in mind, featuring compile-time source-generated mediation, native PostgreSQL JSONB localization, time-sortable **UUIDv7** identities, hierarchical taxonomy schemas with PostgreSQL `ltree`, transactional outbox pattern, automated audit interceptors, and optimistic concurrency control.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Key Features & Engineering Highlights](#key-features--engineering-highlights)
- [Project & Solution Structure](#project--solution-structure)
- [Technology Stack](#technology-stack)
- [Domain Model & Invariants](#domain-model--invariants)
  - [1. Product Aggregate](#1-product-aggregate)
  - [2. ProductType Aggregate & Dynamic Attribute Schema](#2-producttype-aggregate--dynamic-attribute-schema)
  - [3. Value Objects](#3-value-objects)
- [Persistence & Database Design](#persistence--database-design)
  - [Schemas & Tables](#schemas-and-tables)
  - [Strategic Indexes](#strategic-indexes)
- [API Endpoints Reference](#api-endpoints-reference)
  - [Product Endpoints](#product-endpoints)
  - [Product Type & Attribute Endpoints](#product-type--attribute-endpoints)
  - [Sample Payloads & Responses](#sample-payloads--responses)
- [Error Handling & Problem Details](#error-handling--problem-details)
- [Getting Started & Local Setup](#getting-started--local-setup)
- [Testing Strategy](#testing-strategy)
- [Design Patterns & Practices](#design-patterns--practices)

---

## Architecture Overview

CommerceCore follows **Clean Architecture** (Onion / Hexagonal Architecture) with strict boundary isolation and inward dependency flow. Layer boundaries and decoupling rules are continuously asserted by automated architectural tests (`NetArchTest`).

```
                     ┌──────────────────────────────────────┐
                     │          Presentation Layer          │
                     │         (CommerceCore.Api)           │
                     │  - Minimal APIs (Products, Types)    │
                     │  - OpenAPI / Swagger Metadatas       │
                     │  - RFC 7807 Global Exception Handler │
                     └───────────────┬──────────────────────┘
                                     │
                     ┌───────────────▼──────────────────────┐
                     │          Application Layer           │
                     │     (CommerceCore.Application)       │
                     │  - CQRS Commands & Queries           │
                     │  - Source-Generated Mediator Handlers│
                     │  - FluentValidation Pipeline Behavior│
                     │  - Core Interfaces (IClock, etc.)    │
                     └───────────────┬──────────────────────┘
                                     │
                     ┌───────────────▼──────────────────────┐
                     │             Domain Layer             │
                     │        (CommerceCore.Domain)         │
                     │  - Aggregate Roots & Entities        │
                     │  - Immutable Value Objects           │
                     │  - Domain Events & Domain Exceptions │
                     │  - Pure Business Rules & Invariants  │
                     └──────────────────────────────────────┘
                                     ▲
                     ┌───────────────┴──────────────────────┐
                     │         Infrastructure Layer         │
                     │     (CommerceCore.Persistence &      │
                     │      CommerceCore.Infrastructure)    │
                     │  - PostgreSQL EF Core 10 DbContext   │
                     │  - PostgreSQL ltree Hierarchies      │
                     │  - Auditing & Outbox Interceptors    │
                     │  - JSONB Value Converters & Schemas  │
                     │  - System Clock & External Services  │
                     └──────────────────────────────────────┘
```

### Layer Dependency Rules

- **Domain**: Pure business logic with **zero** external framework dependencies. No references to EF Core, ASP.NET Core, FluentValidation, or Mediator.
- **Application**: Depends only on **Domain**. Contains use cases, CQRS pipelines, and abstractions. Does not reference database drivers, infrastructure, or presentation frameworks.
- **Persistence & Infrastructure**: Depend on **Application** and **Domain** to provide concrete implementations for persistence (PostgreSQL/EF Core) and system infrastructure.
- **Presentation (API)**: Entry point composing all layers, hosting Minimal APIs, OpenAPI specs, and HTTP middlewares.

---

## Key Features & Engineering Highlights

- **Compile-Time Source-Generated Mediator**: Utilizes `Mediator.SourceGenerator` by Martin Ullrich for zero-reflection, high-throughput CQRS dispatching with compile-time pipeline behaviors (`ValidationBehavior`).
- **Native UUIDv7 Primary Keys**: Uses .NET 10's native `Guid.CreateVersion7()` for time-sortable sequential identifiers, eliminating B-Tree index fragmentation and boosting PostgreSQL write throughput.
- **Hierarchical Product Taxonomies (`ltree`)**: Implements dynamic category and product type hierarchies backed by PostgreSQL's native `ltree` extension with GiST indexing for lightning-fast subtree queries.
- **Dynamic Attribute Schema & Versioning**: Enables catalog administrators to define strongly-typed attributes (`Text`, `Integer`, `Decimal`, `Boolean`, `SingleSelect`, `MultiSelect`, `Measurement`) with scopes, validation bounds, enforcement states (`Draft`, `Backfilling`, `Enforced`, `Deprecated`), and compiled JSONB effective schemas.
- **Multilingual Localization via PostgreSQL JSONB**: Rich localized fields (`LocalizedText`, `LanguageCode`) stored directly as native PostgreSQL `jsonb` with custom EF Core value converters, value comparers, and RFC language-tag validation.
- **Transactional Outbox Pattern**: Domain events (`ProductCreatedDomainEvent`, `ProductArchivedDomainEvent`) are automatically serialized into the `outbox.messages` table within the same atomic database transaction via EF Core interceptors (`OutboxSaveChangesInterceptor`).
- **Automated Auditing Interceptor**: EF Core `AuditingSaveChangesInterceptor` automatically stamps `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, and `UpdatedBy` across entities and nested owned entities without polluting command handlers.
- **Optimistic Concurrency via PostgreSQL `xmin`**: Uses PostgreSQL system column `xmin` (`IsRowVersion()`) to detect concurrent modifications and automatically return HTTP `409 Conflict` problem details.
- **Soft Deletion & Global Query Filters**: Entities inheriting `SoftDeletableAggregateRoot<TKey>` automatically apply `!IsDeleted` EF Core query filters while supporting restoration workflows (`IgnoreQueryFilters()`).
- **Standardized RFC 7807 Error Responses**: Custom `IExceptionHandler` (`GlobalExceptionHandler`) produces structured JSON problem details with correlation trace IDs for validation errors (400), domain invariant violations (422), concurrency conflicts (409), not found (404), and unhandled server faults (500).
- **Automated Verification**: 195 tests across architectural integrity (`NetArchTest`), domain invariants (`xUnit v3`), API behavior, and real PostgreSQL integration tests (`Testcontainers.PostgreSql`).

---

## Project & Solution Structure

```
CommerceCore/
│
├── CommerceCore.slnx                       # Solution configuration (modern .slnx format)
├── docker-compose.yml                      # PostgreSQL 18.6 & pgAdmin 4 local environment
├── dotnet-tools.json                       # Local dotnet tools (dotnet-ef 10.0.11)
├── global.json                             # Microsoft Testing Platform configuration
│
├── src/
│   ├── Core/
│   │   ├── CommerceCore.Domain/            # Pure Business Domain Layer
│   │   │   ├── Catalog/
│   │   │   │   ├── Attributes/             # Dynamic Attribute Value Bags & Values
│   │   │   │   ├── Categories/             # Category Identifiers & Value Objects
│   │   │   │   ├── Products/               # Product Aggregate, Status, Events, Exceptions, ProductId
│   │   │   │   ├── ProductTypes/           # ProductType Aggregate, AttributeDefinition, AttributeOption,
│   │   │   │   │                           # Data Types, Enforcement Status, Measurement Families
│   │   │   └── Common/                     # BaseEntity, AggregateRoot, Auditable & SoftDelete base classes,
│   │   │                                   # Value Objects (Money, LanguageCode, LocalizedText)
│   │   │
│   │   └── CommerceCore.Application/       # Application & Use Cases Layer
│   │       ├── Catalog/
│   │       │   ├── Products/               # Product CQRS Commands (Create, Activate, Deactivate,
│   │       │   │                           # Archive, Restore, ChangePrice, ChangeName) & Queries (GetById)
│   │       │   └── ProductTypes/           # ProductType CQRS Commands (CreateProductType,
│   │       │                               # DefineAttribute, AddAttributeOption) + Validators
│   │       └── Common/                     # Abstractions (IClock, ICurrentUser, Persistence Coordinators),
│   │                                       # Pipeline Behaviors (ValidationBehavior), Validation Rules
│   │
│   ├── Infrastructure/
│   │   ├── CommerceCore.Infrastructure/    # Concrete Infrastructure implementations (SystemClock)
│   │   └── CommerceCore.Persistence/       # EF Core 10 PostgreSQL Persistence Layer
│   │       ├── Configurations/             # Fluent API entity configs (Product, ProductType,
│   │       │                               # AttributeDefinition, AttributeOption, EffectiveSchema, Outbox)
│   │       ├── Interceptors/               # AuditingSaveChangesInterceptor, OutboxSaveChangesInterceptor
│   │       ├── Migrations/                 # EF Core Code-First database migrations (ltree, jsonb, outbox)
│   │       ├── Outbox/                     # OutboxMessage entity model
│   │       ├── ProductTypes/               # AttributeDefinitionRegistry, ProductTypeSchemaCoordinator
│   │       └── CommerceCoreDbContext.cs    # Application DbContext implementation
│   │
│   └── Presentation/
│       └── CommerceCore.Api/               # ASP.NET Core Minimal API Presentation Layer
│           ├── Common/Errors/              # GlobalExceptionHandler (RFC 7807 Problem Details)
│           ├── Endpoints/V1/
│           │   ├── Products/               # Product Minimal API Route Endpoints
│           │   └── ProductTypes/           # ProductType & Attribute Minimal API Route Endpoints
│           ├── Identity/                   # HttpCurrentUser (ClaimsPrincipal resolver)
│           ├── Program.cs                  # Web Application Composition Root & Middleware Pipeline
│           └── appsettings.json            # Configuration settings
│
└── tests/
    ├── CommerceCore.ArchitectureTests/     # Architecture integrity & dependency boundary tests (NetArchTest)
    ├── CommerceCore.Domain.UnitTests/      # Domain entities, value objects & business invariants
    └── CommerceCore.Persistence.IntegrationTests/ # Integration tests with PostgreSQL Testcontainers
```

---

## Technology Stack

| Technology / Library | Version | Purpose |
|---|---|---|
| **.NET SDK** | 10.0 (`net10.0`) | Primary runtime framework & C# 13/14 language features |
| **ASP.NET Core** | 10.0 | High-performance Minimal APIs & OpenAPI integration |
| **Entity Framework Core** | 10.0.11 | Modern ORM with PostgreSQL provider (`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3) |
| **PostgreSQL** | 18.6 | Relational database with native `ltree`, `jsonb`, `xmin` concurrency, and custom schemas |
| **Mediator (Source Generator)** | 3.0.2 | Zero-reflection compile-time CQRS messaging & pipeline execution |
| **FluentValidation** | 12.1.1 | Strongly-typed request & business validation rules |
| **xUnit** | v3 (4.0.0) | Unit & integration testing framework |
| **NetArchTest.eNhancedEdition**| 1.4.5 | Automated architecture rule enforcement |
| **Testcontainers.PostgreSql** | 4.14.0 | Disposable PostgreSQL containers for integration tests |
| **Docker Compose** | - | Local development infrastructure (PostgreSQL & pgAdmin) |

---

## Domain Model & Invariants

### 1. Product Aggregate (`Product`)

Inherits `SoftDeletableAggregateRoot<ProductId>`:

- **Identity**: `ProductId` (wrapping UUIDv7 `Guid`).
- **Name**: `LocalizedText` stored as JSONB with default language guarantee.
- **Variants**: A product owns explicit variants, each with a SKU, price, options, lifecycle status, and a single default variant.
- **Current price model**: `Money` uses ISO 4217 3-letter currency codes and precision scale $\le 4$. A dedicated Pricing context with market-specific price lists is the planned authority for multi-currency selling prices.
- **Status**: `ProductStatus` (`Draft = 1`, `Active = 2`, `Inactive = 3`).
- **Audit & Soft-Delete**: `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy`, `IsDeleted`, `DeletedAtUtc`, `DeletedBy`.

#### Core Lifecycle Invariants:
1. **Creation**: Products are initialized in `Draft` status and emit `ProductCreatedDomainEvent`.
2. **Price & Currency**: Currency is immutable for a product or variant price. Active products and variants cannot have a zero price.
3. **Activation**: A product requires an active default variant; a variant requires a positive price before activation.
4. **Deactivation**: Only active products can be deactivated.
5. **Archiving (Soft Delete)**: Idempotent soft deletion stamping UTC timestamp and actor, emitting `ProductArchivedDomainEvent`. Modifying archived products is forbidden (`product.archived`).
6. **Restoration**: Restoring an archived product that was previously `Active` resets its status to `Inactive` to prevent accidental immediate exposure.

---

### 2. ProductType Aggregate & Dynamic Attribute Schema

Inherits `AggregateRoot<ProductTypeId>`:

- **Identity**: `ProductTypeId` (UUIDv7).
- **Code**: Unique normalized identifier `ProductTypeCode` (e.g., `apparel`, `footwear`, `running-shoes`).
- **Hierarchy & Taxonomies**: Modeled via `ParentProductTypeId` and PostgreSQL `ltree` `path`.
- **Assignment Control**: `IsAssignable` indicates whether concrete products can be assigned to this type or if it acts purely as an abstract categorization node.
- **Schema Evolution**: `OwnSchemaVersion` increments on definition changes, compiling into `ProductTypeEffectiveSchema` for fast runtime validation.

#### Attribute Definitions (`AttributeDefinition`):
- **Key & Ordering**: Strongly-typed `AttributeKey` and unique `DisplayOrder`.
- **Data Types**: `Text`, `Integer`, `Decimal`, `Boolean`, `SingleSelect`, `MultiSelect`, `Measurement`.
- **Scope**: `ProductSpecification` (product-level property) or `VariantOption` (matrix variation axis).
- **Validation Bounds**: `MinimumValue`, `MaximumValue`, `MinimumLength`, `MaximumLength`, and `MeasurementUnitFamily`.
- **Enforcement Lifecycle**: `Draft` $\rightarrow$ `Backfilling` $\rightarrow$ `Enforced` $\rightarrow$ `Deprecated`.

#### Attribute Options (`AttributeOption`):
- Predefined selectable choices for select-type attributes with `AttributeOptionCode`, `DisplayOrder`, and deprecation flags.

---

### 3. Value Objects

- **`Money`**: Validates non-negative amounts, enforces maximum scale of 4 decimal places, and normalizes 3-letter uppercase ISO currency codes.
- **`LanguageCode`**: Validates language tags against standard RFC patterns (`^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$`) and normalizes casing (e.g., `en`, `az`, `en-US`).
- **`LocalizedText`**: Immutable multilingual map ensuring the default language translation always exists and non-empty. Supports fallback resolution via `GetOrDefault()`.
- **`ProductId` / `ProductTypeId`**: Strongly-typed readonly record struct wrappers around `Guid` utilizing `Guid.CreateVersion7()`.
- **`AttributeValueBag`**: Strongly-typed container for dynamic product attributes supporting typed extraction and JSONB serialization.

---

## Persistence & Database Design

### Schemas and Tables

The database is divided into logical PostgreSQL schemas:

#### `catalog.products`
```sql
CREATE TABLE catalog.products (
    id uuid NOT NULL,
    status varchar(16) NOT NULL,
    name jsonb NOT NULL,
    price_amount numeric(18,4) NOT NULL,
    price_currency varchar(3) NOT NULL,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    deleted_at_utc timestamp with time zone NULL,
    deleted_by varchar(200) NULL,
    created_at_utc timestamp with time zone NOT NULL,
    created_by varchar(200) NULL,
    updated_at_utc timestamp with time zone NULL,
    updated_by varchar(200) NULL,
    xmin xid NOT NULL, -- PostgreSQL concurrency token
    CONSTRAINT pk_products PRIMARY KEY (id)
);
```

#### `catalog.product_types`
```sql
CREATE TABLE catalog.product_types (
    id uuid NOT NULL,
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
    CONSTRAINT fk_product_types_parent_product_type FOREIGN KEY (parent_product_type_id) REFERENCES catalog.product_types (id) ON DELETE RESTRICT
);
```

#### `catalog.attribute_definitions`
```sql
CREATE TABLE catalog.attribute_definitions (
    id uuid NOT NULL,
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
    measurement_unit_family varchar(32) NULL,
    CONSTRAINT pk_attribute_definitions PRIMARY KEY (id),
    CONSTRAINT fk_attribute_definitions_product_type FOREIGN KEY (product_type_id) REFERENCES catalog.product_types (id) ON DELETE RESTRICT
);
```

#### `catalog.attribute_options`
```sql
CREATE TABLE catalog.attribute_options (
    id uuid NOT NULL,
    attribute_definition_id uuid NOT NULL,
    code varchar(64) NOT NULL,
    display_order integer NOT NULL,
    is_deprecated boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT pk_attribute_options PRIMARY KEY (id),
    CONSTRAINT fk_attribute_options_attribute_definition FOREIGN KEY (attribute_definition_id) REFERENCES catalog.attribute_definitions (id) ON DELETE RESTRICT
);
```

#### `catalog.product_type_effective_schema`
```sql
CREATE TABLE catalog.product_type_effective_schema (
    product_type_id uuid NOT NULL,
    effective_schema_version bigint NOT NULL DEFAULT 0,
    schema jsonb NOT NULL DEFAULT '{}'::jsonb,
    updated_at_utc timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT pk_product_type_effective_schema PRIMARY KEY (product_type_id),
    CONSTRAINT fk_product_type_effective_schema_product_type FOREIGN KEY (product_type_id) REFERENCES catalog.product_types (id) ON DELETE CASCADE
);
```

#### `outbox.messages`
```sql
CREATE TABLE outbox.messages (
    id uuid NOT NULL,
    occurred_on_utc timestamp with time zone NOT NULL,
    type varchar(500) NOT NULL,
    content jsonb NOT NULL,
    processed_on_utc timestamp with time zone NULL,
    attempt_count integer NOT NULL DEFAULT 0,
    last_error text NULL,
    CONSTRAINT pk_messages PRIMARY KEY (id)
);
```

### Strategic Indexes

- `ix_products_status_is_deleted` on `(status, is_deleted)`: Fast catalog filtering.
- `ix_products_price_currency_amount` on `(price_currency, price_amount)`: Efficient pricing lookups.
- `ux_product_types_code` on `code` (Unique): Prevents duplicate taxonomy codes.
- `ix_product_types_path_gist` on `path USING gist`: Lightning-fast hierarchical subtree operations (`@>`, `<@`, `~`).
- `ux_attribute_definitions_product_type_key` on `(product_type_id, key)` (Unique): Uniqueness of attribute keys per product type.
- `ux_attribute_definitions_product_type_display_order` on `(product_type_id, display_order)` (Unique): Consistent attribute sorting.
- `ix_messages_pending_occurred_on_utc` on `occurred_on_utc WHERE processed_on_utc IS NULL`: High-speed partial index for outbox worker polling.

---

## API Endpoints Reference

### Product Endpoints

Base route: `/api/products`

| Method | Endpoint | Description | Response Codes |
|---|---|---|---|
| `POST` | `/api/products` | Create a new product | `201 Created`, `400 Bad Request`, `422 Unprocessable` |
| `GET` | `/api/products/{productId:guid}` | Get product details by ID | `200 OK`, `404 Not Found`, `400 Bad Request` |
| `POST` | `/api/products/{productId:guid}/activate` | Activate a draft or inactive product | `200 OK`, `404 Not Found`, `422 Unprocessable`, `409 Conflict` |
| `POST` | `/api/products/{productId:guid}/deactivate` | Deactivate an active product | `200 OK`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/products/{productId:guid}/archive` | Archive (soft delete) product | `200 OK`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/products/{productId:guid}/restore` | Restore an archived product | `200 OK`, `404 Not Found`, `409 Conflict` |
| `PUT` | `/api/products/{productId:guid}/price` | Change product price | `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict`, `422` |
| `PUT` | `/api/products/{productId:guid}/name` | Change localized product name | `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict`, `422` |

---

### Product Type & Attribute Endpoints

Base route: `/api/product-types`

| Method | Endpoint | Description | Response Codes |
|---|---|---|---|
| `POST` | `/api/product-types` | Create root or child product type | `201 Created`, `400 Bad Request`, `422 Unprocessable` |
| `POST` | `/api/product-types/{productTypeId:guid}/attributes` | Define an attribute on a product type | `201 Created`, `400 Bad Request`, `422 Unprocessable` |
| `POST` | `/api/product-types/{productTypeId:guid}/attributes/{attributeDefinitionId:guid}/options` | Add predefined select option | `201 Created`, `400 Bad Request`, `422 Unprocessable` |

---

### Sample Payloads & Responses

#### 1. Create Product (`POST /api/products`)
```json
{
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

#### 2. Change Product Name (`PUT /api/products/{productId}/name`)
```json
{
  "defaultLanguage": "en",
  "nameTranslations": {
    "en": "RGB Mechanical Keyboard",
    "az": "RGB Mexaniki Klaviatura"
  }
}
```
**Response (`200 OK`):**
```json
{
  "productId": "019139f4-1800-7521-97b7-5bfcfca91b92",
  "defaultLanguage": "en",
  "nameTranslations": {
    "en": "RGB Mechanical Keyboard",
    "az": "RGB Mexaniki Klaviatura"
  },
  "status": "Draft"
}
```

#### 3. Create Product Type (`POST /api/product-types`)
```json
{
  "code": "keyboards",
  "parentProductTypeId": null,
  "isAssignable": true
}
```
**Response (`201 Created`):**
```json
{
  "productTypeId": "019139f6-3c00-74a9-832c-9a489d28e751"
}
```

#### 4. Define Attribute on Product Type (`POST /api/product-types/{id}/attributes`)
```json
{
  "key": "switch_type",
  "dataType": "single_select",
  "scope": "product_specification",
  "isRequired": true,
  "displayOrder": 1,
  "minimumValue": null,
  "maximumValue": null,
  "minimumLength": null,
  "maximumLength": null,
  "measurementUnitFamily": null
}
```
**Response (`201 Created`):**
```json
{
  "attributeDefinitionId": "019139f8-7e10-72cb-b51f-2e90c8a14920"
}
```

---

## Error Handling & Problem Details

Errors are returned in standard **RFC 7807 Problem Details** format with unique `traceId` correlation tags:

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
  "detail": "A product with a zero price cannot be activated.",
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
Start containerized PostgreSQL 18.6 and pgAdmin 4:
```bash
docker compose up -d
```
- **PostgreSQL**: `localhost:5432` (User: `CommerceCore`, Password: `Commerce123!`, DB: `CommerceCoreDb`)
- **pgAdmin 4**: `http://localhost:5050` (Email: `admin@commercecore.com`, Password: `Admin123!`)

### 3. Apply EF Core Database Migrations
Restore local tools and update the database:
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

## Testing Strategy

The repository includes a comprehensive, multi-tiered testing suite with **195 passing automated tests** at the current baseline:

```
tests/
├── CommerceCore.ArchitectureTests/            # Architecture & layer dependency rules (NetArchTest)
├── CommerceCore.Domain.UnitTests/             # Domain entities, value objects & business invariants
├── CommerceCore.Persistence.IntegrationTests/ # EF Core & PostgreSQL tests with Testcontainers
└── CommerceCore.Api.UnitTests/                # Endpoint serialization and error-contract behavior
```

### Run Architecture Tests
Verifies Clean Architecture rules and asserts that the Domain layer has zero outer dependencies:
```bash
dotnet test tests/CommerceCore.ArchitectureTests
```

### Run Domain Unit Tests
Tests domain aggregates, value objects (`Money`, `LocalizedText`, `LanguageCode`, `AttributeValueBag`), idempotency, and invariant validations:
```bash
dotnet test tests/CommerceCore.Domain.UnitTests
```

### Run Integration Tests (Requires Docker)
Spawns an isolated PostgreSQL container via Testcontainers, applies migrations, and validates outbox generation, concurrency tokens, audit interceptors, and soft-delete filters:
```bash
dotnet test tests/CommerceCore.Persistence.IntegrationTests
```

### Run All Tests
```bash
dotnet test
```

---

## Design Patterns & Practices

- **Clean Architecture**: Inward-pointing dependencies preserving pure domain models.
- **Domain-Driven Design (DDD)**: Explicit Aggregate Roots, Encapsulated State, Immutability, Value Objects, Domain Events.
- **CQRS (Command Query Responsibility Segregation)**: Clean separation between read and write models with optimized query handlers.
- **Source-Generated Mediation**: Zero-allocation compile-time pipeline dispatching via `Mediator`.
- **Transactional Outbox**: Guaranteed at-least-once domain event persistence within relational transactions.
- **Hierarchical Schemas (`ltree`)**: Native PostgreSQL path indexing for flexible taxonomy trees.
- **Optimistic Concurrency Control**: Automatic conflict detection and 409 handling via PostgreSQL `xmin`.
- **Automated Auditing**: Seamless metadata enrichment for created/updated timestamps and actors via EF Core Interceptors.
- **UUIDv7 Primary Keys**: Time-ordered UUIDs for optimal database index locality and clustered index performance.
- **RFC 7807 Standard Error Responses**: Uniform problem details contract across all endpoints.

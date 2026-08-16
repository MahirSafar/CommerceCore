# CommerceCore

[![CommerceCore CI](https://github.com/MahirSafar/CommerceCore/actions/workflows/ci.yml/badge.svg)](https://github.com/MahirSafar/CommerceCore/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![C# 13/14](https://img.shields.io/badge/C%23-13%2F14-239120?style=flat&logo=csharp)](https://docs.microsoft.com/dotnet/csharp/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18.6-4169E1?style=flat&logo=postgresql)](https://www.postgresql.org/)
[![EF Core](https://img.shields.io/badge/EF%20Core-10.0-512BD4?style=flat&logo=dotnet)](https://docs.microsoft.com/ef/core/)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20DDD%20%2F%20CQRS-blueviolet?style=flat)]()
[![Tests](https://img.shields.io/badge/Tests-xUnit%20v3%20%7C%20Testcontainers-orange?style=flat)]()

**CommerceCore** is an enterprise-grade, high-performance modular e-commerce backend built with **.NET 10**, adhering strictly to the principles of **Clean Architecture**, **Domain-Driven Design (DDD)**, and **CQRS (Command Query Responsibility Segregation)**.

The project is designed with production-readiness in mind, featuring zero-reflection source-generated mediation, native PostgreSQL JSONB localization, time-sortable **UUIDv7** identities, transactional outbox pattern, automated audit interceptors, and optimistic concurrency control.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Key Features & Engineering Highlights](#key-features--engineering-highlights)
- [Project & Solution Structure](#project--solution-structure)
- [Technology Stack](#technology-stack)
- [Domain Model & Invariants](#domain-model--invariants)
- [Persistence & Database Design](#persistence--database-design)
- [API Endpoints Reference](#api-endpoints-reference)
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
                     │  - Minimal APIs                      │
                     │  - OpenAPI / Swagger                 │
                     │  - Global Exception Handler          │
                     └───────────────┬──────────────────────┘
                                     │
                     ┌───────────────▼──────────────────────┐
                     │          Application Layer           │
                     │     (CommerceCore.Application)       │
                     │  - CQRS Commands & Queries           │
                     │  - Source-Generated Mediator Handlers│
                     │  - FluentValidation Behaviors        │
                     │  - Core Interfaces (IClock, etc.)    │
                     └───────────────┬──────────────────────┘
                                     │
                     ┌───────────────▼──────────────────────┐
                     │             Domain Layer             │
                     │        (CommerceCore.Domain)         │
                     │  - Aggregate Roots & Entities        │
                     │  - Immutable Value Objects           │
                     │  - Domain Events & Domain Exceptions │
                     │  - Business Rules & Invariants       │
                     └──────────────────────────────────────┘
                                     ▲
                     ┌───────────────┴──────────────────────┐
                     │         Infrastructure Layer         │
                     │     (CommerceCore.Persistence &      │
                     │      CommerceCore.Infrastructure)    │
                     │  - PostgreSQL EF Core 10 DbContext   │
                     │  - Auditing & Outbox Interceptors    │
                     │  - JSONB Value Converters            │
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

- **Compile-Time Source-Generated Mediator**: Utilizes `Mediator.SourceGenerator` by Martin Ullrich for zero-reflection, high-throughput CQRS dispatching with compile-time pipeline behaviors.
- **Native UUIDv7 Primary Keys**: Uses .NET 10's native `Guid.CreateVersion7()` for time-sortable sequential identifiers, eliminating B-Tree index fragmentation and boosting PostgreSQL write throughput.
- **Multilingual Localization via PostgreSQL JSONB**: Rich localized fields (`LocalizedText`, `LanguageCode`) stored directly as native PostgreSQL `jsonb` with custom EF Core value converters, value comparers, and RFC language-tag validation.
- **Transactional Outbox Pattern**: Domain events (`ProductCreatedDomainEvent`, `ProductArchivedDomainEvent`) are automatically serialized into the `outbox.messages` table within the same atomic database transaction via EF Core interceptors (`OutboxSaveChangesInterceptor`).
- **Automated Auditing Interceptor**: EF Core `AuditingSaveChangesInterceptor` automatically stamps `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, and `UpdatedBy` across entities and nested owned entities without polluting command handlers.
- **Optimistic Concurrency via PostgreSQL `xmin`**: Uses PostgreSQL system column `xmin` (`IsRowVersion()`) to detect concurrent modifications and automatically return HTTP `409 Conflict` problem details.
- **Soft Deletion & Global Query Filters**: Entities inheriting `SoftDeletableAggregateRoot<TKey>` automatically apply `!IsDeleted` EF Core query filters while supporting restoration workflows (`IgnoreQueryFilters()`).
- **Standardized RFC 7807 Error Responses**: Custom `IExceptionHandler` (`GlobalExceptionHandler`) produces structured JSON problem details with correlation trace IDs for validation errors (400), domain invariant violations (422), concurrency conflicts (409), not found (404), and unhandled server faults (500).
- **Integration Testing with Real PostgreSQL (Testcontainers)**: Comprehensive integration suite running against containerized PostgreSQL 18.6 instances via `Testcontainers.PostgreSql`.

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
│   │   ├── CommerceCore.Domain/            # Enterprise Domain Layer
│   │   │   ├── Catalog/Products/           # Product Aggregate, Status Enum, Events, Exceptions, ProductId
│   │   │   └── Common/                     # BaseEntity, AggregateRoot, Auditing & SoftDelete base classes,
│   │   │                                   # ValueObjects (Money, LanguageCode, LocalizedText)
│   │   │
│   │   └── CommerceCore.Application/       # Application & Use Cases Layer
│   │       ├── Catalog/Products/           # CQRS Commands & Queries (Create, Activate, Deactivate,
│   │       │                               # Archive, Restore, ChangePrice, GetById) + Validators
│   │       └── Common/                     # Abstractions (IClock, ICurrentUser, ICommerceCoreDbContext),
│   │                                       # Pipeline Behaviors (ValidationBehavior), Validation Rules
│   │
│   ├── Infrastructure/
│   │   ├── CommerceCore.Infrastructure/    # Concrete Infrastructure implementations (SystemClock)
│   │   └── CommerceCore.Persistence/       # EF Core 10 PostgreSQL Persistence Layer
│   │       ├── Configurations/             # Fluent API entity configs (ProductConfiguration, OutboxMessageConfiguration)
│   │       ├── Interceptors/               # AuditingSaveChangesInterceptor, OutboxSaveChangesInterceptor
│   │       ├── Migrations/                 # EF Core Code-First database migrations
│   │       ├── Outbox/                     # OutboxMessage entity model
│   │       └── CommerceCoreDbContext.cs    # Application DbContext implementation
│   │
│   └── Presentation/
│       └── CommerceCore.Api/               # ASP.NET Core Minimal API Presentation Layer
│           ├── Common/Errors/              # GlobalExceptionHandler (RFC 7807 Problem Details)
│           ├── Endpoints/V1/Products/      # Product Minimal API Route Endpoints
│           ├── Identity/                   # HttpCurrentUser (ClaimsPrincipal resolver)
│           ├── Program.cs                  # Web Application Composition Root & Middleware Pipeline
│           └── appsettings.json            # Configuration settings
│
└── tests/
    ├── CommerceCore.ArchitectureTests/     # Architecture integrity & dependency boundary tests (NetArchTest)
    ├── CommerceCore.Domain.UnitTests/      # Domain aggregate & value object unit tests
    └── CommerceCore.Persistence.IntegrationTests/ # Integration tests with PostgreSQL Testcontainers
```

---

## Technology Stack

| Technology / Library | Version | Purpose |
|---|---|---|
| **.NET SDK** | 10.0 (`net10.0`) | Primary runtime framework & C# 13/14 language features |
| **ASP.NET Core** | 10.0 | High-performance Minimal APIs & OpenAPI integration |
| **Entity Framework Core** | 10.0.11 | Modern ORM with PostgreSQL provider (`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3) |
| **PostgreSQL** | 18.6 | Relational database with native JSONB, `xmin` concurrency, and custom schemas |
| **Mediator (Source Generator)** | 3.0.2 | High-speed, compile-time CQRS messaging & pipeline execution |
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
- **Price**: `Money` with ISO 4217 3-letter currency code and precision scale $\le 4$.
- **Status**: `ProductStatus` (`Draft = 1`, `Active = 2`, `Inactive = 3`).
- **Audit & Soft-Delete**: `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy`, `IsDeleted`, `DeletedAtUtc`, `DeletedBy`.

#### Core Lifecycle Invariants:
1. **Creation**: Products are initialized in `Draft` status and emit `ProductCreatedDomainEvent`.
2. **Price & Currency**: A product's currency is immutable once created. An active product cannot have a zero price (`product.active_price_must_be_positive`).
3. **Activation**: Requires a positive price amount (`product.activation_requires_price`).
4. **Deactivation**: Only active products can be deactivated.
5. **Archiving (Soft Delete)**: Idempotent soft deletion stamping UTC timestamp and actor, emitting `ProductArchivedDomainEvent`. Modifying archived products is forbidden (`product.archived`).
6. **Restoration**: Restoring an archived product that was previously `Active` resets its status to `Inactive` to prevent accidental immediate exposure.

### 2. Value Objects

- **`Money`**: Validates non-negative amounts, enforces maximum scale of 4 decimal places, and normalizes 3-letter uppercase ISO currency codes.
- **`LanguageCode`**: Validates language tags against standard RFC patterns (`^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$`) and normalizes casing (e.g., `en`, `az`, `en-US`).
- **`LocalizedText`**: Immutable multilingual map ensuring the default language translation always exists and non-empty. Supports fallback resolution via `GetOrDefault()`.
- **`ProductId`**: Strongly-typed readonly record struct wrapper around `Guid` utilizing `Guid.CreateVersion7()`.

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
- `ix_messages_pending_occurred_on_utc` on `occurred_on_utc WHERE processed_on_utc IS NULL`: High-speed partial index for outbox workers polling pending messages.

---

## API Endpoints Reference

Base route: `/api/products`

| Method | Endpoint | Description | Request Body | Response Codes |
|---|---|---|---|---|
| `POST` | `/api/products` | Create a new product | `CreateProductRequest` | `201 Created`, `400 Bad Request`, `422 Unprocessable` |
| `GET` | `/api/products/{productId:guid}` | Get product details by ID | *None* | `200 OK`, `404 Not Found`, `400 Bad Request` |
| `POST` | `/api/products/{productId:guid}/activate` | Activate a draft or inactive product | *None* | `200 OK`, `404 Not Found`, `422 Unprocessable`, `409 Conflict` |
| `POST` | `/api/products/{productId:guid}/deactivate` | Deactivate an active product | *None* | `200 OK`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/products/{productId:guid}/archive` | Archive (soft delete) product | *None* | `200 OK`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/products/{productId:guid}/restore` | Restore an archived product | *None* | `200 OK`, `404 Not Found`, `409 Conflict` |
| `PUT` | `/api/products/{productId:guid}/price` | Change product price | `ChangeProductPriceRequest` | `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict`, `422 Unprocessable` |

### Sample Payloads

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

#### 2. Change Product Price (`PUT /api/products/{productId}/price`)
```json
{
  "priceAmount": 169.99,
  "currency": "USD"
}
```
**Response (`200 OK`):**
```json
{
  "productId": "019139f4-1800-7521-97b7-5bfcfca91b92",
  "priceAmount": 169.99,
  "currency": "USD",
  "status": "Draft"
}
```

#### 3. Get Product (`GET /api/products/{productId}`)
**Response (`200 OK`):**
```json
{
  "productId": "019139f4-1800-7521-97b7-5bfcfca91b92",
  "defaultLanguage": "en",
  "nameTranslations": {
    "en": "Mechanical Keyboard",
    "az": "Mexaniki Klaviatura"
  },
  "priceAmount": 169.99,
  "currency": "USD",
  "status": "Draft"
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
Start the containerized PostgreSQL 18.6 and pgAdmin 4 services:
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
OpenAPI endpoint is available at `/openapi/v1.json`.

---

## Testing Strategy

The repository includes a comprehensive, multi-tiered testing suite:

```
tests/
├── CommerceCore.ArchitectureTests/            # Architecture & layer dependency rules (NetArchTest)
├── CommerceCore.Domain.UnitTests/             # Domain entities, value objects & business invariants
└── CommerceCore.Persistence.IntegrationTests/ # EF Core & PostgreSQL tests with Testcontainers
```

### Run Architecture Tests
Verifies Clean Architecture rules and asserts that the Domain layer has zero outer dependencies:
```bash
dotnet test tests/CommerceCore.ArchitectureTests
```

### Run Domain Unit Tests
Tests domain aggregates, value objects (`Money`, `LocalizedText`, `LanguageCode`), idempotency, and invariant validations:
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
- **Optimistic Concurrency Control**: Automatic conflict detection and 409 handling via PostgreSQL `xmin`.
- **Automated Auditing**: Seamless metadata enrichment for created/updated timestamps and actors via EF Core Interceptors.
- **UUIDv7 Primary Keys**: Time-ordered UUIDs for optimal database index locality and clustered index performance.
- **RFC 7807 Standard Error Responses**: Uniform problem details contract across all endpoints.
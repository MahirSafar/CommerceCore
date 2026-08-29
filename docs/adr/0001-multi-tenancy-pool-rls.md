# ADR 0001: Multi-Tenancy Architecture — Shared Database (Pool) with PostgreSQL RLS

## Status
Approved

## Kontekst
CommerceCore bir backend üzərində çoxlu tenant-lara, brendlərə və storefront-lara xidmət edən composable e-commerce platformasıdır. Multi-tenancy yanaşmasında verilənlərin tam izolyasiyası, resurs effektivliyi və əməliyyat xərclərinin optimallaşdırılması əsas tələblərdəndir.

## Qərar
1. **Multi-Tenancy Modeli**:
   - Model: **Shared Database, Pool Multi-Tenancy**.
   - Bütün tenant məlumatlarını saxlayan cədvəllərdə `tenant_id uuid NOT NULL` sütunu məcburidir.
   - Bütün tenant-owned cədvəllərdə `(tenant_id, id)` üzrə unikal məhdudiyyət (unique constraint), tenant-aware xarici açarlar (composite foreign keys) və indekslər tətbiq olunur.

2. **Tenant İdentifikasiyası və Resolution**:
   - Tenant heç vaxt etibarsız client sorğusunun `body` və ya `query` parametrlərindən birbaşa qəbul edilmir.
   - **Public / Storefront sorğularında**: Sorğunun `Host` header-i (domain) əsasında `platform.storefronts` cədvəlindən `storefront` və ona aid `tenant_id` təyin olunur.
   - **Admin sorğularında**: Tokenin `sub` claim-i əsasında `platform.tenant_memberships` cədvəlindən istifadəçinin üzv olduğu icazəli `tenant_id` təyin olunur.
   - **Partner sorğularında**: `client_id` / API credential əsasında icazəli `tenant_id` təyin olunur.
   - Əgər tenant konteksti tapılmazsa və ya uyğunsuzluq olarsa, sorğu DB-yə çatmadan `400 Bad Request` və ya `403 Forbidden` ilə rədd edilir.

3. **Verilənlər Bazası Səviyyəsində İzolyasiya (PostgreSQL RLS)**:
   - PostgreSQL Row Level Security (RLS) məcburidir:
     ```sql
     ALTER TABLE catalog.products ENABLE ROW LEVEL SECURITY;
     ALTER TABLE catalog.products FORCE ROW LEVEL SECURITY;
     CREATE POLICY tenant_isolation_policy ON catalog.products
       USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
     ```
   - `FORCE ROW LEVEL SECURITY` tətbiq edilir ki, table owner belə RLS-dən yayına bilməsin.
   - Runtime verilənlər bazası istifadəçisi `SUPERUSER`, cədvəl sahibi (table owner) və ya `BYPASSRLS` rolunda olmayacaq.
   - Tenant konteksti təyin edilmədikdə (`app.tenant_id` boş və ya null olduqda), verilənlər bazası heç bir tenant məlumatı qaytarmır (`NULLIF` -> `NULL`, heç bir `tenant_id` ilə uyğunlaşmır).

## Nəticələr
- **Müsbət tərəflər**:
  - Hər yeni tenant üçün DB provisioning ehtiyacı olmur (əməliyyat və infrastruktur xərcləri minimumdur).
  - Verilənlər bazası mühərriki səviyyəsində cross-tenant data sızması (data leakage) riskinin qarşısı qəti alınır.
  - Kompozit foreign key-lər fərqli tenant-lara məxsus entity-lərin bir-birinə bağlanmasının (məsələn Tenant A-nın məhsulunun Tenant B-nin ProductType-na bağlanmasının) qarşısını tam alır.
- **Mənfi tərəflər / Risk**:
  - Hər DB sorğusundan/tranzaksiyasından əvvəl `SET LOCAL app.tenant_id` sessiya konfiqurasiyasının təyin olunması interceptor və ya middleware vasitəsilə təmin edilməlidir.

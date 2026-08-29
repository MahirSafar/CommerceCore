# ADR 0003: Catalog-dan Pricing Kontekstinə Mərhələli Keçid və Miqrasiya Planı

## Status
Approved

## Kontekst
Mövcud Catalog MVP implementasiyasında sadə qiymət məlumatları birbaşa `Product` və `ProductVariant` entity-ləri üzərində (`Price` sahəsi kimi) saxlanılır. Gələcək e-commerce arxitekturasında qiymətləndirmə (Pricing Bounded Context — PriceBook, PriceList, Temporal pricing, Tiered pricing, Market-specific currency pricing) ayrıca modul kimi nəzərdə tutulur (Faza 4).

## Qərar
1. **Mövcud Qiymət Sahələrinin Qorunması**:
   - İndiki mərhələdə Catalog daxilindəki `Product.Price`, `ProductVariant.Price`, `ChangeProductPriceCommand` və müvafiq DB sütunları silinmir.
   - Səbəb: Mövcud Catalog MVP tam işlək vəziyyətdə qalmalıdır və kəsilməsiz inkişaf etdirilməlidir.

2. **Mərhələli Miqrasiya Strategiyası (Expand and Contract)**:
   - **Mərhələ 1 (Expand)**: Faza 4-də yeni `Pricing` modulu, `pricing.*` DB sxeması və aggregate-ləri (`PriceBook`, `PriceEntry`) yaradılır.
   - **Mərhələ 2 (Backfill)**: Mövcud Catalog cədvəllərindəki qiymət məlumatları Pricing cədvəllərinə miqrasiya (backfill script) olunur.
   - **Mərhələ 3 (Dual-Write / Read switch)**: Oxuma və hesablama sorğuları tədricən yeni `Pricing` moduluna yönləndirilir.
   - **Mərhələ 4 (Contract)**: Bütün oxuma və yazma əməliyyatları tam sabitləşdikdən sonra köhnə Catalog cədvəllərindəki `price` sütunları və köhnə qiymət kodları deprecate olunaraq ləğv edilir.

## Nəticələr
- **Müsbət tərəflər**:
  - Sıfır fasiləsizlik və arxitekturada "böyük partlayış" (big bang refactoring) riskinin qarşısının alınması.
  - Mövcud API və testlərin reqresiyasız işləməyə davam etməsi.

# CommerceCore — Strategiya, Arxitektura Qərarları və Tam İcra Yol Xəritəsi (v6)

**Status:** Approved architecture — execution gates pending
**Əhatə:** Tək backend, çoxlu storefront/frontend (veb, mobil, partner) idarə edən, multi-tenant, composable e-commerce platforması.
**İnfrastruktur hədəfi:** Google Cloud Platform (GCP). Bulud provayderi seçimi bu sənədin əhatəsindən kənar, əvvəlcədən qəbul edilmiş biznes qərarıdır (bax bölmə 21-in oxşar qərarlarla eyni statusu) — sənəd bütün infrastruktur qərarlarını bu məhdudiyyət daxilində, GCP-nin konkret idarə olunan xidmətlərinə istinadla verir.

---

## 1. Məqsəd və Miqyas

**Tək backend, çoxlu storefront/frontend (veb, mobil, partner) idarə edən, multi-tenant, composable e-commerce platforması** qurulur — kiçik komandanın indi idarə edə biləcəyi, lakin lazım gəldikdə hər hansı modulu ayrıca servisə çıxara biləcəyi bir təməl üzərində, tamamilə Google Cloud üzərində.

Bu sənədin əsas prinsipi: **hər texniki qərar ya sənayenin sınadığı presedentə, ya rəsmi sənədləşməyə, ya da ölçülə bilən performans/təhlükəsizlik məlumatına əsaslanır.** Fərziyyə əsasında qərar qəbul edilmir; qərarın əsaslandırılması mümkün olmadığı yerlərdə (bölmə 21) bu açıq şəkildə qeyd olunur və mühəndislik həllinə deyil, biznes/hüquqi qərara həvalə edilir. Eyni prinsip GCP xidmət seçimlərinə də tətbiq olunur: hər yerdə Google-un rəsmi sənədləşməsi, GA (Generally Available) statusu və müstəqil müqayisə mənbələri istinad edilir; hələ preview/beta statusunda olan xidmətlər bu qeydlə açıq şəkildə işarələnir.

Platformanın uzunmüddətli hədəfi Amazon və ya Trendyol səviyyəsində miqyas deyil (bax bölmə 20) — commercetools, Shopify Plus və Medusa.js səviyyəsində, kiçik-orta komandanın idarə edə biləcəyi, lazım gələndə genişlənə bilən composable commerce platformasıdır.

---

## 2. Texnologiya Seçimi

### 2.1 Backend: .NET 10 (LTS) + C# 14

**Dəstək müddəti:** .NET 10, 11 noyabr 2025-də buraxılıb və Microsoft-un rəsmi siyasətinə görə Long Term Support (LTS) buraxılışı kimi **2028-ci ilin 10 noyabrına qədər** (üç il) dəstəklənir. .NET 8 (LTS, 2023-cü il noyabrında çıxıb, 36 aylıq dəstək dövrü) və .NET 9 (STS, 2024-cü il noyabrında çıxıb) — hər ikisi — **2026-cı il 10 noyabrında** eyni gündə dəstəkdən çıxır. Bunun səbəbi Microsoft-un Standard Term Support (STS) buraxılışlarının dəstək müddətini 18 aydan 24 aya qədər uzatması siyasətidir: bu dəyişikliklə 2024 noyabrında çıxan .NET 9 (STS, indi 24 ay) və 2023 noyabrında çıxan .NET 8-in (LTS, 36 ay) dəstək bitmə tarixləri təsadüfən eyni günə düşüb. Praktiki nəticə: hazırda .NET 8 və ya .NET 9 istifadə edən istənilən komanda 2026-cı ilin noyabrına qədər məcburi miqrasiya qərarı qarşısındadır. .NET 11 (STS) də məhz həmin ay (10 noyabr 2026) buraxılacaq, lakin STS olduğu üçün miqrasiya hədəfi kimi seçilməsi əlavə, tezliklə təkrarlanacaq migrasiya yükü yaradar — LTS-dən LTS-ə keçid (.NET 8/9 → .NET 10) ən aşağı-riskli yoldur.
*Mənbə: Microsoft — "The official .NET support policy" (dotnet.microsoft.com/platform/support/policy); ".NET 8 and .NET 9 will reach End of Support on November 10, 2026" (devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support); "Announcing .NET 10" (devblogs.microsoft.com/dotnet/announcing-dotnet-10).*

**Performans:** TechEmpower Framework Benchmarks-ın son rəsmi turu (Round 23, fevral 2025) "Fortunes" testində (DB sorğusu + server-side template render — layihənin özünün "ən realistik ssenari" kimi tərif etdiyi test) ASP.NET Core-u populyar backend framework-lər arasında **#1** yerə qoyub:

| Sıra | Dil/Framework | Fortunes RPS | Nisbi əmsal |
|---|---|---|---|
| 1 | C# / ASP.NET Core | 609,966 | 36.3x |
| 2 | Go / Fiber | 338,096 | 20.1x |
| 3 | Rust / Actix | 320,144 | 19.1x |
| 4 | Java / Spring | 243,639 | 14.5x |
| 5 | Node.js / Express | 78,136 | 4.7x |
| 6 | Ruby / Rails | 42,546 | 2.5x |
| 7 | Python / Django | 32,651 | 1.9x |
| 8 | PHP / Laravel | 16,800 | 1.0x |

TechEmpower layihəsi 13 illik fəaliyyətdən sonra **2026-cı ilin 24 martında** rəsmən sonlandırılıb (GitHub reposu "archived, read-only" statusundadır) — komandanın öz açıqlamasına görə səbəb resurs məhdudiyyəti, dəyişən ekosistem (daha granular cloud-native observability alətləri) və 300+ framework-ü saxlamağın artan mühəndislik yükü olub. Bunun nəticəsi olaraq **Round 23 tarixi baxımdan son rəsmi TechEmpower nəticəsi olaraq qalır** və bundan sonra bu formatda rəsmi davamı olmayacaq. Bu rəqəmin necə oxunmalı olduğuna dair metodoloji qeyd üçün bax bölmə 20.
*Mənbə: TechEmpower Framework Benchmarks, Round 23, fevral 2025 (techempower.com/benchmarks/#section=data-r23); GitHub TechEmpower/FrameworkBenchmarks Issue #10932 — "Sunsetting the TechEmpower Framework Benchmarks", 24 mart 2026 (repo bu tarixdə arxivləşdirilib).*

**C# 14-ün domain-model kodu üçün konkret faydası** (.NET 10 ilə birgə, 11 noyabr 2025 buraxılışı):
- **`field` keyword** — auto-property-nin accessor-larında ayrıca private backing field yazmadan compiler-generated field-ə birbaşa çıxış verir; validasiya/normalizasiya məntiqi olan property-lərdə (məs. `Email`, `Money` value object-ləri) boilerplate-i əhəmiyyətli azaldır.
- **Extension members** — extension property, static extension member və extension operator dəstəyi; `extension(Type x) { ... }` blok sintaksisi ilə. Value Object-lərə (məs. `Money.IsZero`, `TenantId.IsSystem`) domain-ə uyğun oxunaqlı davranış əlavə etmək üçün faydalıdır, üçüncü tərəf tiplərini "bükmədən" zənginləşdirməyə imkan verir.
- **Əlavə faydalı dəyişikliklər:** null-conditional assignment (`obj?.Prop = value`), partial constructor və partial event dəstəyi (source generator ssenariləri üçün), generic tiplər üçün genişlənmiş `nameof` (`nameof(List<>)`), implicit span conversion-lar və compound assignment operator-ların yaxşılaşdırılması — bunların heç biri təkbaşına performans "möcüzəsi" yaratmır, lakin domain kodunun "ceremony"-dən təmizlənməsinə töhfə verir.
*Mənbə: Microsoft .NET Blog — "Introducing C# 14" (devblogs.microsoft.com/dotnet/introducing-csharp-14); Microsoft Learn — "What's new in C# 14" (learn.microsoft.com/dotnet/csharp/whats-new/csharp-14).*

**GCP üzərində .NET-in yetkinliyi:** .NET seçiminin GCP-yə köçürülməsi əlavə risk yaratmır — Cloud Run rəsmi sənədləşməsi .NET-i birinci dərəcəli dəstəklənən runtime olaraq təsvir edir (build-pack əsaslı avtomatik konteynerləşdirmə daxil olmaqla), GKE isə istənilən Linux konteynerini (o cümlədən .NET-i) native işlədir. Google həmçinin bütün əsas GCP xidmətləri (Cloud SQL, Pub/Sub, Secret Manager, Cloud Storage və s.) üçün rəsmi, aktiv dəstəklənən `Google.Cloud.*` NuGet client kitabxanalarını təmin edir — bu, "GCP-də .NET ikinci sinif vətəndaşdır" narahatlığını əsassız edir.
*Mənbə: Google Cloud Documentation — "The .NET runtime" (docs.cloud.google.com/run/docs/runtimes/dotnet); Google Cloud — .NET Client Libraries sənədləşməsi (docs.cloud.google.com/dotnet/docs/reference).*

**Nəticə:** dəstək müddəti, ölçülmüş performans profili, komandanın mövcud C#/.NET təcrübəsi və GCP-nin .NET-ə rəsmi dəstəyi eyni istiqaməti göstərir. Dil/framework dəyişikliyinin texniki əsası yoxdur (niyə Go/Rust/Elixir seçilmədiyi üçün bax bölmə 17).

### 2.2 Verilənlər bazası: Cloud SQL for PostgreSQL 18 (+ AlloyDB miqyaslanma yolu)

**Baza mühərriki qərarı dəyişmir — dəyişən yalnız onun hansı idarə olunan xidmətlə işlədiyidir.** PostgreSQL 18-in özü (aşağıda) eyni səbəblərlə seçilir; GCP üzərində bu, iki idarə olunan xidmətdən biri ilə təmin olunur.

**Row-Level Security (RLS)** PostgreSQL-in native mexanizmidir. `ALTER TABLE ... ENABLE ROW LEVEL SECURITY` və `CREATE POLICY` ilə hər sorğuya tenant-scoped `USING`/`WITH CHECK` şərti tətbiq olunur — multi-tenancy modelinin (bölmə 4) texniki təməlidir.
*Mənbə: PostgreSQL 18 Documentation — "5.9. Row Security Policies", "CREATE POLICY" (postgresql.org/docs/current).*

`ltree`, `JSONB`, `pg_advisory_xact_lock`, `xmin`-based optimistic concurrency — bunların hamısı artıq CommerceCore-un Catalog modulunda məhsuldar işləyir və miqrasiya xərci daşımır; bu extension-lar həm Cloud SQL, həm də AlloyDB-də dəstəklənir.

**PostgreSQL 18-in (25 sentyabr 2025 buraxılışı) əlavə xüsusiyyətləri:**
- **Asynchronous I/O (AIO)** — `io_uring`-ə əsaslanan yeni alt-sistem; sequential scan, bitmap heap scan və vacuum əməliyyatlarında real testlərdə **2–3 dəfə** sürətlənmə göstərir. `io_method`, `io_combine_limit` server dəyişənləri ilə tənzimlənir.
- **Native `uuidv7()` funksiyası** — vaxt-sıralı (time-ordered) UUID generasiyası; yüksək-yazma entity-lərdə (Order, Event, Outbox) `UUIDv4`-dən fərqli olaraq B-tree index lokallığını qoruyur və insert performansını yaxşılaşdırır.
- **Virtual generated columns** — indi generated column-lar üçün defolt rejim (əvvəllər yalnız `STORED` var idi); JSONB sahələrindən hesablanan sahələr disk yeri tutmadan sorğulana bilir (indekslənə bilmədiyi qeyd olunmalıdır — indeksləmə lazımdırsa `STORED` və ya expression index seçilməlidir).
- **B-tree "skip scan"** — çox-sütunlu indekslərdə prefiks sütunu bərabərlik şərti olmadan da indeks istifadəsinə imkan verir; paralel GIN index istifadəsi full-text/JSON sorğularını sürətləndirir.
- **`RETURNING OLD/NEW`** — `INSERT/UPDATE/DELETE/MERGE`-də həm köhnə, həm yeni sətir dəyərlərinə eyni anda çıxış; audit/event payload generasiyasını sadələşdirir.
*Mənbə: PostgreSQL 18.0 Release Notes (postgresql.org/docs/release/18.0); PostgreSQL — "PostgreSQL 18 Released" rəsmi press-kit (postgresql.org/about/news/postgresql-18-released-3142).*

**Təhlükəsizlik qeydi — CVE-2026-14666:** rol üzvlüyü/atributu və ya database ownership dəyişəndə, sistem plan reuse vasitəsilə əvvəlki (köhnə) RLS siyasətinə görə hazırlanmış cached query plan-ı istifadə etməyə davam edə bilirmiş — bu, rol-spesifik row-security siyasəti olan tətbiqlərdə icazəsi ləğv edilmiş istifadəçinin qısa müddət ərzində köhnə icazələrlə oxu/yazı əməliyyatı aparmasına imkan verib (CVSS v3.1: 4.2). Rəsmi düzəliş **2026-cı il 13 avqust** buraxılışındadır (18.6, 17.11, 16.15, 15.19, 14.24). Eyni buraxılışda ümumilikdə **28 CVE** düzəldilib — bunlardan **17-si CVSS 8.0 və yuxarı**, **9-u birbaşa "arbitrary code execution"** səviyyəsindədir — bu, PostgreSQL layihəsinin tarixində tək buraxılışda düzəldilən ən böyük CVE sayıdır. **PostgreSQL 14-ün rəsmi dəstək müddəti 12 noyabr 2026-da bitir.** Bu, GCP-də idarə olunan xidmət seçiminin əlavə praktiki üstünlüyünü göstərir: həm Cloud SQL, həm AlloyDB minor versiya təhlükəsizlik yamalarını **avtomatik** tətbiq edir (planlaşdırılmış texniki xidmət pəncərəsi ilə), yəni "bir dəfə quraşdır, unut" riskinin əməliyyat yükü Google-un üzərinə keçir — özü-host edilən Postgres-də bu, komandanın davamlı, manual izləmə öhdəliyi olardı.
*Mənbə: PostgreSQL — "CVE-2026-14666" (postgresql.org/support/security/CVE-2026-14666); "PostgreSQL 18.6, 17.11, 16.15, 15.19, 14.24 and 19 Beta 3 Released!" (postgresql.org, 13 avqust 2026); HeroDevs — "PostgreSQL 14 EOL Nov 2026: 44 CVEs This Year, One Patch Left".*

**Qərar: Cloud SQL for PostgreSQL 18 (Enterprise/Enterprise Plus edition) — default idarə olunan xidmət.** PostgreSQL 18 Cloud SQL-də **artıq GA (Generally Available)** statusundadır (əvvəlcə 2025-ci ilin oktyabrında preview, sonra tam GA elan olunub) — yəni platformanın 2.2-də təsvir olunan bütün PG18 xüsusiyyətləri (RLS, AIO/`io_uring`, native `uuidv7()`, virtual generated columns, B-tree skip scan) birbaşa əlçatandır. Cloud SQL Enterprise Plus edition-da AIO/`io_method` tənzimləmələri açıq şəkildə dəstəklənir və sənədləşdirilib (bax bölmə 19-un yük testi ilə əlaqəsi). Cloud SQL avtomatik backup, on-demand backup, Point-in-Time Recovery (PITR), GKE/BigQuery/Cloud Run ilə native inteqrasiya və 100-dən çox tənzimlənə bilən flag təmin edir — kiçik-orta komanda üçün ən aşağı əməliyyat yükü olan başlanğıc nöqtəsidir, "Pool" multi-tenancy modeli (bölmə 4) ilə birbaşa uzlaşır.
*Mənbə: Google Cloud Documentation — "Cloud SQL for PostgreSQL release notes" (docs.cloud.google.com/sql/docs/postgres/release-notes); Google Cloud — "Cloud SQL for PostgreSQL features" (docs.cloud.google.com/sql/docs/postgres/features); Kumar Ramamurthy — "Postgres 18 on Cloud SQL Enterprise Plus" (Google Cloud Community, Medium, dekabr 2025).*

**Miqyaslanma yolu — AlloyDB for PostgreSQL:** əgər gələcəkdə tranzaksiya həcmi/paralel sorğu yükü Cloud SQL-in tavanına yaxınlaşarsa, **AlloyDB for PostgreSQL** wire-protokol səviyyəsində tam PostgreSQL-uyğun, ayrılmış compute/storage arxitekturalı, yüksək-performanslı alternativdir — miqrasiya "yeni verilənlər bazası öyrənmək" deyil, eyni SQL/driver/ORM ilə işləyən idarə olunan xidmət dəyişikliyidir (Database Migration Service ilə minimal-downtime keçid dəstəklənir). AlloyDB-də də PostgreSQL 18 **GA** statusundadır və eyni B-tree skip scan, paralel GIN, virtual generated columns, native UUIDv7 xüsusiyyətlərini təmin edir. Müstəqil pgbench testləri AlloyDB-nin yüksək-konkurrensiya OLTP ssenarilərində Cloud SQL Enterprise Plus-a nisbətən əlavə performans üstünlüyü göstərdiyini qeyd edir, lakin bu, ~1.5–2x daha yüksək compute-vahid xərci ilə gəlir. **Bu səbəbdən AlloyDB-yə keçid gündən əvvəl planlaşdırılmır** — bölmə 4.1-dəki "Pool→Bridge→Silo" təkamül məntiqinin eyni forması: real yük Cloud SQL-in limitlərini sübut edəndə (Faza 19-un yük testi bunu ölçəcək) qiymətləndirilir, əvvəlcədən yox. Qeyd: AlloyDB-də PostgreSQL 18 seçərkən standby-dan logical replication dəstəklənmir — bu, gələcək DR dizaynında (bölmə 12.1) nəzərə alınmalıdır.
*Mənbə: Google Cloud Blog — "Postgres 18 and Extended Support for legacy versions in AlloyDB" (cloud.google.com/blog/products/databases/postgres-18-and-extended-support-for-legacy-versions-in-alloydb); Google Cloud Documentation — "AlloyDB for PostgreSQL — Database version policies" (docs.cloud.google.com/alloydb/docs/db-version-policies); Suds Kumar — "Cloud SQL Enterprise Plus vs. AlloyDB: A pgbench Showdown" (Google Cloud Community, Medium).*

**Nəticə:** başqa DB mühərrikinə keçid (MySQL, SQL Server, MongoDB — niyə rədd edildiyi üçün bax bölmə 17) RLS, `ltree`, native `uuidv7()` və AIO kimi faydaları itirmək deməkdir; GCP-nin təklif etdiyi iki idarə olunan PostgreSQL-uyğun xidmət (Cloud SQL → AlloyDB) bu faydaları saxlayaraq miqyaslanma yolu açıq saxlayır.

### 2.3 Cache və Idempotency Store: Memorystore for Valkey

Redis Ltd. 2024-cü ilin martında (20 mart) lisenziyanı BSD 3-Clause-dan RSALv2/SSPL-ə (sonra Redis 8.0 ilə AGPLv3 seçimi də əlavə olunub) dəyişdi — bu, Open Source Initiative-in tərifinə görə artıq "open source" sayılmır, çünki bulud provayderlərinin idarə olunan xidmət kimi təklif etməsini kommersiya sazişi tələbi ilə məhdudlaşdırır. Cavab olaraq **Valkey** — AWS, Google Cloud, Oracle, Ericsson, Snap və digərlərinin son BSD-lisenziyalı versiyanı (Redis 7.2.4) fork edib 2024-cü ilin martında Linux Foundation-a verdiyi layihə — "wire-compatible" (RESP2/RESP3 protokolu identik, mövcud client kitabxanaları — `StackExchange.Redis` daxil olmaqla — dəyişiklik tələb etmir) drop-in əvəzedicidir.
*Mənbə: Linux Foundation — "Linux Foundation Launches Open Source Valkey Community" (28 mart 2024); TechCrunch — "Why AWS, Google and Oracle are backing the Valkey Redis fork" (31 mart 2024).*

**Qərar: Memorystore for Valkey.** Google Cloud burada həm layihənin qurucu-təsisçilərindən biri, həm də onu ilk növbədə tam idarə olunan xidmət kimi təklif edən bulud provayderlərindən biridir: Memorystore for Valkey ilk dəfə 2024-cü ilin avqustunda preview olaraq buraxılıb, 2025-ci ilin aprelində **GA** elan olunub (99.99% əlçatanlıq SLA-sı, Private Service Connect, multi-VPC giriş, cross-region replikasiya və persistence dəstəyi ilə), 2026-cı ilin martında isə **Valkey 9.0 GA**-ya yenilənib (əvvəlki versiyaya nisbətən 40%-ə qədər daha yüksək throughput). Bu, platformanın həm "sadə key-value cache/idempotency-key store" ssenarisinə tam uyğun, həm də GCP-nin özü tərəfindən ilk sırada dəstəklənən, tam idarə olunan seçimdir — özəl infrastruktur/patch idarəçiliyi tələb olunmur.
*Mənbə: Google Cloud Blog — "Announcing general availability of Memorystore for Valkey" (cloud.google.com/blog/products/databases/announcing-general-availability-of-memorystore-for-valkey, aprel 2025); Google Cloud Blog — "Memorystore for Valkey 9.0 is now GA" (mart 2026).*

**İstisna:** Redis Stack-in xüsusi modullarına (RedisSearch, RedisJSON) ehtiyac yaranarsa, Valkey-də hələ tam yetkin ekvivalent yoxdur. Bizim ssenarimizdə (cache + idempotency-key store) bu modullara ehtiyac yoxdur.

### 2.4 Axtarış: Vertex AI Search for Commerce (default) və ya özü-idarə edilən OpenSearch (GKE)

Elasticsearch 2021-ci ildə Apache 2.0-dan SSPL/Elastic License-ə keçdi. **OpenSearch** elə bu səbəbdən 2021-ci ildə AWS tərəfindən Elasticsearch-ün son Apache 2.0 versiyasının fork-u kimi doğulub və 2024-cü ilin 16 sentyabrında AWS onu vendor-neytral **OpenSearch Software Foundation**-a (Linux Foundation çətiri altında) transfer edib.
*Mənbə: Linux Foundation — "Linux Foundation Announces OpenSearch Software Foundation to Foster Open Collaboration in Search and Analytics" (16 sentyabr 2024); AWS Open Source Blog — "AWS Welcomes the OpenSearch Software Foundation".*

**Vacib fərq — GCP-də native idarə olunan OpenSearch yoxdur.** AWS-dən fərqli olaraq, Google Cloud OpenSearch üçün birinci-tərəf idarə olunan xidmət təklif etmir; mövcud yollar: (a) GKE üzərində özü-idarə edilən klaster, (b) Elastic Cloud on Google Cloud (üçüncü-tərəf, Google Cloud Marketplace vasitəsilə), (c) Aiven/Instaclustr/Bonsai kimi üçüncü-tərəf idarə olunan OpenSearch xidmətləri. Bu, platformanın "kiçik komanda özü idarə edə bilsin" prinsipini (bölmə 1) OpenSearch seçimi üçün əlavə əməliyyat yükü ilə qarşı-qarşıya qoyur.
*Mənbə: BigDataBoutique — "Google Cloud OpenSearch: Deployment Options and Best Practices" (2026).*

**Qərar: Vertex AI Search for commerce (Retail API) — default, məhsul axtarışı üçün ilk seçim.** Bu, Google-un birbaşa e-commerce kataloq axtarışı üçün qurduğu, tam idarə olunan xidmətdir (Retail Search + Browse + Recommendations API-lərindən ibarətdir): məhsul kataloqu import olunur (Cloud Storage/BigQuery/API), Google-ın öz axtarış/ranking mühərriki relevantlıq, faceting, sinonim/orfoqrafiya səhvi toleransı və (istəyə görə) fərdiləşdirməni "sıfırdan qurmadan" təmin edir. Bu, platformanın "axtarış relevantlıq mühəndisliyi biznes fərqləndiricisi deyil, məhsul kataloqu keyfiyyəti fərqləndiricidir" fərziyyəsi ilə uzlaşır (bax bölmə 20) — kiçik komanda üçün OpenSearch klasterini quraşdırıb saxlamaqdan daha az əməliyyat yükü yaradır.
- **Güzəşt (trade-off):** bu, proprietary Google API-dir — açıq mənbə/vendor-neytral idarəçilik prinsipi (bölmə 2.4-ün əvvəlki versiyasında Redis/Elasticsearch presedentinə istinadla qoyulan) burada tətbiq olunmur. Qərar şüurlu şəkildə "əməliyyat yükünün minimuma enməsi" prinsipini "vendor lock-in-dən qaçınma" prinsipindən üstün tutur, çünki axtarış in-house domen məntiqi deyil, xarici sifariş edilə bilən keyfiyyətdir.
*Mənbə: Google Cloud Documentation — "Vertex AI Search for commerce API" (docs.cloud.google.com/retail/docs/reference/rpc); Google Cloud — "AI Commerce Search" məhsul səhifəsi (cloud.google.com/solutions/vertex-ai-search-commerce).*

**Vendor-neytral alternativ (portativlik prioritet olduqda): özü-idarə edilən OpenSearch, GKE üzərində.** Əgər komanda proprietary API-dən tam qaçınmağı üstün tutursa (məs. gələcəkdə multi-cloud strategiyası nəzərdə tutulursa, ya da axtarış üzərində tam sorğu-səviyyəli nəzarət lazımdırsa), OpenSearch GKE-də StatefulSet olaraq işə salına bilər — Faza 7-də təsvir olunan "seam" (Catalog-un Search-ə göndərdiyi event kontraktı) bu seçimi kod dəyişmədən dəstəkləyəcək şəkildə dizayn olunur, beləliklə Vertex AI Search for Commerce ↔ özü-idarə edilən OpenSearch arasında keçid arxitektura səviyyəsində açıq qalır.

### 2.5 Mesajlaşma: Pub/Sub / Managed Service for Apache Kafka / RabbitMQ (extraction günü)

İndi tələb olunmur — in-process bus abstraksiyası (`IIntegrationEventPublisher`/`Subscriber`, bax bölmə 7) bu keçidi kod dəyişmədən dəstəkləyəcək şəkildə əvvəlcədən dizayn olunur. Broker seçimi yalnız real trafik/throughput ehtiyacı sübut olunanda (Faza 17-yə bənzər gate məntiqi ilə) qiymətləndirilməlidir — vaxtından əvvəl broker seçmək əməliyyat mürəkkəbliyini əsassız artırar.

Bu keçid günü gələndə GCP üç fərqli-səviyyəli seçim təqdim edir:
- **Google Cloud Pub/Sub** — tam idarə olunan, serverless, sıfıra qədər miqyaslana bilən mesajlaşma xidməti; broker/partition/consumer-group idarəçiliyi tələb etmir, GKE/Cloud Run/Cloud Functions ilə native inteqrasiya edir. At-least-once çatdırma təmin edir, `ordering_key` ilə açar-səviyyəli sıralama dəstəkləyir (qlobal sıralama yoxdur). Ən aşağı əməliyyat yükü olan defolt seçimdir — "sadə fan-out, tenant-səviyyəli decoupling" ssenarisi üçün kifayətdir.
- **Google Cloud Managed Service for Apache Kafka** — tam idarə olunan (broker sizing/rebalancing avtomatik, Cloud Monitoring/Logging/IAM native), lakin Kafka API-uyğun xidmət; partition-səviyyəli sərt sıralama, uzunmüddətli log retention/replay və Kafka Connect ekosistemi lazım olanda seçilir (məs. audit-log tipli uzunmüddətli event saxlama, ya da mövcud Kafka təcrübəsi olan komanda).
- **RabbitMQ** (özü-idarə edilən, GKE üzərində) — komanda artıq AMQP/RabbitMQ təcrübəsinə malikdirsə.

Seçim meyarı sadədir: sırf fan-out/decoupling → Pub/Sub; uzunmüddətli replay/sərt partition ordering/Kafka ekosistemi → Managed Service for Apache Kafka; mövcud RabbitMQ təcrübəsi → RabbitMQ. Hər üçü də `IIntegrationEventPublisher` abstraksiyası arxasında dəyişdirilə bilər.
*Mənbə: Google Cloud — "Pub/Sub" məhsul sənədləşməsi; Google Cloud Documentation — "Managed Service for Apache Kafka overview" (docs.cloud.google.com/managed-service-for-apache-kafka/docs/overview); Confluent — "Apache Kafka® vs Pub/Sub: Key Differences Explained" (confluent.io/compare/kafka-vs-pubsub).*

### 2.6 Yerli İnkişaf Orkestrasiyası və Production Yolu: .NET Aspire → GKE Autopilot / Cloud Run

.NET Aspire — Microsoft-un rəsmi cloud-native stack-i: `ServiceDefaults` layihəsi konsistent health check/telemetry/resilience (Polly) verir; `AppHost` layihəsi bir əmrlə bütün modulları, BFF-ləri, Valkey-i və Postgres-i orkestrasiya edir, local-da işlədilən məntiqi service discovery təmin edir. Bu, bulud provayderindən asılı olmayan, tamamilə local-dev qatıdır — GCP seçimi bu qatı dəyişdirmir.

**Production topologiyasına keçid.** Aspire-ın deployment tərəfi `aspire publish`/`aspire deploy` əmrləri ilə genişlənə bilən "publisher" modeli üzərində işləyir. Bu modelin GCP üçün ən yetkin, rəsmi dəstəklənən yolu bulud-agnostik olanıdır:
- **Kubernetes (→ GKE Autopilot)** — `Aspire.Hosting.Kubernetes` inteqrasiyası ilə: AppHost tərifindən birbaşa **Helm chart** generasiya edilir (`aspire publish`) və ya cari `kubectl` kontekstinə tətbiq edilə bilər (`aspire deploy`). Bu inteqrasiya bulud-neytraldır — generasiya olunan Helm chart-ı olduğu kimi GKE-yə tətbiq etmək mümkündür, əlavə Google-spesifik adaptasiya tələb olunmur. Bu, Aspire-ın hazırda GCP üçün ən "sürtünməsiz" rəsmi yoludur.
- **Docker Compose** — sadə, tək-server və ya erkən staging ssenariləri üçün alternativ export formatı; Compute Engine üzərində və ya lokal sınaqda istifadə oluna bilər.

**GKE Autopilot niyə "gündəm-0 Kubernetes" riskindən fərqlidir (bax bölmə 17):** GKE-nin iki rejimi var — Standard (node pool-ların özü tərəfindən idarə edilməsini tələb edir) və **Autopilot** (node təminatı, təhlükəsizlik yamalanması və resurs optimallaşdırması tamamilə Google tərəfindən idarə olunur, komanda yalnız Pod/Deployment tərifini yazır, ödəniş pod resurslarına görə hesablanır). Kiçik komanda üçün narahatlıq yaradan "Kubernetes-in operativ mürəkkəbliyi" (RBAC, node pool yükseltmə, resurs planlaması) məhz Standard rejimə aiddir — Autopilot bunun böyük hissəsini aradan qaldırır. Bu səbəbdən "gündəm-0 özü-idarə edilən Kubernetes" hələ də rədd edilir (bax bölmə 17-nin yenilənmiş sətri), lakin "Aspire → Helm → GKE Autopilot" yolu bu riski daşımır və default production hədəfi kimi tövsiyə olunur.

**Alternativ/tamamlayıcı yol — Cloud Run.** Cloud Run, Google-un tam serverless konteyner xidmətidir: cluster idarəçiliyi, node planlaması yoxdur, sıfıra qədər miqyaslanır (bursty trafikdə xərci azaldır), HTTP-yönümlü BFF/API workload-lar üçün ən sürətli, ən aşağı-əməliyyatlı yoldur. Hazırda Aspire-ın rəsmi publisher-i Cloud Run üçün birbaşa manifest generasiya etmir (Azure Container Apps üçün olduğu kimi) — bu səbəbdən Cloud Run-a keçid, Aspire-ın `publish`/`deploy` axınından kənarda, standart konteyner CI/CD boru xətti ilə (Cloud Build → Artifact Registry → `gcloud run deploy`, bax bölmə 13.2) aparılır. Praktikada sənayədə geniş yayılmış hibrid nümunə budur: **stateless, HTTP-yönümlü BFF-lər Cloud Run-da, uzunmüddətli/arxa-plan proseslər (Outbox Publisher Worker, gələcək Kafka/Pub/Sub consumer-ləri, özü-idarə edilən OpenSearch seçilərsə) GKE Autopilot-da.** Bu hibrid, hər iki xidmətin güclü tərəfini birləşdirir və Faza 13-də (BFF-lər) qiymətləndirilə bilər.
*Mənbə: Microsoft — .NET Aspire rəsmi sənədləşməsi (learn.microsoft.com/dotnet/aspire); Aspire rəsmi inteqrasiya sənədləşməsi — "Kubernetes integration for Aspire: hosting and client wiring" (aspire.dev/integrations/compute/kubernetes); Google Cloud Documentation — "GKE Autopilot overview"; CloudWebSchool — "GKE vs Cloud Run: Cost, Complexity, and When to Use Each in GCP" (2026); Pixel Guild — "Kubernetes on GKE: When to Use It and When Cloud Run Is Enough" (2026).*

Konkret production hədəfi (yalnız GKE Autopilot, yalnız Cloud Run, ya da hibrid) komandanın operativ təcrübəsinə bağlıdır (bax bölmə 21) — lakin bütün hallarda **local orkestrasiya təsviri ilə production topologiyası arasında əl ilə sinxronlaşdırma tələb olunmur**, bu, .NET Aspire seçiminin əsas praktiki faydasıdır.

---

## 3. Memarlıq Fəlsəfəsi: Modular Monolith

### 3.1 Nəzəri əsas

Martin Fowler-in *"MonolithFirst"* məqaləsi (3 iyun 2015): uğurlu mikroservis sistemlərinin demək olar hamısı əvvəlcə monolit kimi başlayıb, sonra böyüyüb bölünüb; sıfırdan mikroservis kimi qurulan layihələrin əksəriyyəti ciddi problemlərlə üzləşib. Səbəb: mikroservis yalnız bounded context-lər arasında sabit, düzgün sərhədlər olduqda işləyir — bu sərhədləri real istifadədən əvvəl təxmin etmək çətindir, səhv erkən bölmə isə yanlış sərhədləri "dondurur" və sonradan düzəltmək baha başa gəlir.
*Mənbə: Martin Fowler — "MonolithFirst" (martinfowler.com/bliki/MonolithFirst.html, 3 iyun 2015).*

### 3.2 Sənaye presedenti: Shopify

Shopify 2019-cu ildə mikroservisə keçidi açıq şəkildə rədd edib, David Heinemeier Hansson-un termini ilə "Majestic Monolith" adlandırdıqları modular monolit yanaşmasını seçib. Bugün Shopify-ın "Core" monoliti **3 milyondan çox sətir Ruby on Rails kodu**dur və 2019-cu ildə platforma Black Friday zirvəsində **saniyədə 1,27 milyon sorğu** emal edib — bu, modular monolitin yüksək miqyasda işlədiyinin praktiki sübutudur.

Shopify sərhədləri öz açıq mənbəli alətləri **Packwerk** ilə compile/CI səviyyəsində enforce edir (modullar arası icazəsiz reference build-i fail etdirir). Tenant izolyasiyası üçün fiziki səviyyədə "pod" modelini istifadə edirlər: bütün shop data-sı `shop_id`-i sharding açarı kimi istifadə edən müstəqil MySQL klaster qruplarına ("pod") bölünür — bu, "noisy neighbor" riskini azaldır və bir insidentin təsir dairəsini ("blast radius") kiçik bir tenant qrupu ilə məhdudlaşdırır; stateless komponentlər isə Kubernetes ilə avtomatik horizontal miqyaslanır. Bu, bizim `tenant_id`-based RLS + Pool modelimizlə (bölmə 4) eyni məntiqin fərqli fiziki tətbiqidir.
*Mənbə: Shopify Engineering / Dr Milan Milanović — "Inside Shopify's Modular Monolith" (newsletter.techworld-with-milan.com); Shopify Engineering — "Enforcing Modularity in Rails Apps with Packwerk"; InfoQ — "How Shopify Migrated to a Modular Monolith" (Shopify Unite 2019 təhlili).*

**Nəticə:** "modular monolith + tenant_id + gələcək extraction" strategiyası, sənayenin ən böyük e-commerce oyunçularından birinin böyük miqyasda sınadığı və müvəffəq olduğu yoldur.

### 3.3 Fiziki struktur: .NET referens memarlığı

Referens: **Kamil Grzybek-in "Modular Monolith with DDD"** layihəsi (GitHub-da ~14,000 ulduz, MIT lisenziyalı, .NET/C# ilə yazılıb) — sənayədə bu nümunənin bu qədər tam (Domain/Application/Infrastructure/Contracts ayrımı, CQRS, event-driven modul kommunikasiyası, real test strukturu) təqdim edildiyi az sayda açıq mənbəli referensdən biridir. Hər modul öz layihə dəstinə malikdir (Domain/Application/Infrastructure/Contracts), modullar yalnız bir-birinin `Contracts` layihəsinə reference verə bilər.
*Mənbə: Kamil Grzybek — "Modular Monolith with DDD" (github.com/kgrzybek/modular-monolith-with-ddd).*

**Modul daxili təşkilat — Vertical Slice Architecture:** Application qatı Service/Repository kimi texniki qatlar üzrə deyil, hər feature/use-case üçün ayrıca "slice" (Command/Query + Handler + Validator + DTO) təşkil olunur — bu, bir feature üzərində işləyərkən kodun bir neçə texniki qat arasında "sıçramaq" ehtiyacını aradan qaldırır.

**Yekun struktur:**
```
Modules/{Catalog, Pricing, Inventory, Commerce, Promotion, Payment, Fulfillment, Search,
         Customer, Notification, Tax}/
    {Module}.Domain
    {Module}.Contracts            ← yalnız bura başqa modul reference verə bilər (sinxron)
    {Module}.IntegrationEvents    ← yalnız versioned event contract (asinxron)
    {Module}.Application/
        Features/{FeatureName}/   ← Vertical Slice: Command|Query + Handler + Validator + DTO
    {Module}.Infrastructure
Platform/
    Platform.Contracts            (TenantId, StorefrontId, MarketId)
    Platform.EventBus             (bus abstraksiyası — Pub/Sub, Kafka, RabbitMQ, in-process)
    Platform.ControlPlane         (TenantRegistry, TenantProvisioningService)
    Platform.Identity             (OIDC/JWT doğrulama, claims→TenantContext mapping, bax bölmə 5)
Api/
    StorefrontApi, AdminApi, PartnerApi   (3 BFF — Cloud Run-da və ya GKE-də)
    Gateway/                       (ixtiyari: Cloud Armor arxasında rate limiting/per-tenant throttling, bax bölmə 10.3)
AppHost/                          (.NET Aspire orkestrasiya, ServiceDefaults)
```
Bu struktur `NetArchTest` ilə CI-da enforce olunur: bir modulun digərinin `Domain`/`Application`/`Infrastructure`-inə reference verməsi build-i fail etdirir — bu, Shopify-ın Packwerk-lə etdiyinin .NET ekvivalentidir.

---

## 4. Multi-Tenancy Modeli

### 4.1 İzolyasiya strategiyası: Pool

AWS-in *"SaaS Tenant Isolation Strategies"* whitepaper-i üç modeli təsvir edir: **Silo** (ayrıca infrastruktur, yüksək təhlükəsizlik/xərc), **Pool** (paylaşılan infrastruktur, "noisy neighbor" riski, aşağı xərc), **Bridge** (hibrid — bəzi resurslar paylaşılır, bəziləri izolyasiya olunur). Bu konseptual model bulud provayderindən asılı deyil — GCP-də tətbiqi aşağıda təsvir olunur.

**Qərar: Pool (default) + Bridge (enterprise tier üçün gələcək seçim).** Shared Cloud SQL for PostgreSQL + `tenant_id` + RLS — AWS-in "pool" modelinin dəqiq tərifidir. Bu, komandanın operativ yükünü kiçik saxlayır (bir verilənlər bazası klasteri idarə edilir) və xərci minimuma endirir, "gec silo-laşdırma" imkanını (Bridge → Silo təkamülü) açıq saxlayır.
*Mənbə: AWS Whitepaper — "SaaS Tenant Isolation Strategies" (docs.aws.amazon.com/whitepapers).*

**Bridge/Silo-ya keçidin GCP-dəki fiziki mexanizmi:** əgər gələcəkdə konkret tenant(lar) üçün region-pinned data ya da fiziki izolyasiya lazım olarsa (bax bölmə 12.3), bunun GKE-dəki tətbiqi namespace-per-tenant modelidir — Google-un öz "Best practices for enterprise multi-tenancy" sənədləşməsi tenant layihələrini/namespace-ləri RBAC və şəbəkə izolyasiyası ilə ayırmağı tövsiyə edir. Yəni Bridge/Silo qərarı (bölmə 21-də açıq sual olaraq qalır) gələndə, onu icra edəcək GCP-spesifik mexanizm artıq sənədləşdirilmiş, sınanmış bir naxışdır — yeni araşdırma tələb etmir.
*Mənbə: Google Cloud Documentation — "Best practices for enterprise multi-tenancy" (docs.cloud.google.com/kubernetes-engine/docs/best-practices/enterprise-multitenancy).*

### 4.2 Control Plane / Application Plane ayrımı

- **Control Plane** — tenant registry, onboarding/provisioning, tarifləşdirmə siyasəti, metering.
- **Application Plane** — faktiki multi-tenant tətbiq, hər sorğuda RLS enforce olunur.
*Mənbə: AWS Whitepaper — "SaaS Architecture Fundamentals" (docs.aws.amazon.com/whitepapers).*

**Qərar:** `Platform.ControlPlane`: `TenantRegistry`, `TenantProvisioningService`. Faza 0-da manual/CLI provisioning, Faza 14-də tam self-service + metrikaları toplama.

### 4.3 Tenant Resolution

- **Public sorğu:** Host → Storefront → Tenant → Market → Locale.
- **Admin sorğu:** JWT subject + tenant membership + scope → aktiv Tenant konteksti. JWT-nin haradan gəldiyi (identity provider) bölmə 5-də təyin olunur.
- **Partner sorğu:** client credential + icazə verilmiş tenant/storefront → kontekst.

`tenantId` heç vaxt client-in sərbəst göndərdiyi query/body field-i deyil; naməlum domain və ya tenant mismatch dərhal rədd edilir. Bu, ən çox rast gəlinən multi-tenant təhlükəsizlik boşluğunun (client-supplied tenant identifier) qarşısını əvvəlcədən alır.

---

## 5. Identity, Autentifikasiya və Customer Konteksti

### 5.1 Prinsip

Autentifikasiya (kim olduğunu sübut etmək) və Identity/Customer (biznes profili) iki ayrı konsern kimi dizayn olunur. Parol saxlama, token imzalama, MFA, sosial giriş kimi funksionallıq sıfırdan qurulmur — sənayenin sınadığı, xüsusi bu iş üçün hazırlanmış alətlə həll olunur; bu, Payment ACL-də tətbiq olunan eyni prinsipin (bölmə 9-un Faza 9-u, bölmə 10.2) autentifikasiya sahəsinə tətbiqidir.

### 5.2 Qərar: xarici, OIDC-uyğun Identity Provider

Öz-özünə host edilə bilən seçim kimi **Keycloak** tövsiyə olunur: 2014-cü ildə Bill Burke və Stian Thorgersen tərəfindən yaradılıb, Apache 2.0 lisenziyalıdır, **2023-cü ilin 10 aprelindən CNCF-in incubating layihəsidir** (vendor-neytral idarəçilik). Layihə 8 ildən çoxdur production-da istifadə olunur (CERN, Accenture, Cisco, Hitachi kimi təşkilatlar daxil olmaqla) və OIDC/OAuth2/SAML2, multi-realm (hər tenant üçün ayrıca realm və ya bir realm daxilində rol-based ayrım) dəstəyi verir. Keycloak GKE-də (StatefulSet + Persistent Volume, ya da öz Postgres backend-i Cloud SQL-də) ya da Cloud Run-da (stateless rejimdə, xarici Postgres ilə) işə salına bilər.
*Mənbə: CNCF — "Keycloak joins CNCF as an incubating project" (cncf.io/blog/2023/04/11); CNCF — "Keycloak" layihə səhifəsi (cncf.io/projects/keycloak); Keycloak layihəsi, Apache License 2.0.*

**Alternativ: idarə olunan xidmət.** Komanda öz IdP-ni əməliyyat baxımından saxlamaq istəmirsə, iki GCP-uyğun idarə olunan seçim mövcuddur: **Google Cloud Identity Platform** (Firebase Authentication infrastrukturu üzərində qurulmuş, GCP-native CIAM xidməti, OIDC/SAML federasiyası dəstəkləyir) və ya **Auth0** (bulud-agnostik, geniş yayılmış CIAM platforması, GCP Marketplace vasitəsilə də əlçatandır). Konkret seçim komandanın DevOps tutumuna görə edilir (bax bölmə 21).
*Mənbə: Google Cloud — "Identity Platform" məhsul sənədləşməsi (cloud.google.com/security/products/identity-platform).*

`Platform.Identity` — nazik middleware layihəsi: OIDC token-i doğrulayır (issuer, audience, imza, expiry), claim-lərdən `TenantId`/`UserId`/`Scopes`-i çıxarır və `TenantContext`-ə map edir. Domain/Application qatları IdP-nin adını belə tanımır — yalnız `ICurrentUserContext` interfeysini görür.

**Biznes profili — daxili `Customer` modulu (bölmə 15, Faza 10):** `CustomerProfile` aggregate xarici IdP-nin `sub` claim-i ilə əlaqələnir, amma parol/token saxlamır — yalnız ünvanlar, sifariş tarixçəsi görünüşü, marketinq razılığı (consent), loyalty ledger kimi biznes datasını saxlayır. Bu ayrım vacibdir: autentifikasiya təhlükəsizlik-kritik, tez-tez dəyişməyən, "həll edilmiş" bir sahədir; Customer isə davamlı inkişaf edən biznes domenidir.

### 5.3 Nəticə

Parol/token təhlükəsizliyi ixtisaslaşmış sahədir, səhvi bahalıdır (data pozuntusu, hüquqi məsuliyyət). Sənayenin illərlə sınadığı, vendor-neytral idarəçiliyi olan alət seçimi bölmə 2.4-də qoyulan prinsiplə tam uzlaşır.

---

## 6. Composable Commerce Uyğunluğu (MACH)

MACH Alliance — **2020-ci ilin iyununda** Commercetools, Contentstack, EPAM Systems və Valtech tərəfindən qurulan (10 əlavə təsisçi üzvlə birgə başlayan), vendor-neytral, kar məqsədi güdməyən sənaye təşkilatı — rəsmi prinsipləri: **M**icroservices-based, **A**PI-first, **C**loud-native, **H**eadless. Alliance 2023-2024-cü illər ərzində 100-dən çox üzvə qədər böyüyüb.
*Mənbə: MACH Alliance rəsmi sayt (machalliance.org); EPAM — "EPAM Joins Newly Formed MACH Alliance as Founding Member" (24 iyun 2020 mətbuat açıqlaması).*

"Microservices-based" sənayədə artıq "hər business capability müstəqil inkişaf edilə bilər" mənasında da qəbul edilir — modul-based monolit + ciddi context sərhədləri + versioned API/event contract-lar bunu təmin edir. Cloud-native isə .NET/PostgreSQL-in GKE/Cloud Run üzərində konteynerləşdirilmiş, stateless deployment modeli ilə üst-üstə düşür. Headless prinsipi bizim BFF qatının (bölmə 8) storefront-u backend-dən tam ayırması ilə birbaşa üst-üstə düşür.

**Nəticə:** seçilən yol MACH-ın ruhuna uyğundur — "M" hərfini "hər business capability lazım olanda ayrıla bilər" mənasında oxumaq, Fowler/Shopify presedenti ilə tam uzlaşır.

---

## 7. Event-Driven Təməl

Bu tip sistemlər üçün canonical mənbə mikroservis pattern-lərinin ilkin sistemləşdiricisi olan Chris Richardson-un iki pattern-idir:
- **Transactional Outbox pattern** — biznes dəyişikliyi və hadisənin eyni tranzaksiyada yazılması, ayrıca background prosesin (`OutboxPublisherWorker`) etibarlı çatdırma etməsi.
- **Saga pattern** (compensating transaction-ların əsası) — uzun sürən, çox addımlı əməliyyatların uğursuz addımdan sonra geri qaytarılması.
*Mənbə: Chris Richardson — microservices.io — "Pattern: Transactional outbox" (microservices.io/patterns/data/transactional-outbox.html); "Pattern: Saga" (microservices.io/patterns/data/saga.html); Richardson, C. — *Microservices Patterns* (Manning, 2018).*

**Outbox Publisher Worker:** `outbox.messages` cədvəlindən `processed_on_utc IS NULL` sətirlər `FOR UPDATE SKIP LOCKED` ilə batch oxunur, `IIntegrationEventPublisher.PublishAsync()` ilə göndərilir, uğursuz olanda `attempt_count` artır, maksimum təkrardan sonra dead-letter-ə köçürülür. Bu worker Faza 1-də in-process/polling rejimində, gələcəkdə isə GKE-də davamlı işləyən Deployment kimi (bölmə 2.6-dakı hibrid nümunə) qurula bilər.

**Event schema versioning:** integration event-lər versiya nömrəsi daşıyır (`ProductCreatedV1`→`V2`), yeni sahələr yalnız optional/nullable (additive-only evolution); breaking dəyişiklikdə yeni event tipi paralel yazılır, minimum bir deprecation window ilə köhnə tip silinir.

**Bus abstraksiyası:** `IIntegrationEventPublisher`/`IIntegrationEventSubscriber<TEvent>` — indi in-process/outbox, sabah Pub/Sub, Managed Service for Apache Kafka və ya RabbitMQ (bölmə 2.5). Event envelope: `EventId, EventType, Version, TenantId, OccurredAt, CorrelationId, CausationId, Payload`. `CorrelationId`/`CausationId` distributed tracing-ə birbaşa bağlanır (bax bölmə 11.1). Hər consumer `(EventId, ConsumerName)` cütünü qeyd edir (inbox) — at-least-once + idempotent consumer = effectively-once (Pub/Sub-un öz at-least-once davranışı ilə eyni fərziyyəni paylaşır).

---

## 8. BFF və Edge Layer

Backends for Frontends (BFF) pattern-i, terminini 2015-ci ildə formalizə edən Sam Newman-a görə, fərqli client tipləri üçün ayrıca backend xidmətləri yaratmağı tövsiyə edir — hər BFF öz client-inin ehtiyacına uyğun data şəklini (shape) təqdim edir, ümumi/"bir ölçü hər kəsə uyğun" API-nin yaratdığı over-fetching/under-fetching problemini aradan qaldırır. Google Cloud-un öz arxitektura sənədləşməsi də (tiered hybrid pattern bölməsində) frontend-yönümlü API-ni backend-dən ayırmaq üçün eyni BFF/mikrofrontend nümunəsinə birbaşa istinad edir.
*Mənbə: Sam Newman — "Pattern: Backends For Frontends" (samnewman.io/patterns/architectural/bff/, 2015); Google Cloud Documentation — "Tiered hybrid pattern" (docs.cloud.google.com/architecture/hybrid-multicloud-patterns-and-practices/tiered-hybrid-pattern).*

**Qərar:** 3 client-family BFF — Storefront BFF, Admin BFF, Partner BFF. GraphQL yalnız həqiqətən çoxlu data composition ehtiyacı yarananda seçilir (məs. mürəkkəb, çox-modullu admin dashboard-lar) — vaxtından əvvəl GraphQL seçmək əməliyyat mürəkkəbliyini əsassız artırar.

**Edge Layer — Cloud Armor + Cloud Load Balancing.** Public BFF-lərin qarşısında rate limiting, WAF və per-tenant throttling üçün Gateway qatı (bax bölmə 10.3) tam GCP-native bir xidmətlə — Cloud Armor-la — həyata keçirilir; bu, real dünya trafikinə açılan istənilən public API üçün məcburidir. Cloud Armor həm GKE Ingress (BackendConfig resursu vasitəsilə), həm də Cloud Run-a bağlı External HTTP(S) Load Balancer üçün eyni şəkildə tətbiq olunur — bu, bölmə 2.6-da təsvir olunan hibrid (Cloud Run + GKE) deployment modelini pozmadan vahid edge təhlükəsizlik qatı saxlamağa imkan verir.

---

## 9. Idempotency və Konkurrensiya

- **Aggregate-level (mövcud):** `xmin` optimistic concurrency — eyni aggregate-ə iki eyni-anlı yazı `DbUpdateConcurrencyException`/409 ilə bloklanır.
- **Command-level (platform capability):** `Idempotency-Key` bütün mutasiya edən command-larda tenant+operation scope-da Memorystore for Valkey-də saxlanır. Payment webhook-ları provider event ID ilə ayrıca dedup olunur.

**Inventory üçün oversell qarantisi:** Warehouse + Stock Ledger + Reservation (TTL) + Availability projection. Checkout reservation yaradır, Payment/Order nəticəsinə görə reservation commit/release edilir.

---

## 10. Təhlükəsizlik və Compliance

### 10.1 AuthN/AuthZ

Bölmə 5-də təyin olunan OIDC token-lərdən çıxarılan scope/rol claim-ləri Admin/Partner BFF-də endpoint-səviyyəli authorization policy-lərinə map olunur (`[Authorize(Policy = "catalog:write")]` tipli). Fine-grained (resource-level) icazələr üçün (məs. "bu admin yalnız X storefront-u redaktə edə bilər") authorization qərarı domain qatında deyil, BFF/Application qatının authorization handler-ində saxlanır ki, domain modelin özü autentifikasiya konsepsiyasını tanımasın.

### 10.2 Ödəniş məlumatının əhatəsi (PCI DSS)

Faza 9-dakı Payment ACL (`IPaymentGateway`) təkcə arxitektura təmizliyi üçün deyil, compliance əhatəsini minimuma endirmək üçün də kritikdir: kart nömrəsi/CVV heç vaxt CommerceCore-un öz bazasına düşməməlidir — provider-in tokenization/hosted-field həllindən istifadə olunmalıdır (məs. Stripe Elements, Adyen Drop-in tipli). Bu, PCI DSS SAQ (Self-Assessment Questionnaire) səviyyəsini ən yüngül kateqoriyaya (SAQ A/A-EP) endirir — sistemin özü kart datası ilə heç təmasda olmur. Bu qərar Faza 9-un çıxış meyarına daxildir.

### 10.3 Edge təhlükəsizliyi

Public BFF-lərin qarşısında: rate limiting (tenant+IP+endpoint scope-da), bot/DDoS qorunması, WAF qaydaları. **Qərar: Google Cloud Armor.** Cloud Armor Google-un qlobal edge şəbəkəsində (Cloud Load Balancing-lə birgə) işləyən native DDoS/WAF xidmətidir:
- **Pre-configured WAF qaydaları** OWASP ModSecurity Core Rule Set əsasında (SQL injection, XSS, RCE, protokol hücumları daxil olmaqla OWASP Top 10-un böyük hissəsi).
- **Adaptive Protection** — Layer 7 DDoS hücumlarını aşkarlamaq/mitiqasiya etmək üçün müştərinin öz trafikinə öyrədilmiş ML modeli.
- **Rate limiting** — IP, header və ya digər sorğu atributlarına görə çevik qaydalar (tenant+IP+endpoint scope-u dəqiq bu mexanizmlə tətbiq olunur).
- **Bot Management** — reCAPTCHA Enterprise ilə native inteqrasiya, credential stuffing/avtomatlaşdırılmış hücumlara qarşı.

Cloud Armor yalnız Google Cloud Load Balancing arxasındakı trafiki yoxlaya bilir (GKE Ingress + BackendConfig, ya da Cloud Run-a bağlı External HTTP(S) Load Balancer) — bu, bölmə 2.6/8-də seçilən hər iki compute hədəfi (GKE, Cloud Run) ilə uyğundur və əlavə arxitektura dəyişikliyi tələb etmir.
*Mənbə: Google Cloud — "Cloud Armor" məhsul səhifəsi (cloud.google.com/armor); Google Cloud Documentation — "Configuring Google Cloud Armor security policies"; OneUpTime — "How to Choose Between Cloud Armor and Third-Party WAFs" (2026).*

### 10.4 Təhlükəsizlik testi və tədarük zənciri (supply chain)

- **SAST** (statik kod analizi) və **dependency/container scanning** CI pipeline-ına inteqrasiya olunur — yüksək-severity nəticə build-i fail etdirir.
- **Artifact Analysis + SBOM.** Konteyner image-ləri Artifact Registry-yə push olunanda **Artifact Analysis** avtomatik zəiflik skanlaması aparır (OS paketləri + dil-səviyyəli asılılıqlar); hər release üçün SBOM (Software Bill of Materials) `gcloud artifacts sbom export` ilə generasiya olunur və VEX (Vulnerability Exploitability eXchange) bəyanatları əlavə edilə bilər. PostgreSQL-in 2026-cı ilin avqustunda eyni buraxılışda 28 CVE düzəltməsi (bölmə 2.2) göstərir ki, "bir dəfə seç, unut" yanaşması açıq mənbəli stack-də real risk daşıyır — minor versiya yenilənmələri (Cloud SQL/AlloyDB-də avtomatik) və konteyner zəiflikləri (Artifact Analysis ilə davamlı) paralel izlənməlidir.
- **Build provenance və deploy-time enforcement.** Cloud Build SLSA (Supply-chain Levels for Software Artifacts) Level 3 build provenance-ı defolt olaraq generasiya edir — yəni hər artefaktın "necə, haradan, hansı kodla" qurulduğu saxta-edilə bilməz şəkildə qeyd olunur. **Binary Authorization** bunun üzərinə deploy-time siyasət tətbiq edir: yalnız etibarlı registry-dən gələn, imzalanmış/attestasiya edilmiş image-lərin GKE-yə və ya Cloud Run-a deploy olunmasına icazə verilir — bu, "kim nə deploy edir" sualına infrastruktur səviyyəsində məcburi cavab verir, ayrıca proses/sənədləşdirmə tələb etmir.
- **Secrets idarəetməsi.** Connection string, API key, JWT signing key heç vaxt config faylında/environment variable-da açıq saxlanmır — **Google Cloud Secret Manager** (`ISecretProvider` abstraksiyası arxasında) istifadə olunur: hər secret versioned saxlanır, giriş fine-grained IAM rolları ilə idarə olunur, hər çağırış Cloud Audit Logs-a yazılır, secret-lər Cloud KMS ilə (CMEK seçimi ilə) şifrələnir. GKE-də iş yükləri Workload Identity vasitəsilə statik açar olmadan Secret Manager-ə çıxış əldə edir.
- **OWASP ASVS (Application Security Verification Standard) 5.0** (30 may 2025-də buraxılıb — əvvəlki 4.0.3-dən (2021) sonra 6 ildəki ən böyük yenilənmə: ~350 tələb, 17 fəsil üzrə) minimum **Level 1** tələbləri Faza 15-in çıxış meyarına daxildir.
*Mənbə: Google Cloud Documentation — "Software supply chain security" (docs.cloud.google.com/software-supply-chain-security/docs/overview); "Artifact analysis and vulnerability scanning" (docs.cloud.google.com/artifact-registry/docs/analysis); Google Cloud Blog — "Securing Cloud Run Deployments with Binary Authorization"; Google Cloud Documentation — "Secret Manager overview" (docs.cloud.google.com/secret-manager/docs/overview); OWASP — "Application Security Verification Standard (ASVS) 5.0.0" (github.com/OWASP/ASVS).*

---

## 11. Observability və Əməliyyat Hazırlığı

### 11.1 Distributed Tracing

Event envelope-də mövcud olan `CorrelationId`/`CausationId` (bölmə 7) **OpenTelemetry** trace context-i ilə birbaşa uyğunlaşdırılır: bir sifariş sorğusu BFF-dən Commerce-ə, oradan Pricing/Inventory-yə sinxron çağırışla, sonra outbox→consumer zənciri ilə asinxron davam edərkən, hamısı tək bir trace-də görünə bilməlidir. .NET-in native OpenTelemetry SDK-sı və Aspire-ın `ServiceDefaults`-u (bölmə 2.6) bunu əvvəlcədən konfiqurasiya edir. Google Cloud Observability OTLP (OpenTelemetry Protocol) formatını native qəbul edir — yəni .NET-in standart OpenTelemetry çıxışı əlavə adapter/agent olmadan birbaşa **Cloud Trace**-ə göndərilə bilir; əlavə infrastruktur tələb olunmur, yalnız tətbiq (exporter endpoint konfiqurasiyası).
*Mənbə: Google Cloud Documentation — "Collect OpenTelemetry Protocol (OTLP) metrics and traces" (docs.cloud.google.com/monitoring/agent/ops-agent/otlp); "Instrumentation and observability" (docs.cloud.google.com/stackdriver/docs/instrumentation/overview).*

### 11.2 Struktur loglama və metriklər

Serilog (və ya bənzər) ilə struktur loglama, hər log sətrində `TenantId`/`CorrelationId` context-i; log-lar OTLP/Cloud Logging exporter ilə **Cloud Logging**-ə axır. Metrik namespace-ləri modul-səviyyəli ayrılır (`catalog.*`, `pricing.*`) və **Cloud Monitoring**-ə (istəyə görə **Google Cloud Managed Service for Prometheus** vasitəsilə, PromQL-ə tam uyğun) göndərilir — bu, komandanın mövcud Grafana dashboard-larını/PromQL alert-lərini dəyişmədən saxlamasına imkan verir.
*Mənbə: Google Cloud Documentation — "Google Cloud Managed Service for Prometheus" (docs.cloud.google.com/stackdriver/docs/managed-prometheus).*

### 11.3 SLO və alerting

Faza 5-in "yalnız bir sorğu uğurlu olur" testi kimi keyfiyyət meyarları kəmiyyətə bağlanır: hər kritik yol (checkout, stock reservation) üçün latency/throughput SLO təyin olunur, health check-lər (məs. `catalog-projection-lag` 5 dəqiqədən geridədirsə unhealthy) bu SLO-ları pozanda Cloud Monitoring alerting siyasəti vasitəsilə alert/on-call zənciri işə düşür. Konkret rəqəmlər biznes tələbinə bağlıdır (bax bölmə 21), amma bu zəncirin mövcudluğu Faza 14-ün çıxış meyarına şərtdir.

---

## 12. Data Qorunması, Backup/DR və GDPR

### 12.1 Backup/PITR və qat-bazlı RPO/RTO

Tək paylaşılan Cloud SQL-in "blast radius" riskini azaltmaq üçün Point-in-Time Recovery (Cloud SQL-in native, avtomatik backup + PITR xidməti ilə) məcburidir — bu, Google Cloud Well-Architected Framework-un Reliability sütununda birbaşa tövsiyə olunan yanaşmadır: kritik tətbiqlər üçün near-continuous backup (Cloud SQL PITR, ya da AlloyDB-nin Continuous Backup & Recovery-si) istifadə olunur, RPO biznes tələbinə görə təyin edilir və Cloud Monitoring ilə izlənir. Bütün data eyni RPO/RTO tələbinə malik deyil:

| Qat | Nümunə | RPO hədəfi (nümunə) | Səbəb |
|---|---|---|---|
| Tier-1 (tranzaksional) | Order, Payment, Stock Ledger | Dəqiqələr | Maliyyə/hüquqi məsuliyyət, itki bərpa olunmur — Cloud SQL PITR ilə əldə olunur |
| Tier-2 (read model) | Catalog projection, Search index | Yenidən qurula bilər | Outbox/event log-dan sıfırdan hesablana bilir (bölmə 7) — RPO praktiki olaraq 0-a yaxınlaşdırıla bilər, restore isə vaxt aparır (RTO) |
| Tier-3 (cache) | Memorystore for Valkey | Yoxdur | Cache itkisi funksional problem yaratmır, yalnız müvəqqəti performans təsiri |

Bu cədvəldəki konkret rəqəmlər nümunədir — real hədəflər komanda tərəfindən Faza 16-da (bax bölmə 18) təyin və sınanmalıdır.
*Mənbə: Google Cloud Documentation — "Perform testing for recovery from data loss" (Well-Architected Framework, Reliability pillar) (docs.cloud.google.com/architecture/framework/reliability/perform-testing-for-recovery-from-data-loss); "Architecting disaster recovery for cloud infrastructure outages" (docs.cloud.google.com/architecture/disaster-recovery).*

### 12.2 GDPR / "unudulmaq hüququ"

Mövcud soft-delete pattern-i arxivləmə üçün doğrudur, amma silinmə tələbi üçün kifayət deyil. Təklif olunan yanaşma: **anonimləşdirmə job-u** — silinmə tələbi gələndə aggregate-in ID-si və audit izi saxlanır, PII sahələri (ad, email, ünvan) hash-lənir/`[REDACTED]`-lə əvəz olunur. Bu, iki tələb arasında balans yaradır: məlumatların "unudulması" və maliyyə/vergi qeydlərinin qanuni saxlama müddəti (bir çox yurisdiksiyada bu müddət illərlə ölçülür və "unudulmaq hüququ"na istisna təşkil edir). Konkret saxlama müddətləri və istisnalar hüquq məsləhətçisi ilə birgə müəyyən edilməlidir — bu, mühəndislik qərarı deyil.

### 12.3 Data residency

AB müştəriləri planlaşdırılırsa, tək-regionlu paylaşılan Cloud SQL GDPR-tipli data locality tələbləri ilə toqquşa bilər. Cloud SQL/AlloyDB region seçimi ilkin provisioning zamanı təyin olunur; Secret Manager-in regional secret seçimi (bölmə 10.4) də oxşar tələbləri dəstəkləyir. Bridge modelinə keçid (bölmə 4.1) — region-pinned ayrıca storage, paylaşılan tətbiq qatı — bu halda təbii həll yoludur, amma yalnız real müqavilə tələbi yarananda aktivləşdirilməlidir (əvvəlcədən qurmaq əsassız mürəkkəblikdir).

---

## 13. Deployment, Release və Şema Miqrasiyası

### 13.1 Şema miqrasiyası: expand/contract

Hər modul öz EF Core migration tarixçəsini idarə edir (ayrıca migration assembly, `catalog`/`pricing`/`inventory` kimi Postgres schema namespace-ləri ilə ayrılmış cədvəllər) — bir modulun miqrasiyası digərini bloklamır. Breaking dəyişikliklər üç addımlı **expand/contract** pattern-i ilə aparılır:
1. **Expand** — yeni sütun/cədvəl əlavə olunur (nullable/default-lu), köhnə kod dəyişmədən işləməyə davam edir.
2. **Migrate** — kod yeni sxemi istifadə etməyə keçir, deploy olunur, doğrulanır.
3. **Contract** — yalnız bütün instansiyalar keçdikdən sonra köhnə sütun silinir.

Uzun kilidlərdən qaçmaq üçün `CREATE INDEX CONCURRENTLY`, iş saatlarında böyük table lock tələb edən əməliyyatlardan (`ALTER COLUMN TYPE` kimi) qaçınmaq, lazım olduqda `pg_repack` istifadəsi.

### 13.2 Production topologiyası və CI/CD

Bax bölmə 2.6 — Aspire-ın `AppHost`/`aspire publish`/`aspire deploy` axını GKE Autopilot-a (Helm chart vasitəsilə) rəsmi, sürtünməsiz yol açır; Cloud Run isə standart konteyner CI/CD boru xətti ilə tamamlanır. CI/CD boru xətti üçün:
- **Cloud Build** — build addımlarını Google infrastrukturunda işlədir, image-i **Artifact Registry**-yə push edir (Artifact Analysis skanlaması, SLSA provenance-ı avtomatik tətbiq olunur, bax bölmə 10.4).
- **Workload Identity Federation** — GitHub Actions (və ya digər üçüncü-tərəf CI) ilə GCP arasında **açar olmadan** (keyless), qısaömürlü OIDC token-lərlə autentifikasiya təmin edir; static service account key-lərin repo-da saxlanmasına ehtiyac qalmır.
- **Terraform (Google provider)** — infrastruktur-as-code: Cloud SQL/AlloyDB instansiyaları, GKE klasteri, Pub/Sub topic-ləri, Secret Manager secret-ləri, IAM bindings versiyalanan, review olunan kodla idarə olunur.

Konkret production hədəfi (yalnız GKE, yalnız Cloud Run, ya da hibrid) komandanın operativ təcrübəsinə bağlıdır (bax bölmə 21).
*Mənbə: Google Cloud Documentation — "Configure Workload Identity Federation with deployment pipelines" (docs.cloud.google.com/iam/docs/workload-identity-federation-with-deployment-pipelines); Google Cloud — Terraform Google provider rəsmi sənədləşməsi.*

### 13.3 Release strategiyası

BFF səviyyəsində feature flag-lar (Control Plane-in `TenantRegistry`-sinə əlavə olunan sadə bir `FeatureFlags` cədvəli ilə) tenant-based tədricən rollout (canary) imkanı verir — yeni funksiya əvvəlcə az sayda tenant-a açılır, sonra genişlənir. Bu, "hamısı və ya heç nə" deploy riskini azaldır. GKE-də bu, native olaraq rolling update/canary Deployment strategiyaları ilə, Cloud Run-da isə trafik bölüşdürmə (traffic splitting, revision-lar arasında faiz-based) ilə tamamlana bilər.

---

## 14. Komanda Topologiyası (Conway's Law)

Conway's Law-un nəticəsi budur ki, texniki sərhədlər komanda sərhədləri ilə üst-üstə düşməzsə, statik analiz aləti (`NetArchTest`) təkbaşına kifayət etmir — insanlar "asan yol"u tapıb sərhədləri yenə də pozacaq.

**Başlanğıc təklif** (Team Topologies çərçivəsinə əsasən — stream-aligned, platform, enabling, complicated-subsystem komanda tipləri):
- **Stream-aligned "Commerce Core"** — Commerce, Pricing, Inventory (Faza 6-nın saga-sı bu üçünü sıx bağlayır, ayrı komandalarda saxlamaq koordinasiya xərcini artırar).
- **Stream-aligned "Discovery"** — Catalog, Search, Customer.
- **Stream-aligned "Growth"** — Promotion, Notification.
- **Complicated-subsystem "Payments & Compliance"** — Payment, Fulfillment ACL, Tax (tənzimləyici mürəkkəblik ixtisaslaşma tələb edir).
- **Platform komandası** — `Platform.*`, Control Plane, GCP infrastruktur (GKE/Cloud Run, Cloud SQL, IAM, CI/CD), Observability.

Bu, tək düzgün cavab deyil — komanda ölçüsü/işə qəbul planına görə uyğunlaşdırılmalıdır. Vacib olan prinsipdir: modul sərhədləri komanda sərhədləri ilə əvvəlcədən uyğunlaşdırılmalıdır, sonradan deyil.
*Mənbə: Skelton, M. & Pais, M. — "Team Topologies: Organizing Business and Technology Teams for Fast Flow" (IT Revolution Press, 2019).*

---

## 15. Cross-Cutting Platform Servisləri: Notification, Tax/Currency

### 15.1 Notification

Bütün modulların integration event-lərinə abunə olan mərkəzi bildiriş xidməti (email/SMS/push). Tenant-based template/branding, çatdırılma statusu tracking, retry — bu, "detal" deyil, sifariş təsdiqi, stok bildirişi, parol sıfırlama kimi istənilən e-commerce-in məcburi minimum funksionallığıdır. Bax Faza 11.

### 15.2 Tax/Vergi

Payment-ə bənzər ACL pattern-i: domain/application vergi hesablama qaydasının detalını bilmir, yalnız `ITaxCalculator` interfeysini görür. Market-based sadə qayda mühərriki ilə başlana bilər, mürəkkəb yurisdiksiyalar (çox-ştatlı ABŞ vergi sistemi kimi) üçün xarici provider (Avalara/TaxJar tipli) inteqrasiyası eyni ACL arxasında əlavə olunur. Bax Faza 12.

---

## 16. Axtarış və Kəşfin Gələcəyi: AI-Assisted Layer (ixtiyari)

PostgreSQL-in `pgvector` extension-u embedding-lərin saxlanması və vector similarity search-ü mövcud relational data ilə eyni bazada, əlavə infrastruktur olmadan mümkün edir — bu, artıq seçilmiş Cloud SQL/AlloyDB qərarı ilə (bölmə 2.2) təbii uzlaşır; hər iki xidmət `pgvector`-u dəstəkləyən extension siyahısına daxildir.

**Dürüst xəbərdarlıq:** semantic search/"customers also bought" tövsiyə mühərriki avtomatik fərqləndirmə vermir. Keyfiyyətli nəticə üçün real istifadəçi/sifariş datası (soyuq başlanğıc problemi), embedding pipeline-ın davamlı saxlanması və nəticənin real A/B testlə ölçülməsi lazımdır. "AI olsun deyə" əlavə edilməməlidir — yalnız konkret conversion/kəşf problemi həll etdiyi sübut olunanda investisiya davam etməlidir. Qeyd edək ki, Vertex AI Search for Commerce (bölmə 2.4) artıq öz recommendation/personalization qatını (Retail API-nin bir hissəsi olaraq) təmin edir — bu fazada `pgvector`-based xüsusi model yalnız Vertex AI-nin təklif etdiyindən fərqli, komandaya məxsus bir sığnal (məs. tamamilə domain-spesifik bir oxşarlıq metrikası) lazım olduqda əsaslandırılır. Bax Faza 17 — açıq şəkildə biznes-case gate-i ilə qeyd olunub, məcburi deyil.
*Mənbə: pgvector — PostgreSQL vector similarity search extension (github.com/pgvector/pgvector).*

---

## 17. Rədd Edilən Alternativlər

| Alternativ | Niyə rədd edildi |
|---|---|
| Go / Rust / Elixir | Komandanın mövcud dərin C#/.NET təcrübəsi (195 test, işlək CQRS) var; TechEmpower Round 23-ün özü ASP.NET Core-u bu dillərin üzərində göstərir (bölmə 2.1) — performans əsaslı əsas yoxdur, miqrasiya xərci saf itkidir. |
| Sıfırdan mikroservis (day-0) | Fowler/Shopify presedenti: bounded context sərhədləri real istifadədən əvvəl dəqiq bilinmir; erkən bölmə səhv sərhədləri "dondurur" (bölmə 3.1). |
| Fiziki sharding (day-0) | Məlum tenant sayı/yük olmadan sharding strategiyası (hansı açar, neçə shard) sırf təxmindir; Pool→Bridge→Silo təkamül yolu AWS whitepaper-in özü tərəfindən tövsiyə olunur (bölmə 4.1). |
| NoSQL-first (MongoDB və s.) | Sifariş/inventar/qiymət domenində ACID tranzaksional konsistentlik və RLS-in native dəstəyi itirilir; JSONB ilə "sxemsiz" ehtiyacların çoxu onsuz da örtülür. |
| Özü-idarə edilən (Standard mode) Kubernetes, node-pool idarəçiliyi ilə (day-0) | Kiçik komanda üçün node planlaması/yükseltmə/RBAC-ın operativ mürəkkəbliyi erkən sürəti azaldır. GKE Autopilot və ya Cloud Run bu konkret yükü aradan qaldırır (bölmə 2.6) — buna görə "gündəm-0 idarəçilik yükü" indi Autopilot/Cloud Run ilə həll olunub, yalnız node-səviyyəli tam nəzarət tələb edən Standard rejim erkən mərhələdə rədd edilir. |
| Özəl (custom) Identity/Auth | Parol/token təhlükəsizliyi ixtisaslaşmış sahədir, səhv bahalıdır; CNCF idarəçiliyi olan alət seçimi bölmə 2.4-dəki lisenziya/idarəçilik prinsipi ilə tam üst-üstə düşür (bölmə 5). |
| GraphQL-first (bütün BFF-lər) | Composition ehtiyacı sübut olunmadan GraphQL-in schema-stitching/N+1 mürəkkəbliyini qəbul etmək əsassızdır; REST/Minimal API ilə başlamaq, lazım gələndə seçmə əlavə etmək (bölmə 8) daha aşağı-riskli yoldur. |
| Broker-first (Kafka/Pub/Sub day-0) | Faza 1-in in-process outbox-u eyni kontraktla (bölmə 7) başlamağa imkan verir; broker-in operativ yükünü (partitioning, consumer group idarəetməsi, ya da hətta idarə olunan xidmətin tənzimlənməsi) real throughput ehtiyacı olmadan öz üzərinə götürmək vaxtından əvvəldir. |

---

## 18. Tam Ətraflı İcra Yol Xəritəsi

### 18.0 Roadmap-ın oxunma qaydası

Hər faza aşağıdakı struktura malikdir: **Məqsəd**, **Əhatə olunan texniki tapşırıqlar**, **Giriş şərti**, **Çıxış meyarı** (ölçülə bilən, "bitdi" deyə bilmək üçün konkret siyahı), **Test tələbləri**, **Risk və mitigasiya**, **Mürəkkəblik** (S/M/L/XL — nisbi indikator, dəqiq vaxt təxmini deyil, komanda sürətindən asılıdır), **Tövsiyə olunan komanda** (bölmə 14-ə istinadla) və **Paralelləşdirmə** imkanı.

Fazalar arası asılılıq xəritəsi:

```
Faza 0 (Təməl) ─┬─→ Faza 1 (Event Backbone) ─┬─→ Faza 3 (Catalog Projection) ─┬─→ Faza 4 (Pricing) ─┐
                │                             │                                 ├─→ Faza 5 (Inventory)─┤
                │                             ├─→ Faza 7 (Search)               │                      ├─→ Faza 6 (Commerce/Saga) ─┬─→ Faza 9 (Payment)
                │                             └─→ Faza 11 (Notification)        └─→ Faza 8 (Promotion)─┘                          ├─→ Faza 10 (Customer)
                └─→ Faza 2 (Modul Bootstrap)                                                                                        ├─→ Faza 12 (Tax)
                                                                                                                                      └─→ Faza 13 (BFF-lər) ─→ Faza 14 (Observability tam) ─→ Faza 15 (Security Gate) ─→ Production
Faza 16 (Backup/DR) — istənilən vaxt, Faza 0-dan sonra paralel
Faza 17 (AI-Assisted Discovery) — ixtiyari, yalnız Faza 7 + real data sonrası
```

---

### Faza 0A — Qərarlar, threat model və migration planı

**Məqsəd:** Multi-tenancy, identity provayderi, kataloq-qiymətləndirmə miqrasiyası üzrə arxitektura qərarlarını (ADR) sənədləşdirmək, threat modeli və təhlükəsizlik sərhədlərini dəqiqləşdirmək.

**Əhatə olunan texniki tapşırıqlar:**
- ADR 0001: Multi-tenancy (Pool + PostgreSQL RLS).
- ADR 0002: Identity Provider seçimi (Google Cloud Identity Platform).
- ADR 0003: Catalog-dan Pricing-ə keçid və məlumat miqrasiyası planı.
- Threat modeling: Cross-tenant data leakage, token context confusion, privilege escalation risklərinin təhlili.
- GCP layihə strukturunun və Terraform infrastruktur skeletinin (`infra/terraform/`) planlaşdırılması.

**Giriş şərti:** yoxdur.
**Çıxış meyarı:** Bütün ADR-lər təsdiqlənib, arxitektura və miqrasiya planı hazırdır.
**Mürəkkəblik:** M.
**Paralelləşdirmə:** yoxdur.

---

### Faza 0B — Multi-tenancy və platform təməlinin implementasiyası

**Məqsəd:** Bütün sistemin üzərində quracağı multi-tenancy, tenant resolution middleware, identity mapping, PostgreSQL RLS və Catalog modular monolith qatını implementasiya etmək.

**Əhatə olunan texniki tapşırıqlar:**
- `TenantId`, `StorefrontId`, `MarketId`, `TenantContext`, `ITenantContext` modellərinin (`Platform.Contracts`) yaradılması.
- `platform.tenants`, `platform.storefronts`, `platform.tenant_memberships` cədvəllərinin və `Platform.ControlPlane` domen modellərinin yaradılması.
- `Platform.Identity` və `TenantContextMiddleware`: Public (domain → storefront), Admin (sub → membership), Partner (client_id → tenant) həlli.
- Catalog cədvəllərinə və `outbox.messages`-ə `tenant_id` əlavəsi, tenant-aware unique constraints, foreign key və indekslərin qurulması.
- Bütün tenant cədvəllərində PostgreSQL RLS və `FORCE ROW LEVEL SECURITY` tətbiqi (`tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid`).
- Mövcud Catalog kodunun `Modules/Catalog/{Domain,Application,Infrastructure,Contracts}` strukturuna `git mv` ilə köçürülməsi.
- `NetArchTest` qaydalarının ilkin dəstinin yazılması və RLS izolyasiya testlərinin təmin edilməsi.

**Giriş şərti:** Faza 0A.

**Çıxış meyarı:**
- `tenant_id` sütunu bütün tenant-owned cədvəllərdə mövcuddur və RLS aktivdir (`FORCE ROW LEVEL SECURITY`).
- `TenantResolutionMiddleware` işləyir, naməlum/uyğunsuz tenant sorğuları 400/403 ilə rədd edilir.
- RLS izolyasiya testləri (Tenant A, Tenant B, context-siz sorğu, cross-tenant FK bloklaması) uğurla keçir.
- Catalog modullaşdırılıb və testlər yaşıl qalır.

**Test tələbləri:** RLS inteqrasiya testləri, middleware unit/inteqrasiya testləri, architecture testləri.

**Risk və mitigasiya:** RLS siyasətində boşluq cross-tenant sızmaya səbəb ola bilər. Mitigasiya: RLS testləri olmadan sonrakı fazalara keçilməməsi və `FORCE ROW LEVEL SECURITY` məcburiyyəti.

**Mürəkkəblik:** L.
**Tövsiyə olunan komanda:** Platform komandası.
**Paralelləşdirmə:** yoxdur — bu fazanın tamamlanması digər bütün fazalar üçün ilkin şərtdir.

---

### Faza 1 — Platform Capability: Event Backbone

**Məqsəd:** bütün modullar arası asinxron kommunikasiyanın əsasını qoymaq — sonrakı hər bir modul (Pricing, Inventory, Commerce, Search, Notification) bu backbone üzərində qurulur.

**Əhatə olunan texniki tapşırıqlar:**
- `outbox.messages` cədvəli, `OutboxPublisherWorker` (SKIP LOCKED polling-based batch processing).
- Versioned event envelope strukturu (`EventId, EventType, Version, TenantId, OccurredAt, CorrelationId, CausationId, Payload`).
- Inbox idempotency cədvəli (`(EventId, ConsumerName)` unikal açar).
- `IIntegrationEventPublisher`/`IIntegrationEventSubscriber<TEvent>` bus abstraksiyası (in-process implementasiya; Pub/Sub/Kafka üçün seam saxlanır, bölmə 2.5).
- Event schema versioning siyasətinin sənədləşdirilməsi (additive-only evolution qaydası, bölmə 7).
- Dead-letter mexanizmi (maksimum təkrar sayından sonra).

**Giriş şərti:** Faza 0.

**Çıxış meyarı:**
- Outbox-a yazılan mesajlar ayrıca kod müdaxiləsi olmadan avtomatik emal olunur (polling worker-in işlədiyi sübut edilir).
- Ən azı bir test consumer-i uğurla event alır və inbox idempotency-si vasitəsilə eyni event-i iki dəfə emal etmir.
- Dead-letter ssenarisi (süni uğursuz consumer ilə) test edilib.

**Test tələbləri:** outbox→consumer axınının inteqrasiya testi, at-least-once + idempotent consumer kombinasiyasının "effectively-once" davranışını sübut edən test.

**Risk və mitigasiya:** `SKIP LOCKED` polling-in yük altında performansı — erkən mərhələdə batch ölçüsü və polling intervalının konfiqurasiya edilə bilən olması təmin edilməlidir ki, gələcək tuning kod dəyişikliyi tələb etməsin.

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** Platform komandası.
**Paralelləşdirmə:** yoxdur.

---

### Faza 2 — Modul Bootstrap Pattern

**Məqsəd:** yeni modulların sistemə əlavə edilməsini standartlaşdırmaq.

**Əhatə olunan texniki tapşırıqlar:**
- `IModule` interfeysi (`RegisterServices`, `MapEndpoints`, `RegisterMigrations` kimi metodlarla).
- `Program.cs`-də modul-based composition — yeni modul əlavə etmək bir sətir reflection/registration tələb edir.
- Modul-səviyyəli health check qeydiyyatı üçün ilkin konvensiya (tam tətbiqi Faza 14-də).

**Giriş şərti:** Faza 0.

**Çıxış meyarı:** yeni, boş bir "nümunə modul" `IModule` pattern-i ilə sistemə qoşulur və heç bir mövcud kodu pozmadan build/run olur.

**Test tələbləri:** modul qeydiyyatının inteqrasiya testi.

**Mürəkkəblik:** S.
**Tövsiyə olunan komanda:** Platform komandası.
**Paralelləşdirmə:** Faza 1 ilə paralel aparıla bilər (fərqli sahə).

---

### Faza 3 — Catalog Read Projection

**Məqsəd:** Catalog modulunun yazma modelini (mövcud) oxuma modelindən ayırmaq, CQRS-in oxuma tərəfini qurmaq.

**Əhatə olunan texniki tapşırıqlar:**
- Denormalized read model sxeması (storefront sorğuları üçün optimallaşdırılmış).
- Outbox→projector axını (Catalog domain event-lərini dinləyib read model-i yeniləyən consumer).
- Checkpoint-based rebuild mexanizmi Catalog write model-dən yenidən qurulur. Outbox yalnız dəyişikliklərin çatdırılması üçündür; event store deyil.

**Giriş şərti:** Faza 1.

**Çıxış meyarı:** Catalog-da edilən dəyişiklik (yeni məhsul, qiymət dəyişikliyi deyil — bu Faza 4-dədir) müəyyən gecikmə ilə read model-də görünür; checkpoint sıfırlanıb rebuild edildikdə eyni son nəticə alınır.

**Test tələbləri:** projection-ın idempotentliyini sübut edən test (eyni event-in iki dəfə emalı nəticəni dəyişmir), rebuild-in son nəticəsinin canlı axınla eyni olduğunu sübut edən test.

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** Discovery komandası.
**Paralelləşdirmə:** yoxdur (Faza 1-dən sonra ardıcıl).

---

### Faza 4 — Pricing Context

**Məqsəd:** qiymətləndirmə domenini müstəqil bounded context kimi qurmaq.

**Əhatə olunan texniki tapşırıqlar:**
- `PriceBook`/`PriceEntry`/`PriceQuote` aggregate-ləri.
- Temporal modeling (qiymətin zaman ərzində dəyişməsi, gələcək tarixli qiymət planlaması).
- Memorystore for Valkey-də qiymət sorğusu cache-i (yüksək-tezlikli oxu ssenarisi üçün).

**Giriş şərti:** Faza 1, Faza 3.

**Çıxış meyarı:** verilmiş tenant/market/tarix kombinasiyası üçün düzgün qiymət qaytarılır; cache invalidasiyası qiymət dəyişikliyində düzgün işləyir (test edilir).

**Test tələbləri:** temporal qiymət ssenarilərinin (keçmiş/cari/gələcək) unit testləri, cache invalidasiyasının inteqrasiya testi.

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** Commerce Core komandası.
**Paralelləşdirmə:** Faza 5 ilə paralel.

---

### Faza 5 — Inventory Context

**Məqsəd:** anbar/stok idarəetməsini oversell riski olmadan qurmaq.

**Əhatə olunan texniki tapşırıqlar:**
- `Warehouse`, `Stock Ledger` (append-only hərəkət tarixçəsi), `Reservation` (TTL-li müvəqqəti bloklama), `Availability` projection.
- Konkurrensiya nəzarəti — eyni məhsul üçün paralel sorğuların düzgün sıralanması.

**Giriş şərti:** Faza 1.

**Çıxış meyarı:** paralel N sorğu eyni son stok vahidi üçün rəqabət edəndə yalnız biri uğurlu olur — bu, CI-da avtomatlaşdırılmış konkurrensiya testi ilə hər build-də doğrulanır (rəqəmsiz "gözlə işləyir" qəbuledilməzdir).

**Test tələbləri:** yüksək-konkurrensiya (məs. 50+ paralel sorğu) simulyasiya testi CI-da; reservation TTL-in düzgün expire olduğunu sübut edən test.

**Risk və mitigasiya:** oversell — biznes üçün ən bahalı bug kateqoriyasıdır (maliyyə itkisi + müştəri etimadının itməsi). Mitigasiya: bu fazanın çıxış meyarı digər fazalardan fərqli olaraq açıq şəkildə rəqəmsiz test qəbul etmir.

**Mürəkkəblik:** L.
**Tövsiyə olunan komanda:** Commerce Core komandası.
**Paralelləşdirmə:** Faza 4 ilə paralel.

---

### Faza 6 — Commerce (Cart/Checkout/Order)

**Məqsəd:** sistemin ən yüksək-riskli, ən çox kontekst birləşdirən hissəsini — sifariş axınını — etibarlı şəkildə qurmaq.

**Əhatə olunan texniki tapşırıqlar:**
- Saga/compensation orkestrasiyası (Pricing → Inventory → Payment addımları).
- Saga state-inin verilənlər bazasında persist edilməsi (proses crash olsa belə bərpa oluna bilməsi üçün).
- Timeout handling və dead-saga detection (nə uğurlu, nə uğursuz olaraq "asılı qalan" saga-ların aşkarlanması).
- Command-level idempotency-key tətbiqi (bölmə 9).
- Cross-context sinxron çağırışlar üçün `Result<T,E>` pattern-i (exception-based axın idarəetməsi əvəzinə).
- Fallback və circuit breaker (Polly ilə, Aspire `ServiceDefaults`-un təbii davamı olaraq).

**Giriş şərti:** Faza 4, Faza 5.

**Çıxış meyarı:**
- Pricing və ya Inventory addımı süni şəkildə uğursuz edildikdə avtomatik geri qaytarma (compensation) baş verir və sistem konsistent vəziyyətdə qalır.
- Eyni sorğu iki dəfə göndərildikdə iki order yaranmır (idempotency-key ilə sübut edilir).
- Fault-injection (chaos) testi CI-da işləyir və hər addımın uğursuzluq ssenarisini əhatə edir.
- Dead-saga detection alert-i işə düşür (süni "asılı qalmış" saga simulyasiyası ilə sübut edilir).

**Test tələbləri:** hər saga addımı üçün ayrıca fault-injection testi, crash-recovery testi (proses ortada dayandırılıb yenidən başladılanda saga öz vəziyyətindən davam edir).

**Risk və mitigasiya:** bu, roadmap-ın ən yüksək-riskli fazasıdır — səhv saga dizaynı qismən tamamlanmış sifarişlərə (məs. ödəniş alınıb, lakin stok rezerv edilməyib) səbəb ola bilər. Mitigasiya: bu fazaya digərlərindən daha çox test resursu ayrılmalı, production-a keçmədən əvvəl xüsusi kod review dövrü aparılmalıdır.

**Mürəkkəblik:** XL — ən yüksək-riskli faza.
**Tövsiyə olunan komanda:** Commerce Core komandası, Platform komandasının dəstəyi ilə (saga infrastrukturu üçün).
**Paralelləşdirmə:** Faza 8 ilə paralel (ayrı sub-komanda).

---

### Faza 7 — Search

**Məqsəd:** məhsul axtarışı funksionallığını, gələcək miqyaslanma yolunu bağlamadan, minimal infrastruktur xərci ilə qurmaq.

**Əhatə olunan texniki tapşırıqlar:**
- PostgreSQL Full-Text Search (FTS) ilə ilkin implementasiya (Cloud SQL üzərində, əlavə xidmət tələb olunmur).
- Vertex AI Search for Commerce-ə (default) və ya özü-idarə edilən OpenSearch-ə (GKE, vendor-neytral alternativ) keçid üçün "seam" (Catalog-un Search-ə göndərdiyi event kontraktının hər ikisinə bəslənə biləcək formada dizaynı, bölmə 2.4).
- Gələcək semantic search üçün `pgvector` seam-inin qeydə alınması (bax bölmə 16, Faza 17) — bu fazada tətbiq olunmur, yalnız gələcək inteqrasiyaya mane olmayan dizayn saxlanır.

**Giriş şərti:** Faza 1 və Faza 3.

**Çıxış meyarı:** storefront-da mətn-əsaslı axtarış işləyir, nəticələr Catalog-dakı dəyişiklikləri qəbul edilə bilən gecikmə ilə əks etdirir.

**Test tələbləri:** axtarış nəticələrinin Catalog dəyişikliyindən sonra yenilənməsini sübut edən inteqrasiya testi.

**Mürəkkəblik:** M (Postgres FTS ilə); gələcək Vertex AI Search for Commerce/OpenSearch keçidi L.
**Tövsiyə olunan komanda:** Discovery komandası.
**Paralelləşdirmə:** yoxdur (Faza 1-dən sonra ardıcıl, digər fazalarla paralel aparıla bilər).

---

### Faza 8 — Promotion & Bundle

**Məqsəd:** kampaniya/endirim/bundle məntiqini Pricing və Inventory ilə düzgün koordinasiyada qurmaq.

**Əhatə olunan texniki tapşırıqlar:**
- Campaign/Coupon/Discount qaydaları mühərriki.
- Temporal modeling (kampaniyanın başlama/bitmə tarixi).
- Bundle-ın komponent-əsaslı qiymət/stok həlli (bundle-ın hər komponentinin öz qiyməti/stoku ilə əlaqəsi).

**Giriş şərti:** Faza 4, Faza 5.

**Çıxış meyarı:** aktiv kampaniya checkout zamanı düzgün endirimi tətbiq edir; bundle-ın komponent stoku bundle satışında düzgün azalır.

**Test tələbləri:** üst-üstə düşən (overlapping) kampaniyaların prioritet qaydalarının test edilməsi, bundle-stok konsistentliyinin inteqrasiya testi.

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** Growth komandası.
**Paralelləşdirmə:** Faza 6 ilə paralel, ayrı sub-komanda.

---

### Faza 9 — Payment & Fulfillment (Anti-Corruption Layer)

**Məqsəd:** xarici ödəniş/kargo provayderləri ilə inteqrasiyanı domenə sızdırmadan qurmaq, PCI DSS əhatəsini minimuma endirmək.

**Əhatə olunan texniki tapşırıqlar:**
- `IPaymentGateway`, `IShippingProvider` ACL interfeysləri (domain heç vaxt konkret provayder adını bilmir).
- Kart datasının tokenization/hosted-field (Stripe Elements/Adyen Drop-in tipli) ilə inteqrasiyası — kart nömrəsi/CVV heç vaxt CommerceCore bazasına düşmür.
- PCI DSS SAQ A/A-EP əhatəsinin sənədləşdirilməsi (bölmə 10.2).
- Webhook-ların provider event ID ilə dedup edilməsi (bölmə 9); webhook endpoint-ləri Cloud Armor-un rate-limit qaydaları arxasında qorunur (bölmə 10.3).

**Giriş şərti:** Faza 6.

**Çıxış meyarı:** ödəniş uğurlu/uğursuz hər iki ssenari Commerce saga-sına düzgün geri bildirilir; webhook-un təkrar göndərilməsi (provider-in at-least-once davranışı) ikiqat emal yaratmır; PCI DSS əhatə sənədi hazırdır.

**Test tələbləri:** provider sandbox-ı ilə uçdan-uca test, webhook replay testi.

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** Payments & Compliance komandası.
**Paralelləşdirmə:** yoxdur (Faza 6-dan sonra).

---

### Faza 10 — Customer Konteksti

**Məqsəd:** müştəri biznes profilini autentifikasiyadan ayrı, GDPR-a hazır şəkildə qurmaq.

**Əhatə olunan texniki tapşırıqlar:**
- `CustomerProfile` aggregate-i (Faza 0-ın Identity-sindən gələn `sub` claim-inə bağlı, parol saxlamır).
- Ünvan idarəetməsi, sifariş tarixçəsi görünüşü.
- Marketinq razılığı (consent) — GDPR-relevant, audit-lənən dəyişiklik tarixçəsi ilə.
- Loyalty ledger əsası (gələcək loyallıq proqramı üçün).

**Giriş şərti:** Faza 0, Faza 6.

**Çıxış meyarı:** müştəri profilini/ünvanlarını idarə edə bilir; consent dəyişiklikləri (verilib/geri götürülüb) tam audit izi ilə saxlanılır.

**Test tələbləri:** consent audit-inin tamlığını sübut edən test, GDPR anonimləşdirmə job-unun (bölmə 12.2) bu modulla inteqrasiya testi.

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** Discovery komandası.
**Paralelləşdirmə:** yoxdur.

---

### Faza 11 — Notification Platform

**Məqsəd:** bütün modulların event-lərinə abunə olan, tenant-based mərkəzi bildiriş xidmətini qurmaq.

**Əhatə olunan texniki tapşırıqlar:**
- Email/SMS/push kanalları üçün provayder abstraksiyası.
- Tenant-based template/branding (hər tenant öz email şablonunu fərdiləşdirə bilir).
- Çatdırılma statusu tracking (göndərildi/çatdı/açıldı/bounce).
- Retry siyasəti (uğursuz göndərişlər üçün).

**Giriş şərti:** Faza 1.

**Çıxış meyarı:** sifariş yaradılanda müştəri email alır, çatdırılma statusu admin panelində görünür.

**Test tələbləri:** provayder sandbox-ı ilə uçdan-uca test, template fərdiləşdirməsinin tenant-lar arası izolyasiya testi.

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** Growth komandası.
**Paralelləşdirmə:** yalnız Faza 1-ə bağlıdır, istənilən digər faza ilə paralel aparıla bilər.

---

### Faza 12 — Tax/Vergi Konteksti

**Məqsəd:** vergi hesablamasını market-based sadə qaydadan başlayıb mürəkkəb yurisdiksiyalara qədər genişlənə bilən ACL arxasında qurmaq.

**Əhatə olunan texniki tapşırıqlar:**
- `ITaxCalculator` ACL interfeysi.
- Market-based sadə qayda mühərriki (ilkin versiya).
- Xarici provider (Avalara/TaxJar tipli) inteqrasiyası üçün seam (mürəkkəb yurisdiksiyalar üçün, ilkin mərhələdə tətbiq olunmaya bilər).
- Pricing/Checkout-a vergi məbləğinin inteqrasiyası.

**Giriş şərti:** Faza 4, Faza 6.

**Çıxış meyarı:** checkout zamanı doğru vergi məbləği hesablanır və göstərilir; market dəyişikliyində vergi qaydası düzgün seçilir.

**Test tələbləri:** çox-marketli vergi ssenarilərinin unit testləri.

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** Payments & Compliance komandası.
**Paralelləşdirmə:** yoxdur.

---

### Faza 13 — BFF-lər və Edge Layer

**Məqsəd:** Storefront/Admin/Partner client-ləri üçün ayrıca, öz ehtiyaclarına uyğun API səthini tamamlamaq və edge təhlükəsizlik qatını istehsala hazır hala gətirmək.

**Əhatə olunan texniki tapşırıqlar:**
- İlkin mərhələdə Storefront, Admin və Partner API səthləri bir deployment daxilində ayrı route group/contract kimi qurulur. Ayrı BFF deployment-ləri yalnız fərqli release ritmi və ya fərqli data-composition ehtiyacı sübut olunanda yaradılır.
- Hər API səthi üçün consumer-driven contract testlər (client-in gözlədiyi kontraktın backend dəyişikliyi ilə pozulmadığını sübut edən testlər).
- Cloud Armor security policy-lərinin qurulması: OWASP CRS pre-configured qaydalar, tenant+IP+endpoint scope-da rate limiting, Adaptive Protection aktivasiyası (bölmə 10.3).
- Compute seçimi: Cloud Run (default, HTTP-yönümlü, sürətli iterasiya) və ya GKE Autopilot (hibrid model üçün). External HTTP(S) Load Balancer + Cloud Armor inteqrasiyası.

**Giriş şərti:** əvvəlki bütün context-lərin (Faza 3–12) public contract-ları stabil olmalıdır.

**Çıxış meyarı:** 3 BFF də production-ready səviyyədə işləyir; consumer-driven contract testlər CI-da hər backend dəyişikliyində işə düşür; Cloud Armor qaydaları logging rejimindən enforce rejiminə keçirilib və test hücumları (SQLi/XSS simulyasiyası) bloklanır.

**Test tələbləri:** hər BFF üçün ayrıca contract test dəsti, edge layer-in rate-limit davranışının yük testi, Cloud Armor qaydalarının "qəsdən hücum" testi.

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** hər BFF üzrə müvafiq stream-aligned komanda + Platform komandası (Edge/Gateway/GCP infrastruktur üçün).
**Paralelləşdirmə:** əvvəlki fazaların statusuna bağlı, tədricən başlana bilər (məs. Storefront BFF-ə Faza 3/4/5 stabilləşən kimi başlamaq mümkündür).

---

### Faza 14 — Observability, Control Plane Tamamlanması, Health, Config

**Məqsəd:** sistemi əməliyyat baxımından tam "production-ready" səviyyəyə çatdırmaq.

**Əhatə olunan texniki tapşırıqlar:**
- Hər modulun health check-i, config bölməsi, metric namespace-i (bölmə 11.2).
- .NET Aspire `ServiceDefaults`/`AppHost` tam tətbiqi, OpenTelemetry exporter-inin Cloud Trace/Cloud Logging/Cloud Monitoring-ə (ya da Managed Service for Prometheus-a) bağlanması.
- Distributed tracing-in uçdan-uca doğrulanması (bölmə 11.1).
- Control Plane-in self-service onboarding + usage metering-ə çatdırılması (bölmə 4.2).
- SLO/alerting zəncirinin Cloud Monitoring alerting siyasətləri ilə qurulması (bölmə 11.3).

**Giriş şərti:** paralel, tədricən hər fazaya inteqrə oluna bilər — bu fazanın işi Faza 0-dan başlayaraq davamlı aparıla bilər, formal "bitmə" nöqtəsi digər fazaların əksəriyyəti tamamlandıqdan sonradır.

**Çıxış meyarı:** hər kritik yol üçün SLO təyin olunub və avtomatlaşdırılmış yük testi ilə yoxlanılır; SLO pozulanda Cloud Monitoring alert/on-call zənciri işə düşür (sübut edilir); yeni tenant tam self-service axını ilə (manual/CLI müdaxiləsi olmadan) yaradıla bilir; Cloud Trace-də bir sifariş sorğusunun bütün modullar arası keçidi tək trace kimi görünür.

**Test tələbləri:** hər kritik yol üçün avtomatlaşdırılmış yük testi, alert zəncirinin süni SLO pozuntusu ilə test edilməsi.

**Mürəkkəblik:** L.
**Tövsiyə olunan komanda:** Platform komandası.
**Paralelləşdirmə:** istənilən vaxt, digər fazalarla paralel.

---

### Faza 15 — Təhlükəsizlik Sərtləşdirmə və Compliance Gate

**Məqsəd:** production-a çıxmazdan əvvəl bağlanmalı son təhlükəsizlik qapısını qurmaq.

**Əhatə olunan texniki tapşırıqlar:**
- SAST/DAST/dependency+container scanning CI-a inteqrasiyası; Artifact Analysis-in Artifact Registry-də tam aktivləşdirilməsi.
- Cloud Build-də SLSA Level 3 build provenance-ın doğrulanması, Binary Authorization siyasətinin GKE/Cloud Run-a tətbiqi (yalnız imzalanmış/attestasiya olunmuş image-lərin deploy oluna bilməsi).
- Secret Manager-ə tam keçid (heç bir secret config/environment variable-da qalmır) və Workload Identity ilə giriş auditinin doğrulanması.
- SBOM generasiyası hər release üçün (bölmə 10.4).
- Minimum bir xarici pentest.
- OWASP ASVS 5.0 Level 1 doğrulaması (bölmə 10.4).

**Giriş şərti:** Faza 9, Faza 13.

**Çıxış meyarı:** CI-da yüksək-severity zəiflik aşkarlananda build fail olur; imzalanmamış/skan edilməmiş image-in deploy cəhdi Binary Authorization tərəfindən rədd edilir (test edilir); xarici pentest hesabatındakı kritik/yüksək tapıntılar bağlanıb; ASVS 5.0 Level 1 tələblərinin checklist-i tamamlanıb.

**Test tələbləri:** SAST/DAST-in "qəsdən zəiflik" test case-i ilə işlədiyinin sübutu; Binary Authorization-un "qəsdən icazəsiz image" test case-i ilə işlədiyinin sübutu.

**Mürəkkəblik:** L.
**Tövsiyə olunan komanda:** Payments & Compliance komandası + Platform komandası.
**Paralelləşdirmə:** yoxdur — bu, production-a çıxış üçün son gate-dir.

---

### Faza 16 — Backup/DR Əməliyyat Doğrulaması

**Məqsəd:** backup strategiyasının nəzəri deyil, real olaraq işlədiyini sübut etmək.

**Əhatə olunan texniki tapşırıqlar:**
- Cloud SQL PITR restore-un real (staging-də) sınağı.
- Qat-bazlı RPO/RTO hədəflərinin ölçülməsi (bölmə 12.1), Google Cloud Well-Architected Framework-un DR test metodologiyasına uyğun.
- Runbook sənədləşdirilməsi (kim, nə vaxt, hansı addımları atır, hansı Terraform/`gcloud` əmrləri istifadə olunur).

**Giriş şərti:** Faza 0.

**Çıxış meyarı:** tam restore staging-də edilib, ölçülmüş RTO sənədləşdirilmiş hədəfə uyğundur; runbook komandanın istənilən üzvü tərəfindən izlənə bilən dəqiqlikdədir.

**Test tələbləri:** ən azı bir "sürprizsiz" tam DR məşqi (planlaşdırılmış, lakin icraçı komanda üzvünün əvvəlcədən bütün detalları bilmədiyi).

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** Platform komandası.
**Paralelləşdirmə:** istənilən vaxt aparıla bilər.

---

### Faza 17 — AI-Assisted Discovery (ixtiyari)

**Məqsəd:** `pgvector` ilə semantic search/embedding-based tövsiyə üçün ilkin sınaq, yalnız Vertex AI Search for Commerce-in defolt təklif etdiyi personalization/recommendation qatı biznes ehtiyacını qarşılamadığı sübut olunanda (bölmə 16).

**Əhatə olunan texniki tapşırıqlar:**
- Embedding pipeline-ın qurulması (məhsul datasından vector generasiyası).
- `pgvector` ilə similarity sorğularının implementasiyası.
- A/B test infrastrukturu (bu fazaya xas, mövcud deyilsə) — nəticə Vertex AI Search for Commerce-in defolt recommendation nəticəsi ilə müqayisə edilir.

**Giriş şərti:** Faza 7, real istifadəçi/sifariş datası.

**Çıxış meyarı:** A/B test ilə conversion-a real təsir ölçülüb — yoxdursa investisiya davam etməməlidir.

**Test tələbləri:** A/B test metodologiyasının statistik etibarlılığının (nümunə ölçüsü, müddət) əvvəlcədən təyin edilməsi.

**Mürəkkəblik:** L.
**Qeyd:** yalnız təsdiqlənmiş biznes case ilə başlanmalıdır, "AI olsun deyə" yox.
**Tövsiyə olunan komanda:** Discovery komandası.
**Paralelləşdirmə:** digər fazalardan asılı olmayaraq, yalnız giriş şərti ödəndikdən sonra istənilən vaxt.

---

### Faza 18 — Beynəlxalqlaşdırma və Çox-Valyuta Genişlənməsi (ixtiyari, biznes-gated)

**Məqsəd:** yeni bazarlara çıxış qərarı veriləndə (bölmə 21-dəki biznes qərarına bağlı) platformanın buna hazır olması.

**Əhatə olunan texniki tapşırıqlar:**
- Çoxdilli məzmun modelinin Catalog-a əlavə edilməsi (locale-based tərcümə sahələri).
- Çox-valyuta Pricing dəstəyi (məzənnə mənbəyi, yenilənmə tezliyi — biznes qərarı).
- Market-based hüquqi/vergi fərqliliklərinin Faza 12-dəki ACL vasitəsilə genişlənməsi.
- Data residency qərarının (bölmə 12.3) aktivləşdirilməsi lazım gələrsə — Cloud SQL/AlloyDB-nin region seçimi, Secret Manager regional secret-lərinin tətbiqi.

**Giriş şərti:** Faza 4, Faza 12, real bazar genişlənməsi qərarı (bax bölmə 21).

**Çıxış meyarı:** yeni market/valyuta/dil kombinasiyası kod dəyişikliyi tələb etmədən, yalnız konfiqurasiya ilə əlavə edilə bilir.

**Mürəkkəblik:** L.
**Qeyd:** bu fazanın vaxtı tamamilə biznesin bazar genişlənməsi planına bağlıdır — erkən başlamaq (real bazar tələbi olmadan) əsassız mürəkkəblikdir (bölmə 4.1-dəki eyni prinsip).
**Tövsiyə olunan komanda:** Discovery + Payments & Compliance komandaları birgə.
**Paralelləşdirmə:** yalnız giriş şərti ödəndikdən sonra.

---

### Faza 19 — Performans və Yük Sərtləşdirməsi (davamlı, production-dan əvvəl son mərhələ)

**Məqsəd:** SLO-ların (Faza 14) nəzəri deyil, real yük altında sübut edilməsi.

**Əhatə olunan texniki tapşırıqlar:**
- Hər kritik yol üçün realistik ssenarili yük testi (checkout, axtarış, kataloq baxışı — həqiqi trafik nümunəsinə uyğun qarışıqla), GKE Autopilot/Cloud Run staging mühitində.
- Verilənlər bazası connection pool, `pg_advisory_xact_lock` istifadəsinin yük altında davranışının doğrulanması.
- Cloud SQL Enterprise Plus-un AIO/`io_method` tənzimləmələrinin real workload-a uyğunlaşdırılması (bölmə 2.2) — sync/worker/io_uring rejimləri arasında müqayisəli ölçmə.
- Outbox Publisher Worker-in yüksək event həcmi altında latency profilinin ölçülməsi.
- Cloud Armor rate-limit qaydalarının real trafik həcmində "false positive" yaratmadığının doğrulanması.

**Giriş şərti:** Faza 6, Faza 13, Faza 14.

**Çıxış meyarı:** hər kritik yol Faza 14-də təyin edilmiş SLO-nu real yük testində keçir; darboğaz (bottleneck) aşkarlananda ya optimallaşdırılıb (Cloud SQL → AlloyDB miqrasiyası daxil olmaqla, bölmə 2.2), ya da qəbul edilən risk kimi sənədləşdirilib.

**Test tələbləri:** production-a bənzər həcmdə sintetik yük generasiyası, davamlı (soak) test (qısa spike deyil, saatlarla davam edən yük).

**Mürəkkəblik:** M.
**Tövsiyə olunan komanda:** Platform komandası, hər stream-aligned komandanın öz modulunun profilini təqdim etməsi ilə.
**Paralelləşdirmə:** Faza 15/16 ilə paralel aparıla bilər.

---

## 19. Test və Etibarlılıq Strategiyası

Mövcud 4 test qatı (Domain.UnitTests, Api.UnitTests, ArchitectureTests, Persistence.IntegrationTests — ~195 test, "No Dynamic Mocking", real PostgreSQL Testcontainers) üzərinə aşağıdakılar əlavə olunur:

1. **Modullararası kontrakt testləri** — `Contracts` layihələri üzərində snapshot/approval test, CI aşkar edir.
2. **Outbox/Saga üçün fault-injection (chaos) testi** — Pricing/Inventory addımının süni uğursuz edildiyi test dəsti Faza 6-nın çıxış meyarına daxildir.
3. **3 BFF üçün consumer-driven contract testlər** — Faza 13-dən etibarən.
4. **Konkurrensiya/yük testləri üçün rəqəmsiz hədəf qəbuledilməzdir** — hər kritik yol üçün SLO (bölmə 11.3) avtomatlaşdırılmış yük testi ilə yoxlanılır (bax həmçinin Faza 19), staging-də GKE Autopilot/Cloud Run üzərində icra olunur.
5. **Təhlükəsizlik testi** — SAST/DAST/dependency scanning + Artifact Analysis CI-da avtomatlaşdırılır (Faza 15); minimum illik pentest.
6. **DR/restore testi** — Faza 16-nın çıxış meyarına daxil olan, ən azı illik təkrarlanan tam Cloud SQL PITR restore məşqi.
7. **A/B test infrastrukturu** — yalnız Faza 17/18 kimi biznes-gated fazalar aktivləşəndə tələb olunur, əvvəlcədən qurulmur.

---

## 20. Uzunmüddətli Fərqləndirmə Reallığı

Yuxarıdakı arxitektura Amazon və ya Trendyol ilə **eyni miqyasda** rəqabət təmin etmir — onların üstünlüyü memarlıqdan yox, illərin data-sı, öz logistika şəbəkəsi və kapitaldan gəlir. Bu arxitektura isə **commercetools/Shopify Plus/Medusa.js səviyyəsində** platforma səviyyəsinə çıxarır:
- Modular monolith → Shopify-ın milyonlarla sətir kodla və milyonlarla sorğu/saniyə miqyasında sübut etdiyi yol (bölmə 3.2).
- Pool + RLS multi-tenancy, Control/Application Plane ayrımı ilə tamamlanmış → AWS-in sənədləşdirdiyi standart SaaS modeli, GKE-nin öz namespace-based tenant izolyasiya təcrübəsi ilə tamamlanan (bölmə 4.1).
- Event-driven backbone, schema versioning → Chris Richardson-un mikroservis pattern kataloqunda olan, sənayenin ən çox istinad etdiyi canonical pattern-lər.
- Xarici IdP (Keycloak/OIDC) ilə autentifikasiya, SAST/DAST/SBOM + Binary Authorization ilə tədarük zənciri təhlükəsizliyi → OWASP/CNCF sənayə standartı + Google Cloud-un native supply-chain təhlükəsizlik toolset-i.
- Qat-bazlı RPO/RTO, expand/contract miqrasiya strategiyası → Google Cloud Well-Architected Framework-un Reliability sütununda rəsmiləşdirilmiş operativ yetkinlik.
- Vertex AI Search for Commerce ilə e-commerce-ə xüsusi qurulmuş, idarə olunan axtarış/kəşf qatı → kiçik komandanın axtarış mühəndisliyinə resurs sərf etmədən Google-səviyyəli relevantlıqdan faydalanması.

**Dürüst qeyd TechEmpower rəqəmi haqqında (bölmə 2.1):** bu benchmark 2026-cı ilin martında sonlandırılıb, və layihənin son illərdəki tənqidi ondan ibarətdir ki, "plaintext" testi wrk alətinin məhdudiyyəti səbəbindən süni tavana dəyir, framework-lərə aşağı-səviyyəli "trick"-lərdən istifadəyə icazə verilib, və nəticələr real tətbiq performansından çox "socket benchmark"-a bənzəyir tənqidi var. Bu o demək deyil ki, ASP.NET Core-un performansı yaxşı deyil (məlum, sabit performans profilinə malikdir), sadəcə "#1 yerdə olmaq" tək başına arxitektura qərarının əsası kimi tam etibar edilə bilməz — komandanın öz təcrübəsi və dəstək müddəti (bölmə 2.1) daha etibarlı əsaslardır.
*Mənbə: GitHub TechEmpower/FrameworkBenchmarks Issue #10932; DEV Community — "TechEmpower Framework Benchmarks are now Archived - What's next?".*

Real fərqləndirmə texnologiyadan sonra gələcək: **Search/kəşf keyfiyyəti, ekosistem, əməliyyat etibarlılığı (SLA, uptime), müştəri dəstəyi, satış/marketinq icrası** — bunlar məhz bu təməlin üzərində, Faza 8-dən sonra qurulur.

---

## 21. Qalan Həqiqi Açıq Suallar

Bəzi suallar mühəndislik sənədi ilə həll oluna bilməz, çünki cavabı biznes məlumatına və ya hüquqi məsləhətə bağlıdır:

- **Konkret RPO/RTO rəqəmləri** (bölmə 12.1-dəki cədvəl nümunədir) — biznesin risk toleransına və müştəri SLA öhdəliklərinə bağlıdır.
- **Konkret GDPR saxlama müddətləri və istisnalar** (bölmə 12.2) — hüquq məsləhətçisi ilə təsdiqlənməlidir, bu sənəd hüquqi məsləhət deyil.
- **IdP seçimi: özü-host Keycloak vs idarə olunan xidmət (Google Cloud Identity Platform / Auth0)** (bölmə 5.2) — komandanın DevOps tutumuna və büdcəsinə bağlıdır.
- **Compute hədəfi: GKE Autopilot vs Cloud Run vs hibrid model** (bölmə 2.6, 13.2) — komandanın Kubernetes təcrübəsinə, xərc modelinə və hər BFF-in trafik profilinə (bursty vs sabit) bağlıdır.
- **Axtarış xidməti: Vertex AI Search for Commerce (proprietary, aşağı əməliyyat yükü) vs özü-idarə edilən OpenSearch (vendor-neytral, GKE-də əlavə ops yükü)** (bölmə 2.4) — komandanın vendor lock-in toleransına bağlıdır.
- **Komanda topologiyasının (bölmə 14) real HR/işə qəbul planına uyğunlaşdırılması.**
- **Faza 18-dəki beynəlxalq genişlənmə vaxtı və hədəf bazarlar** — tamamilə biznes strategiyası qərarıdır.

---

## 22. Mənbələr

- Microsoft — "The official .NET support policy" (dotnet.microsoft.com/platform/support/policy)
- Microsoft — ".NET 8 and .NET 9 will reach End of Support on November 10, 2026" (devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support)
- Microsoft — "Announcing .NET 10" (devblogs.microsoft.com/dotnet/announcing-dotnet-10)
- Microsoft .NET Blog — "Introducing C# 14" (devblogs.microsoft.com/dotnet/introducing-csharp-14)
- Microsoft Learn — "What's new in C# 14" (learn.microsoft.com/dotnet/csharp/whats-new/csharp-14)
- TechEmpower Framework Benchmarks, Round 23, fevral 2025 (techempower.com/benchmarks/#section=data-r23)
- GitHub TechEmpower/FrameworkBenchmarks, Issue #10932 — "Sunsetting the TechEmpower Framework Benchmarks", 24 mart 2026
- DEV Community — "TechEmpower Framework Benchmarks are now Archived - What's next?" (dev.to, 25 mart 2026)
- Google Cloud Documentation — "The .NET runtime" (docs.cloud.google.com/run/docs/runtimes/dotnet)
- Google Cloud — .NET Client Libraries sənədləşməsi (docs.cloud.google.com/dotnet/docs/reference)
- PostgreSQL 18.0 Release Notes (postgresql.org/docs/release/18.0)
- PostgreSQL — "PostgreSQL 18 Released" press-kit (postgresql.org/about/news/postgresql-18-released-3142)
- PostgreSQL Documentation — "5.9. Row Security Policies", "CREATE POLICY" (postgresql.org/docs/current)
- PostgreSQL — "CVE-2026-14666: PostgreSQL row security caching disregards role modifications" (postgresql.org/support/security/CVE-2026-14666)
- PostgreSQL — "PostgreSQL 18.6, 17.11, 16.15, 15.19, 14.24 and 19 Beta 3 Released!" (postgresql.org, 13 avqust 2026)
- HeroDevs — "PostgreSQL 14 EOL Nov 2026: 44 CVEs This Year, One Patch Left"
- Google Cloud Documentation — "Cloud SQL for PostgreSQL release notes" (docs.cloud.google.com/sql/docs/postgres/release-notes)
- Google Cloud Documentation — "Cloud SQL for PostgreSQL features" (docs.cloud.google.com/sql/docs/postgres/features)
- Kumar Ramamurthy — "Postgres 18 on Cloud SQL Enterprise Plus — better together" (Google Cloud Community, Medium, dekabr 2025)
- Google Cloud Blog — "Postgres 18 and Extended Support for legacy versions in AlloyDB" (cloud.google.com/blog/products/databases/postgres-18-and-extended-support-for-legacy-versions-in-alloydb)
- Google Cloud Documentation — "AlloyDB for PostgreSQL — Database version policies" (docs.cloud.google.com/alloydb/docs/db-version-policies)
- Suds Kumar — "Cloud SQL Enterprise Plus vs. AlloyDB: A pgbench Showdown for High-Performance OLTP" (Google Cloud Community, Medium)
- Linux Foundation — "Linux Foundation Launches Open Source Valkey Community" (28 mart 2024)
- Linux Foundation — "Forking Ahead: A Year of Valkey"
- TechCrunch — "Why AWS, Google and Oracle are backing the Valkey Redis fork" (31 mart 2024)
- Google Cloud Blog — "Announcing general availability of Memorystore for Valkey" (aprel 2025)
- Google Cloud Blog — "Memorystore for Valkey 9.0 is now GA" (mart 2026)
- Linux Foundation — "Linux Foundation Announces OpenSearch Software Foundation to Foster Open Collaboration in Search and Analytics" (16 sentyabr 2024)
- AWS Open Source Blog — "AWS Welcomes the OpenSearch Software Foundation"
- BigDataBoutique — "Google Cloud OpenSearch: Deployment Options and Best Practices" (2026)
- Google Cloud Documentation — "Vertex AI Search for commerce API" (docs.cloud.google.com/retail/docs/reference/rpc)
- Google Cloud — "AI Commerce Search" məhsul səhifəsi (cloud.google.com/solutions/vertex-ai-search-commerce)
- Google Cloud — "Pub/Sub" məhsul sənədləşməsi
- Google Cloud Documentation — "Managed Service for Apache Kafka overview" (docs.cloud.google.com/managed-service-for-apache-kafka/docs/overview)
- Confluent — "Apache Kafka® vs Pub/Sub: Key Differences Explained" (confluent.io/compare/kafka-vs-pubsub)
- CNCF — "Keycloak joins CNCF as an incubating project" (cncf.io/blog/2023/04/11); CNCF — "Keycloak" layihə səhifəsi (cncf.io/projects/keycloak); Keycloak layihəsi, Apache License 2.0
- Google Cloud — "Identity Platform" məhsul sənədləşməsi (cloud.google.com/security/products/identity-platform)
- MACH Alliance rəsmi sayt (machalliance.org)
- EPAM — "EPAM Joins Newly Formed MACH Alliance as Founding Member" (24 iyun 2020)
- Martin Fowler — "MonolithFirst" (martinfowler.com/bliki/MonolithFirst.html, 3 iyun 2015)
- Dr Milan Milanović — "Inside Shopify's Modular Monolith" (newsletter.techworld-with-milan.com)
- Shopify Engineering — "Enforcing Modularity in Rails Apps with Packwerk"
- InfoQ — "How Shopify Migrated to a Modular Monolith" (Shopify Unite 2019 təhlili)
- Kamil Grzybek — "Modular Monolith with DDD" (github.com/kgrzybek/modular-monolith-with-ddd)
- AWS Whitepaper — "SaaS Tenant Isolation Strategies"; "SaaS Architecture Fundamentals" (docs.aws.amazon.com/whitepapers)
- Google Cloud Documentation — "Best practices for enterprise multi-tenancy" (docs.cloud.google.com/kubernetes-engine/docs/best-practices/enterprise-multitenancy)
- Chris Richardson — microservices.io — "Pattern: Transactional outbox" (microservices.io/patterns/data/transactional-outbox.html); "Pattern: Saga" (microservices.io/patterns/data/saga.html)
- Richardson, C. — *Microservices Patterns* (Manning, 2018)
- Sam Newman — "Pattern: Backends For Frontends" (samnewman.io/patterns/architectural/bff/, 2015)
- Google Cloud Documentation — "Tiered hybrid pattern" (docs.cloud.google.com/architecture/hybrid-multicloud-patterns-and-practices/tiered-hybrid-pattern)
- Microsoft — .NET Aspire rəsmi sənədləşməsi (learn.microsoft.com/dotnet/aspire)
- Aspire rəsmi inteqrasiya sənədləşməsi — "Kubernetes integration for Aspire: hosting and client wiring" (aspire.dev/integrations/compute/kubernetes)
- Google Cloud Documentation — "GKE Autopilot overview"
- CloudWebSchool — "GKE vs Cloud Run: Cost, Complexity, and When to Use Each in GCP" (2026)
- Pixel Guild — "Kubernetes on GKE: When to Use It and When Cloud Run Is Enough" (2026)
- Google Cloud — "Cloud Armor" məhsul səhifəsi (cloud.google.com/armor)
- Google Cloud Documentation — "Configuring Google Cloud Armor security policies"
- OneUpTime — "How to Choose Between Cloud Armor and Third-Party WAFs" (2026)
- Google Cloud Documentation — "Software supply chain security" (docs.cloud.google.com/software-supply-chain-security/docs/overview)
- Google Cloud Documentation — "Artifact analysis and vulnerability scanning" (docs.cloud.google.com/artifact-registry/docs/analysis)
- Google Cloud Blog — "Securing Cloud Run Deployments with Binary Authorization"
- Google Cloud Documentation — "Secret Manager overview" (docs.cloud.google.com/secret-manager/docs/overview)
- OWASP — "Application Security Verification Standard (ASVS) 5.0.0" (github.com/OWASP/ASVS)
- SoftwareMill — "What's New in ASVS 5.0"
- Google Cloud Documentation — "Collect OpenTelemetry Protocol (OTLP) metrics and traces" (docs.cloud.google.com/monitoring/agent/ops-agent/otlp)
- Google Cloud Documentation — "Instrumentation and observability" (docs.cloud.google.com/stackdriver/docs/instrumentation/overview)
- Google Cloud Documentation — "Google Cloud Managed Service for Prometheus" (docs.cloud.google.com/stackdriver/docs/managed-prometheus)
- Google Cloud Documentation — "Perform testing for recovery from data loss" (Well-Architected Framework, Reliability pillar) (docs.cloud.google.com/architecture/framework/reliability/perform-testing-for-recovery-from-data-loss)
- Google Cloud Documentation — "Architecting disaster recovery for cloud infrastructure outages" (docs.cloud.google.com/architecture/disaster-recovery)
- Google Cloud Documentation — "Configure Workload Identity Federation with deployment pipelines" (docs.cloud.google.com/iam/docs/workload-identity-federation-with-deployment-pipelines)
- Google Cloud — Terraform Google provider rəsmi sənədləşməsi
- Skelton, M. & Pais, M. — *Team Topologies: Organizing Business and Technology Teams for Fast Flow* (IT Revolution Press, 2019)
- pgvector — PostgreSQL vector similarity search extension (github.com/pgvector/pgvector)

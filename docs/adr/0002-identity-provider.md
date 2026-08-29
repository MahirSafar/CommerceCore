# ADR 0002: Identity Provider — Google Cloud Identity Platform

## Status
Approved

## Kontekst
CommerceCore sistemində istifadəçilərin (həm müştərilər, həm mağaza inzibatçıları) autentifikasiyası, parol idarəetməsi, token generasiyası və MFA kimi təhlükəsizlik əməliyyatlarını təmin etmək üçün xarici Identity Provider (IdP) həllinə ehtiyac vardır.

## Qərar
1. **İlkin İdarə Olunan Həll**:
   - İlkin seçim olaraq **Google Cloud Identity Platform** (idarə olunan OIDC/OAuth2 xidməti) qəbul edilir.
   - Səbəb: Kiçik mühəndislik komandası üçün əməliyyat (operational) yükünü minimuma endirmək, infrastruktur təhlükəsizliyini və SLA-nı Google Cloud-un idarə olunan xidmətinə həvalə etmək.

2. **Niyə Keycloak İndi Seçilmir**:
   - Keycloak özü-host edilən (self-hosted) arxitekturada ayrıca PostgreSQL verilənlər bazası klasteri, daimi versiya yeniləmələri (upgrade/migration), backup/DR planlaması, JVM sazlaması və təhlükəsizlik yamalarının (CVE patch-lərinin) tətbiqi kimi ciddi əməliyyat yükü yaradır.
   - Mövcud mərhələdə bu xərc platformanın biznes dəyəri yaratmasına xidmət etmir.

3. **Gələcək Uyğunluq**:
   - Backend `Platform.Identity` qatı standart OIDC/JWT kontraktları (`sub`, `iss`, `aud`, claims) üzərində qurulur.
   - Əgər gələcəkdə xüsusi on-premise, multi-cloud və ya qabaqcıl federasiya ehtiyacları yaranarsa, OIDC standartı sayəsində Keycloak və ya digər provayderə keçid backend kodunda dəyişiklik tələb etmədən konfiqurasiya səviyyəsində həyata keçirilə biləcəkdir.

## Nəticələr
- **Müsbət tərəflər**:
  - Sıfır server/verilənlər bazası texniki xidməti və yüksək əlçatanlıq (HA).
  - GCP ekosistemi (Cloud Run/GKE, Secret Manager, Cloud IAM) ilə native inteqrasiya.
- **Mənfi tərəflər**:
  - Google Cloud Identity Platform xidmətindən asılılıq (standart JWT/OIDC abstraksiyası vasitəsilə risk azaldılır).

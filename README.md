# StajDesk

Mağazalı bir şirketin bilgi işlem destek birimi için yardım masası ve envanter uygulaması (GLPI benzeri).
Devoplus Stajyer Gelişim Programı kapsamında geliştirilmektedir.

| Katman | Teknoloji |
|---|---|
| Arayüz | React 19 + TypeScript + Vite + Tailwind CSS v4 (nginx ile sunulur) |
| API | ASP.NET Core Web API (.NET 10 LTS), Clean Architecture + CQRS, FluentValidation |
| Kimlik | OAuth 2.0 (Authorization Code + PKCE, Refresh Token rotasyonu, Client Credentials), RS256 JWT |
| Yetki | Rol + izin tabanlı (RBAC); roller ekrandan düzenlenebilir |
| Arka plan servisi | .NET 10 Worker Service (bildirim + e-posta) |
| Veritabanı | PostgreSQL 17 + Entity Framework Core 10 |
| Önbellek / hız sınırı | Redis 7 |
| Mesaj kuyruğu | RabbitMQ 4 (transactional outbox ile) |
| Merkezî loglama | Serilog → Seq |
| Demo e-posta | Mailpit (e-postalar gerçekten SMTP ile gönderilir ama dışarı çıkmaz) |

## Hızlı başlangıç (tek komut)

Gereken tek şey Docker Desktop.

```bash
cp .env.example .env
```

```bash
docker compose up -d --build
```

| Adres | Ne var? |
|---|---|
| http://localhost:8080 | StajDesk arayüzü |
| http://localhost:5080/docs | API dokümantasyonu (Scalar / OpenAPI) |
| http://localhost:8025 | Mailpit — uygulamanın gönderdiği tüm e-postalar |
| http://localhost:5380 | Seq — tüm servislerin logları |
| http://localhost:15672 | RabbitMQ yönetim paneli (`.env` içindeki kullanıcı) |

> Bilgisayarınızda 5432 portunu başka bir PostgreSQL kullanıyorsa `.env` içinde `POSTGRES_PORT=5433` yapın.

### İlk kurulum hesapları

Açılışta migration'lar uygulanır, izinler/roller senkronlanır ve **Sistem Yöneticisi** oluşturulur.

| Rol | E-posta | Şifre |
|---|---|---|
| **Sistem Yöneticisi** | `sysadmin@stajdesk.local` | `StajDesk!2026` (`.env` → `SYSADMIN_PASSWORD`) |

`SEED_DEMO_DATA=true` ise 3 mağaza, 8 kullanıcı, cihazlar ve talepler de eklenir (şifre: `DEMO_PASSWORD`, varsayılan `Demo!2026pass`).
Tam liste ve deneme senaryosu: [docs/DEMO.md](docs/DEMO.md).

> Canlıya çıkmadan önce `.env` içindeki tüm şifreleri değiştirin.

## Roller (özet)

| Rol | Ne görür? |
|---|---|
| Sistem Yöneticisi | Her şey: kullanıcılar, roller ve izinler, mağazalar, kategoriler, denetim kayıtları, gönderilen e-postalar |
| Destek Personeli | Tüm talepler, atama/durum/öncelik, dahili notlar, envanter, genel özet paneli |
| Mağaza Sahibi | Kendi mağazasının talepleri, cihazları, çalışanları ve mağaza özet paneli |
| Kullanıcı | Yalnızca kendi talepleri ve kendisine zimmetli cihazlar |
| İK Personeli | Personel bilgileri; yeni çalışan / ayrılan çalışan için **hesap talebi** açar (hesap açamaz, rol veremez) |
| BT Hesap Yöneticisi | Hesap taleplerini onaylar; hesap açar, pasife alır, kilitler; yalnızca kendi yetkileri kadar rol verir |

StajDesk kapalı bir portaldır: kimse kendi hesabını açamaz. Süreç: İK talep → BT onay → çalışana "ilk şifreni belirle" e-postası.

Unvan (Müdür, Uzman, Stajyer…) ile sistem rolü ayrıdır; yetkiyi rol belirler. Ayrıntı: [docs/ORGANIZASYON.md](docs/ORGANIZASYON.md).

Ayrıntılı izin matrisi: [docs/RBAC.md](docs/RBAC.md).

## Geliştirme ortamı

Altyapıyı Docker'da, uygulamaları yerelde çalıştırın:

```bash
docker compose up -d postgres redis rabbitmq seq mailpit
```

```bash
cd src/backend && dotnet run --project StajDesk.Api
```

```bash
cd src/backend && dotnet run --project StajDesk.Worker
```

```bash
cd src/frontend && npm install && npm run dev
```

### Testler

```bash
cd src/backend && dotnet test
```

```bash
cd src/frontend && npm run build
```

```bash
bash scripts/smoke-test.sh
```

`smoke-test.sh` çalışan Docker ortamına karşı uçtan uca 21 kontrol yapar (OAuth + PKCE, refresh rotasyonu, rol görünürlüğü, XSS reddi, e-posta zinciri…).

## Dokümanlar

| Dosya | İçerik |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Katmanlar, CQRS, outbox, veri modeli, API uçları |
| [docs/AUTH.md](docs/AUTH.md) | OAuth 2.0 akışları, token'lar, oturum güvenliği |
| [docs/RBAC.md](docs/RBAC.md) | Roller, izinler, veri kapsamı (kendi / mağaza / tümü) |
| [docs/SECURITY.md](docs/SECURITY.md) | XSS, CSP, hız sınırı, denetim kaydı, kontrol listesi |
| [docs/DEMO.md](docs/DEMO.md) | Demo hesapları ve adım adım deneme senaryosu |
| [docs/ORGANIZASYON.md](docs/ORGANIZASYON.md) | Departman, unvan, yönetici, personel kartı, İK rolü, e-posta doğrulama, davet, SMS doğrulaması |
| [docs/PORTAL_ANALIZ.md](docs/PORTAL_ANALIZ.md) | Kurumsal portal analizi, kararlar ve aşama planı |
| [docs/GELISTIRME_LOG.md](docs/GELISTIRME_LOG.md) | Geliştirme günlüğü: tamamlananlar, hatalar ve çözümler |
| [docs/PANEL.md](docs/PANEL.md) | Ana sayfa paneli, duyurular, nöbet çizelgesi, dosya ekleri, anlık bildirim, tam metin arama |
| [docs/postman/](docs/postman) | Postman koleksiyonu (PKCE girişi dahil tüm uçlar) |
| [docs/SUNUM.md](docs/SUNUM.md) | Final sunumu taslağı ve demo senaryosu |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Mimari karar kayıtları (neden böyle yaptık?) |
| [AGENTS.md](AGENTS.md) | Projede çalışan geliştiriciler ve kod asistanları için kurallar |
| [docs/playbooks/](docs/playbooks) | Sık yapılan işler için adım adım rehberler |

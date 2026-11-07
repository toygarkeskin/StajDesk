# StajDesk

Bir şirketin destek birimine gelen talepleri yöneten web uygulaması (GLPI benzeri yardım masası + envanter).
Devoplus Stajyer Gelişim Programı kapsamında geliştirilmektedir.

| Katman | Teknoloji |
|---|---|
| Arayüz | React 19 + TypeScript + Vite + Tailwind CSS v4 (nginx ile sunulur) |
| API | ASP.NET Core Web API (.NET 10 LTS), JWT kimlik doğrulama, FluentValidation |
| Arka plan servisi | .NET 10 Worker Service (bildirimler) |
| Veritabanı | PostgreSQL 17 + Entity Framework Core 10 (migration'lar) |
| Önbellek / hız sınırı | Redis 7 |
| Mesaj kuyruğu | RabbitMQ 4 |
| Merkezî loglama | Serilog → Seq |

## Hızlı başlangıç (tek komut)

Gereken tek şey Docker Desktop.

```bash
cp .env.example .env    # isteğe bağlı: şifreleri ve portları değiştirin
docker compose up -d --build
```

| Adres | Ne var? |
|---|---|
| http://localhost:8080 | StajDesk arayüzü |
| http://localhost:5080/docs | API dokümantasyonu (Scalar / OpenAPI) |
| http://localhost:5380 | Seq — tüm servislerin logları |
| http://localhost:15672 | RabbitMQ yönetim paneli (`.env` içindeki kullanıcı) |

Açılışta migration'lar otomatik uygulanır, varsayılan kategoriler ve yönetici hesabı oluşturulur.
`SEED_DEMO_DATA=true` ise örnek kullanıcılar, cihazlar ve talepler de eklenir.

| Rol | E-posta | Şifre (varsayılan) |
|---|---|---|
| Yönetici | `admin@stajdesk.local` | `.env` → `ADMIN_PASSWORD` |
| Destek personeli | `elif.destek@stajdesk.local`, `burak.destek@stajdesk.local` | `.env` → `DEMO_PASSWORD` |
| Kullanıcı | `ayse@stajdesk.local`, `mehmet@stajdesk.local`, `zeynep@stajdesk.local` | `.env` → `DEMO_PASSWORD` |

> Bilgisayarınızda 5432 portunu kullanan başka bir PostgreSQL varsa `.env` içinde `POSTGRES_PORT=5433` yapın.

## Geliştirme ortamı (kodu değiştirirken)

Altyapıyı Docker'da, uygulamaları yerelde çalıştırın:

```bash
docker compose up -d postgres redis rabbitmq seq
```

```bash
cd src/backend && dotnet run --project StajDesk.Api      # http://localhost:5080
```

```bash
cd src/backend && dotnet run --project StajDesk.Worker
```

```bash
cd src/frontend && npm install && npm run dev            # http://localhost:5173
```

Yerel ayarlar `appsettings.Development.json` dosyalarındadır ve `.env.example` varsayılanlarıyla eşleşir.

### Testler

```bash
cd src/backend && dotnet test
```

### Yeni migration

```bash
cd src/backend && dotnet tool restore && dotnet ef migrations add <Ad> -p StajDesk.Infrastructure -s StajDesk.Api -o Persistence/Migrations
```

## Özellikler

- Kayıt / giriş (JWT), rol bazlı yetki: **Kullanıcı**, **Destek Personeli**, **Yönetici**
- Talep açma, listeleme, filtreleme (durum, öncelik, kategori, tür, arama), sayfalama, sıralama
- Durum akışı: Açık → İşlemde → Çözüldü → Kapandı (kurallar domain'de); talep sahibi çözümü onaylayıp kapatabilir
- Yorumlar ve **dahili notlar** (yalnızca destek ekibi görür), değişiklik geçmişi (zaman çizelgesi)
- Atama (atanınca otomatik "İşlemde"), öncelik yönetimi
- **Envanter** (GLPI "Assets"): cihaz, zimmet, garanti, cihaza bağlı talepler
- Özet paneli: durum/öncelik/kategori dağılımı, 14 günlük trend, ortalama çözüm süresi, ekip iş yükü
- RabbitMQ üzerinden asenkron bildirimler (Worker), arayüzde bildirim zili (15 sn polling)
- Redis: kategori ve istatistik önbelleği (cache-aside, 60 sn), e-posta başına brute-force koruması (5 dk'da 5 hatalı giriş → 429)
- Açık / koyu tema, mobil uyumlu arayüz

Mimari ayrıntıları, veri modeli ve API uçları için: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

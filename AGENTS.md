# AGENTS.md

Bu dosya projede çalışan herkes (geliştiriciler ve kod asistanları) için kısa çalışma kurallarıdır.
Ayrıntılı bilgi `docs/` altındadır; burada yalnızca "bunu bilmeden kod yazma" kuralları var.

## Proje haritası

| Yol | İçerik |
|---|---|
| `src/backend/StajDesk.Domain` | Varlıklar, iş kuralları, `Security/Permissions.cs` (izin ve rol tanımları) |
| `src/backend/StajDesk.Application` | CQRS handler'ları (`Features/Tickets`), servisler, doğrulayıcılar, soyutlamalar |
| `src/backend/StajDesk.Infrastructure` | EF Core, migration'lar, `DbSeeder`, Redis, RabbitMQ + outbox |
| `src/backend/StajDesk.Api` | Controller'lar, `OAuthController`, `Infrastructure/Auth.cs` (JWT + izin middleware'i) |
| `src/backend/StajDesk.Worker` | Kuyruk tüketicisi, bildirim ve e-posta |
| `src/frontend/src` | React arayüzü (`lib/`, `stores/`, `components/`, `pages/`) |
| `scripts/smoke-test.sh` | Çalışan ortama karşı uçtan uca kontroller |

## Komutlar

| İş | Komut |
|---|---|
| Tüm ortamı başlat | `docker compose up -d --build` |
| Backend testleri | `cd src/backend && dotnet test` |
| Frontend derleme + tip kontrolü | `cd src/frontend && npm run build` |
| Uçtan uca kontrol | `bash scripts/smoke-test.sh` |
| Migration ekle | `cd src/backend && dotnet ef migrations add <Ad> -p StajDesk.Infrastructure -s StajDesk.Api -o Persistence/Migrations` |

Bir değişiklik bu üç kontrolden (test, build, smoke) geçmeden tamamlanmış sayılmaz.

## Değişmez kurallar

1. **Yetki kontrolü izne göre yapılır, rol adına göre değil.** `if (user.Role == "admin")` yazma; `currentUser.Require(Permissions.X)` veya `[RequirePermission(...)]` kullan.
2. **Arayüzdeki gizleme güvenlik değildir.** Her yeni uçta API tarafında izin ve veri kapsamı (own / store / all) kontrol edilir.
3. **Olaylar outbox ile yayınlanır.** RabbitMQ'ya doğrudan publish etme; `IEventPublisher.Enqueue` + `IUnitOfWork` kullan.
4. **Worker idempotent kalmalı.** Yeni bir tüketici `processed_messages` kontrolünü atlamamalı.
5. **Kullanıcı metni HTML değildir.** Tek satırlık alanlara `NoMarkup()` ekle; arayüzde `dangerouslySetInnerHTML` kullanma; e-posta şablonlarında `HtmlEncode`.
6. **Gizli bilgi depoya girmez.** Şifre, client secret, imza anahtarı `.env` / ortam değişkeni / volume'dadır. Yeni bir ayar eklersen `.env.example`'a da ekle.
7. **Token'lar DB'de hash'li tutulur.** Yeni bir tek kullanımlık token türü eklersen aynı deseni (`Sha256`) kullan.
8. **Loglara kişisel/gizli veri yazılmaz.** Şifre, token, tam e-posta içeriği loglanmaz.
9. **Güvenlik açısından önemli her işlem denetim kaydı bırakır** (`IAuditLogger.Add`).
10. **Önbellek geçersizleştirmeyi unutma.** İzin/rol değişince `perms:*`, talep değişince `stats:*`, kategori değişince `categories:all` silinir.

## Kod stili

- Backend: dosya kapsamlı namespace, `record` DTO'lar, `async` + `CancellationToken`, açıklamalar Türkçe.
- Frontend: TypeScript `strict`; API tipleri `lib/types.ts`'te; sunucu verisi TanStack Query ile; yeni izin anahtarı `Permission` tipine eklenir.
- Arayüz metinleri Türkçe; buton etiketleri fiil ile başlar ("Talebi kaydet", "Kullanıcı ekle").
- Commit mesajları Türkçe ve ne/neden anlatır.

## Rehberler

| İş | Rehber |
|---|---|
| Yeni izin veya rol eklemek | [docs/playbooks/add-permission.md](docs/playbooks/add-permission.md) |
| Yeni olay + e-posta eklemek | [docs/playbooks/add-event.md](docs/playbooks/add-event.md) |
| Yeni CQRS komutu / sorgusu | [docs/playbooks/add-command.md](docs/playbooks/add-command.md) |
| Yeni ekran eklemek | [docs/playbooks/add-page.md](docs/playbooks/add-page.md) |
| Ortam sorunlarını çözmek | [docs/playbooks/troubleshooting.md](docs/playbooks/troubleshooting.md) |

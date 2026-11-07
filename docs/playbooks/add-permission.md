# Yeni izin veya rol eklemek

## Yeni izin

1. `src/backend/StajDesk.Domain/Security/Permissions.cs`
   - Sabiti ekle: `public const string ReportsExport = "reports.export";`
   - `All` listesine tanımını ekle (grup + Türkçe açıklama). Roller ekranı bu listeden çizilir.
   - Hangi varsayılan rollerde olacağını `RoleNames.Defaults` içinde belirt.
2. API'de kullan: `[RequirePermission(Permissions.ReportsExport)]` veya handler içinde `currentUser.Require(Permissions.ReportsExport)`.
3. Frontend: `src/frontend/src/lib/types.ts` → `Permission` birleşik tipine `'reports.export'` ekle. Menü/buton için `useCan()`.
4. Test: `SecurityTests` içine izni olan ve olmayan kullanıcıyla birer test ekle.
5. Dokümantasyon: [RBAC.md](../RBAC.md) matrisine satır ekle.

Uygulama açılırken `DbSeeder` yeni izni `permissions` tablosuna ekler.
Varsayılan rollere yalnızca **ilk kurulumda** atanır; mevcut ortamda sistem yöneticisi Roller ve İzinler ekranından verir.
Migration gerekmez.

## Yeni rol

- Yalnızca bu ortam için: kod değişikliği gerekmez, veritabanına rol eklenip ekrandan izinleri seçilir.
- Her kurulumda varsayılan olsun: `RoleNames` içine sabit ve `Defaults` listesine tanım ekle. Arayüzde etiket için `labels.ts` → `roleLabels`.

## Kontrol

```bash
cd src/backend && dotnet test
```

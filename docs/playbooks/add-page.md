# Yeni ekran eklemek

1. **Tipler** — API'nin döndüğü şekli `src/frontend/src/lib/types.ts` içine yaz. `any` kullanma.
2. **API çağrısı** — `src/frontend/src/lib/api.ts` içinde ilgili gruba fonksiyon ekle. Token ve 401 → refresh otomatik.
3. **Sayfa** — `src/frontend/src/pages/` altında. Desen:
   - Liste: `DataFrame` + `ListToolbar` + `Pagination`; filtreler URL'de (`useSearchParams`) — paylaşılabilir ve geri tuşu çalışır.
   - Durumlar: yükleniyor → `TableSkeleton`, hata → `ErrorState` (tekrar dene), boş → `EmptyState` (eylem öner).
   - Form: `Field` + görünür etiket; doğrulama alan terk edilince; sunucu hataları alanlara dağıtılır.
4. **Route** — `src/frontend/src/App.tsx`. İzin gerekiyorsa `<RequireAuth perms={['x.read']}>`.
5. **Menü** — `components/layout/Sidebar.tsx`; `can('x.read') && { … }` ile izin koşullu.
6. **Kontrol** — `npm run build` (tip kontrolü dahil); farklı rollerle girip menüyü ve ekranı dene; 375 px genişlikte yatay kaydırma olmamalı.

Görsel dil: kart (`card`), menekşe vurgu, `btn-primary` / `btn-secondary` / `btn-violet`. Yeni renk veya gölge icat etme.

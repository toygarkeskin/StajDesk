# Görevler, duyurular ve talep durumları (Aşama 6.2)

## 1. Görev yönetimi

**Ekran:** Genel → **Görevler** (`/tasks`). Panelde **Yapılacaklarım** kutucuğu (açık, geciken ve bugün teslim sayılarıyla).

Görev, destek talebinden farklıdır: talep bir sorun bildirimidir, görev ise ekip içi iş atamasıdır.

| Kural | Açıklama |
|---|---|
| Kendine görev | Herkes açabilir |
| Başkasına atama | Atanan kişi organizasyonda sizin altınızdaysa (doğrudan veya dolaylı rapor; yönetici zinciri yukarı doğru en fazla 15 basamak kontrol edilir) ya da `tasks.manage` izniniz varsa |
| Görünürlük | Atanan, açan ve `tasks.manage` sahibi. Diğerleri için görev "bulunamadı" döner |
| Düzenleme / silme | Yalnızca açan kişi (veya `tasks.manage`) |
| Durum değiştirme | Atanan veya açan |
| Bildirim | Atanan kişiye "Size görev atandı"; atanan tamamlayınca açan kişiye "Görev tamamlandı". Bildirim görevi açar |

Durumlar: Yapılacak, Devam ediyor, Tamamlandı, İptal. Teslim tarihi geçmiş ve açık görevler "gecikti" olarak işaretlenir.

## 2. Duyurular

| Özellik | Açıklama |
|---|---|
| Hedef departman | Seçilirse yalnızca o departmandaki çalışanlar görür; mağaza hedefiyle birlikte uygulanır. Hedeflenmiş duyuru kurumsal sitede asla gösterilmez |
| Okundu bilgisi | Çalışan duyurular sayfasını açtığında görünen ve yayında olan duyurular bir kez "okundu" işaretlenir. Yönetici her duyuruda "okuyan / hedef kitle" sayısını ve okuyanlar listesini (kim, ne zaman) görür |
| Ek dosya / görsel | PDF, görsel, Word, Excel veya metin, en fazla 5 MB. Dosya içeriği uzantısıyla doğrulanır (talep ekleriyle aynı kurallar). İndirme yetki denetimlidir: yalnızca duyuruyu görebilen indirir |
| Önemli duyuru | Önem (Bilgi / Önemli / Acil) ve "Panelde en üstte sabitle" seçenekleri |

Arama, ana sayfa ve duyuru listesi aynı görünürlük kuralını kullanır.

## 3. Destek talebi durumları

| Durum | Anlamı |
|---|---|
| **Yeni** | Talep açıldı, henüz ele alınmadı. Talep sahibi bu durumda düzenleyebilir |
| Açık | Destek ekibi kabul etti |
| İşlemde | Üzerinde çalışılıyor. Yeni veya Açık talep birine atanınca otomatik bu duruma geçer |
| Beklemede | Kullanıcıdan bilgi veya parça bekleniyor |
| Çözüldü | Talep sahibi onaylayıp kapatabilir veya "çözülmedi" diyebilir |
| Kapandı | Sonuçlandı; yalnızca yeniden açılabilir |
| **Reddedildi** | Destek ekibi gerekçeyle reddetti (kapsam dışı, mükerrer...). Gerekçe zorunludur ve talep sahibine görünen yorum olarak eklenir. Yorum, ek ve düzenleme yapılmaz; destek ekibi yeniden açabilir |

Reddedilen talepler "aktif" sayımlarına girmez (panel sayaçları, mağaza, kategori ve cihaz özetleri).

**İlgili departman:** Talep açılırken veya düzenlenirken seçilebilir (isteğe bağlı). Talep listesinde departman filtresi var.

## 4. API

| Uç | Yetki |
|---|---|
| `GET /api/tasks?scope=mine\|created\|all&openOnly=` · `GET /api/tasks/summary` · `GET /api/tasks/{id}` | Oturum (`all` → `tasks.manage`) |
| `POST /api/tasks` · `PUT /api/tasks/{id}` · `PATCH /api/tasks/{id}/status` · `DELETE /api/tasks/{id}` | Kurallar yukarıda |
| `POST /api/announcements/{id}/read` | Duyuruyu görebilen |
| `GET /api/announcements/{id}/readers` | `announcements.manage` |
| `PUT` / `DELETE /api/announcements/{id}/attachment` | `announcements.manage` |
| `GET /api/announcements/{id}/attachment` | Duyuruyu görebilen |
| `PATCH /api/tickets/{id}/status` (`{ status, reason }`) | Reddedildi için `reason` zorunlu |
| `GET /api/tickets?departmentId=` | Talep görünürlüğü |

## 5. Veri modeli

`work_tasks`, `announcement_reads` (duyuru + kullanıcı), `announcements.target_department_id` ve ek alanları, `tickets.department_id`. Talep durum sayıları: `New = 5`, `Rejected = 6` (mevcut kayıtlar değişmez). Migration: `GorevDuyuruTalep`.

## 6. Testler

- **Birim testleri:** `TasksAnnouncementsTests` (6) ve talep testlerine eklenenler (4).
  - Görev atama kuralları: ekip zinciri, yukarı atama yasağı, `tasks.manage`.
  - Görevin görünürlüğü ve atananın içerik değiştirememesi.
  - Hedefli duyurunun yalnızca hedefe görünmesi.
  - Okundu bilgisinin bir kez sayılması.
  - Ek dosyanın doğrulanması ve yalnızca hedefe indirilebilmesi.
  - Ret gerekçesi ve yeniden açma kısıtı.
  - Departmana göre süzme.
- **Smoke testi:** "Görevler, hedefli duyurular, talep durumları" bölümü (16 kontrol). Test kendi oluşturduğu görevi ve duyuruyu siler. Test verisi Türkçe karakter içermez, çünkü Windows'taki curl komut satırı argümanlarını UTF-8 göndermiyor.

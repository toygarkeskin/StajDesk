# İş akışı (onay) motoru ve izin sistemi (Aşama 6.1)

## 1. Onay motoru

Her talep türünün bir **onay zinciri** vardır (Yönetim → **Onay zincirleri**, `workflows.manage`). Zincir sıralı adımlardan oluşur; her adımın onaylayıcısı şunlardan biridir:

| Onaylayıcı | Açıklama |
|---|---|
| Talep sahibinin yöneticisi | Organizasyondaki doğrudan yöneticisi |
| Yöneticinin yöneticisi (müdür) | "Gerekirse müdür onaylar" senaryosu |
| Departman yöneticisi | Talep sahibinin departmanı ya da adımda seçilen sabit departman (**departman bazlı yönlendirme**) |
| Belirli bir kişi | Seçilen çalışan |
| Bir role sahip herkes | Rolün herhangi bir üyesi karar verebilir (ör. İzin Yöneticisi) |

**Kurallar**
- Talep başlarken her adımın onaylayıcısı çözülür ve kaydedilir. Sonradan yönetici değişse veya zincir düzenlense bile başlamış talep başladığı zincirle biter.
- Onaylayıcısı bulunamayan adım otomatik atlanır ve gerekçesi zaman çizelgesine yazılır. Bulunamama durumları: yönetici tanımlı değil, onaylayıcı pasif hesap, onaylayıcı talep sahibinin kendisi.
- Ardışık iki adım aynı kişiye düşerse kişi bir kez onaylar; ikinci adım "atlandı" olur.
- Talep sahibi kendi talebini hiçbir koşulda onaylayamaz (rol adımında da).
- **Ret gerekçesi zorunludur**; talep sahibine bildirimle gider. Ret sonrası kalan adımlar çalışmaz.
- Her adımda onaylayıcılara uygulama içi bildirim gider (Onay kutum bağlantısıyla). Sonuç talep sahibine bildirilir.
- Her karar denetim kaydı bırakır (`workflow.step_approved` / `workflow.step_rejected`).

**Ekranlar:** **Onay kutum** (`/approvals`): kararımı bekleyenler, onay zinciri geçmişi, onayla / gerekçeyle reddet. Panelde "Onay kutum" kutucuğu da vardır.

## 2. İzin sistemi

**Ekran:** İK ve İdari İşler → **İzin talepleri** (`/leave`). Profilde **İzinler** sekmesi, panelde **İzin bakiyem** kutucuğu.

| Kim | Ne yapar |
|---|---|
| Çalışan (`leave.request`) | Bakiyesini görür, izin talebi açar, onay durumunu izler, bekleyen veya başlamamış onaylı iznini geri çeker |
| Yönetici | Ekibinin taleplerini Onay kutusunda onaylar / reddeder (izin değil, organizasyon ilişkisi belirler) |
| İzin Yöneticisi (`leave.manage`, rol: `leave_admin`) | İzin türleri, kişiye özel haklar, tüm talepler; varsayılan zincirde ikinci onay adımı |

**İK ≠ izin yönetimi:** İK rolü (`hr`) izin yönetimi vermez. İzin yönetimi ayrı bir izin ve ayrı bir roldür (*İzin Yöneticisi*); kişiye ayrıca atanır. Demo ortamında `deniz.ik` hesabına bu rol ek olarak verilmiştir.

**Varsayılan zincir:** 1) Yönetici onayı → 2) İzin yöneticisi onayı (rol). Ekrandan değiştirilebilir.

**İzin türleri** (yönetilebilir): Yıllık (14 gün, bakiye takipli), Mazeret (5 gün, bakiye takipli), Ücretsiz, Doğum, Süt, Görev, Uzaktan çalışma ve Denkleştirme (bakiye takipli, hak izin yöneticisince verilir).

**Bakiye:** Kalan = hak − onaylanan − bekleyen. Hak, kişiye özel tanımlandıysa o değer, tanımlanmadıysa türün yıllık varsayılanıdır (Haklar sekmesi; kıdem, devreden izin gibi durumlar için). Bakiye yıllıktır; yıl dönümünü kapsayan izin iki ayrı talep olarak açılır.

**Gün hesabı:** İş günü sayılır; hafta sonu ve sabit tarihli resmî tatiller (1 Ocak, 23 Nisan, 1 Mayıs, 19 Mayıs, 15 Temmuz, 30 Ağustos, 29 Ekim) düşülür. Form yazarken hesabı ve kalan bakiyeyi canlı gösterir.

**Kurallar:** Bitiş ≥ başlangıç; tek talepte en fazla 120 günlük aralık; 30 günden eski tarih için talep açılmaz; bekleyen veya onaylı başka bir izinle çakışma reddedilir; bakiye takipli türde yetersiz bakiye reddedilir. Talebi yalnızca sahibi, onay zincirindeki kişiler ve izin yöneticisi görür.

## 3. API

| Uç | Yetki |
|---|---|
| `GET /api/approvals` · `GET /api/approvals/count` | Oturum (yalnızca bana düşenler) |
| `GET /api/approvals/instances/{id}` | Zincirdeki kişi, talep sahibi veya yönetici izinleri |
| `POST /api/approvals/{approvalId}/approve` · `/reject` (`{ comment }`) | Adımın onaylayıcısı |
| `GET /api/workflows` · `GET /api/workflows/roles` · `PUT /api/workflows/{id}` | `workflows.manage` |
| `GET /api/leave/types` | Oturum |
| `POST /api/leave/types` · `PUT /api/leave/types/{id}` · `PUT /api/leave/entitlements` | `leave.manage` |
| `GET /api/leave/balances?userId=&year=` | Kendisi; başkası için `leave.manage` |
| `GET /api/leave/days?start=&end=` | Oturum |
| `GET /api/leave/requests` (`all=true` → `leave.manage`) · `POST /api/leave/requests` | `leave.request` |
| `GET /api/leave/requests/{id}` · `/workflow` · `POST .../cancel` | Sahibi, zincirdeki kişi, `leave.manage` |

## 4. Veri modeli

`workflow_definitions` (anahtar tekil), `workflow_steps`, `workflow_instances` (konu türü + kimlik), `workflow_approvals` (adım, çözülmüş onaylayıcı / rol, karar, karar veren, açıklama), `leave_types`, `leave_entitlements` (kişi + tür + yıl), `leave_requests` (bitiş ≥ başlangıç ve gün > 0 kısıtı), `notifications.link`. Migration: `IsAkisiVeIzin`.

Motor genel amaçlıdır: yeni bir talep türü (masraf, avans, satın alma — Aşama 7) yalnızca bir `IWorkflowSubjectHandler` yazıp `IWorkflowEngine.StartAsync` çağırarak aynı onay kutusunu ve zinciri kullanır.

## 5. Testler

`WorkflowLeaveTests` (10 test):
- Zincir sırası: izin yöneticisi sırası gelmeden karar veremez.
- İK izin yöneticisi adımını onaylayamaz; bakiye güncellenir.
- Ret gerekçesi zorunludur; reddedilen talebin günleri bakiyeye geri döner.
- Talep sahibi rol adımında da kendi talebini onaylayamaz.
- Ardışık aynı onaylayıcı bir kez onaylar; müdür adımı doğru kişiye yönlenir.
- Bakiye, çakışma, hafta sonu ve eski tarih kuralları uygulanır; kişiye özel hak tanımlanabilir.
- İş günü hesabı tatilleri düşer; geri çekilen talebin akışı kapanır.
- Başkasının izni ve bakiyesi görülemez; zinciri yalnızca yetkili düzenler.

Smoke testinde "İzin ve onay zinciri" bölümü (12 kontrol) uçtan uca akışı gerçek API'de dener ve test verisini geri çekerek temizler.

## 6. Bilinen eksikler

- Dinî bayramlar (her yıl değişen tarihler) gün hesabına dahil değildir; resmî tatil tablosu yönetim ekranı ile eklenecek.
- Yarım gün izin yok.
- İzin onay e-postası yok, yalnızca uygulama içi bildirim var. E-posta tercihleriyle birlikte eklenecek.

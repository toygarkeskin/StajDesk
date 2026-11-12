# Final Sunumu Taslağı (Hafta 16, Gün 47–48)

PDF'in istediği yapı: 10–15 slayt · problem, mimari, her teknolojinin görevi ve "neden"i, demo senaryosu, öğrendiklerim.
Toplam 20 dakika sunum + soru-cevap. Aşağıdaki taslak sunumun iskeletidir; konuşma notları kendi cümlelerinle yazılmalıdır.

| # | Slayt | İçerik | Süre |
|---|---|---|---|
| 1 | Kapak | StajDesk — Destek Talebi Yönetim Sistemi · ad, mentor, tarih | 0:30 |
| 2 | Problem | Talepler e-posta/telefonla kayboluyor, kim ne yapıyor görünmüyor | 1:00 |
| 3 | Çözüm | Talep aç → takip et → çöz → kapat; rol bazlı ekranlar | 1:00 |
| 4 | Mimari | Tarayıcı → nginx → .NET API → PostgreSQL / Redis / RabbitMQ → Worker → Mailpit, Seq | 2:00 |
| 5 | Restoran benzetmesi | Her teknolojinin görevi (PDF s. 4 tablosu, kendi benzetmelerinle) | 2:00 |
| 6 | Veri modeli | ER diyagramı, index gerekçeleri, silme kuralları | 1:30 |
| 7 | Güvenlik | OAuth 2.0 + PKCE, rol/izin, hız sınırı, XSS, dosya yükleme kontrolleri | 2:00 |
| 8 | Asenkron mimari | Outbox → RabbitMQ → Worker; Worker kapalıyken mesaj kaybolmaz deneyi | 1:30 |
| 9 | Önbellek | Redis cache-aside, Stopwatch ölçümleri (LOG.md'deki gerçek rakamlar) | 1:00 |
| 10 | Canlı demo | Aşağıdaki senaryo | 5:00 |
| 11 | Kalite | 51 birim testi, 33 uçtan uca kontrol, Seq logları | 1:00 |
| 12 | Öğrendiklerim | En zorlandığım 3 konu ve nasıl çözdüğüm | 1:00 |
| 13 | Yeniden yapsaydım | Farklı yapacaklarım, sonraki öğrenme hedefim | 0:30 |

## Demo senaryosu (5 dk)

1. Ayşe (kullanıcı) olarak giriş → panel: duyuru, nöbetçi, sayaçlar.
2. Yeni talep: "Yazıcı kâğıt sıkıştırıyor" + ekran görüntüsü ekle.
3. Mailpit'te "Talebiniz alındı" e-postası; Elif'in ekranında anlık bildirim (yeşil nokta).
4. Elif (destek) olarak: "Üzerime al", dahili not, durum → Çözüldü.
5. Ayşe'nin panelinde "Onayımı bekleyenler" → Onayla ve kapat.
6. Selin (mağaza sahibi): yalnızca Kadıköy'ün talepleri görünüyor.
7. Sistem yöneticisi: Roller ve İzinler matrisinden bir izni kaldır → Selin'in menüsü anında değişir.
8. Yedek: Worker'ı durdur (`docker compose stop worker`), 3 talep aç, Worker'ı başlat → e-postalar gelir.

## Prova kontrol listesi

- [ ] `docker compose down -v && docker compose up -d --build` ile temiz kurulum çalışıyor
- [ ] `bash scripts/smoke-test.sh` yeşil
- [ ] Demo hesaplarının şifreleri elinde (docs/DEMO.md)
- [ ] Mailpit ve Seq sekmeleri açık
- [ ] Sunum bilgisayarında Docker Desktop çalışıyor, imajlar önceden indirildi

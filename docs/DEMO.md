# Demo Ortamı

`docker compose up -d --build` ile gelen ortam gerçek bir şirketi taklit eder. E-postalar gerçekten SMTP ile
gönderilir ama dışarı çıkmaz: hepsi **Mailpit** posta kutusuna düşer (http://localhost:8025).

## Hesaplar

| Rol | Ad | E-posta | Mağaza |
|---|---|---|---|
| Sistem Yöneticisi | Sistem Yöneticisi | `sysadmin@stajdesk.local` | — |
| Destek Personeli | Elif Yılmaz | `elif.destek@stajdesk.local` | — |
| Destek Personeli | Burak Demir | `burak.destek@stajdesk.local` | — |
| Mağaza Sahibi | Selin Aydın | `selin.magaza@stajdesk.local` | IST-01 Kadıköy |
| Mağaza Sahibi | Can Öztürk | `can.magaza@stajdesk.local` | IST-02 Beşiktaş |
| Kullanıcı | Ayşe Kaya | `ayse@stajdesk.local` | IST-01 Kadıköy |
| Kullanıcı | Mehmet Çelik | `mehmet@stajdesk.local` | IST-01 Kadıköy |
| Kullanıcı | Zeynep Arslan | `zeynep@stajdesk.local` | IST-02 Beşiktaş |
| Kullanıcı | Emre Şahin | `emre@stajdesk.local` | ANK-01 Kızılay |

| Şifre | Değer (varsayılan) | Ayar |
|---|---|---|
| Sistem Yöneticisi | `StajDesk!2026` | `.env` → `SYSADMIN_PASSWORD` |
| Diğer tüm demo hesapları | `Demo!2026pass` | `.env` → `DEMO_PASSWORD` |
| Raporlama istemcisi (`stajdesk-reporting`) | `Reporting!Dev2026` | `.env` → `REPORTING_CLIENT_SECRET` |

Seed yalnızca boş veritabanında demo verisi ekler. Sıfırdan başlamak için:

```bash
docker compose down -v
```

## 10 dakikalık deneme senaryosu

1. **Kullanıcı olarak talep aç.** `ayse@stajdesk.local` ile girin → Yeni talep → "Kasa yazıcısı fiş basmıyor", kategori *Kasa / POS*.
   Taleplerim listesinde yalnızca Ayşe'nin talepleri görünür.
2. **E-postaları gör.** http://localhost:8025 → destek ekibine "Yeni talep" e-postaları gelmiştir.
3. **Mağaza sahibi olarak bak.** Çıkış yapıp `selin.magaza@stajdesk.local` ile girin. Menüde *Mağaza talepleri*, *Envanter*, *Mağazam*, *Mağaza çalışanları* vardır.
   Kadıköy'ün tüm talepleri (Ayşe'ninki dahil) görünür; Beşiktaş'ınkiler görünmez. Özet paneli "Mağaza" kapsamındadır.
4. **Destek personeli olarak çöz.** `elif.destek@stajdesk.local` ile girin → talebi kendinize atayın (otomatik *İşlemde*),
   dahili not ekleyin (Ayşe görmez), sonra *Çözüldü* yapın. Ayşe'ye durum e-postası gider.
5. **Sistem yöneticisi olarak yönet.** `sysadmin@stajdesk.local` ile girin:
   - *Roller ve İzinler*: Mağaza Sahibi'nden `assets.read.store` iznini kaldırıp kaydedin. Selin bir sonraki istekte Envanter menüsünü kaybeder.
   - *Denetim kayıtları*: girişler, başarısız denemeler, izin değişikliği görünür.
   - *Gönderilen e-postalar*: Worker'ın gönderdiği tüm e-postalar.
6. **Şifremi unuttum.** Giriş ekranında *Şifremi unuttum* → `zeynep@stajdesk.local`. Bağlantı Mailpit'e gelir; yeni şifre belirleyip giriş yapın.
7. **Kaba kuvvet koruması.** Aynı e-postayla 5 kez yanlış şifre girin → 6. denemede "çok fazla deneme" uyarısı (429).
8. **Panel ve anlık bildirim.** İki ayrı tarayıcıda Ayşe ve Elif olarak girin. Ayşe talep açtığında Elif'in ekranında
   sayfayı yenilemeden bildirim belirir (üst çubuktaki nokta yeşilse bağlantı canlıdır).
9. **Dosya eki.** Talep detayında "Dosya ekle" ile ekran görüntüsü yükleyin; Selin aynı talepte dosyayı görür, Zeynep (başka mağaza) göremez.
10. **Duyuru ve nöbet.** Sistem yöneticisi olarak Duyurular sayfasından yalnızca Kadıköy'e duyuru yayınlayın; Selin görür, Can görmez.
11. **Loglar.** http://localhost:5380 (Seq) → `Application = 'StajDesk.Worker'` ile Worker loglarını filtreleyin.

## Otomatik kontrol

```bash
bash scripts/smoke-test.sh
```

Senaryonun büyük bölümünü (PKCE girişi, refresh rotasyonu ve yeniden kullanım tespiti, rol görünürlüğü, XSS reddi,
client_credentials, şifre sıfırlama e-postası) API üzerinden doğrular.

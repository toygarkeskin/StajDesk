# Geliştirme Günlüğü

Yapılan geliştirmeler, karşılaşılan hatalar ve çözümleri, tamamlanan aşamalar. (Stajyerin kişisel öğrenme günlüğü kök dizindeki `LOG.md`'dir.)

## 12.11.2026 — Panel, bonus görevler ve portal analizi

**Tamamlanan** (commit `b465df2`)
- Portal tarzı ana sayfa, duyurular, nöbet çizelgesi, dosya ekleri, SignalR anlık bildirim, tam metin arama.
- Postman koleksiyonu, PANEL.md, SUNUM.md.

**Hatalar ve çözümler**
| Hata | Neden | Çözüm |
|---|---|---|
| Dosya yükleme 500 | Depolama alt klasörü oluşturulmuyordu | `Directory.CreateDirectory` |
| Seq'te 400/401 hatalar "500" görünüyordu | İstek logu hata yakalayıcının içindeydi | `UseSerilogRequestLogging` öne alındı |
| Yeni sürüm yayınlanınca eski ekran | `index.html` tarayıcı önbelleğindeydi | nginx `expires -1` |
| Telefonda sayfa uzaklaştırılmış görünüyordu | Tablo içindeki `sr-only` etiketi kaydırma kutusundan taşıyordu | `.overflow-x-auto { position: relative }` |
| İki sekmede oturum düşüyordu | Eşzamanlı yenileme "token çalınması" sanılıyordu | 20 sn tolerans (ADR-013) |

**Testler:** 51 birim, 33 uçtan uca, 12 sayfa × 375 px taşma kontrolü.

**Aşama 1 (analiz) tamamlandı:** `docs/PORTAL_ANALIZ.md`. Kararlar: React, Form + İş Akışı motoru, açık sitede yalnızca kurumsal içerik, TCMB + Open-Meteo (altın anahtar bekliyor). Sıradaki: Aşama 3 — kimlik ve organizasyon.

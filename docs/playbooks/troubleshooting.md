# Sorun giderme

| Belirti | Neden / Çözüm |
|---|---|
| `postgres` başlamıyor, "port is already allocated" | Bilgisayarda başka PostgreSQL var. `.env` → `POSTGRES_PORT=5433`. |
| Seq arayüzü açılmıyor | Port çakışması. `.env` → `SEQ_UI_PORT` değiştir (varsayılan 5380). |
| Girişten sonra sayfa yenileyince çıkış yapılıyor | Refresh cookie gönderilmiyor. Arayüze API portundan değil `http://localhost:8080` (nginx) veya Vite (5173) üzerinden girin; `/connect` aynı origin'den gitmeli. |
| `invalid_grant: redirect_uri` | Kullanılan adres `OAuth:WebRedirectUris` listesinde yok. `.env` / compose'a ekleyin. |
| 429 "çok fazla deneme" | Hız sınırı. 5 dk bekleyin veya geliştirmede `docker compose exec redis redis-cli --scan --pattern 'stajdesk:rl:*'` ile anahtarları silin. |
| E-posta gelmiyor | Seq'te Worker logu; RabbitMQ panelinde kuyruk; `outbox_messages` tablosunda işlenmemiş satır. |
| Rol değişikliği yansımadı | İzin önbelleği 5 dk. Normalde değişiklikte silinir; elle: `perms:user:*` anahtarlarını silin. |
| Demo verisi gelmiyor | Seed yalnızca boş veritabanında çalışır. `docker compose down -v` ile sıfırlayın. |
| Git Bash'te Türkçe karakterli JSON bozuk gidiyor | Terminal kodlaması. `curl --data-binary @dosya.json` kullanın veya ASCII ile deneyin. |
| İmza anahtarı değişti, herkes çıkış yaptı | `apikeys` volume'u silinmiş. Beklenen davranış; yeniden giriş yeterli. |

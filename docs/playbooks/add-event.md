# Yeni olay ve e-posta eklemek

Örnek: "talep yeniden açıldı" olayı ve talep sahibine e-posta.

1. **Sözleşme** — `src/backend/StajDesk.Contracts/Events.cs`
   - `public sealed record TicketReopenedEvent(...)` ekle. Yalnızca Worker'ın ihtiyaç duyduğu alanları koy (ad, e-posta, başlık); şifre/token koyma.
   - `RoutingKeys` içine `TicketReopened = "ticket.reopened"` ekle. `ticket.*` ve `user.*` kalıpları kuyruğa zaten bağlı; başka bir önek kullanırsan `RabbitMq.cs` topolojisine bağlama ekle.
2. **Yayınlama** — ilgili handler'da, veriyi değiştirdiğin **aynı** iş biriminde:
   ```csharp
   events.Enqueue(new TicketReopenedEvent(...), RoutingKeys.TicketReopened);
   await uow.SaveChangesAsync(ct);
   ```
   RabbitMQ'ya doğrudan yazma; `OutboxDispatcher` taşır.
3. **Tüketim** — `src/backend/StajDesk.Worker/NotificationHandler.cs`
   - Routing key için yeni bir dal ekle. İdempotency kontrolü ortak koddadır; atlama.
   - Uygulama içi bildirim + `Mailer.SendAsync(...)`. Şablondaki tüm kullanıcı metinleri `HtmlEncode` edilir.
4. **Test** — handler testi: olayın outbox'a yazıldığını doğrula (`FakeEventPublisher.Published`).
5. **Uçtan uca** — işlemi arayüzden yap, http://localhost:8025 (Mailpit) ve *Gönderilen e-postalar* ekranında gör.

## Sorun giderme

| Belirti | Bak |
|---|---|
| E-posta gelmiyor | Seq'te `OutboxDispatcher` hatası? `outbox_messages.processed_at` boş mu? |
| Mesaj DLQ'da | RabbitMQ panelinde `stajdesk.notifications.dlq`; Worker logundaki seri hale getirme hatası |
| Aynı e-posta iki kez | `processed_messages` kontrolü atlanmış olabilir |

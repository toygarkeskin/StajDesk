-- StajDesk - Hafta 6
-- Konu: Iliskisel Veritabanlari ve SQL
-- PostgreSQL 16 / Docker

CREATE TABLE IF NOT EXISTS categories (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name VARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS users (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    first_name VARCHAR(50) NOT NULL,
    last_name VARCHAR(50) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS tickets (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    title VARCHAR(100) NOT NULL,
    description TEXT,
    status VARCHAR(20) NOT NULL DEFAULT 'Open'
        CHECK (status IN ('Open', 'InProgress', 'Closed')),
    category_id INTEGER NOT NULL REFERENCES categories(id),
    user_id INTEGER NOT NULL REFERENCES users(id),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS comments (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    content TEXT NOT NULL,
    ticket_id INTEGER NOT NULL REFERENCES tickets(id),
    user_id INTEGER NOT NULL REFERENCES users(id),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS notifications (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    message VARCHAR(200) NOT NULL,
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    user_id INTEGER NOT NULL REFERENCES users(id),
    ticket_id INTEGER REFERENCES tickets(id),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- INDEKSLER
CREATE INDEX IF NOT EXISTS idx_tickets_category_id
    ON tickets(category_id);

CREATE INDEX IF NOT EXISTS idx_tickets_user_id
    ON tickets(user_id);

CREATE INDEX IF NOT EXISTS idx_comments_ticket_id
    ON comments(ticket_id);

CREATE INDEX IF NOT EXISTS idx_notifications_user_id
    ON notifications(user_id);

-- TABLOLARI KONTROL ET
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;

-- KATEGORILER
INSERT INTO categories (name)
VALUES
    ('Yazilim'),
    ('Donanim'),
    ('Ag'),
    ('Guvenlik'),
    ('Diger')
ON CONFLICT (name) DO NOTHING;

-- KULLANICILAR
INSERT INTO users (first_name, last_name, email)
VALUES
    ('Toygar', 'Keskin', 'toygar@example.com'),
    ('Ayse', 'Demir', 'ayse@example.com'),
    ('Mehmet', 'Kaya', 'mehmet@example.com'),
    ('Zeynep', 'Yilmaz', 'zeynep@example.com'),
    ('Burak', 'Besli', 'burak@example.com')
ON CONFLICT (email) DO NOTHING;

-- KONTROL
SELECT * FROM categories;
SELECT id, first_name, last_name, email FROM users;

-- ORNEK TALEPLER
INSERT INTO tickets (title, description, status, category_id, user_id)
SELECT v.title, v.description, v.status, c.id, u.id
FROM (VALUES
    ('Bilgisayar acilmiyor', 'Bilgisayar hic acilmiyor.', 'Open', 'Donanim', 'toygar@example.com'),
    ('Uygulama hatasi', 'Uygulama hata veriyor.', 'InProgress', 'Yazilim', 'ayse@example.com'),
    ('Internet baglantisi yok', 'Ag baglantisi kurulamiyor.', 'Open', 'Ag', 'mehmet@example.com'),
    ('Yazilim guncellemesi', 'Uygulama guncellenmeli.', 'Closed', 'Yazilim', 'zeynep@example.com'),
    ('Klavye calismiyor', 'Klavye tuslari tepki vermiyor.', 'Open', 'Donanim', 'burak@example.com')
) AS v(title, description, status, category_name, user_email)
JOIN categories c ON c.name = v.category_name
JOIN users u ON u.email = v.user_email
WHERE NOT EXISTS (
    SELECT 1 FROM tickets t WHERE t.title = v.title
);

-- ORNEK YORUMLAR

INSERT INTO comments (content, ticket_id, user_id)
SELECT v.content, t.id, u.id
FROM (VALUES
    ('Talep inceleniyor.', 'Bilgisayar acilmiyor', 'ayse@example.com'),
    ('Surucu kontrol edilmeli.', 'Uygulama hatasi', 'toygar@example.com'),
    ('Modem yeniden baslatildi.', 'Internet baglantisi yok', 'mehmet@example.com'),
    ('Guncelleme tamamlandi.', 'Yazilim guncellemesi', 'zeynep@example.com'),
    ('Donanim kontrolu yapilacak.', 'Klavye calismiyor', 'toygar@example.com')
) AS v(content, ticket_title, user_email)
JOIN tickets t ON t.title = v.ticket_title
JOIN users u ON u.email = v.user_email
WHERE NOT EXISTS (
    SELECT 1
    FROM comments cm
    WHERE cm.content = v.content
      AND cm.ticket_id = t.id
);

-- ORNEK BILDIRIMLER
INSERT INTO notifications (message, is_read, user_id, ticket_id)
SELECT v.message, v.is_read, u.id, t.id
FROM (VALUES
    ('Talebiniz alindi.', FALSE, 'toygar@example.com', 'Bilgisayar acilmiyor'),
    ('Talebiniz inceleniyor.', FALSE, 'ayse@example.com', 'Uygulama hatasi'),
    ('Ag talebi guncellendi.', TRUE, 'mehmet@example.com', 'Internet baglantisi yok')
) AS v(message, is_read, user_email, ticket_title)
JOIN users u ON u.email = v.user_email
JOIN tickets t ON t.title = v.ticket_title
WHERE NOT EXISTS (
    SELECT 1
    FROM notifications n
    WHERE n.message = v.message
      AND n.user_id = u.id
      AND n.ticket_id = t.id
);

-- KONTROL
SELECT * FROM tickets;
SELECT * FROM comments;
SELECT * FROM notifications;

-- SORGU 1: Tum talepler
SELECT * FROM tickets;

-- SORGU 2: Acik talepler
SELECT * FROM tickets WHERE status = 'Open';

-- SORGU 3: Talepleri tarihe gore sirala
SELECT * FROM tickets ORDER BY created_at DESC;

-- SORGU 4: Toplam talep sayisi
SELECT COUNT(*) AS toplam_talep FROM tickets;

-- SORGU 5: Duruma gore talep sayisi
SELECT status, COUNT(*) AS adet
FROM tickets
GROUP BY status;

-- SORGU 6: Talepleri kullanici ve kategoriyle getir
SELECT t.title, u.first_name, c.name AS kategori
FROM tickets t
JOIN users u ON t.user_id = u.id
JOIN categories c ON t.category_id = c.id;

-- SORGU 7: Kategorilere gore talep sayisi
SELECT c.name, COUNT(t.id) AS talep_sayisi
FROM categories c
LEFT JOIN tickets t ON t.category_id = c.id
GROUP BY c.id, c.name;

-- SORGU 8: Birden fazla talebi olan kategoriler
SELECT c.name, COUNT(t.id) AS talep_sayisi
FROM categories c
JOIN tickets t ON t.category_id = c.id
GROUP BY c.id, c.name
HAVING COUNT(t.id) > 1;

-- SORGU 9: Okunmamis bildirimler
SELECT * FROM notifications WHERE is_read = FALSE;

-- SORGU 10: Kapanmis talepler
SELECT * FROM tickets WHERE status = 'Closed';
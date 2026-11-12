#!/usr/bin/env bash
# StajDesk uçtan uca duman testi (smoke test).
# Çalışan bir sisteme karşı OAuth 2.0 PKCE girişi, rol bazlı erişim, outbox → RabbitMQ → Worker → Mailpit zincirini doğrular.
# Kullanım:  bash scripts/smoke-test.sh            (varsayılan API: http://localhost:5080)
#            API=http://localhost:8080 bash scripts/smoke-test.sh   (nginx üzerinden)
set -euo pipefail

API="${API:-http://localhost:5080}"
MAILPIT="${MAILPIT:-http://localhost:8025}"
ADMIN_EMAIL="${SYSADMIN_EMAIL:-sysadmin@stajdesk.local}"
ADMIN_PASSWORD="${SYSADMIN_PASSWORD:-StajDesk!2026}"
DEMO_PASSWORD="${DEMO_PASSWORD:-Demo!2026pass}"
REDIRECT="${REDIRECT:-http://localhost:8080/auth/callback}"
PASS=0; FAIL=0

ok()   { echo "  ✓ $1"; PASS=$((PASS+1)); }
bad()  { echo "  ✗ $1"; FAIL=$((FAIL+1)); }
check() { if [ "$2" = "$3" ]; then ok "$1"; else bad "$1 (beklenen: $3, gelen: $2)"; fi; }
json()  { sed -n "s/.*\"$1\":\"\([^\"]*\)\".*/\1/p"; }

# RFC 7636 PKCE: verifier → S256 challenge
pkce_verifier() { head -c 48 /dev/urandom | base64 | tr '+/' '-_' | tr -d '=\n' | cut -c1-64; }
pkce_challenge() { printf '%s' "$1" | openssl dgst -sha256 -binary | base64 | tr '+/' '-_' | tr -d '=\n'; }

# Authorization Code + PKCE ile token al (stdout: access_token)
login() {
  local email="$1" password="$2" verifier challenge code
  verifier=$(pkce_verifier); challenge=$(pkce_challenge "$verifier")
  code=$(curl -s -X POST "$API/connect/authorize" -H 'Content-Type: application/json' \
    -d "{\"responseType\":\"code\",\"clientId\":\"stajdesk-web\",\"redirectUri\":\"$REDIRECT\",\"codeChallenge\":\"$challenge\",\"codeChallengeMethod\":\"S256\",\"state\":\"s1\",\"email\":\"$email\",\"password\":\"$password\"}" | json code)
  curl -s -X POST "$API/connect/token" -c /tmp/stajdesk-cookies.txt \
    --data-urlencode grant_type=authorization_code --data-urlencode client_id=stajdesk-web \
    --data-urlencode "code=$code" --data-urlencode "code_verifier=$verifier" --data-urlencode "redirect_uri=$REDIRECT" | json access_token
}

echo "== Sağlık ve keşif"
check "GET /api/health" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/health")" 200
check "OAuth meta verisi (RFC 8414)" "$(curl -s "$API/.well-known/oauth-authorization-server" | grep -c code_challenge_methods_supported)" 1
check "JWKS açık anahtar" "$(curl -s "$API/.well-known/jwks.json" | grep -c '"kty":"RSA"')" 1

echo "== OAuth 2.0 Authorization Code + PKCE"
ADMIN=$(login "$ADMIN_EMAIL" "$ADMIN_PASSWORD")
[ -n "$ADMIN" ] && ok "Sistem yöneticisi giriş yaptı (RS256 JWT)" || bad "Sistem yöneticisi giriş yapamadı"
check "Refresh token httpOnly cookie olarak verildi" "$(grep -c sd_rt /tmp/stajdesk-cookies.txt)" 1
REFRESHED=$(curl -s -X POST "$API/connect/token" -b /tmp/stajdesk-cookies.txt --data-urlencode grant_type=refresh_token --data-urlencode client_id=stajdesk-web | json access_token)
[ -n "$REFRESHED" ] && ok "Refresh token ile yeni erişim token'ı alındı (rotasyon)" || bad "Refresh başarısız"
check "Yanlış şifre → access_denied" "$(curl -s -X POST "$API/connect/authorize" -H 'Content-Type: application/json' -d "{\"responseType\":\"code\",\"clientId\":\"stajdesk-web\",\"redirectUri\":\"$REDIRECT\",\"codeChallenge\":\"$(pkce_challenge "$(pkce_verifier)")\",\"codeChallengeMethod\":\"S256\",\"email\":\"$ADMIN_EMAIL\",\"password\":\"yanlis\"}" | json error)" access_denied
check "Kayıtsız redirect_uri reddedilir" "$(curl -s -X POST "$API/connect/authorize" -H 'Content-Type: application/json' -d "{\"responseType\":\"code\",\"clientId\":\"stajdesk-web\",\"redirectUri\":\"http://evil.example/cb\",\"codeChallenge\":\"$(pkce_challenge "$(pkce_verifier)")\",\"codeChallengeMethod\":\"S256\",\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" | json error)" invalid_request

echo "== Client Credentials (servisten servise)"
SVC=$(curl -s -X POST "$API/connect/token" -u "stajdesk-reporting:${REPORTING_CLIENT_SECRET:-Reporting!Dev2026}" --data-urlencode grant_type=client_credentials | json access_token)
check "Servis token'ı ile talep listesi" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/tickets" -H "Authorization: Bearer $SVC")" 200
check "Servis token'ı ile kullanıcı yönetimi yasak" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/users" -H "Authorization: Bearer $SVC")" 403

echo "== Rol ve izin tabanlı erişim"
USER=$(login "ayse@stajdesk.local" "$DEMO_PASSWORD")
OWNER=$(login "selin.magaza@stajdesk.local" "$DEMO_PASSWORD")
AGENT=$(login "elif.destek@stajdesk.local" "$DEMO_PASSWORD")
total() { curl -s "$API/api/tickets?pageSize=100" -H "Authorization: Bearer $1" | sed -n 's/.*"totalCount":\([0-9]*\).*/\1/p'; }
T_ALL=$(total "$AGENT"); T_OWNER=$(total "$OWNER"); T_USER=$(total "$USER")
[ "$T_ALL" -gt "$T_OWNER" ] && [ "$T_OWNER" -gt "$T_USER" ] && ok "Görünürlük: destek=$T_ALL > mağaza sahibi=$T_OWNER > kullanıcı=$T_USER" || bad "Görünürlük sırası hatalı ($T_ALL/$T_OWNER/$T_USER)"
check "Kullanıcı → /api/users 403" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/users" -H "Authorization: Bearer $USER")" 403
check "Mağaza sahibi → /api/users 200 (kendi mağazası)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/users" -H "Authorization: Bearer $OWNER")" 200
check "Destek personeli → /api/roles 403" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/roles" -H "Authorization: Bearer $AGENT")" 403
check "Token yok → 401" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/tickets")" 401
check "Kullanıcının /me izinleri (tickets.manage yok)" "$(curl -s "$API/api/auth/me" -H "Authorization: Bearer $USER" | grep -c 'tickets.manage')" 0

echo "== XSS koruması"
check "HTML içeren başlık reddedilir (400)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/tickets" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' -d '{"title":"<script>alert(1)</script> test","description":"Açıklama metni yeterince uzun","categoryId":1}')" 400

echo "== Talep → outbox → RabbitMQ → Worker → bildirim + e-posta (Mailpit)"
ID=$(curl -s -X POST "$API/api/tickets" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' \
  -d '{"title":"Duman testi talebi","description":"Uctan uca test icin acildi.","categoryId":1,"priority":"High"}' | sed -n 's/.*"data":{"id":\([0-9]*\).*/\1/p')
[ -n "$ID" ] && ok "Talep oluşturuldu (#$ID)" || bad "Talep oluşturulamadı"
# Mailpit yalnızca son ~500 e-postayı saklar; toplam sayı yerine bu talebin numarasını içeren e-posta aranır.
FOUND=0
for _ in $(seq 1 20); do
  FOUND=$(curl -s "$MAILPIT/api/v1/search?query=subject:%22%23$ID%3A%22" | sed -n 's/.*"messages_count":\([0-9]*\).*/\1/p'); FOUND=${FOUND:-0}
  [ "$FOUND" -gt 0 ] && break; sleep 1
done
[ "$FOUND" -gt 0 ] && ok "E-posta Mailpit'e ulaştı (konu: #$ID)" || bad "E-posta gelmedi"
check "Talep sahibine bildirim oluştu" "$(curl -s "$API/api/notifications/unread-count" -H "Authorization: Bearer $USER" | grep -c '"data":[1-9]')" 1

echo "== Ana sayfa paneli, duyurular, nöbet"
check "GET /api/home (kullanıcı)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/home" -H "Authorization: Bearer $USER")" 200
check "Panelde nöbetçi personel var" "$(curl -s "$API/api/home" -H "Authorization: Bearer $USER" | grep -c '"onCall":\[{')" 1
check "Kullanıcı duyuru yayınlayamaz (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/announcements" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' -d '{"title":"Deneme","body":"Metin"}')" 403
ANN=$(curl -s -X POST "$API/api/announcements" -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d '{"title":"Duman testi duyurusu","body":"Otomatik test metni"}' | sed -n 's/.*"data":{"id":\([0-9]*\).*/\1/p')
[ -n "$ANN" ] && ok "Yönetici duyuru yayınlar (#$ANN)" || bad "Yönetici duyuru yayınlayamadı"
check "Test duyurusu silinir (204)" "$(curl -s -o /dev/null -w '%{http_code}' -X DELETE "$API/api/announcements/$ANN" -H "Authorization: Bearer $ADMIN")" 204

echo "== Talep ekleri (dosya yükleme)"
TMP=$(mktemp -d)
# Git Bash + Windows curl: /tmp yolu curl'a Windows yolu olarak verilmeli
FILES="$TMP"; command -v cygpath >/dev/null 2>&1 && FILES=$(cygpath -m "$TMP")
printf 'Hata kaydi: yazici baglantisi koptu\n' > "$TMP/hata.log"
printf 'MZ\x90\x00\x03' > "$TMP/resim.png"
check "Metin dosyası eklenir (201)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/tickets/$ID/attachments" -H "Authorization: Bearer $USER" -F "file=@$FILES/hata.log;type=text/plain")" 201
check "Uzantısı .png olan sahte dosya reddedilir (400)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/tickets/$ID/attachments" -H "Authorization: Bearer $USER" -F "file=@$FILES/resim.png;type=image/png")" 400
ATT=$(curl -s "$API/api/tickets/$ID" -H "Authorization: Bearer $USER" | sed -n 's/.*"attachments":\[{"id":\([0-9]*\).*/\1/p')
check "Ek indirilir (attachment olarak)" "$(curl -s -D - -o /dev/null "$API/api/tickets/$ID/attachments/$ATT" -H "Authorization: Bearer $USER" | grep -ci '^content-disposition: attachment')" 1
OTHER=$(login "zeynep@stajdesk.local" "$DEMO_PASSWORD")
check "Başka mağazadaki kullanıcı eki indiremez (403)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/tickets/$ID/attachments/$ATT" -H "Authorization: Bearer $OTHER")" 403
rm -rf "$TMP"

echo "== Tam metin arama ve anlık bildirim"
FOUND=$(curl -s "$API/api/tickets?search=yaz%C4%B1c%C4%B1lar" -H "Authorization: Bearer $AGENT" | sed -n 's/.*"totalCount":\([0-9]*\).*/\1/p')
[ "${FOUND:-0}" -ge 1 ] && ok "Türkçe kök ile tam metin arama ('yazıcılar' → $FOUND talep)" || bad "Tam metin arama sonuç bulamadı"
check "SignalR negotiate (token ile 200)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/hubs/notifications/negotiate?negotiateVersion=1" -H "Authorization: Bearer $USER")" 200
check "SignalR negotiate (token yok → 401)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/hubs/notifications/negotiate?negotiateVersion=1")" 401

echo "== Kimlik ve organizasyon"
BYNAME=$(login "elif" "$DEMO_PASSWORD")
[ -n "$BYNAME" ] && ok "Kullanıcı adıyla giriş (elif)" || bad "Kullanıcı adıyla giriş yapılamadı"
HR=$(login "deniz.ik@stajdesk.local" "$DEMO_PASSWORD")
check "Organizasyon şeması (herkes)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/org/chart" -H "Authorization: Bearer $USER")" 200
AYSE_ID=$(curl -s "$API/api/auth/me" -H "Authorization: Bearer $USER" | sed -n 's/.*"data":{"id":\([0-9]*\).*/\1/p')
check "Çalışma arkadaşı kişisel bilgileri göremez" "$(curl -s "$API/api/employees/$AYSE_ID" -H "Authorization: Bearer $OTHER" | grep -c '"private":null')" 1
check "İK kişisel bilgileri görür" "$(curl -s "$API/api/employees/$AYSE_ID" -H "Authorization: Bearer $HR" | grep -c '"private":{')" 1
check "Kullanıcı iş bilgisi değiştiremez (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/employees/$AYSE_ID/work" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' -d '{"fullName":"x","employmentType":"FullTime"}')" 403
check "İK rol/izin yönetemez (403)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/roles" -H "Authorization: Bearer $HR")" 403
check "SMS sağlayıcısı yok → iki adımlı doğrulama kapalı görünür" "$(curl -s "$API/api/me/security" -H "Authorization: Bearer $USER" | grep -c '"smsAvailable":false')" 1

echo "== Kurumsal hesap süreci (kapalı portal)"
check "Kendi kendine kayıt yok (404)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/auth/register" -H 'Content-Type: application/json' -d '{"fullName":"x","email":"x@x.com","password":"Guclu!Sifre123"}')" 404
check "İK doğrudan hesap açamaz (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/users" -H "Authorization: Bearer $HR" -H 'Content-Type: application/json' -d '{"fullName":"x","email":"x@stajdesk.local","roles":["user"]}')" 403
check "Çalışan hesap talebi açamaz (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/account-requests/new-account" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' -d '{}')" 403
STAMP=$(date +%s)
REQ=$(curl -s -X POST "$API/api/account-requests/new-account" -H "Authorization: Bearer $HR" -H 'Content-Type: application/json' \
  -d "{\"fullName\":\"Duman Test\",\"email\":\"duman.$STAMP@stajdesk.local\",\"employeeNumber\":\"T$STAMP\",\"departmentId\":1,\"positionId\":1,\"employmentType\":\"Intern\",\"hireDate\":\"2026-12-01\"}" | sed -n 's/.*"data":{"id":\([0-9]*\).*/\1/p')
[ -n "$REQ" ] && ok "İK yeni çalışan hesabı talebi açar (#$REQ)" || bad "İK hesap talebi açamadı"
check "İK talebi onaylayamaz (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/account-requests/$REQ/approve" -H "Authorization: Bearer $HR" -H 'Content-Type: application/json' -d '{}')" 403
check "İK toplu onay yapamaz (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/account-requests/bulk-approve" -H "Authorization: Bearer $HR" -H 'Content-Type: application/json' -d "{\"ids\":[$REQ]}")" 403
check "Çalışan bekleyen talep listesini alamaz (403)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/account-requests/pending-refs" -H "Authorization: Bearer $USER")" 403
check "Boş toplu onay reddedilir (400)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/account-requests/bulk-approve" -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d '{"ids":[]}')" 400
# CSV ile toplu talep: yalnızca önizleme (dryRun) denenir, veri değişmez
csv_body() { printf 'Ad Soyad;E-posta;Personel Kodu;Departman;Unvan;İşe Başlama\r\nCsv Test;csv.%s@stajdesk.local;C%s;MG;Uzman;01.12.2026\r\nHatalı;gecersiz;;MG;Uzman;01.12.2026\r\n' "$STAMP" "$STAMP"; }
check "CSV şablonu indirilir" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/account-requests/import/template" -H "Authorization: Bearer $HR")" 200
check "CSV önizlemesi hatalı satırı ayırır" "$(csv_body | curl -s -X POST "$API/api/account-requests/import?dryRun=true" -H "Authorization: Bearer $HR" -F "file=@-;filename=personel.csv;type=text/csv" | grep -o '"validCount":1' | head -1)" '"validCount":1'
check "Çalışan CSV ile talep açamaz (403)" "$(csv_body | curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/account-requests/import?dryRun=true" -H "Authorization: Bearer $USER" -F "file=@-;filename=personel.csv;type=text/csv")" 403
IT=$(login "kaan" "$DEMO_PASSWORD")
check "BT sistem yöneticisi rolü veremez (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/account-requests/$REQ/approve" -H "Authorization: Bearer $IT" -H 'Content-Type: application/json' -d '{"roles":["system_admin"]}')" 403
NEW_ID=$(curl -s -X POST "$API/api/account-requests/$REQ/approve" -H "Authorization: Bearer $IT" -H 'Content-Type: application/json' -d "{\"username\":\"duman$STAMP\",\"roles\":[\"user\"]}" | sed -n 's/.*"createdUserId":\([0-9]*\).*/\1/p')
[ -n "$NEW_ID" ] && ok "BT onaylar → hesap açıldı (#$NEW_ID)" || bad "BT onayı hesap açmadı"
# Mailpit yalnızca son ~500 e-postayı saklar; sayı yerine bu hesabın adresine giden e-posta aranır.
INV=0
for _ in $(seq 1 15); do
  INV=$(curl -s "$MAILPIT/api/v1/search?query=to:duman.$STAMP@stajdesk.local" | sed -n 's/.*"messages_count":\([0-9]*\).*/\1/p'); INV=${INV:-0}
  [ "$INV" -gt 0 ] && break; sleep 1
done
[ "$INV" -gt 0 ] && ok "İlk şifre davet e-postası Mailpit'te" || bad "Davet e-postası gelmedi"
check "BT hesabı kilitler" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/users/$NEW_ID/lock" -H "Authorization: Bearer $IT" -H 'Content-Type: application/json' -d '{"reason":"Duman testi"}')" 200
# Test hesabını kapat (rehberde kalmasın)
check "BT test hesabını pasife alır" "$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/users/$NEW_ID" -H "Authorization: Bearer $IT" -H 'Content-Type: application/json' -d '{"fullName":"Duman Test","roles":["user"],"isActive":false}')" 200

echo "== Kurumsal site (herkese açık) ve içerik yönetimi"
check "Kurumsal ana sayfa verisi (oturumsuz)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/public/landing")" 200
check "Herkese açık sitede kişisel veri yok (e-posta alanı dönmez)" "$(curl -s "$API/api/public/landing" | grep -c '"email"')" 0
check "Taslak haber herkese açık değil (404)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/public/news/taslak-haber")" 404
check "Hava durumu kaynaklı döner" "$(curl -s "$API/api/public/weather" | grep -oE '"status":"(live|stale|unavailable)"' | head -1 | grep -c status)" 1
check "Döviz kuru kaynaklı döner" "$(curl -s "$API/api/public/rates" | grep -oE '"status":"(live|stale|unavailable)"' | head -1 | grep -c status)" 1
check "Gram altın sahte veri üretmez (yapılandırılmadı)" "$(curl -s "$API/api/public/gold" | grep -c '"status":"not_configured"')" 1
check "Reklam tıklaması güvenli yönlendirme (302)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/public/ads/1/click")" 302
check "İçerik yönetimi oturumsuz kapalı (401)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/content/ads")" 401
check "Çalışan reklam yönetemez (403)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/content/ads" -H "Authorization: Bearer $USER")" 403
check "İK haber yönetemez (403)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/content/news" -H "Authorization: Bearer $HR")" 403
check "Yönetici reklam listesini görür" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/content/ads" -H "Authorization: Bearer $ADMIN")" 200
check "Reklamda javascript: bağlantısı reddedilir (400)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/content/ads" -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d '{"title":"x","imageKey":"00000000000000000000000000000000","linkUrl":"javascript:alert(1)","page":"Landing","placement":"Hero","sortOrder":1,"startsAt":"2026-01-01T00:00:00Z","isActive":true}')" 400

echo "== Çalışan portalı: kutucuklar, genel arama, portal akışları"
check "Panel düzeni (kullanıcı)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/dashboard/layout" -H "Authorization: Bearer $USER")" 200
check "Çalışan izin gerektiren kutucuğu görmez (kişi arama)" "$(curl -s "$API/api/dashboard/layout" -H "Authorization: Bearer $USER" | grep -c '"key":"people-search"')" 0
check "Destek personeli kişi arama kutucuğunu görür" "$(curl -s "$API/api/dashboard/layout" -H "Authorization: Bearer $AGENT" | grep -c '"key":"people-search"')" 1
check "Kişisel düzen kaydedilir" "$(curl -s -o /dev/null -w '%{http_code}' -X PUT "$API/api/dashboard/preferences" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' -d '{"order":["rates","welcome"],"hidden":["gold"]}')" 200
check "Kişisel düzen varsayılana döner" "$(curl -s -o /dev/null -w '%{http_code}' -X DELETE "$API/api/dashboard/preferences" -H "Authorization: Bearer $USER")" 200
check "Çalışan kutucuk yönetemez (403)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/admin/widgets" -H "Authorization: Bearer $USER")" 403
check "Yönetici kutucuk listesini görür" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/admin/widgets" -H "Authorization: Bearer $ADMIN")" 200
check "Genel arama oturumsuz kapalı (401)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/search?q=ay")" 401
check "Genel arama kişi bulur" "$(curl -s "$API/api/search?q=elif" -H "Authorization: Bearer $USER" | grep -c '"people":\[{')" 1
for p in birthdays new-hires events recognitions; do
  check "Portal akışı: $p" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/portal/$p" -H "Authorization: Bearer $USER")" 200
done
check "Çalışan etkinlik yönetemez (403)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/content/portal/events" -H "Authorization: Bearer $USER")" 403
check "İK takdirleri yönetebilir" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/content/portal/recognitions" -H "Authorization: Bearer $HR")" 200

echo "== İzin ve onay zinciri (yönetici → izin yöneticisi)"
UNPAID=$(curl -s "$API/api/leave/types" -H "Authorization: Bearer $USER" | grep -o '"id":[0-9]*,"code":"UCRETSIZ"' | grep -o '[0-9]\+' | head -1)
OFFSET=$(( 40 + STAMP % 500 )); LDAY=$(date -d "+$OFFSET days" +%F)
while [ "$(date -d "$LDAY" +%u)" -ge 6 ]; do LDAY=$(date -d "$LDAY +1 day" +%F); done
LEAVE=$(curl -s -X POST "$API/api/leave/requests" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' \
  -d "{\"leaveTypeId\":$UNPAID,\"startDate\":\"$LDAY\",\"endDate\":\"$LDAY\",\"reason\":\"Smoke testi\"}" | sed -n 's/.*"data":{"id":\([0-9]*\).*/\1/p')
[ -n "$LEAVE" ] && ok "Çalışan izin talebi açar (#$LEAVE, $LDAY)" || bad "İzin talebi açılamadı"
# ayse → yöneticisi selin.magaza (demo organizasyonu)
MANAGER=$(login "selin.magaza@stajdesk.local" "$DEMO_PASSWORD")
STEP1=$(curl -s "$API/api/leave/requests/$LEAVE/workflow" -H "Authorization: Bearer $MANAGER" | sed -n 's/.*"myApprovalId":\([0-9]*\).*/\1/p')
[ -n "$STEP1" ] && ok "Talep yöneticinin onay kutusunda" || bad "Yönetici onay adımı bulunamadı"
check "İK yönetici adımını onaylayamaz (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/approvals/$STEP1/approve" -H "Authorization: Bearer $HR" -H 'Content-Type: application/json' -d '{}')" 403
check "Talep sahibi kendi talebini onaylayamaz (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/approvals/$STEP1/approve" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' -d '{}')" 403
check "Yönetici onaylar" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/approvals/$STEP1/approve" -H "Authorization: Bearer $MANAGER" -H 'Content-Type: application/json' -d '{"comment":"Uygun"}')" 200
STEP2=$(curl -s "$API/api/leave/requests/$LEAVE/workflow" -H "Authorization: Bearer $HR" | sed -n 's/.*"myApprovalId":\([0-9]*\).*/\1/p')
[ -n "$STEP2" ] && ok "İzin yöneticisi adımı sırada (İK'da ayrıca İzin Yöneticisi rolü var)" || bad "İzin yöneticisi adımı bulunamadı"
check "İzin yöneticisi onaylar" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/approvals/$STEP2/approve" -H "Authorization: Bearer $HR" -H 'Content-Type: application/json' -d '{}')" 200
check "İzin onaylandı" "$(curl -s "$API/api/leave/requests/$LEAVE" -H "Authorization: Bearer $USER" | grep -c '"status":"Approved"')" 1
check "Başka çalışan izni göremez (404)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/leave/requests/$LEAVE" -H "Authorization: Bearer $OTHER")" 404
check "Çalışan izin türü ekleyemez (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/leave/types" -H "Authorization: Bearer $OTHER" -H 'Content-Type: application/json' -d '{"code":"X1","name":"x","tracksBalance":false,"defaultDaysPerYear":0,"isPaid":true,"isActive":true,"sortOrder":1}')" 403
check "Çalışan onay zinciri yönetemez (403)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/workflows" -H "Authorization: Bearer $USER")" 403
check "Onaylı gelecek izin geri çekilir (test verisi temizlenir)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/leave/requests/$LEAVE/cancel" -H "Authorization: Bearer $USER")" 200

echo "== Görevler, hedefli duyurular, talep durumları (Yeni / Reddedildi)"
TASK=$(curl -s -X POST "$API/api/tasks" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' -d '{"title":"Smoke gorevi","priority":"Normal"}' | sed -n 's/.*"data":{"id":\([0-9]*\).*/\1/p')
[ -n "$TASK" ] && ok "Çalışan kendine görev açar (#$TASK)" || bad "Görev açılamadı"
check "Çalışan başkasına (ekibi dışına) görev atayamaz (403)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/tasks" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' -d '{"title":"x","priority":"Normal","assigneeId":1}')" 403
check "Başkasının görevi görünmez (404)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/tasks/$TASK" -H "Authorization: Bearer $OTHER")" 404
check "Görev tamamlanır" "$(curl -s -o /dev/null -w '%{http_code}' -X PATCH "$API/api/tasks/$TASK/status" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' -d '{"status":"Done"}')" 200
check "Görev silinir (test verisi temizlenir)" "$(curl -s -o /dev/null -w '%{http_code}' -X DELETE "$API/api/tasks/$TASK" -H "Authorization: Bearer $USER")" 204
NEWT=$(curl -s -X POST "$API/api/tickets" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' -d '{"title":"Smoke ret testi","description":"Reddedilecek test talebi.","categoryId":1}' | sed -n 's/.*"data":{"id":\([0-9]*\).*/\1/p')
check "Yeni talep 'Yeni' durumunda başlar" "$(curl -s "$API/api/tickets/$NEWT" -H "Authorization: Bearer $USER" | grep -c '"status":"New"')" 1
check "Gerekçesiz ret reddedilir (400)" "$(curl -s -o /dev/null -w '%{http_code}' -X PATCH "$API/api/tickets/$NEWT/status" -H "Authorization: Bearer $AGENT" -H 'Content-Type: application/json' -d '{"status":"Rejected"}')" 400
check "Destek personeli gerekçeyle reddeder" "$(curl -s -o /dev/null -w '%{http_code}' -X PATCH "$API/api/tickets/$NEWT/status" -H "Authorization: Bearer $AGENT" -H 'Content-Type: application/json' -d '{"status":"Rejected","reason":"Smoke: kapsam disi"}')" 200
check "Ret gerekçesi talep sahibine görünür" "$(curl -s "$API/api/tickets/$NEWT" -H "Authorization: Bearer $USER" | grep -c 'Smoke: kapsam disi')" 1
DEPT=$(curl -s "$API/api/org/departments" -H "Authorization: Bearer $ADMIN" | grep -o '"id":[0-9]*,"code":"BT"' | grep -o '[0-9]\+' | head -1)
ANN=$(curl -s -X POST "$API/api/announcements" -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d "{\"title\":\"Smoke hedefli duyuru\",\"body\":\"Yalnizca BT\",\"targetDepartmentId\":$DEPT}" | sed -n 's/.*"data":{"id":\([0-9]*\).*/\1/p')
[ -n "$ANN" ] && ok "Departmana hedefli duyuru yayınlanır (#$ANN)" || bad "Hedefli duyuru yayınlanamadı"
check "Hedef dışındaki çalışan duyuruyu okuyamaz (404)" "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/announcements/$ANN/read" -H "Authorization: Bearer $USER")" 404
check "Çalışan okuyanlar listesini göremez (403)" "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/announcements/$ANN/readers" -H "Authorization: Bearer $USER")" 403
check "Yönetici okuyanları görür" "$(curl -s "$API/api/announcements/$ANN/readers" -H "Authorization: Bearer $ADMIN" | grep -c '"audienceCount"')" 1
check "Hedefli duyuru kurumsal sitede yok" "$(curl -s "$API/api/public/announcements" | grep -c 'Smoke hedefli duyuru')" 0
check "Test duyurusu silinir" "$(curl -s -o /dev/null -w '%{http_code}' -X DELETE "$API/api/announcements/$ANN" -H "Authorization: Bearer $ADMIN")" 204

echo "== Şifre sıfırlama e-postası"
curl -s -X POST "$API/api/auth/forgot-password" -H 'Content-Type: application/json' -d '{"email":"mehmet@stajdesk.local"}' >/dev/null
sleep 4
check "Sıfırlama e-postası Mailpit'te" "$(curl -s "$MAILPIT/api/v1/search?query=subject:%22%C5%9Eifre%20s%C4%B1f%C4%B1rlama%22" | grep -c 'mehmet@stajdesk.local')" 1

echo
echo "Sonuç: $PASS başarılı, $FAIL başarısız"
[ "$FAIL" -eq 0 ]

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
BEFORE=$(curl -s "$MAILPIT/api/v1/messages" | sed -n 's/.*"total":\([0-9]*\).*/\1/p'); BEFORE=${BEFORE:-0}
ID=$(curl -s -X POST "$API/api/tickets" -H "Authorization: Bearer $USER" -H 'Content-Type: application/json' \
  -d '{"title":"Duman testi talebi","description":"Uctan uca test icin acildi.","categoryId":1,"priority":"High"}' | sed -n 's/.*"data":{"id":\([0-9]*\).*/\1/p')
[ -n "$ID" ] && ok "Talep oluşturuldu (#$ID)" || bad "Talep oluşturulamadı"
for _ in $(seq 1 20); do
  AFTER=$(curl -s "$MAILPIT/api/v1/messages" | sed -n 's/.*"total":\([0-9]*\).*/\1/p'); AFTER=${AFTER:-0}
  [ "$AFTER" -gt "$BEFORE" ] && break; sleep 1
done
[ "$AFTER" -gt "$BEFORE" ] && ok "E-posta Mailpit'e ulaştı ($BEFORE → $AFTER)" || bad "E-posta gelmedi"
check "Talep sahibine bildirim oluştu" "$(curl -s "$API/api/notifications/unread-count" -H "Authorization: Bearer $USER" | grep -c '"data":[1-9]')" 1

echo "== Şifre sıfırlama e-postası"
curl -s -X POST "$API/api/auth/forgot-password" -H 'Content-Type: application/json' -d '{"email":"mehmet@stajdesk.local"}' >/dev/null
sleep 4
check "Sıfırlama e-postası Mailpit'te" "$(curl -s "$MAILPIT/api/v1/search?query=subject:%22%C5%9Eifre%20s%C4%B1f%C4%B1rlama%22" | grep -c 'mehmet@stajdesk.local')" 1

echo
echo "Sonuç: $PASS başarılı, $FAIL başarısız"
[ "$FAIL" -eq 0 ]

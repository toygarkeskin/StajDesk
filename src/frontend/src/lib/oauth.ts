import axios from 'axios'

/**
 * OAuth 2.0 Authorization Code + PKCE (RFC 7636) istemcisi.
 *  1) Rastgele code_verifier üret → SHA-256 → code_challenge
 *  2) POST /connect/authorize (kimlik bilgileri + challenge) → tek kullanımlık kod
 *  3) POST /connect/token (kod + verifier) → erişim token'ı; refresh token httpOnly cookie olarak gelir
 * Refresh token'a JavaScript erişemez; yenileme isteği tarayıcının cookie'yi otomatik göndermesiyle yapılır.
 */
export const CLIENT_ID = 'stajdesk-web'
export const redirectUri = () => `${window.location.origin}/auth/callback`

const oauthHttp = axios.create({ withCredentials: true })

const base64Url = (bytes: ArrayBuffer | Uint8Array) =>
  btoa(String.fromCharCode(...new Uint8Array(bytes))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')

export function createVerifier() {
  return base64Url(crypto.getRandomValues(new Uint8Array(48)))
}

export async function challengeOf(verifier: string) {
  return base64Url(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier)))
}

export interface TokenResponse {
  access_token: string
  token_type: string
  expires_in: number
  scope: string
}

/** Kimlik bilgileriyle yetkilendirme kodu al (giriş formu). */
export async function authorize(input: { email: string; password: string; clientId?: string; redirectUri?: string; codeChallenge: string; state?: string; scope?: string }) {
  const { data } = await oauthHttp.post<{ code: string; state?: string; redirectUri: string }>('/connect/authorize', {
    responseType: 'code',
    clientId: input.clientId ?? CLIENT_ID,
    redirectUri: input.redirectUri ?? redirectUri(),
    codeChallenge: input.codeChallenge,
    codeChallengeMethod: 'S256',
    scope: input.scope,
    state: input.state,
    email: input.email,
    password: input.password,
  })
  return data
}

const form = (values: Record<string, string>) => new URLSearchParams(values)

export async function exchangeCode(code: string, verifier: string) {
  const { data } = await oauthHttp.post<TokenResponse>(
    '/connect/token',
    form({ grant_type: 'authorization_code', client_id: CLIENT_ID, code, code_verifier: verifier, redirect_uri: redirectUri() }),
  )
  return data
}

/** httpOnly cookie'deki refresh token ile yeni erişim token'ı (rotasyon: eski token geçersiz olur). */
export async function refresh() {
  const { data } = await oauthHttp.post<TokenResponse>('/connect/token', form({ grant_type: 'refresh_token', client_id: CLIENT_ID }))
  return data
}

export async function revoke() {
  await oauthHttp.post('/connect/revoke', form({ client_id: CLIENT_ID })).catch(() => {})
}

/** Tek adımda giriş: PKCE ile kod al ve token'a çevir. */
export async function loginWithPassword(email: string, password: string) {
  const verifier = createVerifier()
  const { code } = await authorize({ email, password, codeChallenge: await challengeOf(verifier) })
  return exchangeCode(code, verifier)
}

import { useEffect, useState } from 'react'
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useAuth } from '@/stores/auth'
import { toast } from '@/stores/ui'
import { refreshAccessToken } from './api'

export type RealtimeStatus = 'connecting' | 'live' | 'offline'

/**
 * Anlık bildirimler (SignalR). Worker bir bildirim oluşturduğunda sunucu bu kullanıcıya sinyal gönderir;
 * arayüz ilgili listeleri yeniden çeker. Bağlantı koparsa otomatik yeniden bağlanır;
 * o sırada bildirimler 15 sn'lik yenilemeyle gelmeye devam eder (yedek yol).
 */
export function useRealtime(): RealtimeStatus {
  const qc = useQueryClient()
  const userId = useAuth((s) => s.me?.id)
  const [status, setStatus] = useState<RealtimeStatus>('connecting')

  useEffect(() => {
    if (!userId) return
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/notifications', {
        // WebSocket başlık taşıyamadığı için token sorgu parametresiyle gider; süresi dolmuşsa önce yenilenir.
        accessTokenFactory: async () => useAuth.getState().accessToken ?? (await refreshAccessToken()) ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('notification', (msg: { ticketId?: number | null; title?: string | null }) => {
      qc.invalidateQueries({ queryKey: ['notifications'] })
      qc.invalidateQueries({ queryKey: ['home'] })
      qc.invalidateQueries({ queryKey: ['tickets'] })
      qc.invalidateQueries({ queryKey: ['stats'] })
      if (msg.ticketId) qc.invalidateQueries({ queryKey: ['ticket', msg.ticketId] })
      if (msg.title) toast(msg.title)
    })
    // Birebir mesaj: konuşma listesi ve okunmamış sayısı yenilenir; mesajlar sayfasında değilse kısa bildirim gösterilir.
    connection.on('message', (msg: { title?: string | null }) => {
      qc.invalidateQueries({ queryKey: ['messages'] })
      if (!location.pathname.startsWith('/messages')) toast(`${msg.title ?? 'Bir çalışma arkadaşınız'} size mesaj gönderdi.`)
    })
    connection.on('message-read', () => qc.invalidateQueries({ queryKey: ['messages'] }))
    connection.onreconnecting(() => setStatus('connecting'))
    connection.onreconnected(() => setStatus('live'))
    connection.onclose(() => setStatus('offline'))

    let stopped = false
    connection.start()
      .then(() => !stopped && setStatus('live'))
      .catch(() => !stopped && setStatus('offline'))

    return () => {
      stopped = true
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
    }
  }, [userId, qc])

  return status
}

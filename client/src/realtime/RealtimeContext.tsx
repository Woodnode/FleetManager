import { useEffect, useState, type ReactNode } from 'react'
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { refresh } from '../api/auth'
import { ALL_KEYS, RETRY_DELAY_MS, RealtimeContext, applyChange, hubUrl, type RealtimeStatus } from './realtime'

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const qc = useQueryClient()
  const [status, setStatus] = useState<RealtimeStatus>('connecting')

  useEffect(() => {
    let disposed = false
    let retryTimer: ReturnType<typeof setTimeout> | undefined
    // Cookies httpOnly envoyés à la négociation et à l'ouverture du WebSocket (même mécanisme que l'API).
    const connection: HubConnection = new HubConnectionBuilder()
      .withUrl(hubUrl(), { withCredentials: true })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(LogLevel.None)
      .build()

    connection.on('changed', (payload: { entities?: string[] }) => applyChange(qc, payload?.entities ?? []))

    connection.onreconnecting(() => { if (!disposed) setStatus('reconnecting') })
    connection.onreconnected(() => {
      if (disposed) return
      setStatus('connected')
      // Des signaux ont pu être manqués pendant la coupure : on recharge tout.
      ALL_KEYS.forEach(key => qc.invalidateQueries({ queryKey: [key] }))
    })

    // Lu via une fonction : l'état change pendant les await, ce que TypeScript ne voit pas.
    const isConnected = () => connection.state === HubConnectionState.Connected

    const start = async () => {
      if (disposed) return
      try {
        await connection.start()
        if (disposed) { await connection.stop(); return }
        setStatus('connected')
      } catch {
        if (disposed) return
        setStatus('offline')
        retryTimer = setTimeout(reconnectWithFreshSession, RETRY_DELAY_MS)
      }
    }

    // Le serveur ferme la connexion à l'expiration du jeton (CloseOnAuthenticationExpiration) :
    // on rafraîchit la session avant de rouvrir, comme le fait l'intercepteur HTTP.
    const reconnectWithFreshSession = async () => {
      if (disposed || connection.state !== HubConnectionState.Disconnected) return
      setStatus('reconnecting')
      try { await refresh() } catch { /* session expirée : la prochaine requête HTTP déconnectera */ }
      await start()
      if (!disposed && isConnected())
        ALL_KEYS.forEach(key => qc.invalidateQueries({ queryKey: [key] }))
    }

    connection.onclose(() => {
      if (disposed) return
      setStatus('offline')
      retryTimer = setTimeout(reconnectWithFreshSession, 1_000)
    })

    start()

    return () => {
      disposed = true
      clearTimeout(retryTimer)
      connection.stop().catch(() => { /* arrêt pendant la négociation : sans conséquence */ })
    }
  }, [qc])

  return <RealtimeContext.Provider value={status}>{children}</RealtimeContext.Provider>
}

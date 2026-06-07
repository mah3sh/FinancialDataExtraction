import { useState, useEffect, useRef } from 'react'
import { api, DocumentSummary } from '../api/documentsApi'

const POLL_INTERVAL_MS = 3000

export function useDocumentPoller(documentId: string | null) {
  const [status, setStatus] = useState<DocumentSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  useEffect(() => {
    if (!documentId) return

    const poll = async () => {
      try {
        const doc = await api.getStatus(documentId)
        setStatus(doc)
        if (doc.status === 'Completed' || doc.status === 'Failed') {
          clearInterval(intervalRef.current!)
        }
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Polling failed')
        clearInterval(intervalRef.current!)
      }
    }

    poll()
    intervalRef.current = setInterval(poll, POLL_INTERVAL_MS)

    return () => { if (intervalRef.current) clearInterval(intervalRef.current) }
  }, [documentId])

  return { status, error }
}

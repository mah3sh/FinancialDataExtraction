import { useState, useEffect } from 'react'
import { api, DocumentSummary } from '../api/documentsApi'
import { useDocumentPoller } from '../hooks/useDocumentPoller'
import { ExtractionResults } from './ExtractionResults'

const STATUS_COLORS: Record<string, string> = {
  Pending: '#f59e0b', Processing: '#3b82f6', Completed: '#22c55e', Failed: '#ef4444'
}

function DocumentRow({ doc: initial }: { doc: DocumentSummary }) {
  const [expanded, setExpanded] = useState(false)
  const needsPolling = initial.status === 'Pending' || initial.status === 'Processing'
  const { status: polled } = useDocumentPoller(needsPolling ? initial.id : null)
  const doc = polled ?? initial

  return (
    <div style={styles.row}>
      <div style={styles.rowHeader} onClick={() => setExpanded(e => !e)}>
        <div>
          <span style={styles.fileName}>{doc.fileName}</span>
          <span style={{ ...styles.badge, background: STATUS_COLORS[doc.status] + '22', color: STATUS_COLORS[doc.status] }}>
            {doc.status}
          </span>
        </div>
        <div style={styles.meta}>
          {(doc.fileSizeBytes / 1024).toFixed(1)} KB · {new Date(doc.uploadedAt).toLocaleString()}
          <span style={styles.chevron}>{expanded ? '▲' : '▼'}</span>
        </div>
      </div>
      {expanded && <ExtractionResults documentId={doc.id} status={doc.status} />}
    </div>
  )
}

export function DocumentList({ refresh }: { refresh: number }) {
  const [docs, setDocs] = useState<DocumentSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setLoading(true)
    api.list().then(setDocs).catch(e => setError(e.message)).finally(() => setLoading(false))
  }, [refresh])

  if (loading) return <p style={{ color: '#94a3b8' }}>Loading documents…</p>
  if (error) return <p style={{ color: '#ef4444' }}>{error}</p>
  if (docs.length === 0) return <p style={{ color: '#94a3b8' }}>No documents yet. Upload one above.</p>

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {docs.map(doc => <DocumentRow key={doc.id} doc={doc} />)}
    </div>
  )
}

const styles: Record<string, React.CSSProperties> = {
  row: { border: '1px solid #e2e8f0', borderRadius: 10, overflow: 'hidden' },
  rowHeader: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '12px 16px', cursor: 'pointer', background: 'white' },
  fileName: { fontWeight: 600, color: '#1e293b', marginRight: 8 },
  badge: { fontSize: 11, padding: '2px 8px', borderRadius: 99, fontWeight: 600 },
  meta: { color: '#94a3b8', fontSize: 13, display: 'flex', gap: 8, alignItems: 'center' },
  chevron: { marginLeft: 4, fontSize: 10 }
}

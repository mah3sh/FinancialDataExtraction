import { useState, useEffect } from 'react'
import { api, DocumentResult } from '../api/documentsApi'

interface Props {
  documentId: string
  status: string
}

function JsonTree({ data, depth = 0 }: { data: unknown; depth?: number }) {
  if (data === null || data === undefined) return <span style={{ color: '#94a3b8' }}>null</span>
  if (typeof data === 'boolean') return <span style={{ color: '#7c3aed' }}>{String(data)}</span>
  if (typeof data === 'number') return <span style={{ color: '#0369a1' }}>{data}</span>
  if (typeof data === 'string') return <span style={{ color: '#15803d' }}>"{data}"</span>

  if (Array.isArray(data)) {
    if (data.length === 0) return <span>[]</span>
    return (
      <details open={depth < 2} style={{ marginLeft: depth * 16 }}>
        <summary style={styles.summary}>Array [{data.length}]</summary>
        {data.map((item, i) => (
          <div key={i} style={styles.row}>
            <span style={styles.key}>{i}: </span>
            <JsonTree data={item} depth={depth + 1} />
          </div>
        ))}
      </details>
    )
  }

  if (typeof data === 'object') {
    const entries = Object.entries(data as Record<string, unknown>)
    if (entries.length === 0) return <span>{'{}'}</span>
    return (
      <details open={depth < 1} style={{ marginLeft: depth * 16 }}>
        <summary style={styles.summary}>Object {`{${entries.length}}`}</summary>
        {entries.map(([k, v]) => (
          <div key={k} style={styles.row}>
            <span style={styles.key}>{k}: </span>
            <JsonTree data={v} depth={depth + 1} />
          </div>
        ))}
      </details>
    )
  }

  return <span>{String(data)}</span>
}

export function ExtractionResults({ documentId, status }: Props) {
  const [result, setResult] = useState<DocumentResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (status !== 'Completed') return
    api.getResult(documentId).then(setResult).catch(e =>
      setError(e instanceof Error ? e.message : 'Failed to load results'))
  }, [documentId, status])

  if (status === 'Pending' || status === 'Processing')
    return <p style={styles.processing}>⏳ Processing document… checking every 3s</p>

  if (status === 'Failed')
    return <div style={styles.errorBox}>
      <strong>Extraction failed</strong>
      <p style={{ margin: '4px 0 0', fontSize: 13 }}>{result?.errorMessage ?? 'Unknown error'}</p>
    </div>

  if (!result) return error ? <p style={{ color: '#ef4444' }}>{error}</p> : null

  return (
    <div style={styles.card}>
      <div style={styles.header}>
        <strong>Extracted Data</strong>
        <span style={styles.badge}>AI-generated · verify before use</span>
      </div>
      <div style={styles.tree}>
        <JsonTree data={result.extractedData} />
      </div>
    </div>
  )
}

const styles: Record<string, React.CSSProperties> = {
  processing: { color: '#64748b', fontSize: 14 },
  errorBox: { padding: 12, background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 8, color: '#dc2626' },
  card: { border: '1px solid #e2e8f0', borderRadius: 10, overflow: 'hidden', marginTop: 16 },
  header: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '10px 16px', background: '#f8fafc', borderBottom: '1px solid #e2e8f0' },
  badge: { fontSize: 11, background: '#fef9c3', color: '#854d0e', padding: '2px 8px', borderRadius: 99, border: '1px solid #fde68a' },
  tree: { padding: 16, fontFamily: 'monospace', fontSize: 13, lineHeight: 1.6, overflowX: 'auto' },
  summary: { cursor: 'pointer', color: '#475569', fontWeight: 600 },
  row: { marginLeft: 16, paddingLeft: 8, borderLeft: '2px solid #e2e8f0' },
  key: { color: '#334155', fontWeight: 600 }
}

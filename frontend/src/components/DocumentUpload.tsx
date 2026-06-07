import { useState, useRef, DragEvent, ChangeEvent } from 'react'
import { api, DocumentSummary } from '../api/documentsApi'

interface Props {
  onUploaded: (doc: DocumentSummary) => void
}

const ALLOWED = ['application/pdf', 'image/png', 'image/jpeg', 'image/webp']
const MAX_MB = 20

export function DocumentUpload({ onUploaded }: Props) {
  const [dragging, setDragging] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const validate = (file: File): string | null => {
    if (!ALLOWED.includes(file.type)) return `Unsupported type: ${file.type}. Use PDF, PNG, JPEG, or WebP.`
    if (file.size > MAX_MB * 1024 * 1024) return `File exceeds ${MAX_MB} MB limit.`
    return null
  }

  const upload = async (file: File) => {
    const err = validate(file)
    if (err) { setError(err); return }

    setError(null)
    setUploading(true)
    try {
      const doc = await api.upload(file)
      onUploaded(doc)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Upload failed')
    } finally {
      setUploading(false)
    }
  }

  const onDrop = (e: DragEvent) => {
    e.preventDefault(); setDragging(false)
    const file = e.dataTransfer.files[0]
    if (file) upload(file)
  }

  const onChange = (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) upload(file)
    e.target.value = ''
  }

  return (
    <div>
      <div
        style={{ ...styles.dropzone, ...(dragging ? styles.dragging : {}), ...(uploading ? styles.disabled : {}) }}
        onDragOver={e => { e.preventDefault(); setDragging(true) }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        onClick={() => !uploading && inputRef.current?.click()}
      >
        <input ref={inputRef} type="file" hidden accept=".pdf,.png,.jpg,.jpeg,.webp" onChange={onChange} />
        {uploading
          ? <p style={styles.hint}>Uploading…</p>
          : <>
              <p style={styles.main}>Drop a financial document here</p>
              <p style={styles.hint}>PDF, PNG, JPEG, WebP · max {MAX_MB} MB · click to browse</p>
            </>
        }
      </div>
      {error && <p style={styles.error}>{error}</p>}
    </div>
  )
}

const styles: Record<string, React.CSSProperties> = {
  dropzone: { border: '2px dashed #cbd5e1', borderRadius: 10, padding: '40px 20px', textAlign: 'center', cursor: 'pointer', transition: 'all 0.2s' },
  dragging: { borderColor: '#3b82f6', background: '#eff6ff' },
  disabled: { opacity: 0.6, cursor: 'not-allowed' },
  main: { margin: 0, fontWeight: 600, color: '#1e293b' },
  hint: { margin: '6px 0 0', color: '#94a3b8', fontSize: 13 },
  error: { color: '#ef4444', fontSize: 13, marginTop: 8 }
}

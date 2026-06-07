import { useState, FormEvent } from 'react'
import { useAuth } from '../context/AuthContext'

export function LoginForm() {
  const { login, register } = useAuth()
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState('Uploader')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      if (mode === 'login') await login(email, password)
      else await register(email, password, role)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Auth failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div style={styles.card}>
      <h2 style={styles.title}>DocPipeline</h2>
      <p style={styles.subtitle}>AI-Powered Document Processing</p>

      <div style={styles.tabs}>
        {(['login', 'register'] as const).map(m => (
          <button key={m} onClick={() => setMode(m)}
            style={{ ...styles.tab, ...(mode === m ? styles.tabActive : {}) }}>
            {m.charAt(0).toUpperCase() + m.slice(1)}
          </button>
        ))}
      </div>

      <form onSubmit={submit} style={styles.form}>
        <input style={styles.input} type="email" placeholder="Email" value={email}
          onChange={e => setEmail(e.target.value)} required />
        <input style={styles.input} type="password" placeholder="Password (min 8 chars)" value={password}
          onChange={e => setPassword(e.target.value)} required minLength={8} />

        {mode === 'register' && (
          <select style={styles.input} value={role} onChange={e => setRole(e.target.value)}>
            <option value="Uploader">Uploader</option>
            <option value="Reviewer">Reviewer (view all documents)</option>
          </select>
        )}

        {error && <p style={styles.error}>{error}</p>}

        <button style={styles.button} type="submit" disabled={loading}>
          {loading ? 'Please wait…' : mode === 'login' ? 'Sign In' : 'Create Account'}
        </button>
      </form>
    </div>
  )
}

const styles: Record<string, React.CSSProperties> = {
  card: { maxWidth: 400, margin: '80px auto', padding: 32, border: '1px solid #e2e8f0', borderRadius: 12, fontFamily: 'system-ui, sans-serif' },
  title: { margin: 0, fontSize: 24, fontWeight: 700, color: '#1e293b' },
  subtitle: { margin: '4px 0 20px', color: '#64748b', fontSize: 14 },
  tabs: { display: 'flex', gap: 8, marginBottom: 20 },
  tab: { flex: 1, padding: '8px 0', border: '1px solid #e2e8f0', borderRadius: 6, cursor: 'pointer', background: 'white', color: '#64748b' },
  tabActive: { background: '#3b82f6', color: 'white', borderColor: '#3b82f6' },
  form: { display: 'flex', flexDirection: 'column', gap: 12 },
  input: { padding: '10px 12px', border: '1px solid #e2e8f0', borderRadius: 6, fontSize: 14, outline: 'none' },
  button: { padding: '10px 0', background: '#3b82f6', color: 'white', border: 'none', borderRadius: 6, fontSize: 15, fontWeight: 600, cursor: 'pointer' },
  error: { color: '#ef4444', fontSize: 13, margin: 0 }
}

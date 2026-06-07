import { useState } from 'react'
import { AuthProvider, useAuth } from './context/AuthContext'
import { LoginForm } from './components/LoginForm'
import { DocumentUpload } from './components/DocumentUpload'
import { DocumentList } from './components/DocumentList'
import { DocumentSummary } from './api/documentsApi'

function Dashboard() {
  const { email, role, logout } = useAuth()
  const [refresh, setRefresh] = useState(0)

  const onUploaded = (_doc: DocumentSummary) => setRefresh(r => r + 1)

  return (
    <div style={styles.page}>
      <header style={styles.header}>
        <div>
          <h1 style={styles.title}>DocPipeline</h1>
          <p style={styles.subtitle}>AI-Powered Financial Document Processing</p>
        </div>
        <div style={styles.userInfo}>
          <span style={styles.roleChip}>{role}</span>
          <span style={{ color: '#64748b', fontSize: 14 }}>{email}</span>
          <button style={styles.logoutBtn} onClick={logout}>Sign out</button>
        </div>
      </header>

      <main style={styles.main}>
        {role === 'Uploader' && (
          <section style={styles.section}>
            <h2 style={styles.sectionTitle}>Upload Document</h2>
            <DocumentUpload onUploaded={onUploaded} />
          </section>
        )}

        <section style={styles.section}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h2 style={{ ...styles.sectionTitle, marginBottom: 0 }}>
              {role === 'Reviewer' ? 'All Documents' : 'My Documents'}
            </h2>
            <button style={styles.refreshBtn} onClick={() => setRefresh(r => r + 1)}>↻ Refresh</button>
          </div>
          <DocumentList refresh={refresh} />
        </section>
      </main>
    </div>
  )
}

function AppInner() {
  const { token } = useAuth()
  return token ? <Dashboard /> : <LoginForm />
}

export default function App() {
  return <AuthProvider><AppInner /></AuthProvider>
}

const styles: Record<string, React.CSSProperties> = {
  page: { minHeight: '100vh', background: '#f8fafc', fontFamily: 'system-ui, -apple-system, sans-serif' },
  header: { background: 'white', borderBottom: '1px solid #e2e8f0', padding: '16px 32px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' },
  title: { margin: 0, fontSize: 22, fontWeight: 700, color: '#1e293b' },
  subtitle: { margin: '2px 0 0', fontSize: 13, color: '#64748b' },
  userInfo: { display: 'flex', gap: 12, alignItems: 'center' },
  roleChip: { fontSize: 11, background: '#dbeafe', color: '#1d4ed8', padding: '3px 10px', borderRadius: 99, fontWeight: 600 },
  logoutBtn: { padding: '6px 14px', background: 'none', border: '1px solid #e2e8f0', borderRadius: 6, cursor: 'pointer', color: '#64748b', fontSize: 13 },
  refreshBtn: { padding: '6px 14px', background: 'none', border: '1px solid #e2e8f0', borderRadius: 6, cursor: 'pointer', color: '#3b82f6', fontSize: 13 },
  main: { maxWidth: 900, margin: '0 auto', padding: '32px 16px', display: 'flex', flexDirection: 'column', gap: 32 },
  section: { background: 'white', borderRadius: 12, padding: 24, boxShadow: '0 1px 3px rgba(0,0,0,0.05)' },
  sectionTitle: { margin: '0 0 16px', fontSize: 17, fontWeight: 600, color: '#1e293b' }
}

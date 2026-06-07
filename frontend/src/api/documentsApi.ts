const BASE = import.meta.env.VITE_API_BASE_URL ?? ''

function authHeaders(): HeadersInit {
  const token = localStorage.getItem('jwt')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.json().catch(() => ({ detail: res.statusText }))
    throw new Error(body.detail ?? 'Request failed')
  }
  return res.json()
}

export interface AuthResponse {
  token: string
  email: string
  role: string
  expiresAt: string
}

export interface DocumentSummary {
  id: string
  fileName: string
  contentType: string
  fileSizeBytes: number
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed'
  uploadedByUserId: string
  uploadedAt: string
  processedAt: string | null
  errorMessage: string | null
}

export interface DocumentResult {
  id: string
  fileName: string
  status: string
  uploadedAt: string
  processedAt: string | null
  extractedData: Record<string, unknown> | null
  errorMessage: string | null
}

export const api = {
  async register(email: string, password: string, role: string): Promise<AuthResponse> {
    const res = await fetch(`${BASE}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, role })
    })
    return handleResponse<AuthResponse>(res)
  },

  async login(email: string, password: string): Promise<AuthResponse> {
    const res = await fetch(`${BASE}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    })
    return handleResponse<AuthResponse>(res)
  },

  async upload(file: File): Promise<DocumentSummary> {
    const form = new FormData()
    form.append('file', file)
    const res = await fetch(`${BASE}/api/documents`, {
      method: 'POST',
      headers: authHeaders(),
      body: form
    })
    return handleResponse<DocumentSummary>(res)
  },

  async list(): Promise<DocumentSummary[]> {
    const res = await fetch(`${BASE}/api/documents`, { headers: authHeaders() })
    return handleResponse<DocumentSummary[]>(res)
  },

  async getStatus(id: string): Promise<DocumentSummary> {
    const res = await fetch(`${BASE}/api/documents/${id}/status`, { headers: authHeaders() })
    return handleResponse<DocumentSummary>(res)
  },

  async getResult(id: string): Promise<DocumentResult> {
    const res = await fetch(`${BASE}/api/documents/${id}/result`, { headers: authHeaders() })
    return handleResponse<DocumentResult>(res)
  },

  async retry(id: string): Promise<{ id: string; status: string }> {
    const res = await fetch(`${BASE}/api/documents/${id}/retry`, {
      method: 'POST',
      headers: authHeaders()
    })
    return handleResponse(res)
  }
}

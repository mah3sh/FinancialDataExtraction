import { createContext, useContext, useState, useCallback, ReactNode } from 'react'
import { api, AuthResponse } from '../api/documentsApi'

interface AuthState {
  token: string | null
  email: string | null
  role: string | null
}

interface AuthContextValue extends AuthState {
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, role: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(() => ({
    token: localStorage.getItem('jwt'),
    email: localStorage.getItem('email'),
    role: localStorage.getItem('role')
  }))

  const applyAuth = useCallback((res: AuthResponse) => {
    localStorage.setItem('jwt', res.token)
    localStorage.setItem('email', res.email)
    localStorage.setItem('role', res.role)
    setState({ token: res.token, email: res.email, role: res.role })
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    applyAuth(await api.login(email, password))
  }, [applyAuth])

  const register = useCallback(async (email: string, password: string, role: string) => {
    applyAuth(await api.register(email, password, role))
  }, [applyAuth])

  const logout = useCallback(() => {
    localStorage.clear()
    setState({ token: null, email: null, role: null })
  }, [])

  return (
    <AuthContext.Provider value={{ ...state, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be inside AuthProvider')
  return ctx
}

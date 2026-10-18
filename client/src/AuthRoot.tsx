import { useEffect, useState } from 'react'
import App from './App.tsx'
import AuthScreen from './AuthScreen.tsx'

export default function AuthRoot() {
  const [status, setStatus] = useState<'loading' | 'signed-in' | 'signed-out'>('loading')
  useEffect(() => {
    fetch('/api/auth/me').then(response => setStatus(response.ok ? 'signed-in' : 'signed-out')).catch(() => setStatus('signed-out'))
  }, [])
  if (status === 'loading') return <div className="auth-loading">Opening ZumiRead…</div>
  if (status === 'signed-out') return <AuthScreen onSignedIn={() => setStatus('signed-in')} />
  return <App />
}

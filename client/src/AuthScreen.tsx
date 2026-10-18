import { useState, type FormEvent } from 'react'
import './AuthScreen.css'

export default function AuthScreen({ onSignedIn }: { onSignedIn: () => void }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const tokenResponse = await fetch('/api/auth/csrf')
      if (!tokenResponse.ok) throw new Error('Could not start sign in. Please try again.')
      const { token } = await tokenResponse.json()
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
        body: JSON.stringify({ email, password })
      })
      if (!response.ok) throw new Error(response.status === 401 ? 'Email or password was not recognized.' : 'Sign in failed. Please try again.')
      onSignedIn()
    } catch (failure) { setError((failure as Error).message) }
    finally { setBusy(false) }
  }

  return <main className="auth-screen">
    <a className="auth-brand" href="/"><span aria-hidden="true">✿</span> ZumiRead</a>
    <div className="auth-layout">
      <section className="auth-intro">
        <span className="auth-eyebrow">A READING SPACE FOR YOUR FAMILY</span>
        <h1>Welcome back to your stories.</h1>
        <p>Your next reading moment, parent guide, and growing story world are waiting.</p>
        <div className="auth-book" aria-hidden="true"><div className="auth-book-left" /><div className="auth-book-right"><span>Once upon a<br />new adventure…</span>✦</div></div>
      </section>
      <section className="auth-card" aria-labelledby="auth-title">
        <span className="auth-eyebrow">PARENT SIGN IN</span>
        <h2 id="auth-title">Open your family space</h2>
        <p>Sign in to make stories, save progress, and guide the next adventure.</p>
        <form onSubmit={submit}>
          <label>Email address<input type="email" autoComplete="email" required value={email} onChange={event => setEmail(event.target.value)} /></label>
          <label>Password<input type="password" autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} /></label>
          {error && <div className="auth-error" role="alert">{error}</div>}
          <button disabled={busy}>{busy ? 'Signing in…' : 'Sign in →'}</button>
        </form>
        <div className="auth-create">New to ZumiRead? <a href="/#signup">Create a free account</a></div>
        <a className="auth-sample" href="/sample.html">Read a sample story first</a>
      </section>
    </div>
  </main>
}

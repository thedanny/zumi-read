import { createRoot } from 'react-dom/client'
import { useEffect, useRef, useState } from 'react'
import ReaderView from './ReaderView'
import './index.css'
import './ReadEntry.css'

async function readerApi(path: string, init?: RequestInit) {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (init?.method && init.method !== 'GET') {
    const csrf = await fetch('/api/auth/csrf').then(response => response.json())
    headers['X-CSRF-TOKEN'] = csrf.token
  }
  const response = await fetch('/api/reader/' + path, { ...init, headers })
  if (!response.ok) {
    if (response.status === 401) throw new Error('Ask a grown-up for a fresh story code.')
    if (response.status === 429) throw new Error('Too many tries. Please wait a minute and try again.')
    const body = await response.json().catch(() => ({}))
    throw new Error(body.error || 'Could not open the story. Please try again.')
  }
  return response.json()
}

export function ChildRead() {
  const [state, setState] = useState<'loading' | 'entry' | 'reading' | 'closed'>('loading')
  const [code, setCode] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const started = useRef(false)
  useEffect(() => {
    if (started.current) return
    started.current = true
    const invite = new URLSearchParams(window.location.hash.slice(1)).get('invite')
    const start = async () => {
      try {
        if (invite) {
          await readerApi('redeem', { method: 'POST', body: JSON.stringify({ token: invite }) })
        } else await readerApi('story')
        setState('reading')
      } catch (caught) { if (invite) setError((caught as Error).message); setState('entry') }
      finally { if (invite) window.history.replaceState(null, '', '/read') }
    }
    start()
  }, [])
  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (code.length !== 8) return
    setBusy(true); setError('')
    try {
      await readerApi('redeem', { method: 'POST', body: JSON.stringify({ code }) })
      window.history.replaceState(null, '', '/read')
      setState('reading')
    } catch (caught) { setError((caught as Error).message) } finally { setBusy(false) }
  }
  if (state === 'reading') return <ReaderView sessionId="shared" readerAccess onClose={() => setState('closed')} />
  return <main className="child-entry"><div className="child-entry-brand"><span>✿</span> ZumiRead</div><div className="child-entry-card">
    {state === 'loading' ? <><span className="child-entry-eyebrow">JUST FOR YOU</span><h1>Opening your book…</h1></> : state === 'closed' ? <><span className="child-entry-eyebrow">YOUR READING ROOM</span><h1>Your book is here.</h1><p>Come back whenever you're ready for another page.</p><button className="child-entry-primary" onClick={() => setState('reading')}>Open story again →</button></> : <><span className="child-entry-eyebrow">YOUR READING ROOM</span><h1>A story is waiting for you.</h1><p>Ask a grown-up for a story code, then enter it here.</p><form onSubmit={submit}><label htmlFor="reader-code">Your eight-digit code</label><input id="reader-code" autoComplete="one-time-code" inputMode="numeric" maxLength={9} placeholder="0000 0000" value={code.length > 4 ? code.slice(0, 4) + ' ' + code.slice(4) : code} onChange={event => setCode(event.target.value.replace(/\D/g, '').slice(0, 8))} /><button className="child-entry-primary" disabled={busy || code.length !== 8}>{busy ? 'Opening…' : 'Open my story →'}</button></form></>}
    {error && <p className="child-entry-error" role="alert">{error}</p>}
  </div><p className="child-entry-foot">A quiet place for one good story.</p></main>
}

createRoot(document.getElementById('root')!).render(<ChildRead />)

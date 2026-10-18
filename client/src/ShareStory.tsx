import { useEffect, useState } from 'react'
import QRCode from 'qrcode'

type ShareStatus = { activeReaders: number; invitationPending: boolean }
type Invitation = { code: string; token: string; expiresAt: string }

async function shareApi<T>(path: string, method = 'GET'): Promise<T> {
  const headers: Record<string, string> = {}
  if (method !== 'GET') {
    const csrf = await fetch('/api/auth/csrf').then(response => response.json())
    headers['X-CSRF-TOKEN'] = csrf.token
  }
  const response = await fetch('/api' + path, { method, headers })
  if (!response.ok) {
    const body = await response.json().catch(() => ({}))
    throw new Error(body.error || `Could not update sharing (${response.status})`)
  }
  return response.json()
}

export default function ShareStory({ sessionId, onClose }: { sessionId: string; onClose: () => void }) {
  const [status, setStatus] = useState<ShareStatus | null>(null)
  const [invitation, setInvitation] = useState<Invitation | null>(null)
  const [qr, setQr] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const codePage = window.location.origin + '/read'
  const inviteLink = invitation ? codePage + '#invite=' + encodeURIComponent(invitation.token) : ''
  const isLocal = /^(localhost|127\.0\.0\.1)$/.test(window.location.hostname)

  useEffect(() => {
    shareApi<ShareStatus>('/sessions/' + sessionId + '/reader-share').then(setStatus).catch(e => setError((e as Error).message))
  }, [sessionId])
  useEffect(() => {
    if (!inviteLink) return
    let cancelled = false
    QRCode.toDataURL(inviteLink, { margin: 2, width: 216, errorCorrectionLevel: 'M' })
      .then(image => { if (!cancelled) setQr(image) })
      .catch(() => { if (!cancelled) setError('Could not draw the QR code. The link and code still work.') })
    return () => { cancelled = true }
  }, [inviteLink])

  async function createInvite() {
    setBusy(true); setError(''); setMessage('')
    try {
      setInvitation(await shareApi<Invitation>('/sessions/' + sessionId + '/reader-share', 'POST'))
      setQr('')
      setStatus(previous => ({ activeReaders: previous?.activeReaders || 0, invitationPending: true }))
    } catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }
  async function refreshStatus() {
    try { setStatus(await shareApi<ShareStatus>('/sessions/' + sessionId + '/reader-share')) }
    catch (e) { setError((e as Error).message) }
  }
  async function revoke() {
    if (!window.confirm('Remove this story from every paired child device and cancel the current invite?')) return
    setBusy(true); setError(''); setMessage('')
    try {
      setStatus(await shareApi<ShareStatus>('/sessions/' + sessionId + '/reader-share/revoke', 'POST'))
      setInvitation(null); setQr(''); setMessage('Reader access removed.')
    } catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }
  async function copy(value: string, label: string) {
    try { await navigator.clipboard.writeText(value); setMessage(label + ' copied.') }
    catch { setError('Copy was unavailable. You can select the link or code instead.') }
  }

  return <section className="card share-story no-print" aria-label="Share story with a child device">
    <div className="share-heading"><div><span className="eyebrow">READ ON THEIR OWN DEVICE</span><h2>Send this story to your reader</h2><p>They can open the book without your family login. Only this story and its child questions are shared.</p></div><button className="link" onClick={onClose} aria-label="Close sharing">Close ×</button></div>
    {error && <p className="share-error" role="alert">{error}</p>}{message && <p className="share-message" role="status">{message}</p>}
    <div className="share-status"><span>{status?.activeReaders || 0} active story {status?.activeReaders === 1 ? 'pass' : 'passes'}</span><button className="link" onClick={refreshStatus}>Refresh</button></div>
    {!invitation ? <div className="share-start"><p>Make a one-time invitation that lasts 15 minutes. After opening it, this device can return to the story for 30 days, or until you remove access.</p><button className="primary" disabled={busy} onClick={createInvite}>{status?.invitationPending ? 'Make a new code' : 'Make a QR code & link'}</button></div> : <div className="share-invite">
      <div className="share-qr">{qr ? <img src={qr} alt="QR code to open this story on another device" /> : <span>Preparing QR code…</span>}<small>Scan with the child's device</small></div>
      <div className="share-invite-details"><h3>Or send a link</h3><div className="share-copy-row"><input aria-label="Invitation link" readOnly value={inviteLink} onFocus={event => event.target.select()} /><button className="secondary" onClick={() => copy(inviteLink, 'Story link')}>Copy link</button></div><h3>Or enter a code</h3><p>Open <a href="/read" target="_blank" rel="noreferrer">{codePage}</a> on the child's device and enter:</p><div className="share-code"><strong>{invitation.code.slice(0, 4)} {invitation.code.slice(4)}</strong><button className="secondary" onClick={() => copy(invitation.code, 'Code')}>Copy</button></div><small>Use once before {new Date(invitation.expiresAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}. A new code replaces this one.</small><div><button className="share-another" disabled={busy} onClick={createInvite}>Make another code</button></div></div>
    </div>}
    {isLocal && <p className="share-local">This local address works only on this computer. To open it on another device, ZumiRead needs a reachable HTTPS address.</p>}
    {(status?.activeReaders || status?.invitationPending) && <button className="share-revoke" disabled={busy} onClick={revoke}>Remove child-device access</button>}
  </section>
}

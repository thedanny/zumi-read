import { forwardRef, useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type ReactNode, type TouchEvent } from 'react'
import HTMLFlipBook from 'react-pageflip'
import './ReaderView.css'

type Paragraph = { id: string; text: string }
export type ReaderData = {
  title: string
  paragraphs: Paragraph[]
  questions: { id: string; text: string }[]
  questionCount: number
  showChildQuestions: boolean
  lockStoryDuringQuestions: boolean
  questionModeStarted: boolean
  questionsCompleted: boolean
  episodeNumber: number | null
}
type FlipController = { flipNext: (corner?: 'top' | 'bottom') => void; flipPrev: (corner?: 'top' | 'bottom') => void }
type FlipBookRef = { pageFlip: () => FlipController }
type BookSize = { width: number; height: number; bookWidth: number }
type Segment = { paragraph: number; text: string }

const BookPage = forwardRef<HTMLDivElement, { children?: ReactNode; side?: 'left' | 'right'; blank?: boolean }>(({ children, side, blank }, ref) =>
  <div className={'reader-book flip-page' + (side ? ' ' + side : '') + (blank ? ' blank' : '')} ref={ref}>{children}</div>
)

function measureBook(viewport: { width: number; height: number }, spread: boolean): BookSize {
  const phone = viewport.width < 600
  const width = spread ? Math.min(600, Math.floor((viewport.width - 48) / 2)) : Math.min(760, Math.max(220, viewport.width - (phone ? 24 : 48)))
  return {
    width,
    bookWidth: width * (spread ? 2 : 1),
    height: Math.min(760, Math.max(250, Math.floor(viewport.height - (phone ? 160 : 145))), Math.floor(viewport.height * (phone ? .7 : .76)))
  }
}

async function request<T>(path: string, method = 'GET'): Promise<T> {
  const headers: Record<string, string> = {}
  if (method !== 'GET') {
    const csrf = await fetch('/api/auth/csrf').then(response => response.json())
    headers['X-CSRF-TOKEN'] = csrf.token
  }
  const response = await fetch('/api' + path, { method, headers })
  if (!response.ok) {
    const body = await response.json().catch(() => ({}))
    throw new Error(body.error || (response.status === 401 ? 'Reader access ended. Ask a grown-up for a new code.' : 'Reader request failed (' + response.status + ')'))
  }
  return response.json()
}

// Measure using the same typography and dimensions as a visible book page.
function paginateToFit(paragraphs: Paragraph[], title: string, episode: number | null, size: BookSize): string[][] {
  const probe = document.createElement('div')
  probe.className = 'reader-book reader-measure'
  probe.style.width = size.width + 'px'
  probe.style.height = size.height + 'px'
  document.body.appendChild(probe)
  const pages: Segment[][] = []
  let current: Segment[] = []
  const append = (base: Segment[], segment: Segment) => {
    const next = base.map(part => ({ ...part }))
    if (next.at(-1)?.paragraph === segment.paragraph) next[next.length - 1].text += ' ' + segment.text
    else next.push({ ...segment })
    return next
  }
  function fits(parts: Segment[], first: boolean) {
    probe.replaceChildren()
    if (first) {
      const overline = document.createElement('div')
      overline.className = 'reader-overline'
      overline.textContent = episode ? 'Episode ' + episode : 'A story for you'
      const heading = document.createElement('h1')
      heading.textContent = title
      probe.append(overline, heading)
    }
    const copy = document.createElement('div')
    copy.className = 'reader-copy'
    for (const part of parts) {
      const p = document.createElement('p')
      p.textContent = part.text
      copy.appendChild(p)
    }
    probe.appendChild(copy)
    const last = copy.lastElementChild?.getBoundingClientRect().bottom ?? copy.getBoundingClientRect().bottom
    const bottom = probe.getBoundingClientRect().bottom - parseFloat(getComputedStyle(probe).paddingBottom)
    return last <= bottom + 1 && probe.scrollHeight <= probe.clientHeight + 1
  }
  try {
    const segmenter = new Intl.Segmenter('en', { granularity: 'sentence' })
    const units: Segment[] = []
    for (const [index, paragraph] of paragraphs.entries()) {
      for (const sentence of segmenter.segment(paragraph.text)) {
        const value = sentence.segment.trim()
        if (!value) continue
        const previous = units.at(-1)
        if (previous?.paragraph === index && /\b(?:Mr|Mrs|Ms|Dr|Prof|St|Jr|Sr)\.$/i.test(previous.text)) previous.text += ' ' + value
        else units.push({ paragraph: index, text: value })
      }
    }
    for (const unit of units) {
      let remaining = unit.text
      while (remaining) {
        const candidate = append(current, { paragraph: unit.paragraph, text: remaining })
        if (fits(candidate, pages.length === 0)) { current = candidate; break }
        if (current.length) { pages.push(current); current = []; continue }
        const words = remaining.split(/\s+/)
        let low = 1, high = words.length, best = 0
        while (low <= high) {
          const middle = Math.floor((low + high) / 2)
          if (fits([{ paragraph: unit.paragraph, text: words.slice(0, middle).join(' ') }], pages.length === 0)) { best = middle; low = middle + 1 }
          else high = middle - 1
        }
        if (!best && pages.length === 0) { pages.push([]); continue }
        best = Math.max(1, best)
        pages.push([{ paragraph: unit.paragraph, text: words.slice(0, best).join(' ') }])
        remaining = words.slice(best).join(' ')
      }
    }
    if (current.length || !pages.length) pages.push(current)
    return pages.map(parts => parts.map(part => part.text))
  } finally { probe.remove() }
}

export default function ReaderView({ sessionId, onClose, preview, onSignup, onAnotherSample, readerAccess = false }: { sessionId: string; onClose: () => void; preview?: ReaderData; onSignup?: () => void; onAnotherSample?: () => void; readerAccess?: boolean }) {
  const storyPath = readerAccess ? '/reader/story' : '/sessions/' + sessionId + '/child'
  const startPath = readerAccess ? '/reader/start-questions' : '/sessions/' + sessionId + '/reader/start-questions'
  const finishPath = readerAccess ? '/reader/finish-questions' : '/sessions/' + sessionId + '/reader/finish-questions'
  const [data, setData] = useState<ReaderData | null>(preview ?? null)
  const [page, setPage] = useState(0)
  const [question, setQuestion] = useState(0)
  const [stage, setStage] = useState<'story' | 'questions' | 'done'>('story')
  const [busy, setBusy] = useState(false)
  const [turning, setTurning] = useState(false)
  const [error, setError] = useState('')
  const [viewport, setViewport] = useState(() => ({ width: window.innerWidth, height: window.innerHeight }))
  const [layoutPreference, setLayoutPreference] = useState<'single' | 'double' | null>(() => {
    const saved = window.localStorage.getItem('storysprout.readerLayout')
    return saved === 'single' || saved === 'double' ? saved : null
  })
  const [reducedMotion, setReducedMotion] = useState(() => window.matchMedia('(prefers-reduced-motion: reduce)').matches)
  const touchStart = useRef<{ x: number; y: number } | null>(null)
  const bookRef = useRef<FlipBookRef | null>(null)
  const wide = viewport.width >= 1050
  const spread = wide && layoutPreference !== 'single'
  const size = useMemo(() => measureBook(viewport, spread), [viewport, spread])
  const [pages, setPages] = useState<string[][]>([])
  const currentPage = Math.min(page, Math.max(0, pages.length - 1))
  const paginationPosition = useRef({ pages, page: currentPage })
  useLayoutEffect(() => { paginationPosition.current = { pages, page: currentPage } }, [pages, currentPage])
  const physicalPages = useMemo(() => {
    if (!spread) return pages.map((_, index) => index as number | null)
    const slots: (number | null)[] = [null, ...pages.map((_, index) => index)]
    if (slots.length % 2) slots.push(null)
    return slots
  }, [pages, spread])
  const physicalStart = spread ? Math.floor((currentPage + 1) / 2) * 2 : currentPage
  const lastPhysicalStart = spread ? physicalPages.length - 2 : physicalPages.length - 1
  const canPrevious = physicalStart > 0
  const canNext = physicalStart < lastPhysicalStart

  useEffect(() => {
    if (preview) return
    request<ReaderData>(storyPath).then(result => {
      setData(result)
      setStage(result.questionModeStarted ? result.questionsCompleted ? 'done' : 'questions' : 'story')
    }).catch(e => setError(e.message))
  }, [storyPath, preview])
  useEffect(() => {
    const resize = () => setViewport(previous => previous.width === window.innerWidth && previous.height === window.innerHeight ? previous : { width: window.innerWidth, height: window.innerHeight })
    const preference = window.matchMedia('(prefers-reduced-motion: reduce)')
    const change = () => setReducedMotion(preference.matches)
    window.addEventListener('resize', resize)
    preference.addEventListener('change', change)
    return () => { window.removeEventListener('resize', resize); preference.removeEventListener('change', change) }
  }, [])
  useLayoutEffect(() => {
    if (!data) return
    const previous = paginationPosition.current
    const fitted = paginateToFit(data.paragraphs, data.title, data.episodeNumber, size)
    const offset = previous.pages.slice(0, previous.page).reduce((total, parts) => total + parts.join(' ').length + 1, 0)
    let target = 0, length = 0
    for (let index = 0; index < fitted.length; index++) {
      if (length > offset) break
      target = index
      length += fitted[index].join(' ').length + 1
    }
    setPages(fitted)
    setPage(target)
  }, [data, size])

  function chooseLayout(layout: 'single' | 'double') {
    setTurning(false)
    setLayoutPreference(layout)
    window.localStorage.setItem('storysprout.readerLayout', layout)
  }

  const turnStory = useCallback((direction: -1 | 1) => {
    const to = spread ? Math.max(0, physicalStart + direction * 2 - 1) : currentPage + direction
    if (turning || (direction < 0 && !canPrevious) || (direction > 0 && !canNext)) return
    if (reducedMotion) setPage(to)
    else {
      const book = bookRef.current?.pageFlip()
      if (direction === 1) book?.flipNext('bottom')
      else book?.flipPrev('bottom')
    }
  }, [currentPage, physicalStart, spread, reducedMotion, turning, canPrevious, canNext])
  function handleTouchStart(event: TouchEvent) {
    touchStart.current = event.touches.length === 1 ? { x: event.touches[0].clientX, y: event.touches[0].clientY } : null
  }
  function handleTouchEnd(event: TouchEvent) {
    const start = touchStart.current
    touchStart.current = null
    if (!start || event.changedTouches.length !== 1) return
    const dx = event.changedTouches[0].clientX - start.x
    const dy = event.changedTouches[0].clientY - start.y
    if (Math.abs(dx) < 55 || Math.abs(dx) < Math.abs(dy) * 1.25) return
    const direction: -1 | 1 = dx < 0 ? 1 : -1
    if (stage === 'story' && reducedMotion) turnStory(direction)
    else if (stage === 'questions') setQuestion(q => Math.max(0, Math.min(data!.questions.length - 1, q + direction)))
  }
  useEffect(() => {
    const keys = (event: KeyboardEvent) => {
      if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return
      event.preventDefault()
      const direction: -1 | 1 = event.key === 'ArrowRight' ? 1 : -1
      if (stage === 'story') turnStory(direction)
      else if (stage === 'questions') setQuestion(q => Math.max(0, Math.min((data?.questions.length || 1) - 1, q + direction)))
    }
    window.addEventListener('keydown', keys)
    return () => window.removeEventListener('keydown', keys)
  }, [stage, turnStory, data?.questions.length])

  async function startQuestions() {
    if (preview) { setStage('questions'); setQuestion(0); return }
    setBusy(true); setError('')
    try {
      await request(startPath, 'POST')
      const next = await request<ReaderData>(storyPath)
      setData(next); setStage('questions'); setQuestion(0)
    } catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }
  async function finishQuestions() {
    if (preview) { setStage('done'); return }
    setBusy(true); setError('')
    try {
      await request(finishPath, 'POST')
      setData(await request<ReaderData>(storyPath)); setStage('done')
    } catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }
  function storyPage(index: number) {
    return <>
      {index === 0 && <><div className="reader-overline">{data!.episodeNumber ? 'Episode ' + data!.episodeNumber : 'A story for you'}</div><h1>{data!.title}</h1></>}
      <div className="reader-copy">{pages[index].map((text, part) => <p key={part}>{text}</p>)}</div>
    </>
  }
  const firstVisible = spread ? Math.max(0, physicalStart - 1) : currentPage
  const lastVisible = spread ? Math.min(pages.length - 1, physicalStart) : currentPage
  const counter = firstVisible === lastVisible ? `Page ${firstVisible + 1} of ${pages.length}` : `Pages ${firstVisible + 1}–${lastVisible + 1} of ${pages.length}`
  const pageSlot = (slot: number) => {
    const index = physicalPages[slot]
    return <BookPage key={slot} side={spread ? slot % 2 ? 'right' : 'left' : undefined} blank={index === null}>
      {index === null ? null : storyPage(index)}
    </BookPage>
  }

  return <div className="reader-shell">
    <button className="reader-exit" onClick={onClose} aria-label="Leave reader">×</button>
    {error && <div className="reader-error" role="alert">{error}</div>}
    {!data || !pages.length ? <div className="reader-loading">Opening your book…</div> : <>
      {stage === 'story' && <>
        {wide && <div className="reader-layout-toggle" role="group" aria-label="Page layout">
          <button aria-pressed={!spread} disabled={turning} onClick={() => chooseLayout('single')}>One page</button>
          <button aria-pressed={spread} disabled={turning} onClick={() => chooseLayout('double')}>Open book</button>
        </div>}
        {reducedMotion ? <div className={'reader-static-spread' + (spread ? ' double' : '')} style={{ width: size.bookWidth, height: size.height }} onTouchStart={handleTouchStart} onTouchEnd={handleTouchEnd} onTouchCancel={() => { touchStart.current = null }}>
          {(spread ? [physicalStart, physicalStart + 1] : [currentPage]).map(slot => {
            const index = physicalPages[slot]
            return <div key={slot} className={'reader-book reader-static-page' + (spread ? slot % 2 ? ' right' : ' left' : '') + (index === null ? ' blank' : '')} style={{ width: size.width, height: size.height }}>
              {index === null ? null : storyPage(index)}
            </div>
          })}
        </div> :
          <div className={'reader-book-frame' + (spread ? ' double' : '')} style={{ width: size.bookWidth, height: size.height }}>
            <HTMLFlipBook
              key={sessionId + '-' + (spread ? 'double' : 'single') + '-' + String(size.width) + 'x' + String(size.height) + '-' + physicalPages.length}
              ref={bookRef}
              className="reader-flip-book"
              style={{ width: size.bookWidth, height: size.height }}
              width={size.width}
              height={size.height}
              size="fixed"
              minWidth={220}
              maxWidth={760}
              minHeight={250}
              maxHeight={760}
              startPage={physicalStart}
              drawShadow={true}
              flippingTime={780}
              usePortrait={true}
              startZIndex={0}
              autoSize={false}
              maxShadowOpacity={0.5}
              showCover={false}
              mobileScrollSupport={false}
              clickEventForward={true}
              useMouseEvents={true}
              swipeDistance={45}
              showPageCorners={true}
              disableFlipByClick={true}
              renderOnlyPageLengthChange={true}
              onFlip={(event: { data: number }) => setPage(spread ? Math.max(0, event.data - 1) : event.data)}
              onChangeState={(event: { data: string }) => setTurning(event.data === 'flipping' || event.data === 'user_fold')}
            >
              {physicalPages.map((_, slot) => pageSlot(slot))}
            </HTMLFlipBook>
          </div>}
        <div className="reader-controls" style={{ width: size.bookWidth }}>
          <button onClick={() => turnStory(-1)} disabled={!canPrevious || turning}>← Previous</button>
          <span>{counter}</span>
          {canNext ? <button onClick={() => turnStory(1)} disabled={turning}>Next →</button> : data.showChildQuestions && !data.questionsCompleted ? <button className="reader-ready" disabled={busy || turning} onClick={startQuestions}>I'm ready for questions →</button> : <button onClick={onClose}>The end ✓</button>}
        </div>
      </>}
      {stage === 'questions' && <>
        <div className="reader-book reader-question" onTouchStart={handleTouchStart} onTouchEnd={handleTouchEnd} onTouchCancel={() => { touchStart.current = null }}>
          <div className="reader-overline">Let's talk about it · {question + 1} of {data.questions.length}</div>
          <h1>{data.questions[question]?.text}</h1>
          <p>Tell a grown-up what you think.</p>
        </div>
        <div className="reader-controls">
          <button onClick={() => setQuestion(q => Math.max(0, q - 1))} disabled={question === 0}>← Previous question</button>
          <span>{question + 1} / {data.questions.length}</span>
          {question < data.questions.length - 1 ? <button onClick={() => setQuestion(q => q + 1)}>Next question →</button> : <button className="reader-ready" disabled={busy} onClick={finishQuestions}>Finished questions ✓</button>}
        </div>
        {!data.lockStoryDuringQuestions && <button className="reader-story-link" onClick={() => setStage('story')}>Return to story</button>}
      </>}
      {stage === 'done' && <div className="reader-book reader-done"><div className="reader-overline">The end</div><h1>Lovely reading.</h1><p>{preview ? 'There is always another adventure to explore.' : 'You finished the questions.'}</p><div className="reader-done-actions"><button onClick={() => { setPage(0); setStage('story') }}>Read again</button>{preview && onAnotherSample && <button onClick={onAnotherSample}>Another story</button>}<button onClick={preview && onSignup ? onSignup : onClose}>{preview ? 'Make your own story →' : 'Leave reader'}</button></div></div>}
    </>}
  </div>
}

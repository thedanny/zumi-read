import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import ReaderView from './ReaderView'
import { nextSampleIndex, sampleStories } from './sampleStories'

const selected = new URLSearchParams(window.location.search).get('story')
const requested = selected === null ? NaN : Number(selected)
const hasFeaturedStory = Number.isInteger(requested) && requested >= 0 && requested < sampleStories.length
const index = hasFeaturedStory ? requested : nextSampleIndex()
const story = sampleStories[index]
if (hasFeaturedStory) window.history.replaceState(null, '', '/sample.html')

createRoot(document.getElementById('root')!).render(<StrictMode>
  <ReaderView sessionId="sample" preview={story} onClose={() => { window.location.href = '/' }} onSignup={() => { window.location.href = '/#signup' }} onAnotherSample={() => { window.location.href = '/sample.html' }} />
</StrictMode>)

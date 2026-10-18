import './landing.css'
import { nextSampleIndex, sampleStories } from './sampleStories'

const sampleIndex = nextSampleIndex()
const featuredStory = sampleStories[sampleIndex]
const sampleThemes = ['Gardens', 'Riverside', 'Museum', 'Hilltop']

function excerpt(value: string, length: number) {
  if (value.length <= length) return value
  return value.slice(0, length).replace(/\s+\S*$/, '') + '…'
}

function setPreviewText(id: string, value: string) {
  const element = document.getElementById(id)
  if (element) element.textContent = value
}

document.querySelectorAll<HTMLAnchorElement>('a[href="/sample.html"]').forEach(link => {
  link.href = `/sample.html?story=${sampleIndex}`
})
setPreviewText('landing-hero-preview-title', featuredStory.title.toUpperCase())
setPreviewText('landing-hero-preview-excerpt', excerpt(featuredStory.paragraphs[0].text, 108))
setPreviewText('landing-hero-preview-second', excerpt(featuredStory.paragraphs[1].text, 42))
setPreviewText('landing-showcase-episode', `EPISODE ${featuredStory.episodeNumber}`)
setPreviewText('landing-showcase-title', featuredStory.title)
setPreviewText('landing-showcase-first', excerpt(featuredStory.paragraphs[0].text, 160))
setPreviewText('landing-showcase-second', excerpt(featuredStory.paragraphs[1].text, 145))
const wordCount = featuredStory.paragraphs.reduce((count, paragraph) => count + paragraph.text.trim().split(/\s+/).length, 0)
setPreviewText('landing-preview-details', `${sampleThemes[sampleIndex]} · ${wordCount} words`)

const form = document.querySelector<HTMLFormElement>('#signup-form')
const errorBox = document.querySelector<HTMLDivElement>('#signup-error')

function showError(message: string) {
  if (!errorBox) return
  errorBox.textContent = message
  errorBox.hidden = false
}

form?.addEventListener('submit', async event => {
  event.preventDefault()
  if (!form || !errorBox) return
  errorBox.hidden = true

  const email = form.elements.namedItem('email') as HTMLInputElement
  const password = form.elements.namedItem('password') as HTMLInputElement
  const confirm = form.elements.namedItem('confirm') as HTMLInputElement
  const website = form.elements.namedItem('website') as HTMLInputElement
  if (!email.validity.valid) { showError('Enter a valid email address.'); email.focus(); return }
  if (password.value.length < 10 || password.value.length > 128) { showError('Use a password between 10 and 128 characters.'); password.focus(); return }
  if (password.value !== confirm.value) { showError('The passwords do not match.'); confirm.focus(); return }

  const button = form.querySelector<HTMLButtonElement>('button[type="submit"]')
  if (button) { button.disabled = true; button.textContent = 'Creating your space…' }
  try {
    const csrfResponse = await fetch('/api/auth/csrf')
    if (!csrfResponse.ok) throw new Error('Could not start signup. Please try again.')
    const { token } = await csrfResponse.json()
    const response = await fetch('/api/auth/signup', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
      body: JSON.stringify({ email: email.value, password: password.value, website: website.value })
    })
    if (!response.ok) {
      const body = await response.json().catch(() => ({}))
      throw new Error(body.error || 'Could not create your account. Please try again.')
    }
    window.location.href = '/app.html?welcome=1'
  } catch (failure) {
    showError((failure as Error).message)
    if (button) { button.disabled = false; button.innerHTML = 'Create free account <span aria-hidden="true">→</span>' }
  }
})

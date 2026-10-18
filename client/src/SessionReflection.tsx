type Reflection = {
  readingDifficulty: string | null
  interestRating: string | null
  storyRating: number | null
  parentNotes: string | null
}

export default function SessionReflection({ session, busy, onChange, onSave }: {
  session: Reflection
  busy: boolean
  onChange: (patch: Partial<Reflection>) => void
  onSave: () => void
}) {
  return <section className="card feedback reading-moment no-print">
    <div><span className="eyebrow">WHEN YOU'RE READY</span><h2>Keep this reading moment</h2><p>Every note is optional. Saving adds this story to your child's reading journey and helps shape the next one.</p></div>
    <details className="reflection-details"><summary>Anything else you noticed? <span>Optional</span></summary>
      <div className="form-grid">
        <label>Reading feel<select value={session.readingDifficulty || ''} onChange={event => onChange({ readingDifficulty: event.target.value || null })}><option value="">Choose if helpful</option><option value="Too easy">A little too light</option><option value="About right">Just right</option><option value="Too difficult">A little too much</option></select></label>
        <label>Interest<select value={session.interestRating || ''} onChange={event => onChange({ interestRating: event.target.value || null })}><option value="">Choose if helpful</option><option>Loved it</option><option>Okay</option><option>Did not enjoy it</option></select></label>
        <label>Story fit<select value={session.storyRating || ''} onChange={event => onChange({ storyRating: event.target.value ? Number(event.target.value) : null })}><option value="">Choose if helpful</option>{[1, 2, 3, 4, 5].map(value => <option key={value} value={value}>{value} / 5</option>)}</select></label>
      </div>
      <label>Anything to remember next time?<textarea maxLength={2000} value={session.parentNotes || ''} onChange={event => onChange({ parentNotes: event.target.value })} placeholder="A character they loved, a tricky moment, or a new interest" /></label>
    </details>
    <div className="form-footer"><p>You can enjoy stories without recording any observations.</p><button type="button" className="primary" disabled={busy} onClick={onSave}>Save reading moment</button></div>
  </section>
}

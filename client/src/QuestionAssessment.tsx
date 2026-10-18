type Question = {
  id: string
  skill: string
  text: string
  type: string
  expectedAnswer: string
  alternativeAnswers: string[]
  paragraphIds: string[]
  hint: string
  followUp: string
}

type Paragraph = { id: string; text: string }

const outcomes = [
  { value: 'Independent', label: 'On their own' },
  { value: 'Hint', label: 'With a nudge' },
  { value: 'Needs practice', label: 'Let’s revisit' },
]

const supports = ['Reread together', 'Used a hint', 'Talked it through']

export default function QuestionAssessment({ question, number, paragraphs, outcome, selectedSupports, onOutcome, onSupports }: {
  question: Question
  number: number
  paragraphs: Paragraph[]
  outcome?: string
  selectedSupports: string[]
  onOutcome: (value: string) => void
  onSupports: (values: string[]) => void
}) {
  return <article className="card question assessment-card">
    <div className="qhead"><span>{String(number).padStart(2, '0')}</span><div><small>{question.skill}</small><h3>{question.text}</h3></div></div>
    <div className="assessment-choice no-print">
      <div className="assessment-choice-label">How did it go?</div>
      <div className="assessment-chips" role="group" aria-label={`How this question went: ${question.text}`}>
        {outcomes.map(choice => <button type="button" key={choice.value} className={outcome === choice.value ? 'selected' : ''} aria-pressed={outcome === choice.value} onClick={() => onOutcome(outcome === choice.value ? 'Not assessed' : choice.value)}>{choice.label}</button>)}
      </div>
      {outcome && outcome !== 'Not assessed' && <button type="button" className="assessment-clear" onClick={() => onOutcome('Not assessed')}>Clear choice</button>}
    </div>
    <details className="assessment-extra no-print"><summary>What helped? <span>Optional</span></summary><p>Tap any that fit. These notes help shape the next story.</p><div className="assessment-chips" role="group" aria-label={`What helped with: ${question.text}`}>
      {supports.map(support => <button type="button" key={support} className={selectedSupports.includes(support) ? 'selected' : ''} aria-pressed={selectedSupports.includes(support)} onClick={() => onSupports(selectedSupports.includes(support) ? selectedSupports.filter(value => value !== support) : [...selectedSupports, support])}>{support}</button>)}
    </div></details>
    <details className="assessment-cues"><summary>Answer ideas & story clues</summary><div className="assessment-cues-body">
      <div className="answer"><b>Listen for</b><p>{question.expectedAnswer}</p>{question.alternativeAnswers.length > 0 && <p>Other reasonable answers: {question.alternativeAnswers.join('; ')}</p>}</div>
      <p><b>Try a hint:</b> {question.hint}</p>
      <p><b>Look back at {question.paragraphIds.join(', ')}:</b> {paragraphs.filter(paragraph => question.paragraphIds.includes(paragraph.id)).map(paragraph => paragraph.text.slice(0, 350)).join(' ')}…</p>
      {question.followUp && <p><b>Keep talking:</b> {question.followUp}</p>}
    </div></details>
  </article>
}

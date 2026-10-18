export default function GoalPicker({ skills, selected, onChange }: { skills: string[]; selected: string[]; onChange: (skills: string[]) => void }) {
  return <details className="goal-picker">
    <summary>Focus goals <span>optional{selected.length ? ` · ${selected.length} chosen` : ''}</span></summary>
    <p>Choose up to three skills to give extra attention in upcoming stories. You can change them any time.</p>
    <div className="goal-options" role="group" aria-label="Reading focus goals">
      {skills.map(skill => <button type="button" key={skill} aria-pressed={selected.includes(skill)} className={selected.includes(skill) ? 'selected' : ''} disabled={!selected.includes(skill) && selected.length >= 3} onClick={() => onChange(selected.includes(skill) ? selected.filter(value => value !== skill) : [...selected, skill])}>{skill}</button>)}
    </div>
  </details>
}

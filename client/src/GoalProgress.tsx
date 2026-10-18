export type GoalProgressItem = {
  childId: string
  childName: string
  skill: string
  independent: number
  together: number
  revisit: number
  recent: string[]
}

const labels: Record<string, string> = { Independent: 'On their own', Hint: 'With a nudge', 'Needs practice': 'To revisit' }

export default function GoalProgress({ goals, onChooseGoals }: { goals: GoalProgressItem[]; onChooseGoals: () => void }) {
  return <section className="card goal-progress">
    <div className="goal-progress-heading"><div><span className="eyebrow">YOUR FAMILY’S FOCUS</span><h2>Reading goals</h2></div><button className="link" onClick={onChooseGoals}>{goals.length ? 'Edit goals →' : 'Choose goals →'}</button></div>
    {goals.length ? <><p className="goal-progress-intro">A record of what you noticed together, one conversation at a time.</p><div className="goal-progress-grid">{goals.map(goal => <div className="goal-progress-card" key={`${goal.childId}:${goal.skill}`}>
      <small>{goal.childName}</small><h3>{goal.skill}</h3>
      <div className="goal-moments"><strong>{goal.independent + goal.together + goal.revisit}</strong><span>moments noted</span></div>
      <div className="goal-breakdown"><span>{goal.independent} on their own</span><span>{goal.together} with a nudge</span><span>{goal.revisit} to revisit</span></div>
      {goal.recent.length > 0 && <div className="goal-recent" aria-label="Recent observations">{goal.recent.map((outcome, index) => <span key={index} className={`goal-dot ${outcome === 'Independent' ? 'own' : outcome === 'Hint' ? 'nudge' : 'revisit'}`} title={labels[outcome] || outcome} aria-label={labels[outcome] || outcome} />)}</div>}
    </div>)}</div></> : <p className="goal-progress-intro">Goals are optional. Choose a few skills when you want to follow them more closely; stories work just as well without them.</p>}
  </section>
}

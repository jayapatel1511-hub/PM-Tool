import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { ArrowDown, ArrowRight, ArrowUpRight, CalendarDays, X } from 'lucide-react'
import { t } from '@/lib/i18n'
import './approved-hero.css'

// Fictional public examples. Capacity and recorded time never come from the live API.
const people = [
  { name: 'alexChen', role: 'civilDesign', initials: 'AC', project: [24, 28], other: [6, 5], forecast: [30, 33, 40, 28] },
  { name: 'morganLee', role: 'geotechnical', initials: 'ML', project: [30, 24], other: [4, 6], forecast: [34, 30, 37.5, 41] },
  { name: 'samRivera', role: 'fieldServices', initials: 'SR', project: [20, 16], other: [8, 10], forecast: [28, 26, 31, 24] },
]
const weeklyCapacity = 37.5
const hours = (value: number) => t('landing.hours', { hours: value })

function Brand() {
  return <a className="brand" href="#main" aria-label={t('landing.tuesdayHome')}>
    <span className="tuesday-logo" aria-hidden="true">
      <svg className="logo-mark" viewBox="0 0 40 40" fill="none">
        <path d="m3 7 26-4 8 7-26 4Zm8 10 18-3 8 7-18 3Zm8 10 10-2 8 7-10 2Z" fill="currentColor" />
        <path d="m3 7 26-4 8 7-26 4Z" fill="#a5442a" />
      </svg><span>tuesday</span>
    </span>
  </a>
}

function Person({ person }: { person: typeof people[number] }) {
  return <div className="person"><span className="avatar" aria-hidden="true">{person.initials}</span>
    <div><strong>{t(`landing.${person.name}`)}</strong><small>{t(`landing.${person.role}`)}</small></div>
  </div>
}

function WorkloadPreview() {
  const [discipline, setDiscipline] = useState('all')
  const rows = people.filter(person => discipline === 'all' || person.role === discipline)
  return <section className="workload-preview" aria-labelledby="workload-title">
    <div className="preview-top"><span className="preview-brand">tuesday</span><span>{t('landing.syntheticPreview')}</span></div>
    <div className="preview-body">
      <div className="preview-heading"><div><span className="ui-meta">{t('landing.resources')}</span><h2 id="workload-title">{t('landing.forecastWorkload')}</h2></div><CalendarDays size={18} aria-hidden="true" /></div>
      <div className="preview-toolbar"><span>{t('landing.october2026')}</span>
        <label><span className="sr-only">{t('landing.filterSampleDiscipline')}</span>
          <select value={discipline} onChange={event => setDiscipline(event.target.value)}>
            <option value="all">{t('landing.allDisciplines')}</option>
            {people.map(person => <option key={person.role} value={person.role}>{t(`landing.${person.role}`)}</option>)}
          </select>
        </label>
      </div>
      <p className="scroll-hint">{t('landing.scrollForecast')}</p>
      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- Named scroll regions need keyboard access. */}
      <div className="table-scroll forecast-scroll" tabIndex={0} role="region" aria-label={t('landing.forecastScroll')}>
        <table className="forecast-grid"><caption className="sr-only">{t('landing.forecastCaption')}</caption>
          <thead><tr><th scope="col">{t('landing.people')}</th>{['05', '12', '19', '26'].map(day => <th scope="col" key={day}>{t('landing.octDay', { day })}</th>)}</tr></thead>
          <tbody>{rows.map(person => <tr key={person.name}><th scope="row"><Person person={person} /></th>
            {person.forecast.map((value, index) => <td key={index}><div className={`load-cell ${value > weeklyCapacity ? 'over' : ''}`}>
              <strong>{hours(value)}</strong><small>{t('landing.ofCapacity', { hours: weeklyCapacity })}</small>
              {value > weeklyCapacity && <span>{t('landing.overCapacity')}</span>}
            </div></td>)}
          </tr>)}</tbody>
        </table>
      </div>
      <div className="preview-foot"><span><i aria-hidden="true" />{t('landing.withinCapacity')}</span><span><i className="warning" aria-hidden="true" />{t('landing.overCapacity')}</span></div>
      <p className="preview-note" role="status">{t(rows.length === 1 ? 'landing.forecastPerson' : 'landing.forecastPeople', { count: rows.length })}</p>
    </div>
  </section>
}

function WeeklyPlanner() {
  const [week, setWeek] = useState(0)
  const available = people.reduce((total, person) => total + weeklyCapacity - person.project[week] - person.other[week], 0)
  function changeWeek(event: KeyboardEvent<HTMLButtonElement>, current: number) {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return
    event.preventDefault()
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? 1 : (current + 1) % 2
    setWeek(next)
    document.getElementById(`week-${next + 1}`)?.focus()
  }
  return <section className="planner" aria-labelledby="planner-title">
    <div className="surface-heading"><h3 id="planner-title">{t('landing.weeklyPlanning')}</h3><span className="ui-meta">{t('landing.syntheticPreview')}</span></div>
    <div className="plan-tabs" role="tablist" aria-label={t('landing.samplePlanningWeek')}>
      {[0, 1].map(index => <button key={index} id={`week-${index + 1}`} role="tab" aria-controls="plan-panel" aria-selected={week === index} tabIndex={week === index ? 0 : -1}
        onClick={() => setWeek(index)} onKeyDown={event => changeWeek(event, index)}>{t(index === 0 ? 'landing.weekOfOct5' : 'landing.weekOfOct12')}</button>)}
    </div>
    <div id="plan-panel" role="tabpanel" aria-labelledby={`week-${week + 1}`}>
      <p className="scroll-hint">{t('landing.scrollTheGridToSeeAllWeekly')}</p>
      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- Named scroll regions need keyboard access. */}
      <div className="table-scroll" tabIndex={0} role="region" aria-label={t('landing.weeklyAllocationsScrollHorizontallyForAllColumns')}>
        <table className="plan-grid"><caption className="sr-only">{t('landing.sampleWeeklyAllocationsInHours')}</caption>
          <thead><tr>{['people', 'projects', 'otherWork', 'available'].map(key => <th key={key} scope="col">{t(`landing.${key}`)}</th>)}</tr></thead>
          <tbody>{people.map(person => <tr key={person.name}><th scope="row"><Person person={person} /></th>
            <td><span className="allocation">{hours(person.project[week])}</span></td><td><span className="allocation other">{hours(person.other[week])}</span></td>
            <td><span className="allocation available">{hours(weeklyCapacity - person.project[week] - person.other[week])}</span></td>
          </tr>)}</tbody>
        </table>
      </div>
      <div className="plan-foot"><span>{t('landing.sampleWeeklyCapacity')}</span><strong id="plan-summary" role="status">{t('landing.availableAcrossTeam', { hours: available })}</strong></div>
    </div>
  </section>
}

function EffortPreview() {
  return <div className="effort-preview">
    <div className="surface-heading"><div><span className="ui-meta">{t('landing.illustrativeSummary')}</span><h3>{t('landing.watermain')}</h3></div><span className="phase">{t('landing.designPhase')}</span></div>
    {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- Named scroll regions need keyboard access. */}
      <div className="table-scroll" tabIndex={0} role="region" aria-label={t('landing.effortScroll')}>
      <table className="effort-table"><caption className="sr-only">{t('landing.effortCaption')}</caption>
        <thead><tr>{['workPackage', 'allocatedWeek', 'recordedWeek', 'remainingEstimate'].map(key => <th key={key} scope="col">{t(`landing.${key}`)}</th>)}</tr></thead>
        <tbody>{[
          ['detailedDesign', 64, 52, 320], ['fieldInspection', 24, 18, 160], ['constructionSupport', 16, 12, 160],
        ].map(([key, allocated, recorded, remaining]) => <tr key={key}><th scope="row">{t(`landing.${key}`)}</th><td>{hours(Number(allocated))}</td><td>{hours(Number(recorded))}</td><td>{hours(Number(remaining))}</td></tr>)}</tbody>
        <tfoot><tr><th scope="row">{t('landing.total')}</th><td>{hours(104)}</td><td>{hours(82)}</td><td>{hours(640)}</td></tr></tfoot>
      </table>
    </div>
    <p className="ui-note">{t('landing.effortPeriod')}</p>
  </div>
}

export function ApprovedHero() {
  const dialog = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    const previousTitle = document.title
    document.title = t('landing.pageTitle')
    return () => { document.title = previousTitle }
  }, [])
  return <div className="approved-hero">
    <a className="skip" href="#main">{t('landing.skipToContent')}</a>
    <header className="wrap"><nav className="nav" aria-label={t('landing.mainNavigation')}><Brand />
      <div className="nav-links"><a href="#workspace">{t('landing.productNav')}</a><a href="#teams">{t('landing.teamsNav')}</a><a href="#data">{t('landing.dataNav')}</a>
        <a className="nav-cta" href="/login">{t('auth.signIn')}<ArrowUpRight size={16} aria-hidden="true" /></a>
      </div>
    </nav></header>
    <main id="main" tabIndex={-1}>
      <div className="hero-shell"><div className="thought-sketches" aria-hidden="true"><Sketches /></div>
      <section className="hero wrap" aria-labelledby="hero-title">
        <div className="hero-copy"><span className="eyebrow">{t('landing.engineeringConsulting')}</span>
          <h1 id="hero-title">{t('landing.projectDelivery')}<br /><em>{t('landing.withoutSpreadsheets')}</em></h1>
          <p>{t('landing.heroDescription')}</p>
          <div className="actions"><a className="primary" href="#workspace">{t('landing.explorePlatform')}<ArrowRight size={18} aria-hidden="true" /></a>
            <button className="text-button" aria-haspopup="dialog" onClick={() => dialog.current?.showModal()}>{t('landing.howItWorks')}<ArrowUpRight size={16} aria-hidden="true" /></button>
          </div><p className="hero-footnote">{t('landing.everyDisciplineOneDirection')}</p>
        </div>
        <div className="hero-product"><WorkloadPreview /><p className="product-caption">{t('landing.forecastExplanation')}</p></div>
      </section></div>
      <div className="discipline-strip"><div className="wrap"><span>{t('landing.builtAroundTeams')}</span><p>{t('landing.disciplineList')}</p></div></div>
      <section id="workspace" className="workspace wrap section" aria-labelledby="workspace-title">
        <div className="section-copy"><span className="section-number">{t('landing.capacityLabel')}</span><h2 id="workspace-title">{t('landing.capacityHeading')}</h2><p>{t('landing.capacityDescription')}</p>
          <ul className="plain-list"><li>{t('landing.capacityPoint')}</li><li>{t('landing.nonProjectPoint')}</li><li>{t('landing.conflictPoint')}</li></ul>
        </div><WeeklyPlanner />
      </section>
      <section className="effort-band" aria-labelledby="effort-title"><div className="wrap section">
        <div className="section-intro"><span className="section-number">{t('landing.effortLabel')}</span><h2 id="effort-title">{t('landing.effortHeading')}</h2><p>{t('landing.effortDescription')}</p></div>
        <EffortPreview /><div className="effort-explainer"><p>{t('landing.effortDistinction')}</p><span>{t('landing.effortStaffing')}</span></div>
      </div></section>
      <section id="approach" className="editorial wrap" aria-labelledby="approach-title"><span className="section-number">{t('landing.approachEyebrow')}</span>
        <h2 id="approach-title">{t('landing.complexityHeading')}<br /><em>{t('landing.complexitySupport')}</em></h2>
      </section>
      <section id="teams" className="teams wrap section" aria-labelledby="teams-title">
        <div className="team-intro"><div><span className="section-number">{t('landing.teamsLabel')}</span><h2 id="teams-title">{t('landing.teamsHeading')}</h2></div><p>{t('landing.teamsDescription')}</p></div>
        <figure className="board-figure">
          {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- Keyboard access to the horizontally scrollable screenshot. */}
          <div className="board-scroll" tabIndex={0} role="region" aria-label={t('landing.boardScroll')}>
          <img src="/landing/board.svg" alt={t('landing.boardAlt')} width="1728" height="873" loading="lazy" decoding="async"
            onError={({ currentTarget }) => { if (currentTarget.getAttribute('src') === '/landing/board.svg') currentTarget.src = '/landing/tuesday-board.jpg' }} />
        </div><p className="board-hint">{t('landing.boardHint')}</p><figcaption><span>{t('landing.actualWorkspace')}</span><span>{t('landing.sampleNotice')}</span></figcaption></figure>
        <div className="team-notes"><div><h3>{t('landing.projectManagers')}</h3><p>{t('landing.followProjectHealthDisciplineHandoffsAndUpcoming')}</p></div><div><h3>{t('landing.teamManagers')}</h3><p>{t('landing.planWeeklyCommitmentsSeeAvailableCapacityAnd')}</p></div><div><h3>{t('landing.teamMembers')}</h3><p>{t('landing.findYourPrioritiesKeepContextCloseAnd')}</p></div></div>
      </section>
      <section id="data" className="data-section" aria-labelledby="data-title"><div className="wrap section data-grid">
        <div className="section-copy"><span className="section-number">{t('landing.dataLabel')}</span><h2 id="data-title">{t('landing.dataHeading')}</h2><p className="data-lead">{t('landing.dataOwnership')}</p><p>{t('landing.dataDescription')}</p><p className="data-detail">{t('landing.integrationBoundary')}</p></div>
        <ol className="data-flow" aria-label={t('landing.dataFlowLabel')}><li><span className="flow-number">01</span><div><h3>{t('landing.businessSystems')}</h3><p>{t('landing.businessSystemsDetail')}</p></div></li><li className="flow-arrow" aria-hidden="true"><ArrowDown size={20} /></li><li><span className="flow-number">02</span><div><h3>{t('landing.companyData')}</h3><p>{t('landing.companyDataDetail')}</p></div></li><li className="flow-arrow" aria-hidden="true"><ArrowDown size={20} /></li><li><span className="flow-number">03</span><div><h3>{t('landing.tuesdayPlatform')}</h3><p>{t('landing.platformDetail')}</p></div></li></ol>
      </div></section>
      <section className="final-cta wrap" aria-labelledby="final-title"><div><h2 id="final-title">{t('landing.finalHeading')}</h2><p>{t('landing.finalDescription')}</p></div><a className="primary" href="#workspace">{t('landing.seePlatform')}<ArrowRight size={18} aria-hidden="true" /></a></section>
    </main>
    <footer className="wrap"><Brand /><span>{t('landing.everyDisciplineOneDirection')}</span><a href="/login">{t('auth.signIn')}<ArrowUpRight size={14} aria-hidden="true" /></a></footer>
    <dialog id="workspace-demo" ref={dialog} aria-labelledby="demo-title" aria-describedby="demo-description" onKeyDown={event => {
      if (event.key !== 'Tab') return
      const controls = event.currentTarget.querySelectorAll<HTMLButtonElement | HTMLAnchorElement>('button, a[href]')
      const first = controls[0], last = controls[controls.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }}>
      <div className="dialog-head"><span className="section-number">{t('landing.aLookInsideTuesday')}</span><button className="close" onClick={() => dialog.current?.close()} aria-label={t('landing.closeWorkspacePreview')} autoFocus><X size={20} aria-hidden="true" /></button></div>
      <h2 id="demo-title">{t('landing.oneWorkspace')}</h2><p id="demo-description">{t('landing.demoDescription')}</p>
      <ol className="demo-steps"><li><h3>{t('landing.demoPlan')}</h3><p>{t('landing.demoPlanDetail')}</p></li><li><h3>{t('landing.demoCoordinate')}</h3><p>{t('landing.demoCoordinateDetail')}</p></li><li><h3>{t('landing.demoReview')}</h3><p>{t('landing.demoReviewDetail')}</p></li></ol>
      <p className="ui-note">{t('landing.sampleNotice')}</p><a className="primary" href="/login">{t('landing.enterTuesday')}<ArrowRight size={18} aria-hidden="true" /></a>
    </dialog>
  </div>
}

function Sketches() {
  return (
    <>
      <svg className="sketch-section" viewBox="0 0 220 170" aria-hidden="true">
        <text x="27" y="26" transform="rotate(-4 27 26)">
          {t('landing.aStartingPoint')}
        </text>
        <path d="M14 77q26-17 47-13t32 4q23-16 42-6t25 7q17-9 39-4M12 80q27-14 48-12t31 3q25-13 44-6t27 8q17-7 38-4"></path>
        <path
          d="M18 96q36-4 65 2t57-1 55 0M21 117q27 7 51 3t53 2 68-4M20 138q29-3 55 1t52 0 68 5"
          opacity=".55"
        ></path>
        <path d="M110 44l-2 94M115 45l-2 96M103 44l19 1M101 143l24 1"></path>
        <path
          d="M39 84l-6 8M61 87l-5 7M161 86l-6 8M178 84l-5 9M47 104l-5 7M148 110l-6 8M67 129l-4 6M170 130l-7 7"
          opacity=".6"
        ></path>
        <path className="rust" d="M144 37q-8 6-19 12m0 0 2-10m-2 10 11-1"></path>
      </svg>
      <svg className="sketch-plan" viewBox="0 0 220 180" aria-hidden="true">
        <text x="42" y="25" transform="rotate(3 42 25)">
          {t('landing.whatIf')}
        </text>
        <path d="M14 68q31-43 62-17t55 5 57-9M14 89q27-39 62-17t58 9 63-18M22 109q31-28 64-13t58 8 53-23M37 132q25-22 54-12t57 4 44-15"></path>
        <path d="M12 146q52-37 92-75t94-12M14 152q52-38 94-74t90-12" strokeWidth="2"></path>
        <path className="rust" d="M147 93q25-17 32-5t-13 22-30-6 11-11"></path>
        <path d="M188 38l11 1m-6-6-1 13M19 37l7 9m-9-1 10-7" opacity=".65"></path>
      </svg>
      <svg className="sketch-handoff" viewBox="0 0 220 170" aria-hidden="true">
        <text x="19" y="29" transform="rotate(-5 19 29)">
          {t('landing.surveyDesign')}
        </text>
        <path d="M22 58l54-2 1 40-57 2zM25 61l49-3 2 36M135 74l61-3 1 45-63 1zM136 78l57-4 1 41"></path>
        <path d="M83 77q14-21 26-7t-6 23-12-12q4-16 37 10m-10-9 10 9-14 1"></path>
        <path d="M30 71l23-1M29 80l34 1M145 90l29-2M145 99l37 1" opacity=".6"></path>
        <text x="49" y="146" transform="rotate(3 49 146)">
          {t('landing.whoNeedsThisNext')}
        </text>
        <path className="rust" d="M64 152q47 5 121-3" opacity=".7"></path>
      </svg>
      <svg className="sketch-ideas" viewBox="0 0 220 190" aria-hidden="true">
        <text x="35" y="24" transform="rotate(-6 35 24)">
          {t('landing.nextWeek')}
        </text>
        <path d="M27 48l12-1 1 13-13 1zM49 54l108-3M29 74l12 1-1 13-12-1zM51 82l120-2M29 102l13 1-1 13-13-1zM51 109l90 2"></path>
        <path className="rust" d="M24 51l8 6 13-19M52 75l101 12m-99 2 101-17"></path>
        <text x="58" y="147">
          {t('landing.leaveRoom')}
        </text>
        <path d="M51 128q73-12 110 10t-15 28-94-4-1-34M54 130q69-10 105 11t-12 23" opacity=".7"></path>
        <path d="M13 138q-12-5-9-18t21-14m-8-3 8 3-5 9"></path>
      </svg>
      <svg className="sketch-scribble" viewBox="0 0 220 170" aria-hidden="true">
        <path
          d="M13 113q25-49 52-74t30-8-33 42-28 16 40-40 29 0-28 52 6-18 43-59-2 36-27 66 46-45 9 11 36-41"
          opacity=".75"
        ></path>
        <text x="32" y="155">
          {t('landing.stillFiguringItOut')}
        </text>
      </svg>
    </>
  )
}

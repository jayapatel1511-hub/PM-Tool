import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { ArrowRight, ArrowUpRight, CalendarDays, Check, Layers, Users, X } from 'lucide-react'
import { t } from '@/lib/i18n'
import './approved-hero.css'

// Fictional planning examples from the approved concept; never live data.
const weeks = [
  [24, 6, 7.5, 30, 4, 3.5, 20, 8, 9.5],
  [28, 5, 4.5, 24, 6, 7.5, 16, 10, 11.5],
]

export function ApprovedHero() {
  const dialog = useRef<HTMLDialogElement>(null)
  const [week, setWeek] = useState(0)
  useEffect(() => {
    const previousTitle = document.title
    document.title = t('landing.pageTitle')
    return () => {
      document.title = previousTitle
    }
  }, [])
  function changeWeek(event: KeyboardEvent<HTMLButtonElement>, current: number) {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return
    event.preventDefault()
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? 1 : (current + 1) % 2
    setWeek(next)
    document.getElementById(`week-${next + 1}`)?.focus()
  }
  return (
    <div className="approved-hero">
      <a className="skip" href="#main">
        {t('landing.skipToContent')}
      </a>
      <header className="wrap">
        <nav className="nav" aria-label={t('landing.mainNavigation')}>
          <a className="brand" href="#main" aria-label={t('landing.tuesdayHome')}>
            <span className="tuesday-logo" aria-hidden="true">
              <svg className="logo-mark" viewBox="0 0 40 40" fill="none">
                <path d="m3 7 26-4 8 7-26 4Zm8 10 18-3 8 7-18 3Zm8 10 10-2 8 7-10 2Z" fill="currentColor" />
                <path d="m3 7 26-4 8 7-26 4Z" fill="#a5442a" />
              </svg>
              <span>tuesday</span>
            </span>
          </a>
          <div className="nav-links">
            <a href="#workspace">{t('landing.theWorkspace')}</a>
            <a href="#approach">{t('landing.ourApproach')}</a>
            <a className="nav-cta" href="/login">
              {t('auth.signIn')}
              <ArrowUpRight className="icon" aria-hidden="true" />
            </a>
          </div>
        </nav>
      </header>
      <main id="main">
        <section className="hero" aria-labelledby="hero-title">
          <div className="thought-sketches" aria-hidden="true">
            <Sketches />
          </div>
          <div className="direction-art paper-art" aria-hidden="true">
            <div className="app-mockup">
              <div className="screen-preview">
                <img
                  id="paper-background"
                  src="/landing/tuesday-board-current.jpg"
                  alt=""
                  width="1728"
                  height={873}
                  decoding="async"
                  fetchPriority="high"
                />
              </div>
            </div>
          </div>
          <div className="hero-copy">
            <span className="eyebrow">
              <span className="dot" aria-hidden="true"></span>
              {t('landing.forTheWorkBetweenDisciplines')}
            </span>
            <h1 id="hero-title">
              <span className="headline-line" id="headline-first">
                {t('landing.everyDiscipline')}
              </span>
              <span className="headline-line">
                <em id="headline-second">{t('landing.oneDirection')}</em>
              </span>
            </h1>
            <p id="hero-description">
              <span className="hero-lead">{t('landing.intro')}</span>
              <br />
              <span className="hero-support">{t('landing.introSupport')}</span>
            </p>
            <div className="actions">
              <button className="primary" aria-haspopup="dialog" onClick={() => dialog.current?.showModal()}>
                {t('landing.previewTheWorkspace')}
                <span className="arrow-box">
                  <ArrowUpRight className="icon" aria-hidden="true" />
                </span>
              </button>
              <a className="text-button" href="#workspace">
                {t('landing.seeWeeklyPlanning')}
                <ArrowRight className="icon" aria-hidden="true" />
              </a>
            </div>
            <div className="discipline-rail" aria-label={t('landing.connectedEngineeringDisciplines')}>
              <span className="discipline">
                <span className="node">
                  <Layers className="icon" aria-hidden="true" />
                </span>
                {t('landing.geotechnical')}
              </span>
              <span className="rail-line" aria-hidden="true"></span>
              <span className="discipline">
                <span className="node">
                  <Users className="icon" aria-hidden="true" />
                </span>
                {t('landing.civilDesign')}
              </span>
              <span className="rail-line" aria-hidden="true"></span>
              <span className="discipline">
                <span className="node">
                  <CalendarDays className="icon" aria-hidden="true" />
                </span>
                {t('landing.fieldServices')}
              </span>
            </div>
          </div>
          <p className="app-background-note">{t('landing.taskBoardSampleWorkspace')}</p>
        </section>

        <section id="workspace" className="workspace wrap" aria-labelledby="workspace-title">
          <div className="workspace-copy">
            <span className="section-number">{t('landing.workspaceEyebrow')}</span>
            <h2 id="workspace-title">
              {t('landing.connectedAbove')}
              <br />
              <em>{t('landing.clearOnTheGround')}</em>
            </h2>
            <p>{t('landing.workspaceDescription')}</p>
            <div className="workspace-tags">
              <span>
                <Layers className="icon" aria-hidden="true" />
                {t('landing.projects')}
              </span>
              <span>
                <Users className="icon" aria-hidden="true" />
                {t('landing.people')}
              </span>
              <span>
                <CalendarDays className="icon" aria-hidden="true" />
                {t('landing.capacity')}
              </span>
            </div>
          </div>
          <section className="planner" aria-labelledby="planner-title">
            <div className="planner-top">
              <span className="planner-label">
                <CalendarDays className="icon" aria-hidden="true" />
                {t('landing.plannerEyebrow')}
              </span>
            </div>
            <div className="planner-heading">
              <h2 id="planner-title">{t('landing.aLittleMoreClarity')}</h2>
              <span>{t('landing.october2026')}</span>
            </div>
            <div className="plan-tabs" role="tablist" aria-label={t('landing.samplePlanningWeek')}>
              <button
                id="week-1"
                role="tab"
                aria-controls="plan-panel"
                aria-selected={week === 0}
                tabIndex={week === 0 ? 0 : -1}
                onClick={() => setWeek(0)}
                onKeyDown={(event) => changeWeek(event, 0)}
              >
                {t('landing.weekOfOct5')}
              </button>
              <button
                id="week-2"
                role="tab"
                aria-controls="plan-panel"
                aria-selected={week === 1}
                tabIndex={week === 1 ? 0 : -1}
                onClick={() => setWeek(1)}
                onKeyDown={(event) => changeWeek(event, 1)}
              >
                {t('landing.weekOfOct12')}
              </button>
            </div>
            <div id="plan-panel" role="tabpanel" aria-labelledby={`week-${week + 1}`}>
              <p className="scroll-hint">{t('landing.scrollTheGridToSeeAllWeekly')}</p>
              <div
                className="plan-scroll"
                // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex -- Keyboard access to the horizontal planning table.
                tabIndex={0}
                role="region"
                aria-label={t('landing.weeklyAllocationsScrollHorizontallyForAllColumns')}
              >
                <table className="plan-grid">
                  <caption className="sr-only">{t('landing.sampleWeeklyAllocationsInHours')}</caption>
                  <colgroup>
                    <col className="person-column" />
                    <col />
                    <col />
                    <col />
                  </colgroup>
                  <thead>
                    <tr>
                      <th className="grid-head" scope="col">
                        {t('landing.people2')}
                      </th>
                      <th className="grid-head" scope="col">
                        {t('landing.projects2')}
                      </th>
                      <th className="grid-head" scope="col">
                        {t('landing.otherWork')}
                      </th>
                      <th className="grid-head" scope="col">
                        {t('landing.available')}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <th scope="row">
                        <div className="person">
                          <span className="avatar" aria-hidden="true">
                            {t('landing.ac')}
                          </span>
                          <div>
                            <div className="person-name">{t('landing.alexChen')}</div>
                            <div className="person-role">{t('landing.civilDesign')}</div>
                          </div>
                        </div>
                      </th>
                      <td>
                        <span className="allocation">{t('landing.hours', { hours: weeks[week][0] })}</span>
                      </td>
                      <td>
                        <span className="allocation tentative">
                          {t('landing.hours', { hours: weeks[week][1] })}
                        </span>
                      </td>
                      <td>
                        <span className="allocation available">
                          {t('landing.hours', { hours: weeks[week][2] })}
                        </span>
                      </td>
                    </tr>
                    <tr>
                      <th scope="row">
                        <div className="person">
                          <span className="avatar" aria-hidden="true">
                            {t('landing.ml')}
                          </span>
                          <div>
                            <div className="person-name">{t('landing.morganLee')}</div>
                            <div className="person-role">{t('landing.geotechnical')}</div>
                          </div>
                        </div>
                      </th>
                      <td>
                        <span className="allocation">{t('landing.hours', { hours: weeks[week][3] })}</span>
                      </td>
                      <td>
                        <span className="allocation tentative">
                          {t('landing.hours', { hours: weeks[week][4] })}
                        </span>
                      </td>
                      <td>
                        <span className="allocation available">
                          {t('landing.hours', { hours: weeks[week][5] })}
                        </span>
                      </td>
                    </tr>
                    <tr>
                      <th scope="row">
                        <div className="person">
                          <span className="avatar" aria-hidden="true">
                            {t('landing.sr')}
                          </span>
                          <div>
                            <div className="person-name">{t('landing.samRivera')}</div>
                            <div className="person-role">{t('landing.fieldServices')}</div>
                          </div>
                        </div>
                      </th>
                      <td>
                        <span className="allocation">{t('landing.hours', { hours: weeks[week][6] })}</span>
                      </td>
                      <td>
                        <span className="allocation tentative">
                          {t('landing.hours', { hours: weeks[week][7] })}
                        </span>
                      </td>
                      <td>
                        <span className="allocation available">
                          {t('landing.hours', { hours: weeks[week][8] })}
                        </span>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <div className="plan-foot">
                <span className="legend">
                  <i aria-hidden="true"></i>
                  {t('landing.sampleWeeklyCapacity')}
                </span>
                <span id="plan-summary" role="status">
                  {t('landing.availableAcrossTeam', {
                    hours: weeks[week][2] + weeks[week][5] + weeks[week][8],
                  })}
                </span>
              </div>
            </div>
          </section>
        </section>

        <section id="approach" className="approach wrap" aria-labelledby="approach-title">
          <div>
            <span className="section-number">{t('landing.approachEyebrow')}</span>
            <h2 id="approach-title">
              {t('landing.lessChasing')}
              <br />
              {t('landing.more')}{' '}
              <em>{t('landing.movingForward')}</em>
            </h2>
            <p>{t('landing.approachDescription')}</p>
          </div>
          <div className="approach-steps">
            <div className="step">
              <span>{t('landing.01')}</span>
              <div>
                <h3>{t('landing.seeTheWorkNotJustTheTask')}</h3>
                <p>{t('landing.connectMilestonesHandoffsAndProjectHealthAcross')}</p>
              </div>
            </div>
            <div className="step">
              <span>{t('landing.02')}</span>
              <div>
                <h3>{t('landing.makeRoomForWhatSComing')}</h3>
                <p>{t('landing.planPeopleAndWeeklyCapacityIncludingThe')}</p>
              </div>
            </div>
            <div className="step">
              <span>{t('landing.03')}</span>
              <div>
                <h3>{t('landing.moveWithSharedContext')}</h3>
                <p>{t('landing.knowWhatSConfirmedWhatSExpected')}</p>
              </div>
            </div>
          </div>
        </section>
      </main>
      <footer className="wrap">
        <span>{t('landing.tuesdayThoughtfullyConnected')}</span>
      </footer>

      <dialog
        id="workspace-demo"
        aria-labelledby="demo-title"
        aria-describedby="demo-description"
        ref={dialog}
      >
        <div className="dialog-head">
          <span className="eyebrow">
            <span className="dot" aria-hidden="true"></span>
            {t('landing.aLookInsideTuesday')}
          </span>
          <button
            className="close"
            onClick={() => dialog.current?.close()}
            aria-label={t('landing.closeWorkspacePreview')}
            autoFocus
          >
            <X className="icon" aria-hidden="true" />
          </button>
        </div>
        <h2 id="demo-title">
          {t('landing.oneWorkspace')}
          <br />
          {t('landing.yourPartOfThePicture')}
        </h2>
        <p id="demo-description">{t('landing.projectsGiveEveryoneSharedContextEachRole')}</p>
        <div className="demo-workflows">
          <article className="demo-workflow">
            <Layers className="icon" aria-hidden="true" />
            <h3>{t('landing.projectManagers')}</h3>
            <p>{t('landing.followProjectHealthDisciplineHandoffsAndUpcoming')}</p>
          </article>
          <article className="demo-workflow">
            <Users className="icon" aria-hidden="true" />
            <h3>{t('landing.teamManagers')}</h3>
            <p>{t('landing.planWeeklyCommitmentsSeeAvailableCapacityAnd')}</p>
          </article>
          <article className="demo-workflow">
            <Check className="icon" aria-hidden="true" />
            <h3>{t('landing.teamMembers')}</h3>
            <p>{t('landing.findYourPrioritiesKeepContextCloseAnd')}</p>
          </article>
        </div>
        <div className="dialog-note">{t('landing.sampleNotice')}</div>
        <a className="primary dialog-sign-in" href="/login">
          {t('landing.enterTuesday')}
          <ArrowRight className="icon" aria-hidden="true" />
        </a>
      </dialog>
    </div>
  )
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

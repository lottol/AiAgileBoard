import { FormEvent, useEffect, useMemo, useRef, useState } from 'react'

type Assignee = 'Human' | 'Agent'

type Ticket = {
  id: string
  title: string
  description: string
  comments: string[]
  storyPoints: number
  state: string
  humanNeeded: boolean
  assignee: Assignee
}

type TicketForm = {
  title: string
  description: string
  storyPoints: string
  assignee: Assignee
  state: string
  note: string
}

const initialForm: TicketForm = {
  title: '',
  description: '',
  storyPoints: '3',
  assignee: 'Human',
  state: 'Backlog',
  note: '',
}

const statuses = [
  'Backlog',
  'Ready for Human',
  'Human In Progress',
  'Waiting for Agent',
  'Agent In Progress',
  'Human Review',
  'Changes Requested',
  'Blocked',
  'Done',
  'Canceled',
]

const agentStatuses = new Set(['Waiting for Agent', 'Agent In Progress', 'Changes Requested'])

function Icon({ name }: { name: 'plus' | 'ticket' | 'person' | 'agent' | 'check' | 'close' }) {
  const paths = {
    plus: <path d="M12 5v14M5 12h14" />,
    ticket: <path d="M4 6.5A2.5 2.5 0 0 1 6.5 4h11A2.5 2.5 0 0 1 20 6.5V9a3 3 0 0 0 0 6v2.5a2.5 2.5 0 0 1-2.5 2.5h-11A2.5 2.5 0 0 1 4 17.5V15a3 3 0 0 0 0-6V6.5ZM9 8v8" />,
    person: <><circle cx="12" cy="8" r="3" /><path d="M5.5 20a6.5 6.5 0 0 1 13 0" /></>,
    agent: <><rect x="5" y="7" width="14" height="12" rx="3" /><path d="M12 3v4M9 12h.01M15 12h.01M9 16h6" /></>,
    check: <path d="m5 12 4 4L19 6" />,
    close: <path d="m6 6 12 12M18 6 6 18" />,
  }

  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      {paths[name]}
    </svg>
  )
}

function formatTicketId(id: string) {
  const compact = id.replaceAll('-', '').slice(0, 5).toUpperCase()
  return `AAB-${compact || 'NEW'}`
}

function statusClass(status: string) {
  return status.toLowerCase().replaceAll(' ', '-')
}

export function App() {
  const [tickets, setTickets] = useState<Ticket[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [submitError, setSubmitError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [form, setForm] = useState<TicketForm>(initialForm)
  const [toast, setToast] = useState('')
  const dialogRef = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function loadTickets() {
      try {
        const response = await fetch('/api/v1/tickets', { signal: controller.signal })
        if (!response.ok) throw new Error('Tickets could not be loaded.')
        setTickets(await response.json() as Ticket[])
      } catch (error) {
        if (error instanceof DOMException && error.name === 'AbortError') return
        setLoadError('We couldn’t reach the board. Try refreshing the page.')
      } finally {
        setIsLoading(false)
      }
    }

    void loadTickets()
    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (!toast) return
    const timeout = window.setTimeout(() => setToast(''), 3500)
    return () => window.clearTimeout(timeout)
  }, [toast])

  const summary = useMemo(() => ({
    total: tickets.length,
    human: tickets.filter((ticket) => ticket.humanNeeded && ticket.state !== 'Done').length,
    agent: tickets.filter((ticket) => !ticket.humanNeeded && !['Done', 'Canceled'].includes(ticket.state)).length,
    done: tickets.filter((ticket) => ticket.state === 'Done').length,
  }), [tickets])

  function openDialog() {
    setSubmitError('')
    dialogRef.current?.showModal()
  }

  function closeDialog() {
    if (isSubmitting) return
    dialogRef.current?.close()
  }

  function updateField<Key extends keyof TicketForm>(key: Key, value: TicketForm[Key]) {
    setForm((current) => {
      const next = { ...current, [key]: value }
      if (key === 'state') next.assignee = agentStatuses.has(String(value)) ? 'Agent' : 'Human'
      return next
    })
  }

  async function submitTicket(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitError('')
    setIsSubmitting(true)

    const payload = {
      title: form.title.trim(),
      description: form.description.trim(),
      storyPoints: Number(form.storyPoints),
      assignee: form.assignee,
      stateId: 0,
      state: {
        name: form.state,
        humanNeeded: !agentStatuses.has(form.state),
      },
      comments: form.note.trim() ? [{ body: form.note.trim() }] : [],
    }

    try {
      const response = await fetch('/api/v1/tickets', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })

      if (!response.ok) throw new Error('Ticket could not be submitted.')

      const createdTicket = await response.json() as Ticket
      setTickets((current) => [...current, createdTicket])
      setForm(initialForm)
      dialogRef.current?.close()
      setToast(`${formatTicketId(createdTicket.id)} was added to the board.`)
    } catch {
      setSubmitError('The ticket could not be submitted. Please review the details and try again.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#main-content" aria-label="AI Agile Board home">
          <span className="brand-mark" aria-hidden="true"><span /></span>
          <span>AI Agile Board</span>
        </a>
        <div className="project-chip" aria-label="Current project">
          <span>AA</span>
          <span>Agile Board</span>
          <svg viewBox="0 0 16 16" aria-hidden="true"><path d="m4 6 4 4 4-4" /></svg>
        </div>
      </header>

      <main id="main-content">
        <section className="summary-grid" aria-label="Ticket summary">
          <article className="summary-card summary-total">
            <div className="summary-icon"><Icon name="ticket" /></div>
            <div><span>All tickets</span><strong>{summary.total}</strong></div>
          </article>
          <article className="summary-card">
            <div className="summary-icon human"><Icon name="person" /></div>
            <div><span>Needs you</span><strong>{summary.human}</strong></div>
          </article>
          <article className="summary-card">
            <div className="summary-icon agent"><Icon name="agent" /></div>
            <div><span>With agents</span><strong>{summary.agent}</strong></div>
          </article>
          <article className="summary-card">
            <div className="summary-icon done"><Icon name="check" /></div>
            <div><span>Completed</span><strong>{summary.done}</strong></div>
          </article>
        </section>

        <section className="ticket-panel" aria-labelledby="tickets-title">
          <div className="panel-heading">
            <div>
              <h2 id="tickets-title">All tickets</h2>
              <p>{tickets.length} {tickets.length === 1 ? 'ticket' : 'tickets'} across your workflow</p>
            </div>
          </div>

          {isLoading && (
            <div className="loading-state" aria-live="polite">
              <span className="spinner" /> Loading tickets…
            </div>
          )}

          {!isLoading && loadError && (
            <div className="error-state" role="alert">
              <strong>Board unavailable</strong>
              <span>{loadError}</span>
            </div>
          )}

          {!isLoading && !loadError && tickets.length === 0 && (
            <div className="empty-state">
              <div className="empty-icon"><Icon name="ticket" /></div>
              <h3>Your board is ready</h3>
              <p>Create the first ticket to give your team or an AI agent something to pick up.</p>
              <button className="secondary-button" type="button" onClick={openDialog}>
                <Icon name="plus" /> Create first ticket
              </button>
            </div>
          )}

          {!isLoading && !loadError && tickets.length > 0 && (
            <div className="ticket-list">
              <div className="ticket-list-header" aria-hidden="true">
                <span>Ticket</span><span>Status</span><span>Owner</span><span>Points</span>
              </div>
              {tickets.map((ticket) => (
                <article className="ticket-row" key={ticket.id}>
                  <div className="ticket-main">
                    <span className="ticket-id">{formatTicketId(ticket.id)}</span>
                    <h3>{ticket.title}</h3>
                    <p>{ticket.description}</p>
                  </div>
                  <div className="ticket-field status-field" data-label="Status">
                    <span className={`status-pill ${statusClass(ticket.state)}`}>{ticket.state}</span>
                  </div>
                  <div className="ticket-field owner-field" data-label="Owner">
                    <span className={`owner-avatar ${ticket.assignee.toLowerCase()}`}><Icon name={ticket.assignee === 'Agent' ? 'agent' : 'person'} /></span>
                    {ticket.assignee}
                  </div>
                  <div className="ticket-field points-field" data-label="Points">
                    <strong>{ticket.storyPoints}</strong><span>pts</span>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      </main>

      <button className="primary-button floating-submit" type="button" onClick={openDialog}>
        <Icon name="plus" />
        Submit a ticket
      </button>

      <dialog
        className="ticket-dialog"
        ref={dialogRef}
        onClick={(event) => { if (event.target === event.currentTarget) closeDialog() }}
        onCancel={(event) => { if (isSubmitting) event.preventDefault() }}
      >
        <form method="dialog" onSubmit={submitTicket}>
          <div className="dialog-header">
            <div className="dialog-kicker"><Icon name="ticket" /></div>
            <div>
              <h2>Submit a new ticket</h2>
              <p>Give the next person or agent a clear place to start.</p>
            </div>
            <button className="icon-button" type="button" onClick={closeDialog} aria-label="Close dialog">
              <Icon name="close" />
            </button>
          </div>

          <div className="form-body">
            <label className="field full-width">
              <span>Ticket title</span>
              <input
                autoFocus
                maxLength={200}
                required
                value={form.title}
                onChange={(event) => updateField('title', event.target.value)}
                placeholder="e.g. Add keyboard shortcuts to the board"
              />
            </label>

            <label className="field full-width">
              <span>Description</span>
              <textarea
                required
                rows={4}
                value={form.description}
                onChange={(event) => updateField('description', event.target.value)}
                placeholder="Describe the outcome, context, and any useful constraints…"
              />
            </label>

            <label className="field">
              <span>Status</span>
              <select value={form.state} onChange={(event) => updateField('state', event.target.value)}>
                {statuses.map((status) => <option key={status}>{status}</option>)}
              </select>
            </label>

            <label className="field">
              <span>Assigned to</span>
              <select value={form.assignee} onChange={(event) => updateField('assignee', event.target.value as Assignee)}>
                <option value="Human">Human</option>
                <option value="Agent">AI agent</option>
              </select>
            </label>

            <label className="field">
              <span>Story points</span>
              <input
                type="number"
                min="0"
                max="100"
                required
                value={form.storyPoints}
                onChange={(event) => updateField('storyPoints', event.target.value)}
              />
            </label>

            <label className="field full-width">
              <span>Initial note <em>Optional</em></span>
              <textarea
                rows={3}
                value={form.note}
                onChange={(event) => updateField('note', event.target.value)}
                placeholder="Add acceptance criteria, a useful link, or handoff note…"
              />
            </label>

            {submitError && <p className="form-error" role="alert">{submitError}</p>}
          </div>

          <div className="dialog-footer">
            <button className="text-button" type="button" onClick={closeDialog}>Cancel</button>
            <button className="primary-button submit-button" type="submit" disabled={isSubmitting}>
              {isSubmitting ? <><span className="button-spinner" /> Submitting…</> : <><Icon name="plus" /> Submit ticket</>}
            </button>
          </div>
        </form>
      </dialog>

      {toast && <div className="toast" role="status"><Icon name="check" /> {toast}</div>}
    </div>
  )
}

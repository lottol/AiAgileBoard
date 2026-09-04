import { useEffect, useMemo, useState } from 'react'
import { TicketCreateModal, type Ticket } from './TicketCreateModal'

function Icon({ name }: { name: 'plus' | 'ticket' | 'person' | 'agent' | 'check' }) {
  const paths = {
    plus: <path d="M12 5v14M5 12h14" />,
    ticket: <path d="M4 6.5A2.5 2.5 0 0 1 6.5 4h11A2.5 2.5 0 0 1 20 6.5V9a3 3 0 0 0 0 6v2.5a2.5 2.5 0 0 1-2.5 2.5h-11A2.5 2.5 0 0 1 4 17.5V15a3 3 0 0 0 0-6V6.5ZM9 8v8" />,
    person: <><circle cx="12" cy="8" r="3" /><path d="M5.5 20a6.5 6.5 0 0 1 13 0" /></>,
    agent: <><rect x="5" y="7" width="14" height="12" rx="3" /><path d="M12 3v4M9 12h.01M15 12h.01M9 16h6" /></>,
    check: <path d="m5 12 4 4L19 6" />,
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
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [toast, setToast] = useState('')

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

  function handleTicketCreated(createdTicket: Ticket) {
    setTickets((current) => [...current, createdTicket])
    setToast(`${formatTicketId(createdTicket.id)} was added to the board.`)
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
              <button className="secondary-button" type="button" onClick={() => setIsCreateOpen(true)}>
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

      <button className="primary-button floating-submit" type="button" onClick={() => setIsCreateOpen(true)}>
        <Icon name="plus" />
        Submit a ticket
      </button>

      <TicketCreateModal
        open={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        onCreated={handleTicketCreated}
      />

      {toast && <div className="toast" role="status"><Icon name="check" /> {toast}</div>}
    </div>
  )
}

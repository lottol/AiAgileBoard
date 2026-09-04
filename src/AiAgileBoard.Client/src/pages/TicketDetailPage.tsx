import { FormEvent, useEffect, useState } from 'react'
import { ticketStatuses, type Ticket } from '../tickets'

type TicketDetailPageProps = {
  ticketId: string
}

type TicketDraft = Pick<Ticket, 'title' | 'description' | 'storyPoints' | 'state' | 'assignee'>

function toDraft(ticket: Ticket): TicketDraft {
  return {
    title: ticket.title,
    description: ticket.description,
    storyPoints: ticket.storyPoints,
    state: ticket.state,
    assignee: ticket.assignee,
  }
}

export function TicketDetailPage({ ticketId }: TicketDetailPageProps) {
  const [ticket, setTicket] = useState<Ticket | null>(null)
  const [draft, setDraft] = useState<TicketDraft | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState('')
  const [savedMessage, setSavedMessage] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function loadTicket() {
      try {
        const response = await fetch(`/api/v1/tickets/${ticketId}`, { signal: controller.signal })
        if (response.status === 404) {
          setError('This ticket does not exist or may have been removed.')
          return
        }
        if (!response.ok) throw new Error('Ticket could not be loaded.')

        const loadedTicket = await response.json() as Ticket
        setTicket(loadedTicket)
        setDraft(toDraft(loadedTicket))
      } catch (loadError) {
        if (loadError instanceof DOMException && loadError.name === 'AbortError') return
        setError('We couldn’t load this ticket. Try refreshing the page.')
      } finally {
        setIsLoading(false)
      }
    }

    void loadTicket()
    return () => controller.abort()
  }, [ticketId])

  function updateDraft<Key extends keyof TicketDraft>(key: Key, value: TicketDraft[Key]) {
    setDraft((current) => current ? { ...current, [key]: value } : current)
    setSavedMessage('')
  }

  async function saveTicket(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!draft) return

    setIsSaving(true)
    setError('')
    setSavedMessage('')

    try {
      const response = await fetch(`/api/v1/tickets/${ticketId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(draft),
      })

      if (!response.ok) throw new Error('Ticket could not be saved.')

      const updatedTicket = await response.json() as Ticket
      setTicket(updatedTicket)
      setDraft(toDraft(updatedTicket))
      setSavedMessage('Ticket changes saved.')
    } catch {
      setError('The ticket could not be saved. Review the details and try again.')
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="/" aria-label="AI Agile Board home">
          <span className="brand-mark" aria-hidden="true"><span /></span>
          <span>AI Agile Board</span>
        </a>
        <div className="project-chip" aria-label="Current project">
          <span>AA</span>
          <span>Agile Board</span>
        </div>
      </header>

      <main id="main-content" className="ticket-detail-main">
        <a className="back-link" href="/">← All tickets</a>

        {isLoading && (
          <div className="detail-state" aria-live="polite">
            <span className="spinner" /> Loading ticket…
          </div>
        )}

        {!isLoading && error && !ticket && (
          <div className="detail-state error-state" role="alert">
            <strong>Ticket unavailable</strong>
            <span>{error}</span>
            <a className="secondary-button" href="/">Return to all tickets</a>
          </div>
        )}

        {!isLoading && ticket && draft && (
          <form className="ticket-detail-card" onSubmit={saveTicket}>
            <div className="ticket-detail-heading">
              <span className="detail-ticket-id">{ticket.id}</span>
              <label className="detail-title-field">
                <span>Title</span>
                <input
                  aria-label="Title"
                  maxLength={200}
                  required
                  value={draft.title}
                  onChange={(event) => updateDraft('title', event.target.value)}
                />
              </label>
            </div>

            <div className="ticket-detail-grid">
              <label className="field detail-description-field">
                <span>Description</span>
                <textarea
                  required
                  rows={14}
                  value={draft.description}
                  onChange={(event) => updateDraft('description', event.target.value)}
                />
              </label>

              <aside className="ticket-properties" aria-label="Ticket properties">
                <h2>Details</h2>
                <label className="field">
                  <span>Assignee</span>
                  <select
                    value={draft.assignee}
                    onChange={(event) => updateDraft('assignee', event.target.value as Ticket['assignee'])}
                  >
                    <option value="Human">Human</option>
                    <option value="Agent">AI agent</option>
                  </select>
                </label>
                <label className="field">
                  <span>Points</span>
                  <input
                    type="number"
                    min="0"
                    max="100"
                    required
                    value={draft.storyPoints}
                    onChange={(event) => updateDraft('storyPoints', Number(event.target.value))}
                  />
                </label>
                <label className="field">
                  <span>State</span>
                  <select value={draft.state} onChange={(event) => updateDraft('state', event.target.value)}>
                    {ticketStatuses.map((status) => <option key={status}>{status}</option>)}
                  </select>
                </label>
              </aside>
            </div>

            <section className="ticket-comments" aria-labelledby="comments-title">
              <div>
                <h2 id="comments-title">Comments</h2>
                <span>{ticket.comments.length}</span>
              </div>
              {ticket.comments.length > 0 ? (
                <ol>
                  {ticket.comments.map((comment, index) => <li key={`${index}-${comment}`}>{comment}</li>)}
                </ol>
              ) : <p>No comments have been added yet.</p>}
            </section>

            {error && <p className="form-error detail-message" role="alert">{error}</p>}
            {savedMessage && <p className="save-success detail-message" role="status">{savedMessage}</p>}

            <div className="ticket-detail-actions">
              <button
                className="text-button"
                type="button"
                onClick={() => {
                  setDraft(toDraft(ticket))
                  setError('')
                  setSavedMessage('')
                }}
              >
                Reset changes
              </button>
              <button className="primary-button" type="submit" disabled={isSaving}>
                {isSaving ? <><span className="button-spinner" /> Saving…</> : 'Save changes'}
              </button>
            </div>
          </form>
        )}
      </main>
    </div>
  )
}

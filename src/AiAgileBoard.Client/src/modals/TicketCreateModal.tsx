import { FormEvent, useEffect, useRef, useState } from 'react'
import { agentStatuses, ticketStatuses, TicketType, type Ticket } from '../tickets'

type TicketForm = {
  type: TicketType
  title: string
  description: string
  storyPoints: string
  assignee: Ticket['assignee']
  state: string
  note: string
}

type TicketCreateModalProps = {
  open: boolean
  onClose: () => void
  onCreated: (ticket: Ticket) => void
}

const initialForm: TicketForm = {
  type: TicketType.Task,
  title: '',
  description: '',
  storyPoints: '3',
  assignee: 'Human',
  state: 'Backlog',
  note: '',
}

function ModalIcon({ name }: { name: 'plus' | 'ticket' | 'close' }) {
  const paths = {
    plus: <path d="M12 5v14M5 12h14" />,
    ticket: <path d="M4 6.5A2.5 2.5 0 0 1 6.5 4h11A2.5 2.5 0 0 1 20 6.5V9a3 3 0 0 0 0 6v2.5a2.5 2.5 0 0 1-2.5 2.5h-11A2.5 2.5 0 0 1 4 17.5V15a3 3 0 0 0 0-6V6.5ZM9 8v8" />,
    close: <path d="m6 6 12 12M18 6 6 18" />,
  }

  return <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">{paths[name]}</svg>
}

export function TicketCreateModal({ open, onClose, onCreated }: TicketCreateModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const [form, setForm] = useState<TicketForm>(initialForm)
  const [submitError, setSubmitError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return

    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  function closeDialog() {
    if (!isSubmitting) onClose()
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
      type: form.type,
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
      setForm(initialForm)
      onCreated(createdTicket)
      onClose()
    } catch {
      setSubmitError('The ticket could not be submitted. Please review the details and try again.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <dialog
      className="ticket-dialog"
      ref={dialogRef}
      onClick={(event) => { if (event.target === event.currentTarget) closeDialog() }}
      onCancel={(event) => {
        event.preventDefault()
        closeDialog()
      }}
    >
      <form method="dialog" onSubmit={submitTicket}>
        <div className="dialog-header">
          <div className="dialog-kicker"><ModalIcon name="ticket" /></div>
          <div>
            <h2>Submit a new ticket</h2>
            <p>Give the next person or agent a clear place to start.</p>
          </div>
          <button className="icon-button" type="button" onClick={closeDialog} aria-label="Close dialog">
            <ModalIcon name="close" />
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
              {ticketStatuses.map((status) => <option key={status}>{status}</option>)}
            </select>
          </label>

          <label className="field">
            <span>Type</span>
            <select value={form.type} onChange={(event) => updateField('type', event.target.value as TicketType)}>
              {Object.values(TicketType).map((type) => <option key={type}>{type}</option>)}
            </select>
          </label>

          <label className="field">
            <span>Assigned to</span>
            <select value={form.assignee} onChange={(event) => updateField('assignee', event.target.value as Ticket['assignee'])}>
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
            {isSubmitting ? <><span className="button-spinner" /> Submitting…</> : <><ModalIcon name="plus" /> Submit ticket</>}
          </button>
        </div>
      </form>
    </dialog>
  )
}

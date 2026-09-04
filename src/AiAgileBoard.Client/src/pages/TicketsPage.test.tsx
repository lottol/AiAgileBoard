import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { TicketsPage } from './TicketsPage'

const ticket = {
  id: '95817b43-5922-4481-80f8-cd930061d2f6',
  title: 'Review agent handoff',
  description: 'Confirm the result and validation evidence.',
  comments: [],
  storyPoints: 3,
  state: 'Human Review',
  humanNeeded: true,
  assignee: 'Human',
}

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('TicketsPage', () => {
  it('loads and displays every ticket', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => [ticket] }))
    render(<TicketsPage />)

    expect(screen.getByRole('heading', { name: 'All tickets' })).toBeInTheDocument()
    expect(screen.queryByText('Welcome back.')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Submit a ticket' })).toHaveClass('floating-submit')
    const ticketTitle = await screen.findByText('Review agent handoff')
    const ticketRow = ticketTitle.closest('article')
    expect(ticketRow).toHaveTextContent(ticket.id)
    expect(ticketRow).toHaveTextContent('Human Review')
    expect(screen.getByRole('link', { name: ticket.id })).toHaveAttribute('href', `/tickets/${ticket.id}`)
  })

  it('opens the submission form', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => [] }))
    render(<TicketsPage />)
    await screen.findByText('Your board is ready')

    fireEvent.click(screen.getByRole('button', { name: 'Submit a ticket' }))

    await waitFor(() => expect(screen.getByRole('dialog')).toHaveAttribute('open'))
    expect(screen.getByRole('heading', { name: 'Submit a new ticket' })).toBeInTheDocument()
    expect(screen.getByLabelText('Ticket title')).toBeRequired()
    expect(screen.getByLabelText('Description')).toBeRequired()
  })
})

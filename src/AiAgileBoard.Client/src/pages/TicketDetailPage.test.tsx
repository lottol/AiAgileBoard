import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { TicketDetailPage } from './TicketDetailPage'

const ticket = {
  type: 'Story',
  id: '95817b43-5922-4481-80f8-cd930061d2f6',
  title: 'Review agent handoff',
  description: 'Confirm the result and validation evidence.',
  comments: ['Run the integration tests before approval.'],
  storyPoints: 3,
  state: 'Human Review',
  humanNeeded: true,
  assignee: 'Human' as const,
}

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('TicketDetailPage', () => {
  it('loads and displays all ticket information in editable fields', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, status: 200, json: async () => ticket }))

    render(<TicketDetailPage ticketId={ticket.id} />)

    expect(await screen.findByDisplayValue(ticket.title)).toBeInTheDocument()
    expect(screen.getByDisplayValue(ticket.description)).toBeInTheDocument()
    expect(screen.getByLabelText('Assignee')).toHaveValue('Human')
    expect(screen.getByLabelText('Type')).toHaveValue('Story')
    expect(screen.getByLabelText('Points')).toHaveValue(3)
    expect(screen.getByLabelText('State')).toHaveValue('Human Review')
    expect(screen.getByText(ticket.comments[0])).toBeInTheDocument()
    expect(screen.getByText(ticket.id)).toBeInTheDocument()
  })

  it('saves edited ticket fields', async () => {
    const updatedTicket = {
      ...ticket,
      title: 'Approve agent handoff',
      type: 'Feature',
      storyPoints: 5,
      state: 'Done',
    }
    const fetchMock = vi.fn()
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => ticket })
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => updatedTicket })
    vi.stubGlobal('fetch', fetchMock)

    render(<TicketDetailPage ticketId={ticket.id} />)
    fireEvent.change(await screen.findByLabelText('Title'), { target: { value: updatedTicket.title } })
    fireEvent.change(screen.getByLabelText('Points'), { target: { value: '5' } })
    fireEvent.change(screen.getByLabelText('Type'), { target: { value: 'Feature' } })
    fireEvent.change(screen.getByLabelText('State'), { target: { value: 'Done' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await screen.findByText('Ticket changes saved.')
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(fetchMock).toHaveBeenLastCalledWith(
      `/api/v1/tickets/${ticket.id}`,
      expect.objectContaining({ method: 'PUT' }),
    )
    const request = fetchMock.mock.calls[1][1] as RequestInit
    expect(JSON.parse(String(request.body))).toMatchObject({
      title: updatedTicket.title,
      type: 'Feature',
      storyPoints: 5,
      state: 'Done',
      assignee: 'Human',
    })
  })

  it('shows a useful message when the ticket is not found', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 404 }))

    render(<TicketDetailPage ticketId={ticket.id} />)

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Ticket unavailable'))
    expect(screen.getByText('This ticket does not exist or may have been removed.')).toBeInTheDocument()
  })
})

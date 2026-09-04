import { TicketDetailPage } from './pages/TicketDetailPage'
import { TicketsPage } from './pages/TicketsPage'

const ticketRoute = /^\/tickets\/([^/]+)\/?$/

export function App() {
  const ticketMatch = window.location.pathname.match(ticketRoute)

  if (ticketMatch) {
    return <TicketDetailPage ticketId={decodeURIComponent(ticketMatch[1])} />
  }

  return <TicketsPage />
}

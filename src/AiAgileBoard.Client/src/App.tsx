import { TicketDetailPage } from './pages/TicketDetailPage'
import { TicketsPage } from './pages/TicketsPage'
import { ProjectWorkspace } from './pages/ProjectWorkspace'

const ticketRoute = /^\/tickets\/([^/]+)\/?$/

export function App() {
  const ticketMatch = window.location.pathname.match(ticketRoute)

  const page = ticketMatch
    ? <TicketDetailPage ticketId={decodeURIComponent(ticketMatch[1])} />
    : <TicketsPage />
  return window.__aiabDesktop ? <ProjectWorkspace>{page}</ProjectWorkspace> : page
}

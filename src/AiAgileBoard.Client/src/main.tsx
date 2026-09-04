import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { TicketsPage } from './pages/TicketsPage'
import './styles.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <TicketsPage />
  </StrictMode>,
)

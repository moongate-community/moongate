import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './styles/globals.css'
import { ThemeToggle } from './components/ThemeToggle'

// Placeholder entry point: Task 6 replaces this body with the router.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeToggle />
  </StrictMode>,
)

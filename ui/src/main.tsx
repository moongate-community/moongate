import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './styles/globals.css'
import './lib/i18n'
import { applyStoredTheme } from './lib/theme'
import { App } from './App'

// Before the first render, and outside the component tree: the login screen carries no theme toggle,
// so nothing inside the tree would apply the remembered choice on the screen most visitors see first.
applyStoredTheme()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

import React from 'react'
import ReactDOM from 'react-dom/client'
import { HeroUIProvider } from '@heroui/react'
import { BrowserRouter } from 'react-router-dom'
import App from './App.tsx'
import './index.css'
import { applyThemeToDocument, useThemeStore } from './store/themeStore'

function ThemeSync() {
  const theme = useThemeStore((s) => s.theme)

  React.useEffect(() => {
    applyThemeToDocument(theme)
  }, [theme])

  return null
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <HeroUIProvider>
        <ThemeSync />
        <main className="text-foreground bg-background min-h-screen">
          <App />
        </main>
      </HeroUIProvider>
    </BrowserRouter>
  </React.StrictMode>,
)

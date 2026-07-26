import { useEffect, useState } from 'react'

export type Theme = 'bright' | 'dark'
const STORAGE_KEY = 'mh-theme'

/** Reads the saved theme, defaulting to bright. */
export function initialTheme(): Theme {
  return localStorage.getItem(STORAGE_KEY) === 'dark' ? 'dark' : 'bright'
}

/** Applies the theme to <html> and persists it. */
export function applyTheme(theme: Theme) {
  document.documentElement.setAttribute('data-theme', theme)
  localStorage.setItem(STORAGE_KEY, theme)
}

/** Fixed top-right button that toggles between the bright (default) and dark themes. */
export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(initialTheme)

  useEffect(() => { applyTheme(theme) }, [theme])

  const toggle = () => setTheme(t => (t === 'bright' ? 'dark' : 'bright'))

  return (
    <button className="theme-toggle" onClick={toggle} aria-label="Toggle color theme"
      title={theme === 'bright' ? 'Switch to dark theme' : 'Switch to bright theme'}>
      {theme === 'bright' ? '🌙 Dark' : '☀️ Bright'}
    </button>
  )
}

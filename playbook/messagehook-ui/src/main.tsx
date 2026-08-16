import React from 'react'
import ReactDOM from 'react-dom/client'
import { createBrowserRouter, Link, Navigate, Outlet, RouterProvider } from 'react-router-dom'
import './styles.css'
import Board from './pages/Board'
import SuiteEditor from './pages/SuiteEditor'
import FlowEditor from './pages/FlowEditor'
import Docs from './pages/Docs'
import { ThemeToggle, applyTheme, initialTheme } from './components/ThemeToggle'

// Apply the saved theme (bright by default) before first paint to avoid a flash.
applyTheme(initialTheme())

// Root layout so the top-right bar's <Link> renders inside the router context (it can't sit outside RouterProvider).
function Layout() {
  return (
    <>
      <div className="top-right-bar">
        <Link to="/docs"><button className="ghost sm">📖 Docs</button></Link>
        <ThemeToggle />
      </div>
      <Outlet />
    </>
  )
}

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: '/', element: <Board /> },
      { path: '/suites/:id', element: <SuiteEditor /> },
      // The node-flow canvas is the (only) test case editor.
      { path: '/suites/:id/cases/:caseId', element: <FlowEditor /> },
      { path: '/docs', element: <Navigate to="/docs/overview" replace /> },
      { path: '/docs/:slug', element: <Docs /> }
    ]
  }
])

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <RouterProvider router={router} />
  </React.StrictMode>
)

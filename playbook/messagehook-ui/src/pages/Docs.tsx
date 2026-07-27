import { Link, useParams } from 'react-router-dom'
import { DOC_PAGES, DOC_SECTIONS } from '../docs/pages'

export default function Docs() {
  const { slug = 'overview' } = useParams()
  const page = DOC_PAGES.find(p => p.slug === slug) ?? DOC_PAGES[0]

  return (
    <div className="app">
      <div className="topbar">
        <div>
          <h1>Documentation</h1>
          <div className="crumbs"><Link to="/">Suites</Link> / Docs / {page.title}</div>
        </div>
        <Link to="/"><button className="ghost">← back to app</button></Link>
      </div>

      <div className="docs-layout">
        <nav className="docs-nav">
          {DOC_SECTIONS.map(section => (
            <div key={section.title} className="docs-nav-section">
              <h3>{section.title}</h3>
              {section.slugs.map(s => {
                const p = DOC_PAGES.find(x => x.slug === s)
                if (!p) return null
                return (
                  <Link key={s} to={`/docs/${s}`} className={'docs-nav-link' + (s === page.slug ? ' active' : '')}>
                    {p.title}
                  </Link>
                )
              })}
            </div>
          ))}
        </nav>

        <div className="docs-content card">
          <h2 style={{ marginBottom: '.2rem' }}>{page.title}</h2>
          <p className="muted small" style={{ marginBottom: '1rem' }}>{page.summary}</p>
          {page.body}
        </div>
      </div>
    </div>
  )
}

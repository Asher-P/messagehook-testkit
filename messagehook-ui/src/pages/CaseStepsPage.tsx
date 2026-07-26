import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api'
import type { Suite, TestCase } from '../types'
import { assembledJson } from '../lib/assemble'
import CaseEditor from '../components/CaseEditor'
import RunPanel from '../components/RunPanel'

export default function CaseStepsPage() {
  const { id = '', caseId = '' } = useParams()
  const [suite, setSuite] = useState<Suite | null>(null)
  const [saving, setSaving] = useState(false)
  const [savedAt, setSavedAt] = useState<string | null>(null)

  useEffect(() => { api.getSuite(id).then(setSuite) }, [id])

  const save = async (): Promise<void> => {
    if (!suite) return
    setSaving(true)
    try {
      const saved = await api.saveSuite(suite)
      setSuite(saved)
      setSavedAt(new Date().toLocaleTimeString())
    } finally {
      setSaving(false)
    }
  }

  if (!suite) return <div className="app"><p className="muted">Loading…</p></div>

  const testCase = suite.testCases.find(c => c.id === caseId)
  if (!testCase) return (
    <div className="app">
      <p className="muted">Test case not found. <Link to={`/suites/${id}`}>Back to suite</Link></p>
    </div>
  )

  const setCase = (updated: TestCase) =>
    setSuite({ ...suite, testCases: suite.testCases.map(c => (c.id === updated.id ? updated : c)) })

  return (
    <div className="app">
      <div className="topbar">
        <div>
          <h1>{testCase.name}</h1>
          <div className="crumbs">
            <Link to="/">Suites</Link> / <Link to={`/suites/${suite.id}`}>{suite.name}</Link> / {testCase.name}
          </div>
        </div>
        <div className="row">
          {savedAt && <span className="muted small">saved {savedAt}</span>}
          <Link to={`/suites/${suite.id}`}><button className="ghost">← back</button></Link>
          <button className="primary" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save'}</button>
        </div>
      </div>

      <div className="card">
        <div className="grid cols-2">
          <div>
            <label>Test case name</label>
            <input value={testCase.name} onChange={e => setCase({ ...testCase, name: e.target.value })} />
          </div>
          <div>
            <label>Description</label>
            <input value={testCase.description ?? ''} placeholder="what this test verifies"
              onChange={e => setCase({ ...testCase, description: e.target.value })} />
          </div>
        </div>
      </div>

      <div className="card"><CaseEditor key={testCase.id} suite={suite} testCase={testCase} onChange={setCase} /></div>

      <RunPanel suiteId={suite.id} caseId={testCase.id} ensureSaved={save} />

      <div className="card">
        <h3>Assembled playbook</h3>
        <div className="jsonview">{assembledJson(suite, testCase)}</div>
      </div>
    </div>
  )
}

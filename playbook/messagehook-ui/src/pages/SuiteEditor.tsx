import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api } from '../api'
import type { Suite, TestCase, RunEvent } from '../types'
import KafkaForm from '../components/KafkaForm'
import TopicsEditor from '../components/TopicsEditor'
import PayloadStack from '../components/PayloadStack'

type RunState = 'idle' | 'running' | 'passed' | 'failed'

export default function SuiteEditor() {
  const { id = '' } = useParams()
  const nav = useNavigate()
  const [suite, setSuite] = useState<Suite | null>(null)
  const [tab, setTab] = useState<'cases' | 'config'>('cases')   // Test cases is the default tab
  const [saving, setSaving] = useState(false)
  const [savedAt, setSavedAt] = useState<string | null>(null)
  const [runStatus, setRunStatus] = useState<Record<string, RunState>>({})

  useEffect(() => { api.getSuite(id).then(setSuite) }, [id])

  const persist = async (next: Suite): Promise<Suite> => {
    setSaving(true)
    try {
      const saved = await api.saveSuite(next)
      setSuite(saved)
      setSavedAt(new Date().toLocaleTimeString())
      return saved
    } finally {
      setSaving(false)
    }
  }

  const refreshPayloads = async () => {
    if (!suite) return
    setSuite({ ...suite, payloads: await api.listPayloads(suite.id) })
  }

  if (!suite) return <div className="app"><p className="muted">Loading…</p></div>

  const setStatus = (caseId: string, s: RunState) => setRunStatus(prev => ({ ...prev, [caseId]: s }))

  // Unpersisted on purpose: a case is only ever written to the suite when the flow editor actually saves it
  // (Save/Validate/Run, or any autosave it adds later). Navigating to the editor and back without doing
  // anything there must leave the suite untouched, so nothing is sent to the server here — the new case only
  // exists in router state until the editor itself persists it.
  const addCase = () => {
    const c: TestCase = {
      id: crypto.randomUUID().replace(/-/g, ''),
      name: `case ${suite.testCases.length + 1}`,
      description: '',
      steps: []
    }
    nav(`/suites/${suite.id}/cases/${c.id}`, { state: { draftCase: c } })
  }

  const deleteCase = async (caseId: string) => {
    if (!confirm('Delete this test case?')) return
    await persist({ ...suite, testCases: suite.testCases.filter(c => c.id !== caseId) })
  }

  const applyEvent = (e: RunEvent) => {
    if (e.type === 'result' && e.caseId) setStatus(e.caseId, e.result.passed ? 'passed' : 'failed')
    else if (e.type === 'error' && e.caseId) setStatus(e.caseId, 'failed')
  }

  const runOne = async (caseId: string) => {
    await persist(suite)
    setStatus(caseId, 'running')
    try { await api.runCase(suite.id, caseId, applyEvent) }
    catch { setStatus(caseId, 'failed') }
  }

  const runAll = async () => {
    if (suite.testCases.length === 0) return
    await persist(suite)
    setRunStatus(Object.fromEntries(suite.testCases.map(c => [c.id, 'running' as RunState])))
    try { await api.runSuite(suite.id, applyEvent) }
    catch { setRunStatus(Object.fromEntries(suite.testCases.map(c => [c.id, 'failed' as RunState]))) }
  }

  return (
    <div className="app">
      <div className="topbar">
        <div className="row">
          <Link to="/" className="back-btn" title="Back to suites" aria-label="Back to suites">←</Link>
          <div>
            <h1>{suite.name}</h1>
            <div className="crumbs"><Link to="/">Suites</Link> / {suite.name}</div>
          </div>
        </div>
        <div className="row">
          {savedAt && <span className="muted small">saved {savedAt}</span>}
          <button className="primary" onClick={() => persist(suite)} disabled={saving}>{saving ? 'Saving…' : 'Save suite'}</button>
        </div>
      </div>

      <div className="tabs">
        <div className={'tab' + (tab === 'cases' ? ' active' : '')} onClick={() => setTab('cases')}>Test cases</div>
        <div className={'tab' + (tab === 'config' ? ' active' : '')} onClick={() => setTab('config')}>Configuration</div>
      </div>

      {tab === 'cases' && (
        <>
          <div className="row" style={{ marginBottom: '.8rem' }}>
            <button className="ghost" onClick={addCase}>+ add case</button>
            <button className="primary" onClick={runAll} disabled={suite.testCases.length === 0}>▶ Run all</button>
            <span className="spacer" />
            <span className="muted small">{suite.testCases.length} case{suite.testCases.length === 1 ? '' : 's'}</span>
          </div>

          {suite.testCases.length === 0 ? (
            <div className="card"><p className="muted">No test cases. Add one to build steps and run it.</p></div>
          ) : (
            <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
              <table className="table">
                <thead>
                  <tr><th>Name</th><th>Description</th><th>Status</th><th></th></tr>
                </thead>
                <tbody>
                  {suite.testCases.map(c => {
                    const st = runStatus[c.id] ?? 'idle'
                    return (
                      <tr key={c.id} className={st === 'passed' ? 'row-pass' : st === 'failed' ? 'row-fail' : ''}>
                        <td><Link to={`/suites/${suite.id}/cases/${c.id}`} style={{ fontWeight: 600 }}>{c.name}</Link></td>
                        <td className="muted">{c.description || <span className="small">—</span>}</td>
                        <td><StatusPill state={st} /></td>
                        <td className="num">
                          <div className="row" style={{ justifyContent: 'flex-end' }}>
                            <button className="sm primary" onClick={() => runOne(c.id)} disabled={st === 'running'}>▶ Run</button>
                            <Link to={`/suites/${suite.id}/cases/${c.id}`}><button className="sm ghost">open</button></Link>
                            <button className="sm danger" onClick={() => deleteCase(c.id)}>✕</button>
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {tab === 'config' && (
        <>
          <div className="card">
            <label>Suite name</label>
            <input value={suite.name} onChange={e => setSuite({ ...suite, name: e.target.value })} />
          </div>
          <KafkaForm kafka={suite.kafka} onChange={k => setSuite({ ...suite, kafka: k })} />
          <div className="grid cols-2">
            <TopicsEditor title="Consume topics" consumer topics={suite.consumeTopics}
              onChange={t => setSuite({ ...suite, consumeTopics: t })} />
            <TopicsEditor title="Produce topics" consumer={false} topics={suite.produceTopics}
              onChange={t => setSuite({ ...suite, produceTopics: t })} />
          </div>
          <PayloadStack suiteId={suite.id} payloads={suite.payloads} onChange={refreshPayloads} />
        </>
      )}
    </div>
  )
}

function StatusPill({ state }: { state: RunState }) {
  if (state === 'passed') return <span className="pill ok">✓ passed</span>
  if (state === 'failed') return <span className="pill fail">✗ failed</span>
  if (state === 'running') return <span className="pill run">⠿ running</span>
  return <span className="pill">idle</span>
}

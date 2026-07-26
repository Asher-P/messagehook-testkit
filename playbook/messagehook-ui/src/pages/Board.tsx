import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../api'
import type { SuiteSummary } from '../types'

export default function Board() {
  const [suites, setSuites] = useState<SuiteSummary[]>([])
  const [loading, setLoading] = useState(true)
  const nav = useNavigate()

  const load = () => api.listSuites().then(setSuites).finally(() => setLoading(false))
  useEffect(() => { load() }, [])

  const create = async () => {
    const name = prompt('Suite name?', 'My suite')
    if (!name) return
    const suite = await api.createSuite(name)
    nav(`/suites/${suite.id}`)
  }

  const remove = async (id: string, name: string) => {
    if (!confirm(`Delete suite "${name}"? This removes its test cases and payloads.`)) return
    await api.deleteSuite(id)
    load()
  }

  return (
    <div className="app">
      <div className="topbar">
        <div>
          <h1>MessageHook Playbook</h1>
          <div className="crumbs">Test suites</div>
        </div>
        <button className="primary" onClick={create}>+ New suite</button>
      </div>

      {loading ? <p className="muted">Loading…</p> : suites.length === 0 ? (
        <div className="card">
          <p className="muted">No suites yet. Create one to configure Kafka, upload payloads, and build test cases.</p>
        </div>
      ) : (
        <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
          <table className="table">
            <thead>
              <tr>
                <th>Name</th>
                <th className="num">Cases</th>
                <th className="num">Payloads</th>
                <th>Bootstrap</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {suites.map(s => (
                <tr key={s.id}>
                  <td><Link to={`/suites/${s.id}`} style={{ fontWeight: 600 }}>{s.name}</Link></td>
                  <td className="num">{s.testCaseCount}</td>
                  <td className="num">{s.payloadCount}</td>
                  <td className="mono muted">{s.bootstrap || '—'}</td>
                  <td className="num"><button className="danger sm" onClick={() => remove(s.id, s.name)}>✕</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

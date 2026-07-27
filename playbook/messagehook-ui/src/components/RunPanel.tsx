import { useRef, useState } from 'react'
import { api } from '../api'
import type { StepResult, PlaybookResult } from '../types'

export default function RunPanel({
  suiteId, caseId, ensureSaved
}: {
  suiteId: string
  caseId: string
  ensureSaved: () => Promise<void>
}) {
  const [validation, setValidation] = useState<{ valid: boolean; errors: string[] } | null>(null)
  const [steps, setSteps] = useState<StepResult[]>([])
  const [result, setResult] = useState<PlaybookResult | null>(null)
  const [runError, setRunError] = useState<string | null>(null)
  const [running, setRunning] = useState(false)
  const abortRef = useRef<AbortController | null>(null)

  const validate = async () => {
    await ensureSaved()
    setResult(null); setSteps([]); setRunError(null)
    setValidation(await api.validateCase(suiteId, caseId))
  }

  const run = async () => {
    await ensureSaved()
    setValidation(null); setSteps([]); setResult(null); setRunError(null); setRunning(true)
    const ctrl = new AbortController()
    abortRef.current = ctrl
    try {
      await api.runCase(suiteId, caseId, e => {
        if (e.type === 'step') setSteps(prev => [...prev, e.step])
        else if (e.type === 'result') setResult(e.result)
        else if (e.type === 'error') setRunError(e.errors?.join('\n') ?? e.error)
      }, ctrl.signal)
    } catch (e) {
      if (!ctrl.signal.aborted) setRunError(String((e as Error).message))
    } finally {
      setRunning(false)
    }
  }

  const cancel = () => abortRef.current?.abort()

  return (
    <div className="card">
      <div className="row">
        <h2 style={{ flex: 1 }}>Run</h2>
        <button className="ghost" onClick={validate} disabled={running}>Validate</button>
        {running
          ? <button className="danger" onClick={cancel}>Cancel</button>
          : <button className="primary" onClick={run}>▶ Run</button>}
      </div>

      {validation && (
        validation.valid
          ? <div className="okbox" style={{ marginTop: '.6rem' }}>Valid — no load-time errors (no broker was contacted).</div>
          : <div className="errbox" style={{ marginTop: '.6rem' }}>
              <strong>Invalid:</strong>
              <ul style={{ margin: '.3rem 0 0 1rem' }}>{validation.errors.map((x, i) => <li key={i}>{x}</li>)}</ul>
            </div>
      )}

      {(running || steps.length > 0 || result || runError) && (
        <div style={{ marginTop: '.8rem' }}>
          {steps.map((s, i) => (
            <div className="runline" key={i}>
              <div className="row">
                <span className={'pill ' + (s.passed ? 'ok' : 'fail')}>{s.passed ? '✓' : '✗'}</span>
                <span style={{ flex: 1 }}>{s.name ?? `step ${i + 1}`}</span>
                <span className="muted small">{s.receivedMessageCount} msg</span>
              </div>
              {s.error && <div className="vdetail" style={{ color: 'var(--danger)' }}>{s.error}</div>}
              {(s.validations ?? []).filter(v => !v.passed).map((v, j) => (
                <div className="vdetail" key={j}>✗ {v.target}:{v.path} {v.type} — expected [{v.expected}], actual [{v.actual}]</div>
              ))}
            </div>
          ))}

          {running && <div className="runline"><span className="pill run">⠿ running</span> <span className="muted small">waiting for messages…</span></div>}

          {result && (
            <div className={result.passed ? 'okbox' : 'errbox'} style={{ marginTop: '.4rem' }}>
              {result.passed ? '✓ Passed' : '✗ Failed'} — {(result.steps ?? []).filter(s => s.passed).length}/{(result.steps ?? []).length} steps
              {result.error && <div>{result.error}</div>}
            </div>
          )}
          {runError && <div className="errbox" style={{ marginTop: '.4rem' }}>{runError}</div>}
        </div>
      )}
    </div>
  )
}

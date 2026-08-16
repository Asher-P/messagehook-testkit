import { useCallback, useEffect, useRef, useState } from 'react'
import type { PointerEvent as ReactPointerEvent } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { api } from '../api'
import type { PlaybookResult, Step, StepResult, Suite, TestCase } from '../types'
import { HOOK_TYPE_LABEL, hookTypeOf } from '../types'
import StepFields from '../components/StepFields'
import {
  GAP_X, MAX_ZOOM, MIN_ZOOM, NODE_ICON, NODE_KIND, NODE_W, PORT_Y, PRESETS, START_W,
  autoLayout, edgePath, loadPositions, reconcile, savePositions
} from '../lib/flow'
import type { FlowPositions, XY } from '../lib/flow'

type RunState = 'idle' | 'running' | 'passed' | 'failed'

const clamp = (v: number, lo: number, hi: number) => Math.min(hi, Math.max(lo, v))

/**
 * The node-flow playbook builder — the test case editor. A test case is a linear chain of steps, so the canvas
 * draws it as a start node followed by one node per step, wired left to right. Node positions are cosmetic and
 * live in localStorage (steps carry no id and nothing about layout may leak into the saved playbook JSON).
 */
export default function FlowEditor() {
  const { id = '', caseId = '' } = useParams()
  const location = useLocation()

  // "Add case" never persists (see SuiteEditor.addCase) — it hands the brand-new, still-unsaved TestCase to
  // this page via router state instead. If the fetched suite doesn't have caseId yet, this is why: the case
  // lives only here until something actually saves it, so leaving without doing anything creates nothing.
  const draftCase = (location.state as { draftCase?: TestCase } | null)?.draftCase

  const [suite, setSuite] = useState<Suite | null>(null)
  const [pos, setPos] = useState<FlowPositions>(() => autoLayout(0))
  const [view, setView] = useState({ x: 0, y: 0, k: 1 })
  const [selected, setSelected] = useState<number | null>(null)
  const [paletteAt, setPaletteAt] = useState<number | null>(null)  // insertion index, or null when closed
  const [dirty, setDirty] = useState(false)
  const [saving, setSaving] = useState(false)
  const [savedAt, setSavedAt] = useState<string | null>(null)

  const [running, setRunning] = useState(false)
  const [status, setStatus] = useState<Record<number, RunState>>({})
  const [results, setResults] = useState<Record<number, StepResult>>({})
  const [outcome, setOutcome] = useState<PlaybookResult | null>(null)
  const [runError, setRunError] = useState<string | null>(null)
  const [validation, setValidation] = useState<{ valid: boolean; errors: string[] } | null>(null)

  const canvasRef = useRef<HTMLDivElement>(null)
  const abortRef = useRef<AbortController | null>(null)
  const stepCursor = useRef(0)
  const fitted = useRef(false)

  const testCase = suite?.testCases.find(c => c.id === caseId) ?? null
  const steps = testCase?.steps ?? []
  const stepCount = steps.length

  useEffect(() => {
    api.getSuite(id).then(s => {
      const isNew = !s.testCases.some(c => c.id === caseId) && draftCase?.id === caseId
      const withDraft = isNew ? { ...s, testCases: [...s.testCases, draftCase!] } : s
      setSuite(withDraft)
      setDirty(isNew)   // the draft case isn't on the server yet — flag it as unsaved from the start
      const n = withDraft.testCases.find(c => c.id === caseId)?.steps.length ?? 0
      const saved = loadPositions(id, caseId)
      setPos(saved ? reconcile(saved, n) : autoLayout(n))
      fitted.current = false
    })
    // draftCase deliberately excluded: it's only relevant to the mount that navigated here with it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, caseId])

  // A save round-trip (or an external reload) can replace the step list wholesale; keep the layout aligned.
  useEffect(() => {
    setPos(p => (p.nodes.length === stepCount ? p : reconcile(p, stepCount)))
  }, [stepCount])

  useEffect(() => { if (suite) savePositions(id, caseId, pos) }, [pos, id, caseId, suite])

  // --- viewport -------------------------------------------------------------------------------------

  const fit = useCallback(() => {
    const el = canvasRef.current
    if (!el) return
    const boxes = [{ ...pos.start, w: START_W }, ...pos.nodes.map(n => ({ ...n, w: NODE_W }))]
    const minX = Math.min(...boxes.map(b => b.x)) - 60
    const maxX = Math.max(...boxes.map(b => b.x + b.w)) + 60
    const minY = Math.min(...boxes.map(b => b.y)) - 60
    const maxY = Math.max(...boxes.map(b => b.y + 160)) + 60
    const k = clamp(Math.min(el.clientWidth / (maxX - minX), el.clientHeight / (maxY - minY)), MIN_ZOOM, 1.1)
    setView({
      k,
      x: (el.clientWidth - (maxX - minX) * k) / 2 - minX * k,
      y: (el.clientHeight - (maxY - minY) * k) / 2 - minY * k
    })
  }, [pos])

  // Frame the graph once, after the first load has produced positions.
  useEffect(() => {
    if (suite && !fitted.current) { fitted.current = true; fit() }
  }, [suite, fit])

  // Wheel must be a non-passive native listener — React's pooled handler cannot preventDefault the page scroll.
  useEffect(() => {
    const el = canvasRef.current
    if (!el) return
    const onWheel = (e: WheelEvent) => {
      e.preventDefault()
      const rect = el.getBoundingClientRect()
      const px = e.clientX - rect.left
      const py = e.clientY - rect.top
      setView(v => {
        const k = clamp(v.k * Math.exp(-e.deltaY * 0.0015), MIN_ZOOM, MAX_ZOOM)
        // Keep the world point under the cursor pinned while zooming.
        return { k, x: px - ((px - v.x) / v.k) * k, y: py - ((py - v.y) / v.k) * k }
      })
    }
    el.addEventListener('wheel', onWheel, { passive: false })
    return () => el.removeEventListener('wheel', onWheel)
  }, [])

  const panRef = useRef<{ sx: number; sy: number; vx: number; vy: number; moved: boolean } | null>(null)

  const onCanvasDown = (e: ReactPointerEvent) => {
    if (e.button !== 0 && e.button !== 1) return
    panRef.current = { sx: e.clientX, sy: e.clientY, vx: view.x, vy: view.y, moved: false }
    e.currentTarget.setPointerCapture(e.pointerId)
  }
  const onCanvasMove = (e: ReactPointerEvent) => {
    const p = panRef.current
    if (!p) return
    const dx = e.clientX - p.sx
    const dy = e.clientY - p.sy
    if (Math.abs(dx) > 2 || Math.abs(dy) > 2) p.moved = true
    setView(v => ({ ...v, x: p.vx + dx, y: p.vy + dy }))
  }
  const onCanvasUp = (e: ReactPointerEvent) => {
    const p = panRef.current
    panRef.current = null
    if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId)
    if (p && !p.moved) setSelected(null)   // a click on empty canvas clears the selection; a drag does not
  }

  const zoomBy = (factor: number) => {
    const el = canvasRef.current
    if (!el) return
    const px = el.clientWidth / 2
    const py = el.clientHeight / 2
    setView(v => {
      const k = clamp(v.k * factor, MIN_ZOOM, MAX_ZOOM)
      return { k, x: px - ((px - v.x) / v.k) * k, y: py - ((py - v.y) / v.k) * k }
    })
  }

  // --- node dragging --------------------------------------------------------------------------------

  const dragRef = useRef<{ target: number | 'start'; sx: number; sy: number; ox: number; oy: number } | null>(null)

  const startNodeDrag = (target: number | 'start', e: ReactPointerEvent) => {
    const p = target === 'start' ? pos.start : pos.nodes[target]
    if (!p) return
    dragRef.current = { target, sx: e.clientX, sy: e.clientY, ox: p.x, oy: p.y }
    e.currentTarget.setPointerCapture(e.pointerId)
  }
  const moveNodeDrag = (e: ReactPointerEvent) => {
    const d = dragRef.current
    if (!d) return
    const x = d.ox + (e.clientX - d.sx) / view.k
    const y = d.oy + (e.clientY - d.sy) / view.k
    setPos(p => d.target === 'start'
      ? { ...p, start: { x, y } }
      : { ...p, nodes: p.nodes.map((n, i) => (i === d.target ? { x, y } : n)) })
  }
  const endNodeDrag = (e: ReactPointerEvent) => {
    dragRef.current = null
    if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId)
  }

  // --- step mutations -------------------------------------------------------------------------------

  const mutate = (fn: (c: TestCase) => TestCase) => {
    setDirty(true)
    setSuite(s => (s ? { ...s, testCases: s.testCases.map(c => (c.id === caseId ? fn(c) : c)) } : s))
  }

  const setStep = (i: number, s: Step) =>
    mutate(c => ({ ...c, steps: c.steps.map((x, idx) => (idx === i ? s : x)) }))

  const insertStep = (index: number, step: Step) => {
    mutate(c => ({ ...c, steps: [...c.steps.slice(0, index), step, ...c.steps.slice(index)] }))
    setPos(p => {
      const nodes = p.nodes.slice()
      const prev = index === 0 ? p.start : nodes[index - 1]
      const at: XY = { x: prev.x + (index === 0 ? START_W : NODE_W) + GAP_X, y: prev.y }
      // Push everything downstream along so the new node never lands on top of an existing one.
      for (let i = index; i < nodes.length; i++) nodes[i] = { ...nodes[i], x: nodes[i].x + NODE_W + GAP_X }
      nodes.splice(index, 0, at)
      return { ...p, nodes }
    })
    setSelected(index)
    setStatus({}); setResults({}); setOutcome(null)
  }

  const removeStep = (i: number) => {
    mutate(c => ({ ...c, steps: c.steps.filter((_, idx) => idx !== i) }))
    setPos(p => ({ ...p, nodes: p.nodes.filter((_, idx) => idx !== i) }))
    setSelected(null)
    setStatus({}); setResults({}); setOutcome(null)
  }

  const duplicateStep = (i: number) => {
    const src = steps[i]
    insertStep(i + 1, { ...structuredClone(src), name: `${src.name ?? `step ${i + 1}`} copy` })
  }

  /**
   * Reorders the steps only — positions stay bound to the slot, not to the step. The chain is drawn in index
   * order, so the moved step takes over the slot it lands in and the drawing stays untangled.
   */
  const moveStep = (from: number, to: number) => {
    if (to < 0 || to >= stepCount) return
    mutate(c => {
      const ns = c.steps.slice()
      const [m] = ns.splice(from, 1)
      ns.splice(to, 0, m)
      return { ...c, steps: ns }
    })
    setSelected(to)
    setStatus({}); setResults({}); setOutcome(null)
  }

  // --- save / validate / run ------------------------------------------------------------------------

  const persist = async (): Promise<Suite | null> => {
    if (!suite) return null
    setSaving(true)
    try {
      const saved = await api.saveSuite(suite)
      setSuite(saved)
      setDirty(false)
      setSavedAt(new Date().toLocaleTimeString())
      return saved
    } finally {
      setSaving(false)
    }
  }

  const validate = async () => {
    await persist()
    setOutcome(null); setRunError(null)
    setValidation(await api.validateCase(id, caseId))
  }

  const clearRunning = (s: Record<number, RunState>): Record<number, RunState> =>
    Object.fromEntries(Object.entries(s).map(([k, v]) => [k, v === 'running' ? 'idle' : v])) as Record<number, RunState>

  const run = async () => {
    if (!stepCount) return
    await persist()
    setValidation(null); setRunError(null); setOutcome(null); setResults({}); setRunning(true)
    stepCursor.current = 0
    setStatus({ 0: 'running' })
    const ctrl = new AbortController()
    abortRef.current = ctrl
    try {
      // Step events stream in execution order, so the n-th one belongs to the n-th node.
      await api.runCase(id, caseId, e => {
        if (e.type === 'step') {
          const i = stepCursor.current++
          setResults(r => ({ ...r, [i]: e.step }))
          setStatus(s => ({ ...s, [i]: e.step.passed ? 'passed' : 'failed', [i + 1]: 'running' }))
        } else if (e.type === 'result') {
          setOutcome(e.result)
          setStatus(clearRunning)
        } else if (e.type === 'error') {
          setRunError(e.errors?.join('\n') ?? e.error)
          setStatus(clearRunning)
        }
      }, ctrl.signal)
    } catch (err) {
      if (!ctrl.signal.aborted) setRunError(String((err as Error).message))
    } finally {
      setRunning(false)
      setStatus(clearRunning)
    }
  }

  // --- keyboard -------------------------------------------------------------------------------------

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const el = document.activeElement as HTMLElement | null
      if (el && (/^(INPUT|TEXTAREA|SELECT)$/.test(el.tagName) || el.isContentEditable)) return
      if (e.key === 'Escape') { setPaletteAt(null); setSelected(null) }
      else if ((e.key === 'Delete' || e.key === 'Backspace') && selected !== null) { e.preventDefault(); removeStep(selected) }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [selected, stepCount])

  if (!suite) return <div className="app"><p className="muted">Loading…</p></div>
  if (!testCase) return (
    <div className="app">
      <p className="muted">Test case not found. <Link to={`/suites/${id}`}>Back to suite</Link></p>
    </div>
  )

  const anchorOut = (i: number): XY => {
    const p = i < 0 ? pos.start : pos.nodes[i]
    return { x: p.x + (i < 0 ? START_W : NODE_W), y: p.y + PORT_Y }
  }
  const anchorIn = (i: number): XY => ({ x: pos.nodes[i].x, y: pos.nodes[i].y + PORT_Y })
  const tail = anchorOut(stepCount - 1)   // free end of the chain (the start node when there are no steps)

  const consumeTopic = suite.consumeTopics.map(t => t.topic).filter(Boolean)[0]
  const produceTopic = suite.produceTopics.map(t => t.topic).filter(Boolean)[0]

  return (
    <div className="flow-page">
      <div className="flow-topbar">
        <div className="row">
          <Link to={`/suites/${id}`} className="back-btn" title={`Back to ${suite.name}`} aria-label="Back to suite">←</Link>
          <div className="flow-title">
            <input className="flow-name" value={testCase.name}
              onChange={e => mutate(c => ({ ...c, name: e.target.value }))} />
            <div className="crumbs">
              <Link to="/">Suites</Link> / <Link to={`/suites/${id}`}>{suite.name}</Link>
            </div>
          </div>
        </div>
        <div className="row">
          {dirty ? <span className="muted small">unsaved</span>
            : savedAt && <span className="muted small">saved {savedAt}</span>}
          <button className="ghost" onClick={() => setPos(autoLayout(stepCount))} title="Re-flow the chain left to right">⌗ Tidy</button>
          <button className="ghost" onClick={validate} disabled={running}>Validate</button>
          <button className="ghost" onClick={() => persist()} disabled={saving}>{saving ? 'Saving…' : 'Save'}</button>
          {running
            ? <button className="danger" onClick={() => abortRef.current?.abort()}>■ Cancel</button>
            : <button className="primary" onClick={run} disabled={!stepCount}>▶ Run</button>}
        </div>
      </div>

      <div className="flow-body">
        <div ref={canvasRef} className="flow-canvas"
          style={{ backgroundSize: `${24 * view.k}px ${24 * view.k}px`, backgroundPosition: `${view.x}px ${view.y}px` }}
          onPointerDown={onCanvasDown} onPointerMove={onCanvasMove}
          onPointerUp={onCanvasUp} onPointerCancel={onCanvasUp}>

          <div className="flow-layer" style={{ transform: `translate(${view.x}px, ${view.y}px) scale(${view.k})` }}>
            <svg className="flow-edges" width="1" height="1">
              <defs>
                <marker id="fa" markerWidth="9" markerHeight="9" refX="7" refY="4.5" orient="auto">
                  <path d="M0,0 L9,4.5 L0,9 z" fill="var(--border)" />
                </marker>
              </defs>
              {steps.map((_, i) => {
                const a = anchorOut(i - 1)
                const b = anchorIn(i)
                const st = status[i] ?? 'idle'
                return <path key={i} d={edgePath(a, b)} className={'flow-edge ' + st} markerEnd="url(#fa)" />
              })}
              <path d={edgePath(tail, { x: tail.x + GAP_X * 0.8, y: tail.y })} className="flow-edge stub" />
            </svg>

            {/* Insert-between buttons sit on the midpoint of each edge — the n8n way to splice a node in. */}
            {steps.map((_, i) => {
              const a = anchorOut(i - 1)
              const b = anchorIn(i)
              return (
                <button key={`ins${i}`} className="flow-edge-add"
                  style={{ left: (a.x + b.x) / 2, top: (a.y + b.y) / 2 }}
                  title="Insert a step here"
                  onPointerDown={e => e.stopPropagation()}
                  onClick={() => setPaletteAt(i)}>+</button>
              )
            })}
            <button className="flow-edge-add tail"
              style={{ left: tail.x + GAP_X * 0.8, top: tail.y }}
              title="Add a step at the end"
              onPointerDown={e => e.stopPropagation()}
              onClick={() => setPaletteAt(stepCount)}>+</button>

            <StartNode suite={suite} at={pos.start}
              onPointerDown={e => { e.stopPropagation(); startNodeDrag('start', e) }}
              onPointerMove={moveNodeDrag} onPointerUp={endNodeDrag} />

            {steps.map((s, i) => (
              <FlowNode key={i} step={s} index={i} total={stepCount} at={pos.nodes[i]}
                selected={selected === i} state={status[i] ?? 'idle'} result={results[i]}
                onPointerDown={e => {
                  e.stopPropagation()
                  setSelected(i)
                  if (!(e.target as HTMLElement).closest('button, input, select, textarea, a')) startNodeDrag(i, e)
                }}
                onPointerMove={moveNodeDrag} onPointerUp={endNodeDrag}
                onMoveEarlier={() => moveStep(i, i - 1)}
                onMoveLater={() => moveStep(i, i + 1)}
                onDuplicate={() => duplicateStep(i)}
                onRemove={() => removeStep(i)} />
            ))}
          </div>

          <div className="flow-toolbar">
            <button className="ghost sm" onClick={() => zoomBy(1 / 1.2)} title="Zoom out">−</button>
            <span className="muted small" style={{ minWidth: '3.2rem', textAlign: 'center' }}>{Math.round(view.k * 100)}%</span>
            <button className="ghost sm" onClick={() => zoomBy(1.2)} title="Zoom in">+</button>
            <button className="ghost sm" onClick={fit} title="Fit to view">⤢</button>
          </div>

          {(outcome || runError || validation) && (
            <div className="flow-status">
              {validation && (validation.valid
                ? <div className="okbox">Valid — no load-time errors (no broker was contacted).</div>
                : <div className="errbox"><strong>Invalid:</strong>
                    <ul style={{ margin: '.3rem 0 0 1rem' }}>{validation.errors.map((x, i) => <li key={i}>{x}</li>)}</ul>
                  </div>)}
              {outcome && (
                <div className={outcome.passed ? 'okbox' : 'errbox'}>
                  {outcome.passed ? '✓ Passed' : '✗ Failed'} — {(outcome.steps ?? []).filter(s => s.passed).length}/{(outcome.steps ?? []).length} steps
                  {outcome.error && <div>{outcome.error}</div>}
                </div>
              )}
              {runError && <div className="errbox">{runError}</div>}
            </div>
          )}
        </div>

        <aside className="flow-inspector">
          {selected === null || !steps[selected] ? (
            <div className="flow-empty">
              <h3>Inspector</h3>
              <p className="muted small">Pick a node to edit its topics, payload, matching and validations.</p>
              <p className="muted small">
                Drag the canvas to pan, scroll to zoom, and use the <strong>+</strong> on a connection to splice a
                step in. Suite-wide Kafka settings, topics and payloads live in the{' '}
                <Link to={`/suites/${id}`}>suite configuration</Link>.
              </p>
              <button className="primary" onClick={() => setPaletteAt(stepCount)}>+ Add step</button>
            </div>
          ) : (
            <Inspector key={selected} suite={suite} step={steps[selected]} index={selected}
              result={results[selected]}
              onChange={s => setStep(selected, s)}
              onDuplicate={() => duplicateStep(selected)}
              onRemove={() => removeStep(selected)} />
          )}
        </aside>
      </div>

      {paletteAt !== null && (
        <Palette
          onClose={() => setPaletteAt(null)}
          onPick={p => { insertStep(paletteAt, p.make(paletteAt, consumeTopic, produceTopic)); setPaletteAt(null) }} />
      )}
    </div>
  )
}

// --- nodes ------------------------------------------------------------------------------------------

function StartNode({ suite, at, onPointerDown, onPointerMove, onPointerUp }: {
  suite: Suite
  at: XY
  onPointerDown: (e: ReactPointerEvent) => void
  onPointerMove: (e: ReactPointerEvent) => void
  onPointerUp: (e: ReactPointerEvent) => void
}) {
  return (
    <div className="flow-node start" style={{ left: at.x, top: at.y, width: START_W }}
      onPointerDown={onPointerDown} onPointerMove={onPointerMove} onPointerUp={onPointerUp} onPointerCancel={onPointerUp}>
      <div className="fn-head">
        <span className="fn-icon">⚡</span>
        <div className="fn-name">Start</div>
      </div>
      <div className="fn-body">
        <div className="fn-line mono">{suite.kafka.bootstrapServers?.[0] || 'no bootstrap set'}</div>
        <div className="fn-badges">
          <span className="chip">{suite.produceTopics.length} produce</span>
          <span className="chip">{suite.consumeTopics.length} consume</span>
        </div>
      </div>
      <span className="flow-port out" />
    </div>
  )
}

function FlowNode({
  step, index, total, at, selected, state, result,
  onPointerDown, onPointerMove, onPointerUp, onMoveEarlier, onMoveLater, onDuplicate, onRemove
}: {
  step: Step
  index: number
  total: number
  at: XY
  selected: boolean
  state: RunState
  result?: StepResult
  onPointerDown: (e: ReactPointerEvent) => void
  onPointerMove: (e: ReactPointerEvent) => void
  onPointerUp: (e: ReactPointerEvent) => void
  onMoveEarlier: () => void
  onMoveLater: () => void
  onDuplicate: () => void
  onRemove: () => void
}) {
  const hook = hookTypeOf(step)
  const cls = ['flow-node', NODE_KIND[hook], selected ? 'selected' : '', state !== 'idle' ? `is-${state}` : '']
    .filter(Boolean).join(' ')
  const from = step.consumeFrom ?? []
  const failures = (result?.validations ?? []).filter(v => !v.passed).length

  // Selection happens on pointerdown (see the page's handler); the node has no click handler of its own, so a
  // tool button's action is never overwritten by a bubbled re-select.
  return (
    <div className={cls} style={{ left: at.x, top: at.y, width: NODE_W }}
      onPointerDown={onPointerDown} onPointerMove={onPointerMove} onPointerUp={onPointerUp} onPointerCancel={onPointerUp}>
      <span className="flow-port in" />
      <div className="fn-head">
        <span className="fn-icon">{NODE_ICON[hook]}</span>
        <div className="fn-name">{step.name || `step ${index + 1}`}</div>
        <span className="fn-idx">{index + 1}</span>
      </div>
      <div className="fn-body">
        <div className="fn-line mono">{step.produceTo ? `→ ${step.produceTo}` : '→ —'}</div>
        <div className="fn-line mono">{from.length ? `← ${from.join(', ')}` : '← —'}</div>
        <div className="fn-badges">
          <span className="chip kind">{HOOK_TYPE_LABEL[hook]}</span>
          {!!step.validations?.length && <span className="chip">✓ {step.validations.length}</span>}
          {!!step.capture && Object.keys(step.capture).length > 0 && <span className="chip">⇱ {Object.keys(step.capture).length}</span>}
        </div>
      </div>

      {(state !== 'idle' || result) && (
        <div className={'fn-status ' + state}>
          {state === 'running' && <>⠿ waiting…</>}
          {state === 'passed' && <>✓ passed · {result?.receivedMessageCount ?? 0} msg</>}
          {state === 'failed' && <>✗ {result?.error ? shorten(result.error) : `${failures} failed check${failures === 1 ? '' : 's'}`}</>}
        </div>
      )}

      <div className="fn-tools">
        <button className="ghost sm" title="Move earlier" disabled={index === 0} onClick={onMoveEarlier}>‹</button>
        <button className="ghost sm" title="Move later" disabled={index === total - 1} onClick={onMoveLater}>›</button>
        <button className="ghost sm" title="Duplicate" onClick={onDuplicate}>⧉</button>
        <button className="danger sm" title="Delete" onClick={onRemove}>✕</button>
      </div>
      <span className="flow-port out" />
    </div>
  )
}

function shorten(s: string) { return s.length > 60 ? `${s.slice(0, 57)}…` : s }

// --- inspector --------------------------------------------------------------------------------------

function Inspector({ suite, step, index, result, onChange, onDuplicate, onRemove }: {
  suite: Suite
  step: Step
  index: number
  result?: StepResult
  onChange: (s: Step) => void
  onDuplicate: () => void
  onRemove: () => void
}) {
  const hook = hookTypeOf(step)
  const failures = (result?.validations ?? []).filter(v => !v.passed)

  return (
    <div className="flow-inspector-body">
      <div className="flow-inspector-head">
        <span className={'fn-icon ' + NODE_KIND[hook]}>{NODE_ICON[hook]}</span>
        <div style={{ flex: 1 }}>
          <input value={step.name ?? ''} placeholder={`step ${index + 1}`}
            onChange={e => onChange({ ...step, name: e.target.value })} />
          <div className="muted small" style={{ marginTop: '.25rem' }}>node {index + 1} · {HOOK_TYPE_LABEL[hook]}</div>
        </div>
      </div>

      {(result?.error || failures.length > 0) && (
        <div className="errbox" style={{ margin: '0 0 .8rem' }}>
          {result?.error && <div>{result.error}</div>}
          {failures.map((v, i) => (
            <div key={i} className="small">✗ {v.target}:{v.path} {v.type} — expected [{v.expected}], actual [{v.actual}]</div>
          ))}
        </div>
      )}

      <StepFields suite={suite} step={step} onChange={onChange} />

      <div className="row" style={{ marginTop: '1rem' }}>
        <button className="ghost sm" onClick={onDuplicate}>⧉ duplicate</button>
        <span className="spacer" />
        <button className="danger sm" onClick={onRemove}>✕ delete node</button>
      </div>
    </div>
  )
}

// --- palette ----------------------------------------------------------------------------------------

function Palette({ onPick, onClose }: { onPick: (p: typeof PRESETS[number]) => void; onClose: () => void }) {
  const [q, setQ] = useState('')
  const hits = PRESETS.filter(p => (p.title + p.blurb).toLowerCase().includes(q.toLowerCase()))
  return (
    <>
      <div className="flow-backdrop" onClick={onClose} />
      <div className="flow-palette">
        <div className="row" style={{ marginBottom: '.8rem' }}>
          <h2 style={{ flex: 1, margin: 0 }}>Add step</h2>
          <button className="ghost sm" onClick={onClose}>✕</button>
        </div>
        <input autoFocus placeholder="Search node types…" value={q} onChange={e => setQ(e.target.value)} />
        <div style={{ marginTop: '.8rem' }}>
          {hits.map(p => (
            <button key={p.id} className="palette-item" title={p.title} aria-label={p.title} onClick={() => onPick(p)}>
              <span className={'fn-icon ' + p.kind}>{p.icon}</span>
              <span>
                <strong>{p.title}</strong>
                <span className="muted small" style={{ display: 'block' }}>{p.blurb}</span>
              </span>
            </button>
          ))}
          {hits.length === 0 && <p className="muted small">No node type matches “{q}”.</p>}
        </div>
      </div>
    </>
  )
}

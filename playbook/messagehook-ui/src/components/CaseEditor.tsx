import { useEffect, useState } from 'react'
import type { DragEvent } from 'react'
import type { Suite, TestCase, Step, Validation, Send } from '../types'
import { HOOK_TYPE_LABEL, VALIDATION_TYPES, hookTypeOf } from '../types'
import { Field, KeyValueEditor, StringListEditor } from './common'

const EXPECTED_OPTIONAL = new Set(['Exists', 'NotExists'])

// Client-only render keys. Steps carry no id (and we must not add one — it would leak into the saved and
// assembled JSON), so we track a stable key per step positionally. Reorder/insert move these keys in lockstep
// with the steps, which is what lets a card's identity — its internal inline-JSON state and its collapsed
// flag — travel with the step rather than staying pinned to a slot index.
let KEY_SEQ = 0
const nextKey = () => `s${++KEY_SEQ}`

export default function CaseEditor({
  suite, testCase, onChange
}: {
  suite: Suite
  testCase: TestCase
  onChange: (c: TestCase) => void
}) {
  const steps = testCase.steps

  const [keys, setKeys] = useState<string[]>(() => steps.map(nextKey))
  // Resync when the step list is replaced from outside (suite reload, save round-trip) with a different length.
  // Reorder/insert/remove below keep keys aligned themselves, so those never hit this reset.
  useEffect(() => {
    setKeys(prev => (prev.length === steps.length ? prev : steps.map(nextKey)))
  }, [steps.length])

  // Collapse is keyed by the stable key (not the index) so a collapsed card stays collapsed after a reorder.
  // Collapsing only hides the body; it never changes the step itself.
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set())
  const toggleCollapsed = (key: string) => setCollapsed(prev => {
    const next = new Set(prev)
    next.has(key) ? next.delete(key) : next.add(key)
    return next
  })
  const setAllCollapsed = (all: boolean) => setCollapsed(all ? new Set(keys) : new Set())

  // Drag-reorder state: which card is being dragged, and which one the pointer is currently over.
  const [dragIdx, setDragIdx] = useState<number | null>(null)
  const [overIdx, setOverIdx] = useState<number | null>(null)

  const setStep = (i: number, s: Step) =>
    onChange({ ...testCase, steps: steps.map((x, idx) => (idx === i ? s : x)) }) // keys unchanged

  // Mutate steps and their keys together so the two arrays never drift apart.
  const commit = (nextSteps: Step[], nextKeys: string[]) => {
    onChange({ ...testCase, steps: nextSteps })
    setKeys(nextKeys)
  }
  const removeStep = (i: number) =>
    commit(steps.filter((_, idx) => idx !== i), keys.filter((_, idx) => idx !== i))
  const insertAt = (i: number, s: Step) => {
    const ns = steps.slice(); ns.splice(i, 0, s)
    const nk = keys.slice(); nk.splice(i, 0, nextKey())
    commit(ns, nk)
  }
  const move = (from: number, to: number) => {
    if (from === to || to < 0 || to >= steps.length) return
    const ns = steps.slice(); const [sm] = ns.splice(from, 1); ns.splice(to, 0, sm)
    const nk = keys.slice(); const [km] = nk.splice(from, 1); nk.splice(to, 0, km)
    commit(ns, nk)
  }

  // A new step starts as fire-and-forget (no consume topics yet). Picking topics is what changes its shape —
  // see hookTypeOf — so there is nothing else to seed here.
  const newStep = (): Step => ({ name: `step ${steps.length + 1}`, consumeFrom: [], timeoutSeconds: 30 })

  const endDrag = () => { setDragIdx(null); setOverIdx(null) }

  return (
    <div>
      <div className="row" style={{ marginBottom: '.8rem' }}>
        <div style={{ flex: 1 }}>
          <label>Test case name</label>
          <input value={testCase.name} onChange={e => onChange({ ...testCase, name: e.target.value })} />
        </div>
      </div>

      {steps.length > 1 && (
        <div className="row" style={{ marginBottom: '.6rem' }}>
          <button className="ghost sm" onClick={() => setAllCollapsed(true)}>collapse all</button>
          <button className="ghost sm" onClick={() => setAllCollapsed(false)}>expand all</button>
        </div>
      )}

      {steps.map((s, i) => (
        <div key={keys[i]}
          className={'step-drop' + (dragIdx !== null && dragIdx !== i && overIdx === i ? ' drag-over' : '')}
          onDragOver={e => { if (dragIdx !== null) { e.preventDefault(); e.dataTransfer.dropEffect = 'move'; setOverIdx(i) } }}
          onDrop={e => { if (dragIdx !== null) { e.preventDefault(); move(dragIdx, i) } endDrag() }}>
          <StepCard suite={suite} step={s} index={i} total={steps.length}
            collapsed={collapsed.has(keys[i])}
            dragging={dragIdx === i}
            onToggleCollapsed={() => toggleCollapsed(keys[i])}
            onChange={x => setStep(i, x)}
            onRemove={() => removeStep(i)}
            onMoveUp={() => move(i, i - 1)}
            onMoveDown={() => move(i, i + 1)}
            onInsertBelow={() => insertAt(i + 1, newStep())}
            onDragStart={e => { setDragIdx(i); e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', String(i)) }}
            onDragEnd={endDrag} />
        </div>
      ))}
      <div className="row">
        <button className="ghost" onClick={() => insertAt(steps.length, newStep())}>+ add step</button>
      </div>
    </div>
  )
}

function StepCard({
  suite, step, index, total, collapsed, dragging,
  onToggleCollapsed, onChange, onRemove, onMoveUp, onMoveDown, onInsertBelow, onDragStart, onDragEnd
}: {
  suite: Suite
  step: Step
  index: number
  total: number
  collapsed: boolean
  dragging: boolean
  onToggleCollapsed: () => void
  onChange: (s: Step) => void
  onRemove: () => void
  onMoveUp: () => void
  onMoveDown: () => void
  onInsertBelow: () => void
  onDragStart: (e: DragEvent) => void
  onDragEnd: () => void
}) {
  const produceOptions = suite.produceTopics.map(t => t.topic).filter(Boolean)
  const consumeOptions = suite.consumeTopics.map(t => t.topic).filter(Boolean)

  const sendMode: 'none' | 'file' | 'inline' =
    step.send == null ? 'none'
      : typeof step.send === 'string' ? 'file'
      : ('file' in step.send && Object.keys(step.send).length === 1) ? 'file' : 'inline'
  const [inlineText, setInlineText] = useState(() =>
    sendMode === 'inline' ? JSON.stringify(step.send, null, 2) : '{\n  \n}')
  const [inlineErr, setInlineErr] = useState<string | null>(null)

  const setSendMode = (mode: 'none' | 'file' | 'inline') => {
    if (mode === 'none') onChange({ ...step, send: undefined })
    else if (mode === 'file') onChange({ ...step, send: { file: `payloads/${suite.payloads[0] ?? ''}` } })
    else applyInline(inlineText)
  }
  const applyInline = (text: string) => {
    setInlineText(text)
    try { onChange({ ...step, send: JSON.parse(text) as Send }); setInlineErr(null) }
    catch (e) { setInlineErr((e as Error).message) }
  }

  const overrideMap = toStringMap(step.override)
  const captureMap = step.capture ?? {}
  const match = step.match ?? { mode: 'CorrelationId' as const }

  // The topics decide the shape (same rule as the engine's BaseMessageHookStep). Without consume topics the step
  // is fire-and-forget: it never waits, so the wait-side fields are not rendered at all.
  const hookType = hookTypeOf(step)
  const waits = hookType !== 'ProduceAndForget'

  // Adding or dropping consume topics is what flips the shape, so carry the rest of the step with it: a waiting
  // step needs a count of at least 1, and a fire-and-forget one receives nothing to match, capture or validate.
  const setConsumeFrom = (topics: string[]) => onChange(topics.length
    ? { ...step, consumeFrom: topics, expectedMessageCount: Math.max(1, step.expectedMessageCount ?? 1) }
    : { ...step, consumeFrom: [], expectedMessageCount: undefined, match: undefined, capture: undefined, validations: undefined })

  return (
    <div className={'step-card' + (dragging ? ' dragging' : '')}>
      <div className="step-head">
        <span className="grip" draggable onDragStart={onDragStart} onDragEnd={onDragEnd}
          title="Drag to reorder" aria-label="Drag to reorder" role="button">⠿</span>
        <button className="ghost sm caret" onClick={onToggleCollapsed}
          title={collapsed ? 'Expand step' : 'Collapse step'}
          aria-label={collapsed ? 'Expand step' : 'Collapse step'} aria-expanded={!collapsed}>
          {collapsed ? '▸' : '▾'}
        </button>
        <span className="pill">#{index + 1}</span>
        <span className="pill run">{HOOK_TYPE_LABEL[hookType]}</span>
        <input style={{ flex: 1 }} value={step.name ?? ''} placeholder="step name"
          onChange={e => onChange({ ...step, name: e.target.value })} />
        <button className="ghost sm" onClick={onMoveUp} disabled={index === 0}
          title="Move up" aria-label="Move step up">↑</button>
        <button className="ghost sm" onClick={onMoveDown} disabled={index === total - 1}
          title="Move down" aria-label="Move step down">↓</button>
        <button className="danger sm" onClick={onRemove}>remove</button>
      </div>

      {collapsed && <div className="step-summary muted small">{stepSummary(step)}</div>}

      {!collapsed && (<>
        <div className="grid cols-2">
          <Field label="Produce to (blank = consume-only)">
            <select value={step.produceTo ?? ''} onChange={e => onChange({ ...step, produceTo: e.target.value || undefined })}>
              <option value="">— none —</option>
              {produceOptions.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
          </Field>
          <Field label="Consume from (blank = produce only)">
            <StringListEditor value={step.consumeFrom ?? []} options={consumeOptions} onChange={setConsumeFrom} />
          </Field>
        </div>

        <div className="grid cols-3" style={{ marginTop: '.6rem' }}>
          <Field label="Key (placeholders ok)">
            <input value={step.key ?? ''} placeholder="{{guid}}" onChange={e => onChange({ ...step, key: e.target.value || undefined })} />
          </Field>
          {waits && (
            <>
              <Field label="Expected message count">
                {/* Min 1: a waiting step that expects 0 can never complete. */}
                <input type="number" min={1} value={step.expectedMessageCount ?? 1}
                  onChange={e => onChange({ ...step, expectedMessageCount: Math.max(1, parseInt(e.target.value || '1', 10) || 1) })} />
              </Field>
              <Field label="Timeout (s)">
                <input type="number" value={step.timeoutSeconds ?? 30}
                  onChange={e => onChange({ ...step, timeoutSeconds: parseInt(e.target.value || '30', 10) })} />
              </Field>
            </>
          )}
        </div>

        {waits && (
          <div className="grid cols-2" style={{ marginTop: '.6rem' }}>
            <Field label="Match mode">
              <select value={match.mode} onChange={e => onChange({ ...step, match: { ...match, mode: e.target.value as any } })}>
                <option value="CorrelationId">CorrelationId</option>
                <option value="MessageKey">MessageKey</option>
              </select>
            </Field>
            {match.mode === 'MessageKey' && (
              <Field label="Expected key (blank = produced key)">
                <input value={match.expectedKey ?? ''} onChange={e => onChange({ ...step, match: { ...match, expectedKey: e.target.value || undefined } })} />
              </Field>
            )}
          </div>
        )}

        {/* Send */}
        <div style={{ marginTop: '.8rem' }}>
          <label>Send payload</label>
          <div className="row" style={{ marginBottom: '.4rem' }}>
            {(['none', 'file', 'inline'] as const).map(m => (
              <button key={m} className={'sm ' + (sendMode === m ? 'primary' : 'ghost')} onClick={() => setSendMode(m)}>{m}</button>
            ))}
          </div>
          {sendMode === 'file' && (
            <select value={fileName(step.send)} onChange={e => onChange({ ...step, send: { file: `payloads/${e.target.value}` } })}>
              <option value="">— pick payload —</option>
              {suite.payloads.map(p => <option key={p} value={p}>{p}</option>)}
            </select>
          )}
          {sendMode === 'inline' && (
            <>
              <textarea rows={5} value={inlineText} onChange={e => applyInline(e.target.value)} />
              {inlineErr && <div className="errbox" style={{ marginTop: '.3rem' }}>Invalid JSON: {inlineErr}</div>}
            </>
          )}
        </div>

        {/* Override */}
        <div style={{ marginTop: '.8rem' }}>
          <label>Override (patch payload paths, or define {'{{'}vars{'}}'})</label>
          <KeyValueEditor value={overrideMap} keyPlaceholder="path or var" valuePlaceholder="value"
            onChange={v => onChange({ ...step, override: Object.keys(v).length ? v : undefined })} />
        </div>

        {/* Capture and Validations read the consumed message, so they don't apply to a produce-only step. */}
        {waits && (
          <>
            <div style={{ marginTop: '.8rem' }}>
              <label>Capture (variable ← payload path)</label>
              <KeyValueEditor value={captureMap} keyPlaceholder="varName" valuePlaceholder="path.in.payload"
                onChange={v => onChange({ ...step, capture: Object.keys(v).length ? v : undefined })} />
            </div>

            <div style={{ marginTop: '.8rem' }}>
              <label>Validations</label>
              <ValidationRows validations={step.validations ?? []} onChange={v => onChange({ ...step, validations: v })} />
            </div>
          </>
        )}
      </>)}

      {/* Insert a fresh step directly after this one — the way to add a step between two existing ones. */}
      <div className="step-insert">
        <button className="ghost sm" onClick={onInsertBelow} title="Insert a new step below this one">+ insert step below</button>
      </div>
    </div>
  )
}

// One-line gist of a step for the collapsed view, so a folded card still says what it does.
function stepSummary(step: Step): string {
  const parts: string[] = []
  if (step.produceTo) parts.push(`→ ${step.produceTo}`)
  const from = step.consumeFrom ?? []
  if (from.length) parts.push(`← ${from.join(', ')}`)
  const n = step.validations?.length ?? 0
  if (n) parts.push(`${n} validation${n === 1 ? '' : 's'}`)
  return parts.length ? parts.join('  ·  ') : 'no topics set'
}

function ValidationRows({ validations, onChange }: { validations: Validation[]; onChange: (v: Validation[]) => void }) {
  const set = (i: number, v: Validation) => onChange(validations.map((x, idx) => (idx === i ? v : x)))
  const remove = (i: number) => onChange(validations.filter((_, idx) => idx !== i))
  const add = () => onChange([...validations, { target: 'Value', path: '', type: 'Equals', expected: '' }])

  return (
    <div>
      {validations.map((v, i) => {
        const needsExpected = !EXPECTED_OPTIONAL.has(v.type)
        return (
          <div className="val-row" key={i}>
            <select value={v.target ?? 'Value'} onChange={e => set(i, { ...v, target: e.target.value as any })}>
              <option value="Value">Value</option>
              <option value="Key">Key</option>
            </select>
            <input placeholder="path (a.b[0].c)" value={v.path ?? ''} onChange={e => set(i, { ...v, path: e.target.value })} />
            <select value={v.type} onChange={e => set(i, { ...v, type: e.target.value })}>
              {VALIDATION_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
            <input placeholder={needsExpected ? 'expected' : '(not used)'} disabled={!needsExpected}
              value={(v.expected as string) ?? ''} onChange={e => set(i, { ...v, expected: e.target.value })} />
            <button className="danger sm" onClick={() => remove(i)}>✕</button>
          </div>
        )
      })}
      <button className="ghost sm" onClick={add}>+ add validation</button>
    </div>
  )
}

function fileName(send?: Send): string {
  if (typeof send === 'string') return send.replace(/^payloads\//, '')
  if (send && typeof send === 'object' && 'file' in send) return String((send as any).file).replace(/^payloads\//, '')
  return ''
}

function toStringMap(o?: Record<string, unknown>): Record<string, string> {
  if (!o) return {}
  return Object.fromEntries(Object.entries(o).map(([k, v]) => [k, typeof v === 'string' ? v : JSON.stringify(v)]))
}

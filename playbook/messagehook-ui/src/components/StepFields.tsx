import { useState } from 'react'
import type { Suite, Step, Validation, Send } from '../types'
import { VALIDATION_TYPES, hookTypeOf } from '../types'
import { Field, KeyValueEditor, StringListEditor } from './common'

const EXPECTED_OPTIONAL = new Set(['Exists', 'NotExists'])

/**
 * Every editable field of a single step, used by the flow editor's inspector panel. Layout is grid-based; a
 * narrow host can collapse the columns with CSS (see .flow-inspector in styles.css).
 */
export default function StepFields({
  suite, step, onChange
}: {
  suite: Suite
  step: Step
  onChange: (s: Step) => void
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
  const waits = hookTypeOf(step) !== 'ProduceAndForget'

  // Adding or dropping consume topics is what flips the shape, so carry the rest of the step with it: a waiting
  // step needs a count of at least 1, and a fire-and-forget one receives nothing to match, capture or validate.
  const setConsumeFrom = (topics: string[]) => onChange(topics.length
    ? { ...step, consumeFrom: topics, expectedMessageCount: Math.max(1, step.expectedMessageCount ?? 1) }
    : { ...step, consumeFrom: [], expectedMessageCount: undefined, match: undefined, capture: undefined, validations: undefined })

  return (
    <>
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
    </>
  )
}

/** One-line gist of a step — used by the collapsed step card and by the flow node body. */
export function stepSummary(step: Step): string {
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

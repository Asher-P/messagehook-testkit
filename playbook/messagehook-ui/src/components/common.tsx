import type { ReactNode } from 'react'

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <label>{label}</label>
      {children}
    </div>
  )
}

/** Edits a Record<string,string> as key/value rows. Values are strings (used for headers, capture, override). */
export function KeyValueEditor({
  value, onChange, keyPlaceholder = 'key', valuePlaceholder = 'value'
}: {
  value: Record<string, string>
  onChange: (v: Record<string, string>) => void
  keyPlaceholder?: string
  valuePlaceholder?: string
}) {
  const entries = Object.entries(value)
  const setEntry = (i: number, k: string, v: string) => {
    const next: Record<string, string> = {}
    entries.forEach(([ek, ev], idx) => {
      const kk = idx === i ? k : ek
      const vv = idx === i ? v : ev
      if (kk) next[kk] = vv
    })
    onChange(next)
  }
  const remove = (i: number) => onChange(Object.fromEntries(entries.filter((_, idx) => idx !== i)))
  const add = () => onChange({ ...value, '': '' })

  return (
    <div>
      {entries.map(([k, v], i) => (
        <div className="kv-row" key={i}>
          <input value={k} placeholder={keyPlaceholder} onChange={e => setEntry(i, e.target.value, v)} />
          <input value={v} placeholder={valuePlaceholder} onChange={e => setEntry(i, k, e.target.value)} />
          <button className="danger sm" onClick={() => remove(i)}>✕</button>
        </div>
      ))}
      <button className="ghost sm" onClick={add}>+ add</button>
    </div>
  )
}

/** Edits a string[] as comma-free chips with add/remove. */
export function StringListEditor({
  value, onChange, options, placeholder
}: {
  value: string[]
  onChange: (v: string[]) => void
  options?: string[]
  placeholder?: string
}) {
  const set = (i: number, v: string) => onChange(value.map((x, idx) => (idx === i ? v : x)))
  const remove = (i: number) => onChange(value.filter((_, idx) => idx !== i))
  return (
    <div>
      {value.map((v, i) => (
        <div className="row" key={i} style={{ marginBottom: '.4rem' }}>
          {options ? (
            <select value={v} onChange={e => set(i, e.target.value)}>
              <option value="">— pick —</option>
              {options.map(o => <option key={o} value={o}>{o}</option>)}
            </select>
          ) : (
            <input value={v} placeholder={placeholder} onChange={e => set(i, e.target.value)} />
          )}
          <button className="danger sm" onClick={() => remove(i)}>✕</button>
        </div>
      ))}
      <button className="ghost sm" onClick={() => onChange([...value, ''])}>+ add</button>
    </div>
  )
}

export function Toast({ text }: { text: string }) {
  return <div className="toast">{text}</div>
}

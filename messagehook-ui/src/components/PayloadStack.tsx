import { useRef, useState } from 'react'
import { api } from '../api'

export default function PayloadStack({
  suiteId, payloads, onChange
}: {
  suiteId: string
  payloads: string[]
  onChange: () => void
}) {
  const [drag, setDrag] = useState(false)
  const [preview, setPreview] = useState<{ name: string; text: string } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const upload = async (files: FileList | null) => {
    if (!files) return
    setError(null)
    try {
      for (const file of Array.from(files)) await api.uploadPayload(suiteId, file)
      onChange()
    } catch (e) {
      setError(String((e as Error).message))
    }
  }

  const remove = async (name: string) => {
    if (!confirm(`Delete payload "${name}"?`)) return
    await api.deletePayload(suiteId, name)
    if (preview?.name === name) setPreview(null)
    onChange()
  }

  const show = async (name: string) => {
    const text = await api.readPayload(suiteId, name)
    setPreview({ name, text })
  }

  return (
    <div className="card">
      <h2>Payload stack</h2>
      <p className="muted small">Upload message JSONs from your PC. They are stored with the suite and referenced by
        a step's <span className="mono">Send</span>.</p>

      <div
        className={'dropzone' + (drag ? ' drag' : '')}
        onClick={() => inputRef.current?.click()}
        onDragOver={e => { e.preventDefault(); setDrag(true) }}
        onDragLeave={() => setDrag(false)}
        onDrop={e => { e.preventDefault(); setDrag(false); upload(e.dataTransfer.files) }}
      >
        Drop a .json file here, or click to choose
        <input ref={inputRef} type="file" accept="application/json,.json" multiple hidden
          onChange={e => upload(e.target.files)} />
      </div>

      {error && <div className="errbox" style={{ marginTop: '.6rem' }}>{error}</div>}

      <div style={{ marginTop: '.8rem' }}>
        {payloads.length === 0 && <p className="muted small">No payloads uploaded.</p>}
        {payloads.map(name => (
          <div className="list-item" key={name}>
            <span className="name mono">{name}</span>
            <button className="ghost sm" onClick={() => show(name)}>preview</button>
            <button className="danger sm" onClick={() => remove(name)}>✕</button>
          </div>
        ))}
      </div>

      {preview && (
        <div style={{ marginTop: '.8rem' }}>
          <div className="row"><h3 style={{ flex: 1 }}>{preview.name}</h3>
            <button className="ghost sm" onClick={() => setPreview(null)}>close</button></div>
          <div className="jsonview">{preview.text}</div>
        </div>
      )}
    </div>
  )
}

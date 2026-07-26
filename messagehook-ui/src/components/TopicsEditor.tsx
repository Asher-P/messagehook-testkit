import type { TopicDeclaration } from '../types'

const SERIALIZERS = ['Json', 'Protobuf']

function TopicRow({
  topic, consumer, onChange, onRemove
}: {
  topic: TopicDeclaration
  consumer: boolean
  onChange: (t: TopicDeclaration) => void
  onRemove: () => void
}) {
  return (
    <div className="step-card" style={{ marginBottom: '.5rem' }}>
      <div className="row wrap">
        <div style={{ flex: '1 1 140px' }}>
          <label>Topic</label>
          <input value={topic.topic} onChange={e => onChange({ ...topic, topic: e.target.value })} />
        </div>
        <div style={{ flex: '0 0 130px' }}>
          <label>Serializer</label>
          <select value={topic.serializer ?? 'Json'} onChange={e => onChange({ ...topic, serializer: e.target.value })}>
            {SERIALIZERS.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>
        <div style={{ flex: '2 1 200px' }}>
          <label>messageType (optional; required for Protobuf)</label>
          <input className="mono" placeholder="Ns.Type, Assembly"
            value={topic.messageType ?? ''}
            onChange={e => onChange({ ...topic, messageType: e.target.value || undefined })} />
        </div>
        {consumer && (
          <>
            <div style={{ flex: '0 0 90px' }}>
              <label>workers</label>
              <input type="number" value={topic.workersCount ?? ''} onChange={e => onChange({ ...topic, workersCount: numOrUndef(e.target.value) })} />
            </div>
            <div style={{ flex: '0 0 90px' }}>
              <label>buffer</label>
              <input type="number" value={topic.bufferSize ?? ''} onChange={e => onChange({ ...topic, bufferSize: numOrUndef(e.target.value) })} />
            </div>
          </>
        )}
        <button className="danger sm" onClick={onRemove} style={{ alignSelf: 'flex-end' }}>✕</button>
      </div>
    </div>
  )
}

export default function TopicsEditor({
  title, consumer, topics, onChange
}: {
  title: string
  consumer: boolean
  topics: TopicDeclaration[]
  onChange: (t: TopicDeclaration[]) => void
}) {
  const set = (i: number, t: TopicDeclaration) => onChange(topics.map((x, idx) => (idx === i ? t : x)))
  const remove = (i: number) => onChange(topics.filter((_, idx) => idx !== i))
  const add = () => onChange([...topics, { topic: '', serializer: 'Json' }])

  return (
    <div className="card">
      <h2>{title}</h2>
      {topics.length === 0 && <p className="muted small">None declared.</p>}
      {topics.map((t, i) => (
        <TopicRow key={i} topic={t} consumer={consumer} onChange={x => set(i, x)} onRemove={() => remove(i)} />
      ))}
      <button className="ghost sm" onClick={add}>+ add topic</button>
    </div>
  )
}

function numOrUndef(v: string): number | undefined {
  const n = parseInt(v, 10)
  return isNaN(n) ? undefined : n
}

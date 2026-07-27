import type { KafkaConfig, Credentials } from '../types'
import { Field, StringListEditor } from './common'

const PROTOCOLS = ['Plaintext', 'Ssl', 'SaslPlaintext', 'SaslSsl']
const MECHANISMS = ['Plain', 'ScramSha256', 'ScramSha512']

export default function KafkaForm({ kafka, onChange }: { kafka: KafkaConfig; onChange: (k: KafkaConfig) => void }) {
  const creds: Credentials = kafka.credentials ?? {}
  const setCreds = (c: Partial<Credentials>) => onChange({ ...kafka, credentials: { ...creds, ...c } })

  return (
    <div className="card">
      <h2>Kafka</h2>
      <p className="muted small">The service connects to an existing broker — it never provisions one. From inside a
        container, a broker on the host is usually <span className="mono">host.docker.internal:9092</span>.</p>

      <div className="grid cols-2">
        <Field label="Bootstrap servers">
          <StringListEditor
            value={kafka.bootstrapServers}
            onChange={v => onChange({ ...kafka, bootstrapServers: v })}
            placeholder="host:9092"
          />
        </Field>
        <Field label="Consumer group (base)">
          <input
            value={kafka.consumerGroup ?? ''}
            placeholder="messagehook-playbook"
            onChange={e => onChange({ ...kafka, consumerGroup: e.target.value })}
          />
        </Field>
      </div>

      <div className="grid cols-3" style={{ marginTop: '.8rem' }}>
        <Field label="Security protocol">
          <select value={creds.securityProtocol ?? 'Plaintext'} onChange={e => setCreds({ securityProtocol: e.target.value })}>
            {PROTOCOLS.map(p => <option key={p} value={p}>{p}</option>)}
          </select>
        </Field>
        <Field label="SASL mechanism">
          <select value={creds.mechanism ?? ''} onChange={e => setCreds({ mechanism: e.target.value || undefined })}>
            <option value="">— none —</option>
            {MECHANISMS.map(m => <option key={m} value={m}>{m}</option>)}
          </select>
        </Field>
        <Field label="TLS">
          <label className="row" style={{ marginTop: '.4rem' }}>
            <input type="checkbox" style={{ width: 'auto' }} checked={!!creds.tlsEnabled}
              onChange={e => setCreds({ tlsEnabled: e.target.checked })} />
            <span className="small">TLS enabled</span>
          </label>
        </Field>
      </div>

      <div className="grid cols-2" style={{ marginTop: '.8rem' }}>
        <Field label="SASL username">
          <input value={creds.username ?? ''} onChange={e => setCreds({ username: e.target.value || undefined })} />
        </Field>
        <Field label="SASL password">
          <input type="password" value={creds.password ?? ''} onChange={e => setCreds({ password: e.target.value || undefined })} />
        </Field>
      </div>
      <p className="muted small" style={{ marginTop: '.5rem' }}>
        Values may use <span className="mono">{'${ENV}'}</span> / <span className="mono">{'${ENV:default}'}</span> placeholders.
      </p>
    </div>
  )
}

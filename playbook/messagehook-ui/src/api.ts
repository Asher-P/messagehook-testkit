import type { Suite, SuiteSummary, RunEvent, TopicDeclaration } from './types'

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}: ${await res.text()}`)
  return res.json() as Promise<T>
}

// The API serializes a plain topic as the bare-string shorthand ("A"); normalize back to objects for editing.
function normalizeTopics(arr: unknown): TopicDeclaration[] {
  return ((arr as unknown[]) ?? []).map(t =>
    typeof t === 'string' ? { topic: t, serializer: 'Json' } : (t as TopicDeclaration))
}
function normalizeSuite(s: Suite): Suite {
  s.consumeTopics = normalizeTopics(s.consumeTopics)
  s.produceTopics = normalizeTopics(s.produceTopics)
  return s
}

export const api = {
  listSuites: () => fetch('/api/suites').then(json<SuiteSummary[]>),

  createSuite: (name: string) =>
    fetch('/api/suites', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name })
    }).then(json<Suite>),

  getSuite: (id: string) => fetch(`/api/suites/${id}`).then(json<Suite>).then(normalizeSuite),

  saveSuite: (suite: Suite) =>
    fetch(`/api/suites/${suite.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(suite)
    }).then(json<Suite>).then(normalizeSuite),

  deleteSuite: (id: string) => fetch(`/api/suites/${id}`, { method: 'DELETE' }),

  listPayloads: (id: string) => fetch(`/api/suites/${id}/payloads`).then(json<string[]>),

  uploadPayload: async (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    const res = await fetch(`/api/suites/${id}/payloads`, { method: 'POST', body: form })
    if (!res.ok) throw new Error((await res.json().catch(() => ({}))).error ?? `Upload failed (${res.status})`)
    return res.json() as Promise<{ name: string }>
  },

  readPayload: (id: string, name: string) =>
    fetch(`/api/suites/${id}/payloads/${encodeURIComponent(name)}`).then(r => r.text()),

  deletePayload: (id: string, name: string) =>
    fetch(`/api/suites/${id}/payloads/${encodeURIComponent(name)}`, { method: 'DELETE' }),

  validateCase: (suiteId: string, caseId: string) =>
    fetch(`/api/suites/${suiteId}/testcases/${caseId}/validate`, { method: 'POST' })
      .then(json<{ valid: boolean; errors: string[] }>),

  // Streams NDJSON run events; calls onEvent for each. Returns when the stream ends.
  runCase: async (suiteId: string, caseId: string, onEvent: (e: RunEvent) => void, signal?: AbortSignal) => {
    const res = await fetch(`/api/suites/${suiteId}/testcases/${caseId}/run`, { method: 'POST', signal })
    await consumeNdjson(res, onEvent)
  },

  runSuite: async (suiteId: string, onEvent: (e: RunEvent) => void, signal?: AbortSignal) => {
    const res = await fetch(`/api/suites/${suiteId}/run`, { method: 'POST', signal })
    await consumeNdjson(res, onEvent)
  }
}

async function consumeNdjson(res: Response, onEvent: (e: RunEvent) => void) {
  if (!res.body) throw new Error('No response stream')
  const reader = res.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  for (;;) {
    const { value, done } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    let nl: number
    while ((nl = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, nl).trim()
      buffer = buffer.slice(nl + 1)
      if (line) onEvent(JSON.parse(line) as RunEvent)
    }
  }
  const tail = buffer.trim()
  if (tail) onEvent(JSON.parse(tail) as RunEvent)
}

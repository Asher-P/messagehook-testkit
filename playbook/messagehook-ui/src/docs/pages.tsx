import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

export interface DocPage {
  slug: string
  title: string
  summary: string
  body: ReactNode
}

export interface DocSection {
  title: string
  slugs: string[]
}

function Code({ children }: { children: string }) {
  return <pre className="jsonview">{children.trim()}</pre>
}

function P({ children }: { children: ReactNode }) {
  return <p style={{ marginBottom: '.8rem' }}>{children}</p>
}

const M = ({ children }: { children: ReactNode }) => <span className="mono">{children}</span>

export const DOC_PAGES: DocPage[] = [
  {
    slug: 'overview',
    title: 'Overview',
    summary: 'What the Playbook UI is and how the pieces fit together.',
    body: (
      <>
        <P>
          The Playbook UI builds and runs Kafka integration tests without writing C#. It edits JSON
          <M> playbooks</M> — the same format the <M>MessageHook.Playbook</M> engine consumes — and streams
          results back from a live run.
        </P>
        <P>Three concepts make up everything you see in the app:</P>
        <table className="table">
          <thead><tr><th>Concept</th><th>What it holds</th><th>Where you edit it</th></tr></thead>
          <tbody>
            <tr><td><strong>Suite</strong></td><td>Kafka connection, declared topics, uploaded payloads, and the test cases that share them</td><td><Link to="/docs/suites-board">Suites board</Link> + suite Configuration tab</td></tr>
            <tr><td><strong>Test case</strong></td><td>An ordered list of steps that exercise the suite's topics</td><td>Suite editor's Test cases tab</td></tr>
            <tr><td><strong>Step</strong></td><td>One produce and/or consume action, plus matching, capture, and validations</td><td>Test case page</td></tr>
          </tbody>
        </table>
        <P>
          A suite's Kafka config, topics, and payloads are shared by every test case inside it — configure them
          once on the <M>Configuration</M> tab, then build as many cases as you need on the <M>Test cases</M> tab.
        </P>
      </>
    )
  },
  {
    slug: 'suites-board',
    title: 'Suites board',
    summary: 'The home page: create, open, and delete suites.',
    body: (
      <>
        <P>The board (<M>/</M>) lists every suite with its case count, payload count, and bootstrap servers.</P>
        <ul>
          <li><strong>+ New suite</strong> — prompts for a name, creates an empty suite, and opens its editor.</li>
          <li>Click a suite's name to open its editor.</li>
          <li>The <M>✕</M> button deletes a suite after confirmation — this also removes its test cases and
            uploaded payloads, so there is no undo.</li>
        </ul>
        <P>An empty board shows a hint instead of an empty table; there is nothing to configure here beyond
          creating your first suite.</P>
      </>
    )
  },
  {
    slug: 'kafka-config',
    title: 'Kafka configuration',
    summary: 'Bootstrap servers, security protocol, credentials, and env placeholders.',
    body: (
      <>
        <P>
          Set on a suite's <M>Configuration</M> tab. The service only ever <em>connects</em> to a broker — it
          never provisions one. From inside a container, a broker on the host machine is usually
          reached at <M>host.docker.internal:9092</M>.
        </P>
        <table className="table">
          <thead><tr><th>Field</th><th>Notes</th></tr></thead>
          <tbody>
            <tr><td>Bootstrap servers</td><td>One or more <M>host:port</M> entries</td></tr>
            <tr><td>Consumer group (base)</td><td>Prefix for the consumer group the run uses</td></tr>
            <tr><td>Security protocol</td><td><M>Plaintext</M> / <M>Ssl</M> / <M>SaslPlaintext</M> / <M>SaslSsl</M></td></tr>
            <tr><td>SASL mechanism</td><td><M>Plain</M> / <M>ScramSha256</M> / <M>ScramSha512</M> (only when SASL is in use)</td></tr>
            <tr><td>TLS</td><td>Checkbox for <M>TlsEnabled</M></td></tr>
            <tr><td>SASL username / password</td><td>Rendered as a password field; stored on the suite</td></tr>
          </tbody>
        </table>
        <P>
          Any value here — bootstrap servers, username, password — may use{' '}
          <M>{'${ENV}'}</M> or <M>{'${ENV:default}'}</M> placeholders, resolved at run time from the
          process environment. This keeps secrets and per-environment hosts out of the saved suite JSON:
        </P>
        <Code>{`{
  "bootstrapServers": [ "\${KAFKA_BOOTSTRAP:localhost:9092}" ],
  "credentials": { "securityProtocol": "Plaintext", "tlsEnabled": false },
  "consumerGroup": "messagehook-playbook"
}`}</Code>
      </>
    )
  },
  {
    slug: 'topics',
    title: 'Topics',
    summary: 'Declaring consume and produce topics for a suite.',
    body: (
      <>
        <P>
          The Configuration tab has two topic lists, <strong>Consume topics</strong> and{' '}
          <strong>Produce topics</strong>. They are declared once for the whole suite — every step's{' '}
          <M>Produce to</M> / <M>Consume from</M> picker only offers topics declared here.
        </P>
        <table className="table">
          <thead><tr><th>Field</th><th>Applies to</th><th>Notes</th></tr></thead>
          <tbody>
            <tr><td>Topic</td><td>both</td><td>Kafka topic name</td></tr>
            <tr><td>Serializer</td><td>both</td><td><M>Json</M> (default) or <M>Protobuf</M>. Consuming is Json-only — Protobuf is produce-only.</td></tr>
            <tr><td>messageType</td><td>both</td><td>Assembly-qualified .NET type (<M>Ns.Type, Assembly</M>). Required for Protobuf, optional otherwise (a schema-less <M>Dictionary&lt;string, object&gt;</M> is used when omitted).</td></tr>
            <tr><td>workers / buffer</td><td>consume only</td><td>Consumer worker count and buffer size for that topic</td></tr>
          </tbody>
        </table>
        <P>Removing every consume topic from a suite means a step can no longer be built as consume-only or
          produce-and-wait — see <Link to="/docs/step-shapes">Step shapes</Link>.</P>
      </>
    )
  },
  {
    slug: 'payloads',
    title: 'Payload stack',
    summary: 'Uploading, previewing, and referencing JSON message bodies.',
    body: (
      <>
        <P>
          The <strong>Payload stack</strong> (Configuration tab) stores JSON files with the suite. Drop one or
          more <M>.json</M> files on the dropzone, or click it to choose files. Each uploaded file is a
          candidate for a step's <M>Send</M> in <em>file</em> mode — see <Link to="/docs/send-payload">Send payload</Link>.
        </P>
        <ul>
          <li><strong>preview</strong> — fetches and shows the raw file content.</li>
          <li><strong>✕</strong> — deletes the payload after confirmation. A step still referencing it by name
            will fail to resolve at run time.</li>
        </ul>
        <P>The original uploaded file is never mutated by a run — per-step edits go through <M>Override</M>
          instead, which patches a copy at run time.</P>
      </>
    )
  },
  {
    slug: 'test-cases',
    title: 'Test cases',
    summary: 'Building and organizing the steps inside a test case.',
    body: (
      <>
        <P>
          The suite editor's <strong>Test cases</strong> tab lists every case with its last run status
          (idle / running / passed / failed). <M>+ add case</M> creates an empty case and opens it;
          <M> ▶ Run</M> runs a single case inline without leaving the list; <M>▶ Run all</M> runs every case
          in the suite in order.
        </P>
        <P>Opening a case (its name, or the <M>edit</M> button) goes to the case page, which has three parts:</P>
        <ul>
          <li>Name and description.</li>
          <li>The step list — reorderable, collapsible cards; see <Link to="/docs/step-shapes">Step shapes</Link>.</li>
          <li>The <Link to="/docs/running">Run panel</Link> and a read-only assembled-playbook JSON preview.</li>
        </ul>
        <P>Within the step list:</P>
        <ul>
          <li><strong>drag handle (⠿)</strong> — reorder steps by dragging.</li>
          <li><strong>▾ / ▸</strong> — collapse a card to a one-line summary, or expand it; <M>collapse all</M> /{' '}
            <M>expand all</M> apply to every step at once.</li>
          <li><strong>↑ / ↓</strong> — nudge a step one position at a time.</li>
          <li><strong>+ insert step below</strong> — appears on hover at the bottom of a card, inserting a new
            step directly after it (the way to add a step in the middle of a case).</li>
          <li><strong>+ add step</strong> — appends a new step at the end.</li>
        </ul>
      </>
    )
  },
  {
    slug: 'step-shapes',
    title: 'Step shapes',
    summary: 'How Produce to / Consume from decide what a step does.',
    body: (
      <>
        <P>
          A step's shape is decided entirely by which of <M>Produce to</M> / <M>Consume from</M> it has — the
          same rule the engine uses. There is no separate "step type" field; picking topics <em>is</em> what
          changes the card. The pill next to the step number reflects the current shape.
        </P>
        <table className="table">
          <thead><tr><th>Shape (pill label)</th><th>Produce to</th><th>Consume from</th><th>Behavior</th></tr></thead>
          <tbody>
            <tr><td>produce &amp; consume</td><td>yes</td><td>yes</td><td>Produces, then waits for and validates a matching response.</td></tr>
            <tr><td>produce only</td><td>yes</td><td>—</td><td>Fire-and-forget: produces and moves on immediately. No wait, no <M>Validations</M>, no <M>Capture</M> — those fields are hidden for this shape (the engine rejects them at load time).</td></tr>
            <tr><td>consume only</td><td>—</td><td>yes</td><td>Waits for a message without producing one. Requires <M>Match mode: MessageKey</M> — <M>CorrelationId</M> needs a produced correlation header, which this shape never sends.</td></tr>
          </tbody>
        </table>
        <P>
          A new step starts as <em>produce only</em> (no consume topics). Adding a consume topic reveals the
          waiting-related fields (expected count, timeout, match, capture, validations) and sets a sensible
          default expected count of 1; clearing all consume topics hides and clears them again.
        </P>
        <P>Use a produce-only step to seed state, then assert on it from a later step:</P>
        <Code>{`{
  "name": "seed cat",
  "produceTo": "A",
  "key": "{{guid}}",
  "send": { "file": "payloads/animal.json" },
  "override": { "name": "cat" }
}`}</Code>
      </>
    )
  },
  {
    slug: 'send-payload',
    title: 'Send payload',
    summary: 'none / file / inline — what a produce step actually sends.',
    body: (
      <>
        <P>The <strong>Send payload</strong> control on a step has three modes:</P>
        <table className="table">
          <thead><tr><th>Mode</th><th>Result</th></tr></thead>
          <tbody>
            <tr><td>none</td><td>No body is produced (rarely useful outside a consume-only step, which never sends anyway).</td></tr>
            <tr><td>file</td><td>Pick an uploaded payload from the suite's <Link to="/docs/payloads">payload stack</Link>. Stored as <M>{'{ file: "payloads/<name>" }'}</M>.</td></tr>
            <tr><td>inline</td><td>Type a raw JSON object directly into the step. Invalid JSON is flagged inline and the last valid value is kept until it parses again.</td></tr>
          </tbody>
        </table>
        <P>Either way, the payload on disk is never changed — <M>Override</M> (next page) patches a copy of it
          for this step only.</P>
      </>
    )
  },
  {
    slug: 'matching',
    title: 'Matching a response',
    summary: 'CorrelationId vs. MessageKey for steps that consume.',
    body: (
      <>
        <P>Only steps that consume (produce &amp; consume, or consume only) have a <strong>Match mode</strong>:</P>
        <table className="table">
          <thead><tr><th>Mode</th><th>Requires</th><th>How it matches</th></tr></thead>
          <tbody>
            <tr><td>CorrelationId <span className="small muted">(default)</span></td><td><M>Produce to</M></td><td>The engine injects a correlation header on produce and waits for a response carrying it.</td></tr>
            <tr><td>MessageKey</td><td>nothing extra</td><td>Waits for a message whose Kafka key equals <M>Expected key</M> (blank = the key this step produced). The only mode valid on a consume-only step.</td></tr>
          </tbody>
        </table>
        <P>
          <strong>Expected message count</strong> and <strong>Timeout (s)</strong> only apply to consuming
          steps and default to <M>1</M> and <M>30</M>.
        </P>
      </>
    )
  },
  {
    slug: 'overrides-placeholders',
    title: 'Override and placeholders',
    summary: 'Patching payloads, defining variables, and template syntax.',
    body: (
      <>
        <P>
          <strong>Override</strong> is the single mechanism for both patching a payload and defining a
          reusable value — the same key/value list does double duty:
        </P>
        <ul>
          <li>An entry whose key matches a <strong>payload path</strong> (<M>owner.city</M>, <M>tags[0]</M>)
            replaces that value in a copy of the sent payload.</li>
          <li>An entry that matches nothing in the payload is a <strong>placeholder value</strong>, usable
            anywhere in the case as <M>{'{{name}}'}</M>.</li>
        </ul>
        <P>
          A suite/test-case can also carry a <M>strictOverride</M> flag (visible in the assembled playbook
          preview) — when set, an override entry that matches neither a payload path nor any placeholder use
          becomes a load-time error instead of being silently ignored.
        </P>
        <P>Any string field — <M>Key</M>, an override value, a header — may contain these placeholders:</P>
        <table className="table">
          <thead><tr><th>Placeholder</th><th>Resolves to</th></tr></thead>
          <tbody>
            <tr><td><M>{'${ENV}'}</M> / <M>{'${ENV:default}'}</M></td><td>Process environment variable, with an optional default</td></tr>
            <tr><td><M>{'{{guid}}'}</M></td><td>A freshly generated GUID</td></tr>
            <tr><td><M>{'{{now}}'}</M></td><td>The current timestamp</td></tr>
            <tr><td><M>{'{{name}}'}</M></td><td>An <M>Override</M> placeholder value, or a variable pulled in by an earlier step's <M>Capture</M></td></tr>
          </tbody>
        </table>
        <Code>{`{
  "key": "{{guid}}",
  "override": { "id": "{{guid}}", "name": "Buddy" }
}`}</Code>
      </>
    )
  },
  {
    slug: 'capture-validations',
    title: 'Capture and validations',
    summary: 'Pulling values out of a response and asserting on it.',
    body: (
      <>
        <P>
          Both fields only appear on a step that consumes — a produce-only step gets nothing back to read,
          so the engine treats <M>Capture</M> or <M>Validations</M> on it as a load-time error, and the UI
          simply hides the fields for that shape.
        </P>
        <P>
          <strong>Capture</strong> pulls a value out of the received message into a named variable, usable
          as <M>{'{{name}}'}</M> in any later step:
        </P>
        <Code>{`"capture": { "echoedId": "id" }`}</Code>
        <P><strong>Validations</strong> are a list of assertions against the received message:</P>
        <table className="table">
          <thead><tr><th>Field</th><th>Meaning</th></tr></thead>
          <tbody>
            <tr><td>Target</td><td><M>Value</M> (the message body) or <M>Key</M> (the Kafka key)</td></tr>
            <tr><td>Path</td><td>Dotted/indexed path into the target, e.g. <M>a.b[0].c</M>. Empty or <M>$</M> means the whole target.</td></tr>
            <tr><td>Type</td><td>See below</td></tr>
            <tr><td>Expected</td><td>Comparison value — disabled for types that don't need one</td></tr>
          </tbody>
        </table>
        <P>Validation types:</P>
        <table className="table">
          <thead><tr><th>Type</th><th>Needs "expected"?</th></tr></thead>
          <tbody>
            <tr><td>Equals / NotEquals</td><td>yes</td></tr>
            <tr><td>Contains / NotContains</td><td>yes</td></tr>
            <tr><td>Exists / NotExists</td><td>no — the path's presence is the whole check</td></tr>
            <tr><td>Matches</td><td>yes (regex)</td></tr>
            <tr><td>GreaterThan / LessThan</td><td>yes</td></tr>
            <tr><td>Count</td><td>yes — for a path that resolves to an array</td></tr>
          </tbody>
        </table>
      </>
    )
  },
  {
    slug: 'running',
    title: 'Running and results',
    summary: 'Validate vs. Run, streamed step results, and cancellation.',
    body: (
      <>
        <P>The Run panel on a test case page (and the Run / Run all buttons on the case list) both save the
          suite first, then act:</P>
        <ul>
          <li><strong>Validate</strong> — runs load-time checks only (topic references, required fields,
            shape rules like consume-only needing MessageKey). No broker connection is made, so it's safe and
            instant to use while editing.</li>
          <li><strong>▶ Run</strong> — connects to the broker and executes every step in order, streaming
            results back as they complete (NDJSON under the hood) so a card appears the moment its step
            finishes rather than waiting for the whole case.</li>
          <li><strong>Cancel</strong> — aborts an in-flight run.</li>
        </ul>
        <P>Each streamed step line shows pass/fail, the step name, and how many messages were received; a
          failed validation is listed underneath with its expected vs. actual value. A final summary line
          shows overall pass/fail and an <M>N/M steps</M> count.</P>
        <P>On the suite's <strong>Test cases</strong> tab, each row's status pill (idle / running / passed /
          failed) reflects the same run, and the whole row is tinted green or red once it finishes.</P>
        <P>
          The <strong>Assembled playbook</strong> panel at the bottom of a test case page shows the exact JSON
          that would be sent to the engine — suite Kafka config, both topic lists, and this case's steps —
          useful for confirming what a run will actually do, or for copying into a standalone playbook file.
        </P>
      </>
    )
  },
  {
    slug: 'theme',
    title: 'Appearance',
    summary: 'Switching between the bright and dark themes.',
    body: (
      <>
        <P>
          The theme button in the top-right corner switches between the bright (default) and dark themes.
          The choice is saved in <M>localStorage</M> and applied immediately on the next load, before first
          paint, so there's no flash of the wrong theme.
        </P>
      </>
    )
  }
]

export const DOC_SECTIONS: DocSection[] = [
  { title: 'Getting started', slugs: ['overview'] },
  { title: 'Setting up a suite', slugs: ['suites-board', 'kafka-config', 'topics', 'payloads'] },
  { title: 'Building test cases', slugs: ['test-cases', 'step-shapes', 'send-payload', 'matching'] },
  { title: 'Templating and assertions', slugs: ['overrides-placeholders', 'capture-validations'] },
  { title: 'Running', slugs: ['running'] },
  { title: 'Appearance', slugs: ['theme'] }
]

// TypeScript mirror of the MessageHook.Playbook model (camelCase, as the Web JSON API emits it).

export interface TopicDeclaration {
  topic: string
  serializer?: string
  messageType?: string
  workersCount?: number
  bufferSize?: number
}

export interface Credentials {
  username?: string
  password?: string
  mechanism?: string
  tlsEnabled?: boolean
  securityProtocol?: string
}

export interface KafkaConfig {
  bootstrapServers: string[]
  credentials?: Credentials
  consumerGroup?: string
}

export type MatchMode = 'CorrelationId' | 'MessageKey'
export type ValidationTarget = 'Value' | 'Key'

export interface Match {
  mode: MatchMode
  expectedKey?: string
}

export interface Validation {
  target?: ValidationTarget
  path?: string
  type: string
  expected?: unknown
}

// Send is an inline object, a file-reference object { file: "payloads/x.json" }, or — as the API serializes a
// file reference back — a bare path string "payloads/x.json".
export type Send = string | { file: string } | Record<string, unknown>

export interface Step {
  name?: string
  produceTo?: string
  consumeFrom?: string[]
  key?: string
  headers?: Record<string, string>
  send?: Send
  override?: Record<string, unknown>
  match?: Match
  expectedMessageCount?: number
  timeoutSeconds?: number
  capture?: Record<string, string>
  validations?: Validation[]
}

export type HookType = 'ProduceAndForget' | 'ProduceAndWait' | 'ConsumeOnly'

// Mirror of BaseMessageHookStep.MessageHookType (and PlaybookStep.HookType): the topics decide the shape of a
// step. No consumeFrom means fire-and-forget — it never waits, so expectedMessageCount does not apply to it.
export function hookTypeOf(step: Step): HookType {
  if (!step.consumeFrom?.length) return 'ProduceAndForget'
  return step.produceTo ? 'ProduceAndWait' : 'ConsumeOnly'
}

export const HOOK_TYPE_LABEL: Record<HookType, string> = {
  ProduceAndForget: 'produce only',
  ProduceAndWait: 'produce & consume',
  ConsumeOnly: 'consume only'
}

export interface TestCase {
  id: string
  name: string
  description?: string
  steps: Step[]
  override?: Record<string, unknown>
  strictOverride?: boolean
}

export interface Suite {
  id: string
  name: string
  kafka: KafkaConfig
  consumeTopics: TopicDeclaration[]
  produceTopics: TopicDeclaration[]
  payloads: string[]
  testCases: TestCase[]
}

export interface SuiteSummary {
  id: string
  name: string
  testCaseCount: number
  payloadCount: number
  bootstrap: string
}

export const VALIDATION_TYPES = [
  'Equals', 'NotEquals', 'Contains', 'NotContains', 'Exists', 'NotExists',
  'Matches', 'GreaterThan', 'LessThan', 'Count'
] as const

// --- run stream events (NDJSON) ---

export interface ValidationResult {
  target?: string
  path?: string
  type?: string
  expected?: string
  actual?: string
  passed: boolean
}

export interface StepResult {
  name?: string
  passed: boolean
  error?: string
  receivedMessageCount: number
  validations: ValidationResult[]
}

export interface PlaybookResult {
  name?: string
  passed: boolean
  error?: string
  steps: StepResult[]
}

export type RunEvent =
  | { type: 'step'; caseId: string; caseName: string; step: StepResult }
  | { type: 'result'; caseId: string; caseName: string; result: PlaybookResult }
  | { type: 'error'; caseId?: string; error: string; errors?: string[] }
  | { type: 'done' }

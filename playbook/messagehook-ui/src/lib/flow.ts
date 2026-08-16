import type { HookType, Step } from '../types'

/** Canvas geometry and position persistence for the V2 node-flow editor. */

export interface XY { x: number; y: number }

/** Where every node on the canvas sits. `nodes[i]` belongs to `testCase.steps[i]`. */
export interface FlowPositions {
  start: XY
  nodes: XY[]
}

export const START_W = 190
export const NODE_W = 240
export const PORT_Y = 30          // port anchor, measured from the top of a node (its header's centre)
export const GAP_X = 110
const ORIGIN: XY = { x: 80, y: 200 }

export const MIN_ZOOM = 0.25
export const MAX_ZOOM = 2

/** Left-to-right chain: the start node, then one node per step. */
export function autoLayout(count: number): FlowPositions {
  const firstX = ORIGIN.x + START_W + GAP_X
  return {
    start: { ...ORIGIN },
    nodes: Array.from({ length: count }, (_, i) => ({ x: firstX + i * (NODE_W + GAP_X), y: ORIGIN.y }))
  }
}

/**
 * Fits saved positions to the current step count. Steps have no id, so positions are tracked by index —
 * dropped tail entries disappear and freshly added ones land after the last known node.
 */
export function reconcile(saved: FlowPositions, count: number): FlowPositions {
  const nodes = saved.nodes.slice(0, count)
  const firstX = saved.start.x + START_W + GAP_X
  while (nodes.length < count) {
    const prev = nodes[nodes.length - 1]
    nodes.push(prev ? { x: prev.x + NODE_W + GAP_X, y: prev.y } : { x: firstX, y: saved.start.y })
  }
  return { start: saved.start, nodes }
}

const storageKey = (suiteId: string, caseId: string) => `mh.flow.v1.${suiteId}.${caseId}`

export function loadPositions(suiteId: string, caseId: string): FlowPositions | null {
  try {
    const raw = localStorage.getItem(storageKey(suiteId, caseId))
    if (!raw) return null
    const parsed = JSON.parse(raw) as FlowPositions
    if (!parsed?.start || !Array.isArray(parsed.nodes)) return null
    return parsed
  } catch {
    return null // canvas layout is cosmetic — a corrupt entry just falls back to auto layout
  }
}

export function savePositions(suiteId: string, caseId: string, pos: FlowPositions): void {
  try { localStorage.setItem(storageKey(suiteId, caseId), JSON.stringify(pos)) } catch { /* quota/private mode */ }
}

/** Horizontal cubic bezier between two port anchors — the n8n/Make edge shape. */
export function edgePath(a: XY, b: XY): string {
  const dx = Math.max(50, Math.abs(b.x - a.x) * 0.45)
  return `M ${a.x} ${a.y} C ${a.x + dx} ${a.y}, ${b.x - dx} ${b.y}, ${b.x} ${b.y}`
}

export const NODE_ICON: Record<HookType, string> = {
  ProduceAndWait: '⇄',
  ProduceAndForget: '→',
  ConsumeOnly: '←'
}

/** CSS modifier per hook type, so a node's colour says what it does at a glance. */
export const NODE_KIND: Record<HookType, string> = {
  ProduceAndWait: 'wait',
  ProduceAndForget: 'produce',
  ConsumeOnly: 'consume'
}

export interface NodePreset {
  id: string
  title: string
  blurb: string
  icon: string
  kind: string
  make: (index: number, consumeTopic?: string, produceTopic?: string) => Step
}

/**
 * The palette. Each preset seeds the fields that define its shape — topics are what the engine reads to decide
 * whether a step waits (see hookTypeOf), so the presets differ only in which topics they pre-fill.
 */
export const PRESETS: NodePreset[] = [
  {
    id: 'wait',
    title: 'Produce & consume',
    blurb: 'Send a message, then wait for the matching reply and validate it.',
    icon: '⇄',
    kind: 'wait',
    make: (i, consumeTopic, produceTopic) => ({
      name: `step ${i + 1}`,
      produceTo: produceTopic,
      consumeFrom: consumeTopic ? [consumeTopic] : [],
      expectedMessageCount: 1,
      timeoutSeconds: 30,
      match: { mode: 'CorrelationId' }
    })
  },
  {
    id: 'produce',
    title: 'Produce only',
    blurb: 'Fire and forget — publish a message and move straight on.',
    icon: '→',
    kind: 'produce',
    make: (i, _consumeTopic, produceTopic) => ({
      name: `step ${i + 1}`,
      produceTo: produceTopic,
      consumeFrom: []
    })
  },
  {
    id: 'consume',
    title: 'Consume only',
    blurb: 'Wait for a message produced by something else, then validate it.',
    icon: '←',
    kind: 'consume',
    make: (i, consumeTopic) => ({
      name: `step ${i + 1}`,
      consumeFrom: consumeTopic ? [consumeTopic] : [],
      expectedMessageCount: 1,
      timeoutSeconds: 30,
      match: { mode: 'CorrelationId' }
    })
  }
]

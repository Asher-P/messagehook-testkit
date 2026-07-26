import type { Suite, TestCase } from '../types'

/** Client-side mirror of the server's PlaybookAssembler — for the read-only assembled-playbook preview. */
export function assembledJson(suite: Suite, testCase: TestCase): string {
  return JSON.stringify({
    name: `${suite.name} / ${testCase.name}`,
    kafkaConfiguration: suite.kafka,
    consumeTopics: suite.consumeTopics,
    produceTopics: suite.produceTopics,
    override: testCase.override,
    strictOverride: testCase.strictOverride,
    test: { steps: testCase.steps }
  }, null, 2)
}

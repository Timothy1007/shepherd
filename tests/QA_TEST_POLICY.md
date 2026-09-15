# QA fixture testing policy

QA seeds in this project guarantee the cards or state needed by the current test target. They do **not** guarantee that unrelated cards are absent from the rest of the random hand unless the fixture explicitly removes those cards.

Rules for QA regression tests:

- Assert required setup: guaranteed card presence, required fire/hand counts, targetability, or other state the fixture intentionally creates.
- Do not assert that unrelated miracle/disaster cards are absent merely because an older fixed seed happened not to draw them.
- When a test truly requires another card to be absent, remove it explicitly in the fixture/setup first, then assert the resulting state.
- Adding a new card changes the shuffled registry and may change which non-guaranteed cards appear for the same deterministic seed. Such changes should not break unrelated QA tests.
- Tests for card behavior should construct or normalize the relevant state rather than depend on incidental draw order.

This policy exists to keep new card registration from repeatedly breaking unrelated QA tests through stale random-hand assumptions.

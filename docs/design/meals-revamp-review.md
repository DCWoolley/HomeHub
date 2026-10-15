# Meals and ingredients implementation review

Reviewed against baseline de57a76 and specification #20, covering tickets #21–24. Two independent review passes examined repository standards and specification behavior.

## Standards

One conditional finding: the legacy recipe URL opens the meal editor during initialization, whose controls did not explicitly wait for interactive readiness. The module documentation requires controls to remain disabled until the Interactive Server circuit renders. Both editors now gate their fieldsets on readiness set in OnAfterRender, and the meal Save operation also checks readiness. The reviewer confirmed this resolves the concern.

The review otherwise found the implementation consistent with vertical slices, module-shared UI placement, direct EF access, household checks, decimal rules, independent ingredient saving, and atomic recipe persistence. No actionable judgment smells were identified.

## Spec

One confirmed finding: catching only domain-rule exceptions allowed database failures to escape Blazor event handling, terminating the circuit and losing the draft. This violated the requirement that failed saves retain a usable draft and actionable feedback. Both editors now handle infrastructure failures with generic safe logging and retry messages; list refresh failures are also controlled. Inline creation distinguishes a successfully saved ingredient from a subsequent failed catalogue refresh. The reviewer confirmed the finding is resolved and found no further gaps or material scope creep.

The browser harness includes guarded failure injection through a rejecting SQL trigger on a uniquely named disposable LocalDB database. It checks that entered values remain available after a real database write error and can be saved successfully on retry. This path is restricted to loopback hosts and generated test-only database names.

Standards: one conditional finding resolved, zero outstanding. Spec: one confirmed finding resolved, zero outstanding. The worst finding within each axis is described above.

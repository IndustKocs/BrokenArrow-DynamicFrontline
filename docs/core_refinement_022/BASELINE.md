# Stage A

Base 34ec3be; clean Core021 and related worktrees. New branch feature/core-refinement-022-pass04, isolated worktree core-refinement-022. Existing test-core021.ps1 -Regression reproduced 184+58=242 checks before modifications; evidence/core022/baseline-242.log and baseline-tests.json retain results. Previous evidence and research files unchanged.

Audit: reuse CombatFormulas, CriticalMath.Apply (movement multiplication, rotate assignment), VisibilityMath.Sphere/Smoke, AircraftMath and CyclicRandom. Extend existing Explorer, no second calculator. Read Pass04 reports, formula cards, dependencies and BuffDebuffSystem native/ISIL1403–1520: movement offset120 multiplies; rotation offset124 assigns config when mobility>0. Ten accumulators named by signature fields offsets0x60..0x84. CDF interpolation/trig orientation remain experimental. No gameplay/adapter/FOW-public changes.

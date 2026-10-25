# Stage A — 0.2.1 audit

Base cfe1b59, branch feature/core-refinement-021, isolated git worktree core-refinement-021. PoC02 worktree clean before start. No merge, game launch or installation.

CombatFormulas already provides F01–F09 and F13 with explicit finite-domain guards. F07 uses D+D/(P*E)*(P-A) and applies the minimum only to a positive intermediate; F08 uses strict >; F09 uses actual range. These functions will be reused, not copied. Existing 36+59+89=184 tests remain unchanged.

VerifiedCombat contains statically recovered fragments with evidence A/B and explicit caller inputs. ExperimentalCombat contains CF18 runtime-independent reconstruction and diagnostic composition. Existing Core/Frontline/Integration rules do not call new formulas. No Unity, MelonLoader, native or network reference will be added to the mathematical library or explorer.

Sources: full_code_audit/FORMULAS.md; combat_systems_audit/COMBAT_FORMULAS.md; pass_02/COMBAT_FORMULAS_PASS02.md; pass_03/COMBAT_FORMULAS_PASS03.md. CF09 uses p=(multiplier-100*floor(multiplier/100))*loss+base, without dividing remainder again. CF18 increments index before lookup and seed-NetID only identifies the reconstructed initialization. Pass03 P3-F02 permits negative step in raw finite math; safe usage validation is separate. FOW and smoke are numeric fragments, never contact authorization.

Baseline evidence/poc02 is preserved. New outputs belong to artifacts/core021 and evidence/core021. Stage commits follow successful targeted tests; complete regression and benchmark verification follow stages B–G.

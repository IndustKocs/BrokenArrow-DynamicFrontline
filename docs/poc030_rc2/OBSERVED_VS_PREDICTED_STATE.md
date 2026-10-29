# Observation lane

Simulation023 defaults to its existing autonomous behavior. Its explicit Observe lane instead uses input HP, stress and stress level. ZOC derives strength from those actual values; Observe never calls the autonomous stress initialization/combat/apply backend. Frame stress and HUD own-unit stress are actual observations. Encirclement state and tau are explicitly labelled predictions.

The 80→20→80 test verifies real-input replacement rather than accumulating a previous forecast. SupplyComplete=false suppresses penalty intents and elapsed penalty time. There is no native stress recovery/F0.5 scheduler in the live lane. Observation gaps advance uncertainty without replaying missed penalty time. A predicted terminal result does not stop subsequent observations.

Checkpoint lane is serialized and checked during restore. An autonomous checkpoint cannot be restored into Observe. Native effects remain disabled; no game mutator implementation was added.

# Performance — not validated

The full TZ17 matrix (10800 steps, three repeats, 100/500/1000 units, equal physical maps at 50/100 m, geometry oracle and allocation/queue statistics) has not yet been executed. No performance limit or live budget compliance is claimed.

Implemented changes include cached reflection bindings, 250 ms snapshots, a single sequential worker, bounded queues and async compact logs capped at 32 MiB. They are implementation choices, not performance evidence. Canonical JSON, checkpoint-derived territory digest and full geometry work still allocate; optimization must follow measured full-pipeline workloads.

Short RC1 benchmarks do not establish RC2 or live performance. Native main-thread read latency cannot be measured without the separately authorized two-client experience.

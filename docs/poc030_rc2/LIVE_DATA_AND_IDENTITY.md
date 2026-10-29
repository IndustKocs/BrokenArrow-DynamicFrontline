# Source and identity

The guarded reader copies owned ECS records on the main thread. Missing health/transform/stress produces incomplete data, not a silently omitted unit. Source revision, binding epoch, native game clock, pause, roster and monotonic freshness are distinct from network heartbeat. Worker queues contain managed records only.

Native candidate binding reads GameController._networkRoomService.State.FightId, Seed, map, and GetPlayers owner/team/net IDs. Metadata contracts were inspected; invocation was not run. Exactly two non-bot participants are required by MatchBinding. Local entity handles do not enter canonical inputs. Network unit ID plus proven generation is used. Unsupported ownership transfer or unexplained local rebind fails closed.

The current snapshot lifecycle handling is incomplete: a disappeared/reused ID without observed death cannot be proven from snapshots alone. Full lifecycle event history and reconnect mapping validation remain open. RosterContractConfirmed is not evidence by itself and must remain false until actual two-client roster checks.

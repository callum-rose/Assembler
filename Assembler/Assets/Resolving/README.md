# Resolving

The third stage of the YAML-to-game pipeline. Converts the deferred "value sources" produced by the parsing stage into runtime value providers that behaviours read during gameplay. Also owns all per-run state: variable storage (with global and per-entity scopes), loaded assets, compiled expression delegates, entity transform lookups, and exclusive-group tracking.

For every behaviour type there is a resolved data object that holds the providers and state needed at runtime — produced from the matching info record at build time, then handed to the corresponding MonoBehaviour during the build stage's two-phase initialisation. Each value source kind also maps to a corresponding provider kind: plain mutable values, compiled-expression delegates, trigger-supplied outputs, and a null sentinel for explicitly absent values.

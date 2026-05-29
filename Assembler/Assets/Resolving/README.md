# Resolving

The third stage of the YAML-to-game pipeline. Converts the deferred "value sources" produced by the parsing stage into runtime value providers that behaviours read during gameplay. Also owns all per-run state: variable storage (with global and per-entity scopes), loaded assets, compiled expression delegates, entity transform lookups, and exclusive-group tracking.

Each value source kind maps to a corresponding provider kind — plain mutable values, compiled-expression delegates, trigger-supplied outputs, and a null sentinel for explicitly absent values. The directory also holds one resolved data class per behaviour: the bag of providers and state that gets handed to the matching MonoBehaviour during the build stage's two-phase initialisation.

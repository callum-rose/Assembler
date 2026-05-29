# Generation.Verification

Closes the loop on LLM-driven YAML generation. Asks the model for a game descriptor, runs the response through the full parse / transform / build pipeline, and feeds any errors back to the model for a fix-up — repeating until the build succeeds or the configured attempt budget is exhausted.

The build step intercepts both thrown exceptions and Unity error logs, so problems that surface as logged messages (rather than as throws) are still treated as build failures and surfaced back to the model. Also contains the Unity Editor window that drives generation interactively, a connectivity smoke test, and a player-build smoke component.

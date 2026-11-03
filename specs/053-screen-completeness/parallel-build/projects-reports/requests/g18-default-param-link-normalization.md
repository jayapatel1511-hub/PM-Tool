# Static review note: implicit defaults become concrete when opening the input link

Application source is held. No repair applied and no runtime claim.

For explicitly scoped requests, opening the new input dialog keeps the report query key unchanged; the new browser script asserts zero additional report GETs for that case and Task/report closes.

An initially implicit server default may instead cause one ordinary report reread when its canonical input href makes that value explicit. Example: `/reports/stale-work` runs with `{}`; its server TaskInputs.Parameters is `{days:14}` and href is `?days=14&taskInput=unestimated...`. ReportPage then has the distinct run key `['report','stale-work',{days:'14'},'capacity_screens']`. The list uses the same canonical scope correctly, but the Run request may repeat during URL normalization. Padded/case-normalized GUID or clamped numeric spellings can have the same key-normalization effect. This is inferred from the static key/URL flow, not an observed browser failure.

If root requires zero additional Run GETs even for previously implicit/default-normalized spellings, please give a named repair seam before reopening app source. Avoid silently introducing a report cache TTL, weakening current-read behavior, dropping concrete default qualifiers from deep links, or reusing a raw-parameter fallback for list scopes. Existing canonical parameter/permission behavior takes priority. C has not changed shared cache policy.

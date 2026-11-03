# Generation 11 S05-A delivery presentation contract

Implementing only the six named S05-A source/test boundaries. Proposed additive omitted-null fields: `SubmissionEndpoints.ListRow.Delivery` (`DeliverySummary`), `SubmissionDetail.Delivery` (`DeliveryPresentation`). No persistence/command/fingerprint change; root generates API and applies locale/backend Text keys.

`DeliverySummary`: availability, nullable distinct canonical BlockingCheckCount, separate PackageProblemCount and UnknownCheckCount. Uses existing internal SubmissionContextData snapshot/readiness mapping, not raw reason counts or optional Fail count. Existing legacy BlockerCount stays unchanged; enabled ordering uses the full filtered cohort and new summary before paging. Existing Export.MaxRows remains an explicit full-scope ceiling, with `delivery.scope_limit` backend Text key requested: `Delivery view exceeds {0} packages. Narrow the filters.`

`DeliveryPresentation`: summary, evaluated-at timestamp, explicit checklist/manifest/issue availability; check presentations mapped by CheckId with checking owner/discipline, effective canonical state, Blocking and exact code/reason/source context; manifest members with recorded/current same-project revision and nullable mismatch; counts by current member deliverable status and unavailable count. Terminal issue evidence stays immutable and separate from timestamped live comparison. Missing partial payload fields are unavailable. Current exact child record/origin-return behavior reused.

No new query/savedview key is needed. List ordering is enabled-only; off response fields/order/nulls and UI remain unchanged. Detail/list reads use an enabled snapshot; root handles API/registry generation after freeze. Locale JSON follows with the actual component keys.

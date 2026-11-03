# S19 correction to the already integrated locale request

Root owns `web/src/i18n/en.ts`. Please update only this value:

```json
{
  "projectBulk.reasonHint": "Completed projects require a reason"
}
```

The current integrated value says paused or completed. The existing project edit command calls `CorrectionReason`, whose canonical `ProjectStatus.NeedsCorrectionReason` predicate applies to Complete only. The UI must describe that existing command rather than introduce a stricter rule. The original `s19-locales.json` request already has this corrected value.

No command, permission, lifecycle rule or locale key change is requested.

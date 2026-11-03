# Shared route seam — project index deep link

Read-only source observation during generation20: `app/routes.tsx` project's index uses `<Navigate to={PROJECT_HOME} replace />`. The string destination does not carry the incoming search string. Thus a project-root deep link's query is not represented by this redirect, unlike the explicitly addressed tab/panel URLs. A does not own routes.tsx or page/ProjectLayout edits in this window.

Recommended root-only tiny integration, if this index URL is part of the supported legacy deep-link promise: a small redirect component retaining `useLocation().search` in `to={{ pathname: PROJECT_HOME, search }}`, retaining replace/relative behavior and all permission/loading boundaries. No new route or API. Then the source route-shell matrix can exercise the original redirect with a scalar query and require its retention. Without that seam the exact index-query acceptance remains a recorded build gap, not a passing assertion or reason to rewrite an owned page.

Independent Shell/phone/layout work continues. Utilities remain held pending Jay's separate required answer.

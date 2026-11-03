using Hub.Api.Infrastructure;
using Hub.Domain;

namespace Hub.Api.Features;

/// Outcome metadata for the existing response command; this never changes finding or approval state.
public static class ReviewResponses
{
    /// Call inside Coordination.Run's callback, after its receipt lookup. The flag-off non-null guard also
    /// belongs before Run, so a disabled feature cannot be revealed by replaying a feature-bearing request.
    /// Do not trim or case-fold: the caller hashes the original typed body, including its old null fields.
    public static string? Validate(bool enabled, string action, string? outcome)
    {
        if (!enabled)
        {
            if (outcome is not null) throw ApiException.NotFound();
            return null;
        }
        if (action == FindingStatus.Responded)
        {
            Check.OneOf(outcome, ["Incorporated", "Not incorporated", "Clarified", "Noted"], "responseOutcome");
            return outcome;
        }
        Check.That(outcome is null, "responseOutcome", "error.invalid");
        return null;
    }
}

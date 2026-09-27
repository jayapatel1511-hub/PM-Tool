namespace Hub.Domain;

/// Risk and issue rules (§12.10): a deliberately simple 3 × 3 score and the review and target-date flags.
public static class Registers
{
    public static readonly int[] Levels = [1, 2, 3];

    /// Severity = probability × impact, each 1–3. Worked example: probability 3, impact 2 → 6.
    public static int Score(int probability, int impact) => probability * impact;

    /// Low 1–2, Medium 3–4, High 6–9 (the products of 1–3 are 1, 2, 3, 4, 6 and 9). Worked example: 6 → High, 4 → Medium.
    public static string Band(int score) => score >= 6 ? Impact.High : score >= 3 ? Impact.Medium : Impact.Low;

    /// The lowest score of a band, so "High" filters to scores of 6 or more.
    public static (int Min, int Max) Range(string band) => band switch { Impact.High => (6, 9), Impact.Medium => (3, 4), _ => (1, 2) };

    /// RSK-02: days an open (Open or Monitoring) risk is past its review date, or 0. Worked example: review 2026-09-10,
    /// today 2026-09-14 → 4; a Closed risk → 0.
    public static int ReviewOverdueDays(string status, DateOnly? reviewDate, DateOnly today) =>
        RiskStatus.IsOpen(status) && reviewDate is { } d && d < today ? today.DayNumber - d.DayNumber : 0;

    /// ISS-03: days an open (Open or In Progress) issue is past its target resolution date, or 0. Worked example: target
    /// 2026-09-12, today 2026-09-14 → 2; Resolved → 0.
    public static int IssueOverdueDays(string status, DateOnly? target, DateOnly today) =>
        IssueStatus.IsOpen(status) && target is { } d && d < today ? today.DayNumber - d.DayNumber : 0;

    /// Days an open (Open or In Progress) meeting action is past its due date, or 0. Worked example: due 2026-09-11, today
    /// 2026-09-14 → 3; Complete → 0.
    public static int ActionOverdueDays(string status, DateOnly? due, DateOnly today) =>
        ActionStatus.IsOpen(status) && due is { } d && d < today ? today.DayNumber - d.DayNumber : 0;

    /// Register sort weight: High first (§13.13).
    public static int Weight(string severity) => severity switch { Impact.High => 0, Impact.Medium => 1, _ => 2 };
}

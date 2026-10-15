using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

public static class UserPreferences
{
    public static async Task<UserSetting> GetOrCreate(HubDb db, Guid userId, CancellationToken ct = default)
    {
        // Daily/weekly jobs and the first preference save may race; preserve the row and each writer's separate fields.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hub.user_setting (user_id, digest_enabled, dense_rows, weekly_summary_enabled, digest_sections_off) VALUES ({userId}, true, true, true, '[]'::jsonb) ON CONFLICT (user_id) DO NOTHING", ct);
        return await db.UserSettings.SingleAsync(s => s.UserId == userId, ct);
    }
}

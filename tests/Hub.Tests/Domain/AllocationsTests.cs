using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class AllocationsTests
{
    [Fact]
    public void Confirmed_reservation_and_linked_forecast_are_counted_once()
    {
        Assert.Equal(15m, AllocationRules.Committed([new AllocationDemand(12m, 8m)], 3m));
        Assert.Equal(17m, AllocationRules.Committed([new AllocationDemand(12m, 14m)], 3m));
        Assert.Equal(3m, AllocationRules.Committed([], 3m));
    }

    [Fact]
    public void Day_override_replaces_capacity_and_holiday_is_zero_without_one()
    {
        var monday = new DateOnly(2026, 10, 5);
        var holiday = monday.AddDays(2);
        var calendar = new WorkCalendar([holiday]);
        Assert.Equal(10m, AllocationRules.DailyCapacity(monday, 40m, calendar, null));
        Assert.Equal(0m, AllocationRules.DailyCapacity(holiday, 40m, calendar, null));
        Assert.Equal(4m, AllocationRules.DailyCapacity(holiday, 40m, calendar, 4m));
        Assert.Equal(0m, AllocationRules.DailyCapacity(monday, 40m, calendar, 0m));
    }

    [Fact]
    public void Spread_preserves_exact_total_and_honours_fixed_days()
    {
        var monday = new DateOnly(2026, 10, 5);
        var calendar = new WorkCalendar([monday.AddDays(2)]);
        var days = AllocationRules.Spread(monday, monday.AddDays(6), 10.01m, calendar, new Dictionary<DateOnly, decimal> { [monday] = 2m });
        Assert.Equal(10.01m, days.Values.Sum());
        Assert.Equal(2m, days[monday]);
        Assert.DoesNotContain(monday.AddDays(2), days.Keys);
        Assert.Throws<ArgumentException>(() => AllocationRules.Spread(monday.AddDays(5), monday.AddDays(6), 1m, calendar));
        Assert.Throws<ArgumentOutOfRangeException>(() => AllocationRules.Spread(monday, monday.AddDays(4), -1m, calendar));
    }

    [Fact]
    public void Material_edit_withdraws_confirmation()
    {
        Assert.Equal(AllocationStatus.Proposed, AllocationRules.AfterMaterialEdit(AllocationStatus.Confirmed));
        Assert.True(AllocationRules.Step(AllocationStatus.Proposed, AllocationStatus.Confirmed));
        Assert.False(AllocationRules.Step(AllocationStatus.Declined, AllocationStatus.Confirmed));
        Assert.False(AllocationRules.Step(AllocationStatus.Confirmed, AllocationStatus.Proposed));
    }
}

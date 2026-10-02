using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class LocationIssueRulesTests
{
    [Fact]
    public void Alignment_requires_ordered_station_range_and_units()
    {
        Registers.ValidateIssueLocation("Alignment", "A", 10, 20, "m", null, null, null, null);
        Assert.Throws<ArgumentException>(() => Registers.ValidateIssueLocation("Alignment", "A", 20, 10, "m", null, null, null, null));
        Assert.Throws<ArgumentException>(() => Registers.ValidateIssueLocation("Alignment", "A", 10, 20, null, null, null, null, null));
    }

    [Fact]
    public void Coordinates_require_declared_reference_system_and_units()
    {
        Registers.ValidateIssueLocation("Coordinate", null, null, null, null, 1, 2, "EPSG:26920", "m");
        Assert.Throws<ArgumentException>(() => Registers.ValidateIssueLocation("Coordinate", null, null, null, null, 1, 2, null, "m"));
    }
}

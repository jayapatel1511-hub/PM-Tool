using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class InputAdoptionRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.InputAdoption), "InputAdoption", ["InputUseId", "SourceRevisionId", "IntendedUse", "Reason"], false, "no item-history surface today; child and organisation history retain existing queries"); }

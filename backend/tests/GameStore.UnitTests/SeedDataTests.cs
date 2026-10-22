using GameStore.Core.Domain;
using GameStore.Core.Services;

namespace GameStore.UnitTests;

public class SeedDataTests
{
    [Fact]
    public void Seed_store_is_Basic_Game_Store_in_Los_Angeles_with_a_fixed_UUIDv7()
    {
        var store = SeedData.Store();

        Assert.Equal("Basic Game Store", store.StoreName);
        Assert.Equal("America/Los_Angeles", store.TimeZone);
        Assert.Equal(Guid.Parse("0199f0a0-0000-7000-8000-000000000001"), store.StoreId);
        Assert.Equal(7, store.StoreId.Version);
    }

    [Fact]
    public void Seed_templates_match_the_spec()
    {
        var rows = SeedData.Templates()
            .Select(t => (t.TemplateId, t.GameType, t.Name, t.DefaultCapacity, t.DefaultEventMinimum,
                Hours: t.DefaultDuration.TotalHours, t.DefaultPlayFormats, t.DefaultDetails))
            .ToList();

        Assert.Equal(
        [
            (0, "Magic: The Gathering", "Default", 30, 3, 3, "1v1 constructed", "3 rounds best of 3"),
            (1, "Magic: The Gathering", "Magic: The Gathering", 30, 3, 3, "1v1 constructed", "3 rounds best of 3"),
            (2, "Magic: The Gathering Commander", "Magic: The Gathering Commander", 30, 6, 4, "pods", "swiss pairing"),
            (3, "Galactic Empires CCG", "Galactic Empires", 30, 7, 4, "7v7", "winner takes all swiss final if more than one pod"),
        ], rows.Select(r => ((int)r.TemplateId, r.GameType, r.Name, (int)r.DefaultCapacity, (int)r.DefaultEventMinimum,
            (int)r.Hours, r.DefaultPlayFormats, r.DefaultDetails)));
        Assert.All(SeedData.Templates(), t => Assert.Equal(SeedData.DefaultStoreId, t.StoreId));
    }

    [Fact]
    public void Seed_template_0_copies_template_1_values_with_name_Default()
    {
        var t0 = SeedData.Templates().Single(t => t.TemplateId == 0);
        var t1 = SeedData.Templates().Single(t => t.TemplateId == 1);

        Assert.Equal("Default", t0.Name);
        Assert.Equal(t1.GameType, t1.Name);
        Assert.Equal(
            (t1.GameType, t1.DefaultCapacity, t1.DefaultEventMinimum, t1.DefaultDuration, t1.DefaultPlayFormats, t1.DefaultDetails),
            (t0.GameType, t0.DefaultCapacity, t0.DefaultEventMinimum, t0.DefaultDuration, t0.DefaultPlayFormats, t0.DefaultDetails));
    }

    [Fact]
    public void Store_time_zone_resolves_and_handles_daylight_saving()
    {
        var zone = StoreTime.FindZone(SeedData.Store().TimeZone);

        // PDT (UTC-7) in July, PST (UTC-8) in December.
        Assert.Equal(new DateTimeOffset(2027, 7, 1, 1, 0, 0, TimeSpan.Zero), StoreTime.ToUtc(new(2027, 6, 30), new(18, 0), zone));
        Assert.Equal(new DateTimeOffset(2027, 12, 1, 2, 0, 0, TimeSpan.Zero), StoreTime.ToUtc(new(2027, 11, 30), new(18, 0), zone));
        // 02:30 on 2027-03-14 doesn't exist in Los Angeles; it is treated as 03:30 PDT.
        Assert.Equal(new DateTimeOffset(2027, 3, 14, 10, 30, 0, TimeSpan.Zero), StoreTime.ToUtc(new(2027, 3, 14), new(2, 30), zone));
    }
}

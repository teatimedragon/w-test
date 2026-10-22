namespace GameStore.Core.Domain;

/// <summary>The data the app starts with (spec section 3).</summary>
public static class SeedData
{
    /// <summary>Fixed, known UUIDv7 for the seeded store. The landing page loads this store.</summary>
    public static readonly Guid DefaultStoreId = Guid.Parse("0199f0a0-0000-7000-8000-000000000001");

    public static Store Store() => new()
    {
        StoreId = DefaultStoreId,
        StoreName = "Basic Game Store",
        TimeZone = "America/Los_Angeles",
    };

    public static IReadOnlyList<Template> Templates()
    {
        var magic = new Template
        {
            TemplateId = 1,
            StoreId = DefaultStoreId,
            GameType = "Magic: The Gathering",
            Name = "Magic: The Gathering",
            DefaultCapacity = 30,
            DefaultEventMinimum = 3,
            DefaultDuration = TimeSpan.FromHours(3),
            DefaultPlayFormats = "1v1 constructed",
            DefaultDetails = "3 rounds best of 3",
        };

        // Template 0 is a copy of template 1's values, named "Default".
        var defaultTemplate = new Template
        {
            TemplateId = 0,
            StoreId = magic.StoreId,
            GameType = magic.GameType,
            Name = "Default",
            DefaultCapacity = magic.DefaultCapacity,
            DefaultEventMinimum = magic.DefaultEventMinimum,
            DefaultDuration = magic.DefaultDuration,
            DefaultPlayFormats = magic.DefaultPlayFormats,
            DefaultDetails = magic.DefaultDetails,
        };

        return
        [
            defaultTemplate,
            magic,
            new Template
            {
                TemplateId = 2,
                StoreId = DefaultStoreId,
                GameType = "Magic: The Gathering Commander",
                Name = "Magic: The Gathering Commander",
                DefaultCapacity = 30,
                DefaultEventMinimum = 6,
                DefaultDuration = TimeSpan.FromHours(4),
                DefaultPlayFormats = "pods",
                DefaultDetails = "swiss pairing",
            },
            new Template
            {
                TemplateId = 3,
                StoreId = DefaultStoreId,
                GameType = "Galactic Empires CCG",
                Name = "Galactic Empires",
                DefaultCapacity = 30,
                DefaultEventMinimum = 7,
                DefaultDuration = TimeSpan.FromHours(4),
                DefaultPlayFormats = "7v7",
                DefaultDetails = "winner takes all swiss final if more than one pod",
            },
        ];
    }
}

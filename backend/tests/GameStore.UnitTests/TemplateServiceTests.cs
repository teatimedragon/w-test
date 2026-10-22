using GameStore.Core.Abstractions;
using GameStore.Core.Contracts;
using GameStore.Core.Domain;
using GameStore.Core.Errors;
using GameStore.Core.Services;
using NSubstitute;

namespace GameStore.UnitTests;

public class TemplateServiceTests
{
    private readonly ITemplateRepository _templates = Substitute.For<ITemplateRepository>();
    private readonly TemplateService _service;

    public TemplateServiceTests()
    {
        _templates.TryAddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>()).Returns(true);
        _service = new TemplateService(TestData.Stores(), _templates);
    }

    private static TemplateDto Request(int? templateId) => new(templateId, "Pokémon TCG", "Pokémon League", 16, 4, 120, "Standard", "Bring a deck");

    [Fact]
    public async Task Template_create_uses_highest_templateId_plus_1()
    {
        _templates.GetMaxTemplateIdAsync(Arg.Any<CancellationToken>()).Returns((short?)3);

        var created = await _service.UpsertAsync(TestData.StoreId, Request(null));

        Assert.Equal(4, created.TemplateId);
        await _templates.Received(1).TryAddAsync(
            Arg.Is<Template>(t => t.TemplateId == 4 && t.Name == "Pokémon League" && t.DefaultDuration == TimeSpan.FromMinutes(120)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Template_create_with_an_unknown_templateId_still_uses_highest_plus_1()
    {
        _templates.GetMaxTemplateIdAsync(Arg.Any<CancellationToken>()).Returns((short?)3);

        var created = await _service.UpsertAsync(TestData.StoreId, Request(99));

        Assert.Equal(4, created.TemplateId);
    }

    [Fact]
    public async Task Template_create_retries_when_another_create_took_the_id()
    {
        _templates.GetMaxTemplateIdAsync(Arg.Any<CancellationToken>()).Returns((short?)3, (short?)4);
        _templates.TryAddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>()).Returns(false, true);

        var created = await _service.UpsertAsync(TestData.StoreId, Request(null));

        Assert.Equal(5, created.TemplateId);
    }

    [Fact]
    public async Task Template_update_changes_the_existing_template()
    {
        var existing = SeedData.Templates().Single(t => t.TemplateId == 2);
        _templates.GetAsync(2, Arg.Any<CancellationToken>()).Returns(existing);

        var updated = await _service.UpsertAsync(TestData.StoreId, Request(2));

        Assert.Equal(Request(2), updated);
        await _templates.Received(1).UpdateAsync(
            Arg.Is<Template>(t => t.TemplateId == 2 && t.Name == "Pokémon League" && t.DefaultCapacity == 16),
            Arg.Any<CancellationToken>());
        await _templates.DidNotReceive().TryAddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Template_upsert_rejects_missing_name()
    {
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _service.UpsertAsync(TestData.StoreId, Request(null) with { Name = "" }));
        Assert.Contains("name", ex.Errors.Keys);
    }

    [Fact]
    public async Task Template_list_is_sorted_by_templateId()
    {
        _templates.ListByStoreAsync(TestData.StoreId, Arg.Any<CancellationToken>()).Returns(SeedData.Templates().Reverse().ToList());

        var list = await _service.ListAsync(TestData.StoreId);

        Assert.Equal([0, 1, 2, 3], list.Select(t => t.TemplateId!.Value));
    }
}

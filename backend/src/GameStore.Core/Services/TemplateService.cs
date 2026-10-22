using GameStore.Core.Abstractions;
using GameStore.Core.Contracts;
using GameStore.Core.Domain;
using GameStore.Core.Errors;
using GameStore.Core.Validation;

namespace GameStore.Core.Services;

public sealed class TemplateService(IStoreRepository stores, ITemplateRepository templates) : ITemplateService
{
    // Two creates racing for the same "highest + 1" id: the loser recomputes and tries again.
    private const int MaxCreateAttempts = 5;

    public async Task<IReadOnlyList<TemplateDto>> ListAsync(Guid storeId, CancellationToken ct = default)
    {
        await StoreService.GetRequiredAsync(stores, storeId, ct);
        return (await templates.ListByStoreAsync(storeId, ct)).OrderBy(t => t.TemplateId).Select(ToDto).ToList();
    }

    public async Task<TemplateDto> UpsertAsync(Guid storeId, TemplateDto request, CancellationToken ct = default)
    {
        await StoreService.GetRequiredAsync(stores, storeId, ct);
        Validators.ThrowIfAny(Validators.ValidateTemplate(request));

        var existing = request.TemplateId is { } id ? await templates.GetAsync((short)id, ct) : null;
        if (existing is not null)
        {
            if (existing.StoreId != storeId)
                throw new NotFoundException($"Template {existing.TemplateId} was not found for store {storeId}.");
            var updated = FromRequest(existing.TemplateId, storeId, request);
            await templates.UpdateAsync(updated, ct);
            return ToDto(updated);
        }

        // No templateId, or one that doesn't exist yet: create with highest existing templateId + 1.
        for (var attempt = 0; attempt < MaxCreateAttempts; attempt++)
        {
            var max = await templates.GetMaxTemplateIdAsync(ct);
            if (max == short.MaxValue) throw new ValidationFailedException(
                new Dictionary<string, string[]> { ["templateId"] = ["No more template ids are available."] });

            var created = FromRequest((short)((max ?? -1) + 1), storeId, request);
            if (await templates.TryAddAsync(created, ct)) return ToDto(created);
        }
        throw new InvalidOperationException("Could not allocate a templateId.");
    }

    private static Template FromRequest(short templateId, Guid storeId, TemplateDto r) => new()
    {
        TemplateId = templateId,
        StoreId = storeId,
        GameType = r.GameType!.Trim(),
        Name = r.Name!.Trim(),
        DefaultCapacity = (short)r.DefaultCapacity!.Value,
        DefaultEventMinimum = (short)r.DefaultEventMinimum!.Value,
        DefaultDuration = TimeSpan.FromMinutes(r.DefaultDuration!.Value),
        DefaultPlayFormats = r.DefaultPlayFormats?.Trim() ?? "",
        DefaultDetails = r.DefaultDetails?.Trim() ?? "",
    };

    internal static TemplateDto ToDto(Template t) => new(
        t.TemplateId, t.GameType, t.Name, t.DefaultCapacity, t.DefaultEventMinimum,
        (int)t.DefaultDuration.TotalMinutes, t.DefaultPlayFormats, t.DefaultDetails);
}

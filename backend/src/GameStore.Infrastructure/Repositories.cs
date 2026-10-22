using GameStore.Core.Abstractions;
using GameStore.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace GameStore.Infrastructure;

// Each call uses its own short-lived context, so reads never see stale tracked entities.

public sealed class StoreRepository(IDbContextFactory<GameStoreDbContext> factory) : IStoreRepository
{
    public async Task<Store?> GetAsync(Guid storeId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.StoreId == storeId, ct);
    }
}

public sealed class EventRepository(IDbContextFactory<GameStoreDbContext> factory) : IEventRepository
{
    public async Task<IReadOnlyList<Event>> ListByStoreAsync(Guid storeId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Events.AsNoTracking().Where(e => e.StoreId == storeId).ToListAsync(ct);
    }

    public async Task<Event?> GetAsync(Guid eventId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);
    }

    public async Task AddAsync(Event evt, Capacity capacity, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Events.Add(evt);
        db.Capacities.Add(capacity);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class RegistrationRepository(IDbContextFactory<GameStoreDbContext> factory) : IRegistrationRepository
{
    public async Task<Registration?> GetAsync(Guid registrationId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Registrations.AsNoTracking().FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountByEventAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Registrations.AsNoTracking()
            .Where(r => eventIds.Contains(r.EventId))
            .GroupBy(r => r.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, ct);
    }
}

/// <summary>
/// The in-memory provider has no transactions, so the compare-and-swap on the Capacity row and the
/// matching Registration insert/delete run under one process-wide lock (registered as a singleton).
/// </summary>
public sealed class CapacityRepository(IDbContextFactory<GameStoreDbContext> factory, CapacityWriteLock writeLock) : ICapacityRepository
{
    public async Task<Capacity?> GetAsync(Guid eventId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Capacities.AsNoTracking().FirstOrDefaultAsync(c => c.EventId == eventId, ct);
    }

    public async Task<bool> TryApplyAsync(CapacityChange change, CancellationToken ct = default)
    {
        await writeLock.Semaphore.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var capacity = await db.Capacities.FirstOrDefaultAsync(c => c.EventId == change.EventId, ct);
            if (capacity is null || capacity.VersionId != change.ExpectedVersionId) return false;

            if (change.RemoveRegistrationId is { } removeId)
            {
                var registration = await db.Registrations.FirstOrDefaultAsync(r => r.RegistrationId == removeId, ct);
                if (registration is null) return false;
                db.Registrations.Remove(registration);
            }
            if (change.AddRegistration is { } add) db.Registrations.Add(add);

            capacity.VersionId = change.NewVersionId;
            capacity.RemainingCapacity = change.NewRemainingCapacity;
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        finally
        {
            writeLock.Semaphore.Release();
        }
    }
}

public sealed class CapacityWriteLock
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}

public sealed class TemplateRepository(IDbContextFactory<GameStoreDbContext> factory) : ITemplateRepository
{
    public async Task<IReadOnlyList<Template>> ListByStoreAsync(Guid storeId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Templates.AsNoTracking().Where(t => t.StoreId == storeId).ToListAsync(ct);
    }

    public async Task<Template?> GetAsync(short templateId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.TemplateId == templateId, ct);
    }

    public async Task<short?> GetMaxTemplateIdAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Templates.AsNoTracking().MaxAsync(t => (short?)t.TemplateId, ct);
    }

    public async Task<bool> TryAddAsync(Template template, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Templates.Add(template);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (ArgumentException)
        {
            // The in-memory provider throws ArgumentException for a duplicate key.
            return false;
        }
    }

    public async Task UpdateAsync(Template template, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Templates.Update(template);
        await db.SaveChangesAsync(ct);
    }
}

using System.Data;
using Flowza.Identity.Application.Abstractions;
using Flowza.Identity.Domain.Organization;
using Flowza.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Flowza.Identity.Infrastructure.Persistence;

internal sealed class UnitTypeRepository(IdentityServiceDbContext context) : IUnitTypeRepository
{
    public Task<UnitType?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.UnitTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> IsCodeTakenAsync(OrgCode code, CancellationToken cancellationToken) =>
        context.UnitTypes.AnyAsync(t => t.Code == code, cancellationToken);

    public Task<int> CountActiveUnitsAsync(Guid unitTypeId, CancellationToken cancellationToken) =>
        context.OrgUnits.CountAsync(u => u.UnitTypeId == unitTypeId && u.IsActive, cancellationToken);

    public void Add(UnitType unitType) => context.UnitTypes.Add(unitType);

    public void SetExpectedVersion(UnitType unitType, byte[] rowVersion) => context.SetExpectedRowVersion(unitType, rowVersion);
}

internal sealed class OrgUnitRepository(IdentityServiceDbContext context) : IOrgUnitRepository
{
    public Task<OrgUnit?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.OrgUnits.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> IsCodeTakenAsync(OrgCode code, CancellationToken cancellationToken) =>
        context.OrgUnits.AnyAsync(u => u.Code == code, cancellationToken);

    public async Task<IReadOnlyList<OrgUnit>> GetDescendantsAsync(OrgUnit unit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unit);

        // A path holds digits and separators only, so it needs no LIKE escaping. The parameter is varchar like the
        // column, so SQL Server seeks the path index instead of converting every row.
        var pattern = new SqlParameter("@pattern", SqlDbType.VarChar, UnitPath.MaxLength + 1) { Value = unit.Path.Value + "%" };
        var unitId = unit.Id;
        return await context.OrgUnits
            .FromSqlRaw("SELECT * FROM [dbo].[OrgUnits] WHERE [Path] LIKE @pattern", pattern)
            .Where(u => u.Id != unitId)
            .ToListAsync(cancellationToken);
    }

    public void Add(OrgUnit unit) => context.OrgUnits.Add(unit);

    public void SetExpectedVersion(OrgUnit unit, byte[] rowVersion) => context.SetExpectedRowVersion(unit, rowVersion);
}

internal sealed class BusinessRoleRepository(IdentityServiceDbContext context) : IBusinessRoleRepository
{
    public Task<BusinessRole?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.BusinessRoles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> IsCodeTakenAsync(OrgCode code, CancellationToken cancellationToken) =>
        context.BusinessRoles.AnyAsync(r => r.Code == code, cancellationToken);

    public void Add(BusinessRole role) => context.BusinessRoles.Add(role);

    public void SetExpectedVersion(BusinessRole role, byte[] rowVersion) => context.SetExpectedRowVersion(role, rowVersion);
}

internal sealed class OrgStructureRepository(IdentityServiceDbContext context) : IOrgStructureRepository
{
    // The migration seeds the row, so a missing row is a broken installation, not an expected failure.
    public async Task<OrgStructure> GetAsync(CancellationToken cancellationToken) =>
        await context.OrgStructure.FirstOrDefaultAsync(s => s.Id == OrgStructure.SingletonId, cancellationToken)
        ?? throw new InvalidOperationException("The org-structure row is missing; apply the database migrations.");
}

internal sealed class UnitNumberSource(IdentityServiceDbContext context) : IUnitNumberSource
{
    private const string NextNumberSql = "SELECT NEXT VALUE FOR [dbo].[" + UnitNumberSequence.Name + "] AS [Value]";

    // NEXT VALUE FOR is not allowed inside a derived table, so the query is read as it is (no composition). O(1).
    public async Task<int> NextAsync(CancellationToken cancellationToken)
    {
        var numbers = await context.Database.SqlQueryRaw<int>(NextNumberSql).ToListAsync(cancellationToken);
        return numbers.Single();
    }
}

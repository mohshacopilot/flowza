using Flowza.Common.Ids;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Flowza.Persistence.Conventions;

/// <summary>Gives new Guid keys a SQL Server-ordered sequential value when the code has not set one (tech discussion T3, D3).</summary>
public sealed class FlowzaGuidValueGenerator : ValueGenerator<Guid>
{
    public override bool GeneratesTemporaryValues => false;

    public override Guid Next(EntityEntry entry) => SequentialGuidGenerator.Shared.NewGuid();
}

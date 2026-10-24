using Flowza.Identity.Domain.Organization;
using Flowza.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Flowza.Identity.Infrastructure.Persistence;

internal sealed class UsernameConverter() : ValueConverter<Username, string>(v => v.Value, v => Username.FromStored(v));

internal sealed class EmailConverter() : ValueConverter<Email, string>(v => v.Value, v => Email.FromStored(v));

internal sealed class OrgCodeConverter() : ValueConverter<OrgCode, string>(v => v.Value, v => OrgCode.FromStored(v));

internal sealed class UnitPathConverter() : ValueConverter<UnitPath, string>(v => v.Value, v => UnitPath.FromStored(v));

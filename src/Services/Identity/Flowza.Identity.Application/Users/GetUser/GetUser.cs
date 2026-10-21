using Flowza.Common.Results;
using Flowza.Identity.Domain.Users;

namespace Flowza.Identity.Application.Users.GetUser;

public sealed record GetUserQuery(Guid Id);

public sealed class GetUserHandler(IUserReadStore users)
{
    public async Task<Result<UserDetails>> Handle(GetUserQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var user = await users.GetAsync(query.Id, cancellationToken);
        return user is null ? UserErrors.NotFound : user;
    }
}

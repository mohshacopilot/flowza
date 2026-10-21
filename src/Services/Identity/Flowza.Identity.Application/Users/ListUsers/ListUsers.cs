using Flowza.Common.Paging;

namespace Flowza.Identity.Application.Users.ListUsers;

/// <summary>The users list for administrators: search, status filter, paging of 25 (plan 001 §5.3, T2 S7).</summary>
public sealed record ListUsersQuery(int Page = 1, int Size = PageRequest.DefaultSize, string? Search = null, UserStatus? Status = null);

public sealed class ListUsersHandler(IUserReadStore users)
{
    public const int SearchMaxLength = 100;

    public Task<PagedResult<UserListItem>> Handle(ListUsersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        if (search?.Length > SearchMaxLength)
        {
            search = search[..SearchMaxLength];
        }

        return users.ListAsync(new UserListFilter(search, query.Status), new PageRequest(query.Page, query.Size), cancellationToken);
    }
}

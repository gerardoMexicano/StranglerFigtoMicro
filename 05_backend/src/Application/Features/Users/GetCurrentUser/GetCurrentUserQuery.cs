using Application.Common.Messaging;

namespace Application.Features.Users.GetCurrentUser;

public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;

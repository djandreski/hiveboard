namespace Hiveboard.Api.Contracts;

public record AdminKeyInfoResponse(
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);

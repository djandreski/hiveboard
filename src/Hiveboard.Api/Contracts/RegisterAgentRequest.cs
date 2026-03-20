using Hiveboard.Core.Enums;

namespace Hiveboard.Api.Contracts;

public record RegisterAgentRequest(
    string Name,
    string Type,
    string Platform,
    Guid OrganizationId);

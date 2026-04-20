namespace DevInsight.Application.DTOs;
public record AuthResponseDto(string Token, string Email, string DisplayName, Guid OrganizationId);

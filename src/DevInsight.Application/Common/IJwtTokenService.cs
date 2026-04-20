using DevInsight.Domain.Entities;
namespace DevInsight.Application.Common;
public interface IJwtTokenService
{
    string GenerateToken(AppUser user);
}

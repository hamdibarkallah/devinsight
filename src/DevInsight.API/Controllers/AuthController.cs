using DevInsight.Application.Common;
using DevInsight.Application.DTOs;
using DevInsight.Domain.Entities;
using DevInsight.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DevInsight.API.Controllers;

[ApiController, Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IRepository<AppUser> _userRepo;
    private readonly IRepository<Organization> _orgRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwt;

    public AuthController(IRepository<AppUser> userRepo, IRepository<Organization> orgRepo, IUnitOfWork unitOfWork, IJwtTokenService jwt)
    {
        _userRepo = userRepo; _orgRepo = orgRepo; _unitOfWork = unitOfWork; _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var org = new Organization { Name = req.OrganizationName };
        await _orgRepo.AddAsync(org, ct);
        var user = new AppUser
        {
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            DisplayName = req.DisplayName,
            OrganizationId = org.Id
        };
        await _userRepo.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        var token = _jwt.GenerateToken(user);
        return Ok(new AuthResponseDto(token, user.Email, user.DisplayName, org.Id));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var users = await _userRepo.GetAllAsync(ct);
        var user = users.FirstOrDefault(u => u.Email == req.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials." });
        var token = _jwt.GenerateToken(user);
        return Ok(new AuthResponseDto(token, user.Email, user.DisplayName, user.OrganizationId));
    }
}

public record RegisterRequest(string Email, string Password, string DisplayName, string OrganizationName);
public record LoginRequest(string Email, string Password);

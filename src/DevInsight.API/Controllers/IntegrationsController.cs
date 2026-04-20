using System.Security.Claims;
using DevInsight.Application.Common;
using DevInsight.Domain.Entities;
using DevInsight.Domain.Enums;
using DevInsight.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevInsight.API.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class IntegrationsController : ControllerBase
{
    private readonly IIntegrationRepository _integrationRepo;
    private readonly ITokenEncryptionService _encryption;
    private readonly IUnitOfWork _unitOfWork;

    public IntegrationsController(IIntegrationRepository integrationRepo, ITokenEncryptionService encryption, IUnitOfWork unitOfWork)
    {
        _integrationRepo = integrationRepo;
        _encryption = encryption;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Store a GitHub personal access token for your organization.</summary>
    [HttpPost("github")]
    public async Task<IActionResult> AddGitHub([FromBody] AddGitHubRequest request, CancellationToken ct)
    {
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);

        var existing = await _integrationRepo.GetByOrganizationAndProviderAsync(orgId, GitProvider.GitHub, ct);
        if (existing is not null)
            return Conflict(new { message = "GitHub integration already exists. Use PUT to update." });

        var integration = new Integration
        {
            Provider = GitProvider.GitHub,
            EncryptedAccessToken = _encryption.Encrypt(request.PersonalAccessToken),
            OrganizationId = orgId
        };
        await _integrationRepo.AddAsync(integration, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = "GitHub integration added.", integrationId = integration.Id });
    }

    /// <summary>Store a GitLab personal access token for your organization.</summary>
    [HttpPost("gitlab")]
    public async Task<IActionResult> AddGitLab([FromBody] AddGitLabRequest request, CancellationToken ct)
    {
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        var existing = await _integrationRepo.GetByOrganizationAndProviderAsync(orgId, GitProvider.GitLab, ct);
        if (existing is not null)
            return Conflict(new { message = "GitLab integration already exists. Use PUT to update." });

        var integration = new Integration
        {
            Provider = GitProvider.GitLab,
            EncryptedAccessToken = _encryption.Encrypt(request.PersonalAccessToken),
            OrganizationId = orgId
        };
        await _integrationRepo.AddAsync(integration, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Ok(new { message = "GitLab integration added.", integrationId = integration.Id });
    }

    /// <summary>Update an existing integration token.</summary>
    [HttpPut("{integrationId:guid}")]
    public async Task<IActionResult> Update(Guid integrationId, [FromBody] UpdateTokenRequest request, CancellationToken ct)
    {
        var integration = await _integrationRepo.GetByIdAsync(integrationId, ct);
        if (integration is null) return NotFound();
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        if (integration.OrganizationId != orgId) return Forbid();
        integration.EncryptedAccessToken = _encryption.Encrypt(request.PersonalAccessToken);
        await _unitOfWork.SaveChangesAsync(ct);
        return Ok(new { message = "Token updated." });
    }

    /// <summary>List integrations for the current org.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        var all = await _integrationRepo.GetAllAsync(ct);
        var mine = all.Where(i => i.OrganizationId == orgId).Select(i => new
        {
            i.Id, Provider = i.Provider.ToString(), i.LastSyncedAt, i.CreatedAt
        });
        return Ok(mine);
    }
}

public record AddGitHubRequest(string PersonalAccessToken);
public record AddGitLabRequest(string PersonalAccessToken);
public record UpdateTokenRequest(string PersonalAccessToken);

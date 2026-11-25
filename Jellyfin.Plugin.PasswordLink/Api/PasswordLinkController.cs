using System.Net.Mime;
using System.Reflection;
using Jellyfin.Plugin.PasswordLink.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.PasswordLink.Api;

[ApiController]
[Route("PasswordLink")]
public class PasswordLinkController : ControllerBase
{
    private const string ContentSecurityPolicy =
        "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; "
        + "base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    private readonly PasswordLinkService _service;

    public PasswordLinkController(PasswordLinkService service)
    {
        _service = service;
    }

    [HttpGet("Links")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<IReadOnlyList<PasswordLinkSummary>>> GetLinks()
    {
        return Ok(await _service.ListAsync().ConfigureAwait(false));
    }

    [HttpPost("Links")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult<CreatedPasswordLink>> CreateLink([FromBody] CreateLinkRequest request)
    {
        var username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username;

        try
        {
            return (request.UserId, username) switch
            {
                ({ } userId, null) => Ok(await _service.CreateAsync(userId, request.ExpiresInDays).ConfigureAwait(false)),
                (null, { } name) => Ok(await _service.CreateForNewUserAsync(name, request.ExpiresInDays).ConfigureAwait(false)),
                _ => BadRequest(new ErrorResponse("UserIdOrUsernameRequired")),
            };
        }
        catch (PasswordLinkException e) when (e.Error == PasswordLinkError.UserNotFound)
        {
            return NotFound(new ErrorResponse(e.Error.ToString()));
        }
        catch (PasswordLinkException e)
        {
            return BadRequest(new ErrorResponse(e.Error.ToString()));
        }
    }

    [HttpDelete("Links/{linkId}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    public async Task<ActionResult> RevokeLink([FromRoute] Guid linkId)
    {
        return await _service.RevokeAsync(linkId).ConfigureAwait(false) ? NoContent() : NotFound();
    }

    [HttpPost("Inspect")]
    [AllowAnonymous]
    public async Task<ActionResult<InspectResponse>> Inspect([FromBody] InspectRequest request)
    {
        SetPublicHeaders();
        try
        {
            var username = await _service.GetUsernameAsync(request.Token).ConfigureAwait(false);
            return Ok(new InspectResponse(username, _service.MinPasswordLength));
        }
        catch (PasswordLinkException e) when (e.Error == PasswordLinkError.InvalidLink)
        {
            return NotFound(new ErrorResponse(e.Error.ToString()));
        }
    }

    [HttpPost("Redeem")]
    [AllowAnonymous]
    public async Task<ActionResult> Redeem([FromBody] RedeemRequest request)
    {
        SetPublicHeaders();
        try
        {
            await _service.RedeemAsync(request.Token, request.Password).ConfigureAwait(false);
            return NoContent();
        }
        catch (PasswordLinkException e) when (e.Error == PasswordLinkError.InvalidLink)
        {
            return NotFound(new ErrorResponse(e.Error.ToString()));
        }
        catch (PasswordLinkException e) when (e.Error == PasswordLinkError.PasswordTooShort)
        {
            return BadRequest(new ErrorResponse(e.Error.ToString()));
        }
    }

    [HttpGet("Set")]
    [AllowAnonymous]
    public ActionResult GetPage() => PublicFile("set-password.html", MediaTypeNames.Text.Html);

    [HttpGet("Assets/set-password.js")]
    [AllowAnonymous]
    public ActionResult GetScript() => PublicFile("set-password.js", "text/javascript");

    [HttpGet("Assets/set-password.css")]
    [AllowAnonymous]
    public ActionResult GetStyles() => PublicFile("set-password.css", "text/css");

    private ActionResult PublicFile(string name, string contentType)
    {
        SetPublicHeaders();
        Response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;

        var resource = $"{typeof(Plugin).Namespace}.Web.{name}";
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource {resource} is missing.");

        return File(stream, contentType + "; charset=utf-8");
    }

    private void SetPublicHeaders()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.XFrameOptions = "DENY";
    }
}

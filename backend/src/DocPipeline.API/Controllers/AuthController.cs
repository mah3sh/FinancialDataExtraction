using DocPipeline.Application.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DocPipeline.API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(
    UserManager<IdentityUser> userManager,
    IConfiguration config) : ControllerBase
{
    private static readonly string[] AllowedRoles = ["Uploader", "Reviewer"];

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!AllowedRoles.Contains(request.Role))
            return BadRequest(new { detail = $"Role must be one of: {string.Join(", ", AllowedRoles)}" });

        var user = new IdentityUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return BadRequest(new { detail = string.Join("; ", result.Errors.Select(e => e.Description)) });

        await userManager.AddToRoleAsync(user, request.Role);

        return Ok(GenerateToken(user, request.Role));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { detail = "Invalid email or password." });

        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Uploader";

        return Ok(GenerateToken(user, role));
    }

    private AuthResponse GenerateToken(IdentityUser user, string role)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured.");
        var issuer = config["Jwt:Issuer"] ?? "docpipeline";
        var audience = config["Jwt:Audience"] ?? "docpipeline";
        var expiresMinutes = int.Parse(config["Jwt:ExpiresMinutes"] ?? "60");
        var expiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            user.Email!,
            role,
            expiresAt);
    }
}

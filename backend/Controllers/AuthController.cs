using backend.Models.Auth;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(IAuthService authService, IOtpService otpService) : ControllerBase
    {
        [HttpPost("register/farmer")]
        public async Task<IActionResult> RegisterFarmer([FromBody] RegisterFarmerRequest request, CancellationToken ct)
        {
            var (success, message) = await authService.RegisterFarmerAsync(request, ct);
            return success
                ? Ok(new { message, email = request.Email.Trim().ToLowerInvariant() })
                : BadRequest(new { message });
        }

        [HttpPost("register/buyer")]
        public async Task<IActionResult> RegisterBuyer([FromBody] RegisterBuyerRequest request, CancellationToken ct)
        {
            var (success, message) = await authService.RegisterBuyerAsync(request, ct);
            return success
                ? Ok(new { message, email = request.Email.Trim().ToLowerInvariant() })
                : BadRequest(new { message });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            var (success, message, data) = await authService.LoginAsync(request, ct);
            return success ? Ok(data) : Unauthorized(new { message });
        }

        [HttpPost("request-otp")]
        public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest request, CancellationToken ct)
        {
            await otpService.GenerateAndSendAsync(request.Email, request.Purpose, ct);
            return Ok(new { message = "If the email is valid, a 6-digit verification code has been sent." });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
        {
            var (success, message, data) = await authService.VerifyOtpAndSignInAsync(request, ct);
            return success ? Ok(data) : BadRequest(new { message });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var profile = await authService.GetCurrentUserProfileAsync(userId, ct);
            return profile is null ? NotFound() : Ok(profile);
        }
    }
}

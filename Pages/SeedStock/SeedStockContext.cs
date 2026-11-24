using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AgriFlow.Pages.SeedStock;

public static class SeedStockContext
{
    public const string ActiveFarmerKey = "ActiveFarmerId";

    public static int? GetActorUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("UserId")
            ?? user.FindFirstValue("sub");

        return int.TryParse(value, out var id) ? id : null;
    }

    public static string GetRole(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Role)
            ?? user.FindFirstValue("role")
            ?? string.Empty;
    }

    public static int? GetFarmerId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("FarmerId")
            ?? (GetRole(user).Equals("Farmer", StringComparison.OrdinalIgnoreCase)
                ? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("UserId")
                : null);

        return int.TryParse(value, out var id) ? id : null;
    }

    public static int? GetActiveFarmerId(HttpContext httpContext, ClaimsPrincipal user)
    {
        var role = GetRole(user);
        if (role.Equals("Farmer", StringComparison.OrdinalIgnoreCase))
            return GetFarmerId(user);

        if (!httpContext.Session.TryGetValue(ActiveFarmerKey, out var bytes))
            return null;

        return int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out var id) ? id : null;
    }
}

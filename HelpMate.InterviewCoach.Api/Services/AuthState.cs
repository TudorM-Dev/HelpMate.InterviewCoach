using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace HelpMate.InterviewCoach.Api.Services;

public class AuthState
{
    private const string StorageKey = "helpmate.token";

    private readonly ProtectedSessionStorage _storage;

    public AuthState(ProtectedSessionStorage storage)
    {
        _storage = storage;
    }

    public string? Token { get; private set; }
    public string? Email { get; private set; }
    public string? DisplayName { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];

    public bool IsInitialized { get; private set; }

    public bool IsSignedIn => !string.IsNullOrEmpty(Token);
    public bool IsAdmin => Roles.Contains("Admin");

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        try
        {
            var stored = await _storage.GetAsync<string>(StorageKey);

            if (stored.Success && !string.IsNullOrEmpty(stored.Value) && !IsExpired(stored.Value))
            {
                Apply(stored.Value, null);
            }
        }
        catch
        {
        }

        IsInitialized = true;
        Changed?.Invoke();
    }

    public async Task SignInAsync(string token, string? displayName = null)
    {
        Apply(token, displayName);
        await _storage.SetAsync(StorageKey, token);
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        Token = null;
        Email = null;
        DisplayName = null;
        Roles = [];

        await _storage.DeleteAsync(StorageKey);
        Changed?.Invoke();
    }

    private void Apply(string token, string? displayName)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Token = token;
        Email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        DisplayName = displayName ?? Email;

        Roles = jwt.Claims
            .Where(c => c.Type == "role" || c.Type.EndsWith("/role"))
            .Select(c => c.Value)
            .ToList();
    }

    private static bool IsExpired(string token)
    {
        try
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo <= DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }
}

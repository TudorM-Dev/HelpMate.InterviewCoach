using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HelpMate.InterviewCoach.Api.Contracts;

namespace HelpMate.InterviewCoach.Api.Services;

public class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AuthState _auth;

    public ApiClient(HttpClient http, AuthState auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<(bool Ok, string? Error)> RegisterAsync(string email, string password, string displayName)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, password, displayName), JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            return (false, await ReadErrorAsync(response));
        }

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        if (payload is null)
        {
            return (false, "Unexpected response from the server.");
        }

        await _auth.SignInAsync(payload.Token, displayName);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password), JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            return (false, "Invalid email or password.");
        }

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        if (payload is null)
        {
            return (false, "Unexpected response from the server.");
        }

        await _auth.SignInAsync(payload.Token);
        return (true, null);
    }

    public Task<IReadOnlyList<SessionSummaryResponse>> GetSessionsAsync() =>
        GetAsync<IReadOnlyList<SessionSummaryResponse>>("/api/sessions", []);

    public Task<SessionDetailResponse?> GetSessionAsync(int id) =>
        GetAsync<SessionDetailResponse?>($"/api/sessions/{id}", null);

    public async Task<(SessionSummaryResponse? Session, string? Error)> CreateSessionAsync(string targetRole)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/sessions",
            JsonContent.Create(new CreateSessionRequest(targetRole), options: JsonOptions));

        if (!response.IsSuccessStatusCode)
        {
            return (null, await ReadErrorAsync(response));
        }

        return (await response.Content.ReadFromJsonAsync<SessionSummaryResponse>(JsonOptions), null);
    }

    public async Task<(SessionDetailResponse? Session, string? Error)> AdvanceAsync(int sessionId)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/sessions/{sessionId}/advance", null);

        if (!response.IsSuccessStatusCode)
        {
            return (null, await ReadErrorAsync(response));
        }

        return (await response.Content.ReadFromJsonAsync<SessionDetailResponse>(JsonOptions), null);
    }

    public async Task<(SessionDetailResponse? Session, string? Error)> SubmitAnswerAsync(
        int sessionId, int questionId, string text)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/sessions/{sessionId}/answers",
            JsonContent.Create(new SubmitAnswerRequest(questionId, text), options: JsonOptions));

        if (!response.IsSuccessStatusCode)
        {
            return (null, await ReadErrorAsync(response));
        }

        return (await response.Content.ReadFromJsonAsync<SessionDetailResponse>(JsonOptions), null);
    }

    public Task<IReadOnlyList<AdminUserResponse>> GetAdminUsersAsync() =>
        GetAsync<IReadOnlyList<AdminUserResponse>>("/api/admin/users", []);

    public Task<IReadOnlyList<AdminSessionResponse>> GetAdminSessionsAsync() =>
        GetAsync<IReadOnlyList<AdminSessionResponse>>("/api/admin/sessions", []);

    public Task<AdminStatsResponse?> GetAdminStatsAsync() =>
        GetAsync<AdminStatsResponse?>("/api/admin/stats", null);

    private async Task<T> GetAsync<T>(string url, T fallback)
    {
        var response = await SendAsync(HttpMethod.Get, url, null);

        if (!response.IsSuccessStatusCode)
        {
            return fallback;
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions) ?? fallback;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };

        if (_auth.Token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);
        }

        return await _http.SendAsync(request);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(body))
        {
            return $"Request failed ({(int)response.StatusCode}).";
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return string.Join(" ", document.RootElement.EnumerateArray().Select(e => e.GetString()));
            }

            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
        }

        return body;
    }
}

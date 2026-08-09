using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;

namespace PortfolioCMS.Web.Services;

// ─── Auth State ───────────────────────────────────────────────────────────────

public class AuthState
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    public bool IsAdmin => Role == "Admin";
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
}

// ─── DTOs (mirrored from API — no project reference needed in WASM) ───────────

public record SectionDto(int Id, string Type, string Title, string? SubTitle,
    string Content, bool IsVisible, int DisplayOrder, string BackgroundColor, string TextColor);

public record ProjectDto(int Id, string Title, string Description, string TechStack,
    string? GitHubUrl, string? LiveUrl, string? ImageUrl, int DisplayOrder, bool IsVisible);

public record ThemeDto(int Id, string PrimaryColor, string SecondaryColor, string AccentColor,
    string BackgroundColor, string SurfaceColor, string TextColor, string FontFamily, string HeadingFontFamily);

public record AuthResponse(string AccessToken, string RefreshToken, string Username, string Role);

/// <summary>Theme + sections + projects for one user, fetched in a single call.</summary>
public record PublicPortfolioDto(string Username, ThemeDto? Theme,
    List<SectionDto> Sections, List<ProjectDto> Projects);

public record ImproveTextResponse(string OriginalText, string ImprovedText);

public record GenerateProjectDescResponse(string Description, string TechStack);

// ─── Portfolio API Client ─────────────────────────────────────────────────────

public class PortfolioApiService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _storage;
    public AuthState Auth { get; } = new();

    public event Action? OnAuthStateChanged;

    public PortfolioApiService(HttpClient http, ILocalStorageService storage)
    { _http = http; _storage = storage; }

    // Call on app startup to restore session from localStorage
    public async Task InitializeAsync()
    {
        var token = await _storage.GetItemAsync<string>("access_token");
        var refresh = await _storage.GetItemAsync<string>("refresh_token");
        var username = await _storage.GetItemAsync<string>("username");
        var role = await _storage.GetItemAsync<string>("role");

        if (!string.IsNullOrEmpty(token))
        {
            Auth.AccessToken = token;
            Auth.RefreshToken = refresh;
            Auth.Username = username;
            Auth.Role = role;
            SetAuthHeader(token);
        }
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    public async Task<bool> LoginAsync(string username, string password)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/login",
            new { username, password });

        if (!resp.IsSuccessStatusCode) return false;

        var result = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        if (result is null) return false;

        await PersistAuth(result);
        return true;
    }

    public async Task LogoutAsync()
    {
        Auth.AccessToken = null;
        Auth.RefreshToken = null;
        Auth.Username = null;
        Auth.Role = null;
        _http.DefaultRequestHeaders.Authorization = null;

        await _storage.RemoveItemAsync("access_token");
        await _storage.RemoveItemAsync("refresh_token");
        await _storage.RemoveItemAsync("username");
        await _storage.RemoveItemAsync("role");

        OnAuthStateChanged?.Invoke();
    }

    private async Task PersistAuth(AuthResponse result)
    {
        Auth.AccessToken = result.AccessToken;
        Auth.RefreshToken = result.RefreshToken;
        Auth.Username = result.Username;
        Auth.Role = result.Role;
        SetAuthHeader(result.AccessToken);

        await _storage.SetItemAsync("access_token", result.AccessToken);
        await _storage.SetItemAsync("refresh_token", result.RefreshToken);
        await _storage.SetItemAsync("username", result.Username);
        await _storage.SetItemAsync("role", result.Role);

        OnAuthStateChanged?.Invoke();
    }

    private void SetAuthHeader(string token)
        => _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    // ── Registration ──────────────────────────────────────────────────────────

    /// <summary>Creates an account and signs straight in. Returns null plus an
    /// error message when the username is taken, reserved, or invalid.</summary>
    public async Task<(bool ok, string? error)> RegisterAsync(string username, string password)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/register", new { username, password });

        if (!resp.IsSuccessStatusCode)
            return (false, await ReadProblemAsync(resp));

        var result = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        if (result is null) return (false, "Registration failed");

        await PersistAuth(result);
        return (true, null);
    }

    /// <summary>Pulls the human-readable message out of an RFC7807 problem
    /// response so the form can show why the server said no.</summary>
    private static async Task<string> ReadProblemAsync(HttpResponseMessage resp)
    {
        try
        {
            var problem = await resp.Content.ReadFromJsonAsync<JsonElement>();

            foreach (var key in new[] { "detail", "title" })
                if (problem.TryGetProperty(key, out var v) && v.GetString() is { Length: > 0 } msg)
                    return msg;

            if (problem.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                    foreach (var m in field.Value.EnumerateArray())
                        return m.GetString() ?? "Registration failed";
            }
        }
        catch { /* not a problem document — fall through */ }

        return $"Registration failed ({(int)resp.StatusCode})";
    }

    // ── Public portfolios ─────────────────────────────────────────────────────

    /// <summary>Anonymous read of any user's published portfolio.</summary>
    public async Task<PublicPortfolioDto?> GetPublicPortfolioAsync(string? username = null)
    {
        var url = string.IsNullOrWhiteSpace(username)
            ? "/api/portfolios/"
            : $"/api/portfolios/{Uri.EscapeDataString(username)}";

        var resp = await _http.GetAsync(url);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PublicPortfolioDto>()
            : null;
    }

    /// <summary>The signed-in user's own portfolio, including hidden items.</summary>
    public async Task<PublicPortfolioDto?> GetMyPortfolioAsync()
    {
        var resp = await _http.GetAsync("/api/me/portfolio");
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PublicPortfolioDto>()
            : null;
    }

    // ── Sections ──────────────────────────────────────────────────────────────

    public Task<List<SectionDto>?> GetSectionsAsync()
        => _http.GetFromJsonAsync<List<SectionDto>>("/api/sections");

    public async Task<SectionDto?> UpdateSectionAsync(int id, object payload)
    {
        var resp = await _http.PutAsJsonAsync($"/api/sections/{id}", payload);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SectionDto>();
    }

    public async Task ReorderSectionsAsync(List<(int Id, int Order)> orders)
    {
        var payload = orders.Select(o => new { id = o.Id, order = o.Order });
        var resp = await _http.PostAsJsonAsync("/api/sections/reorder", payload);
        resp.EnsureSuccessStatusCode();
    }

    // ── Projects ──────────────────────────────────────────────────────────────

    public Task<List<ProjectDto>?> GetProjectsAsync(bool visibleOnly = true)
        => _http.GetFromJsonAsync<List<ProjectDto>>($"/api/projects?visibleOnly={visibleOnly}");

    public async Task<ProjectDto?> CreateProjectAsync(object payload)
    {
        var resp = await _http.PostAsJsonAsync("/api/projects", payload);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ProjectDto>();
    }

    public async Task<ProjectDto?> UpdateProjectAsync(int id, object payload)
    {
        var resp = await _http.PutAsJsonAsync($"/api/projects/{id}", payload);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ProjectDto>();
    }

    public async Task DeleteProjectAsync(int id)
    {
        var resp = await _http.DeleteAsync($"/api/projects/{id}");
        resp.EnsureSuccessStatusCode();
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    public Task<ThemeDto?> GetThemeAsync()
        => _http.GetFromJsonAsync<ThemeDto>("/api/theme");

    public async Task<ThemeDto?> UpdateThemeAsync(object payload)
    {
        var resp = await _http.PutAsJsonAsync("/api/theme", payload);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ThemeDto>();
    }

    // ── Image Upload ──────────────────────────────────────────────────────────

    public async Task<string?> UploadImageAsync(Stream fileStream, string fileName, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        var resp = await _http.PostAsync("/api/images/upload", content);
        if (!resp.IsSuccessStatusCode) return null;

        var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("url").GetString();
    }

    // ── AI ────────────────────────────────────────────────────────────────────

    public async Task<string?> ImproveTextAsync(string text, string context)
    {
        var resp = await _http.PostAsJsonAsync("/api/ai/improve-text",
            new { text, context });
        if (!resp.IsSuccessStatusCode) return null;
        var result = await resp.Content.ReadFromJsonAsync<ImproveTextResponse>();
        return result?.ImprovedText;
    }

    public async Task<GenerateProjectDescResponse?> GenerateProjectDescAsync(
        string readmeContent, string projectTitle)
    {
        var resp = await _http.PostAsJsonAsync("/api/ai/generate-project-desc",
            new { readmeContent, projectTitle });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<GenerateProjectDescResponse>();
    }
}

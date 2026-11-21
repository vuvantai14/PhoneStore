using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Management;

namespace PhoneStore.Business.AdminApi;

public sealed class AdminApiException(string message, HttpStatusCode? statusCode = null, bool isTimeout = false, Exception? inner = null)
    : Exception(message, inner)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public bool IsTimeout { get; } = isTimeout;
}

public sealed class AdminApiClient
{
    private static readonly TimeSpan ProactiveRefreshSkew = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly AdminApiSessionStore _sessions;
    private readonly TimeProvider _timeProvider;

    public AdminApiClient(HttpClient httpClient, AdminApiSessionStore sessionStore, TimeProvider? timeProvider = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _sessions = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public AdminApiSessionStore SessionStore => _sessions;

    public static HttpClient CreateHttpClient(AdminDataAccessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = options.GetNormalizedBaseUri(),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
    }

    public async Task<AdminLoginResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/v1/auth/admin/login", authenticated: false);
        request.Content = JsonContent.Create(new { email = email.Trim(), password });

        using var response = await SendCoreAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return new AdminLoginResult(AdminLoginStatus.InvalidCredentials);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new AdminLoginResult(AdminLoginStatus.Forbidden);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            return new AdminLoginResult(AdminLoginStatus.InvalidCredentials);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new AdminApiException(
                "Quá nhiều yêu cầu đăng nhập trong thời gian ngắn. Vui lòng đợi ít giây rồi thử lại.",
                HttpStatusCode.TooManyRequests);
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadProblemDetailAsync(response, ct);
            throw new AdminApiException(
                detail ?? "Không thể kết nối máy chủ xác thực PhoneStore API.",
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<ApiAuthTokenDto>(JsonOptions, ct)
            ?? throw new AdminApiException("Phản hồi đăng nhập từ máy chủ API không hợp lệ.", response.StatusCode);

        if (payload.User is null || !string.Equals(payload.User.Role, SystemRoles.Admin, StringComparison.Ordinal))
            return new AdminLoginResult(AdminLoginStatus.Forbidden);

        var identity = new AdminIdentity(payload.User.UserId, payload.User.FullName, payload.User.Email, payload.User.Role);
        _sessions.Establish(payload.AccessToken, payload.ExpiresAtUtc, payload.RefreshToken, payload.RefreshExpiresAtUtc, identity);
        return new AdminLoginResult(AdminLoginStatus.Success, identity);
    }

    public async Task<bool> VerifyCurrentAdminAsync(int userId, CancellationToken ct)
    {
        if (userId <= 0 || _sessions.Current is null)
            return false;

        try
        {
            var me = await GetAsync<ApiCurrentUserDto>("api/v1/auth/me", ct);
            if (me is null || me.UserId != userId || !string.Equals(me.Role, SystemRoles.Admin, StringComparison.Ordinal))
            {
                _sessions.Clear();
                return false;
            }
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _sessions.Clear();
            return false;
        }
    }

    public async Task LogoutAsync(CancellationToken ct)
    {
        var snapshot = _sessions.Current;
        try
        {
            if (snapshot is not null && !string.IsNullOrWhiteSpace(snapshot.AccessToken))
            {
                using var request = CreateRequest(HttpMethod.Post, "api/v1/auth/logout", authenticated: false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
                request.Content = JsonContent.Create(new { refreshToken = snapshot.RefreshToken });
                using var _ = await SendCoreAsync(request, ct);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AdminApiException)
        {
            // Always clear local session even if the server cannot be reached during logout.
        }
        finally
        {
            _sessions.Clear();
        }
    }

    public async Task<bool> TryRefreshSessionAsync(CancellationToken ct)
    {
        var before = _sessions.Current;
        if (before is null || string.IsNullOrWhiteSpace(before.RefreshToken))
            return false;

        await _sessions.RefreshLock.WaitAsync(ct);
        try
        {
            var latest = _sessions.Current;
            if (latest is null)
                return false;

            // Another concurrent request already refreshed the session.
            if (!string.Equals(latest.AccessToken, before.AccessToken, StringComparison.Ordinal))
                return true;

            using var request = CreateRequest(HttpMethod.Post, "api/v1/auth/refresh", authenticated: false);
            request.Content = JsonContent.Create(new { refreshToken = latest.RefreshToken });

            using var response = await SendCoreAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    _sessions.Clear();
                return false;
            }

            var refreshed = await response.Content.ReadFromJsonAsync<ApiAuthTokenDto>(JsonOptions, ct);
            if (refreshed?.User is null || !string.Equals(refreshed.User.Role, SystemRoles.Admin, StringComparison.Ordinal))
            {
                _sessions.Clear();
                return false;
            }

            var identity = new AdminIdentity(refreshed.User.UserId, refreshed.User.FullName, refreshed.User.Email, refreshed.User.Role);
            _sessions.Establish(refreshed.AccessToken, refreshed.ExpiresAtUtc, refreshed.RefreshToken, refreshed.RefreshExpiresAtUtc, identity);
            return true;
        }
        finally
        {
            _sessions.RefreshLock.Release();
        }
    }

    public async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct, bool allowNotFound = false)
    {
        await EnsureFreshTokenAsync(ct);

        using var firstRequest = CreateRequest(HttpMethod.Get, relativeUrl, authenticated: true);
        using var firstResponse = await SendCoreAsync(firstRequest, ct);

        HttpResponseMessage response = firstResponse;
        HttpResponseMessage? retryResponse = null;
        try
        {
            if (firstResponse.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshSessionAsync(ct))
            {
                using var retryRequest = CreateRequest(HttpMethod.Get, relativeUrl, authenticated: true);
                retryResponse = await SendCoreAsync(retryRequest, ct);
                response = retryResponse;
            }

            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                return default;

            await EnsureAuthorizedAndSuccessAsync(response, ct);
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        finally
        {
            retryResponse?.Dispose();
        }
    }

    public async Task<ManagementResult> SendManagementAsync(
        HttpMethod method,
        string relativeUrl,
        object? jsonBody,
        CancellationToken ct,
        Func<HttpResponseMessage, CancellationToken, Task<ManagementResult>>? onSuccess = null)
    {
        // Proactively refresh before sending writes so non-idempotent writes do not need blind retry.
        await EnsureFreshTokenAsync(ct);

        using var request = CreateRequest(method, relativeUrl, authenticated: true);
        if (jsonBody is not null)
            request.Content = JsonContent.Create(jsonBody);

        try
        {
            using var response = await SendCoreAsync(request, ct);
            return await MapManagementResponseAsync(response, ct, onSuccess);
        }
        catch (AdminApiException ex)
        {
            return ManagementResult.Fail(ex.Message, ManagementFailure.Database);
        }
    }

    public async Task<ManagementResult> UploadMultipartAsync(
        string relativeUrl,
        string sourceFilePath,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            return ManagementResult.Fail("Không tìm thấy tệp ảnh đã chọn.");

        await EnsureFreshTokenAsync(ct);

        try
        {
            await using var fileStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", Path.GetFileName(sourceFilePath));

            using var request = CreateRequest(HttpMethod.Post, relativeUrl, authenticated: true);
            request.Content = content;

            using var response = await SendCoreAsync(request, ct);
            return await MapManagementResponseAsync(response, ct, async (resp, token) =>
            {
                var uploaded = await resp.Content.ReadFromJsonAsync<ApiProductImageUploadDto>(JsonOptions, token);
                return ManagementResult.Success(uploaded?.ImageId, uploaded?.Notice);
            });
        }
        catch (AdminApiException ex)
        {
            return ManagementResult.Fail(ex.Message, ManagementFailure.Database);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ManagementResult.Fail("Không thể đọc tệp ảnh từ máy tính để tải lên.");
        }
    }

    private async Task EnsureFreshTokenAsync(CancellationToken ct)
    {
        var current = _sessions.Current;
        if (current is null)
            throw new UnauthorizedAccessException("Phiên đăng nhập quản trị đã kết thúc.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (now >= current.AccessExpiresAtUtc - ProactiveRefreshSkew)
        {
            if (!await TryRefreshSessionAsync(ct))
            {
                _sessions.Clear();
                throw new UnauthorizedAccessException("Phiên đăng nhập quản trị đã hết hạn. Vui lòng đăng nhập lại.");
            }
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, bool authenticated)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString("N"));
        if (authenticated)
        {
            var token = _sessions.Current?.AccessToken
                ?? throw new UnauthorizedAccessException("Phiên đăng nhập quản trị đã kết thúc.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return request;
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new AdminApiException("Yêu cầu tới PhoneStore API đã hết thời gian chờ.", isTimeout: true, inner: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AdminApiException("Không thể kết nối tới PhoneStore API. Vui lòng kiểm tra dịch vụ API.", inner: ex);
        }
    }

    private async Task EnsureAuthorizedAndSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessions.Clear();
            throw new UnauthorizedAccessException(response.StatusCode == HttpStatusCode.Forbidden
                ? "Tài khoản quản trị không còn quyền thực hiện thao tác này."
                : "Phiên đăng nhập quản trị đã hết hạn hoặc bị thu hồi.");
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new AdminApiException(
                "Thao tác quá nhanh (vượt giới hạn tần suất API). Vui lòng đợi ít giây rồi thử lại.",
                HttpStatusCode.TooManyRequests);
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadProblemDetailAsync(response, ct);
            throw new AdminApiException(
                detail ?? $"PhoneStore API trả về lỗi HTTP {(int)response.StatusCode}.",
                response.StatusCode);
        }
    }

    private async Task<ManagementResult> MapManagementResponseAsync(
        HttpResponseMessage response,
        CancellationToken ct,
        Func<HttpResponseMessage, CancellationToken, Task<ManagementResult>>? onSuccess)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _sessions.Clear();
            throw new UnauthorizedAccessException(response.StatusCode == HttpStatusCode.Forbidden
                ? "Tài khoản quản trị không còn quyền thực hiện thao tác này."
                : "Phiên đăng nhập quản trị đã hết hạn hoặc bị thu hồi.");
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return ManagementResult.Fail(
                "Thao tác quá nhanh (vượt giới hạn tần suất API). Vui lòng đợi ít giây rồi thử lại.",
                ManagementFailure.Database);
        }

        if (response.IsSuccessStatusCode)
        {
            if (onSuccess is not null)
                return await onSuccess(response, ct);
            return ManagementResult.Success();
        }

        var detail = await ReadProblemDetailAsync(response, ct)
            ?? "Không thể thực hiện thao tác. Vui lòng kiểm tra dữ liệu và thử lại.";
        var failure = response.StatusCode switch
        {
            HttpStatusCode.NotFound => ManagementFailure.NotFound,
            HttpStatusCode.Conflict => ManagementFailure.Conflict,
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError or HttpStatusCode.GatewayTimeout
                => ManagementFailure.Database,
            _ => ManagementFailure.Validation
        };
        return ManagementResult.Fail(detail, failure);
    }

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text)) return null;
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detailProp) && detailProp.ValueKind == JsonValueKind.String)
            {
                var detail = detailProp.GetString();
                if (!string.IsNullOrWhiteSpace(detail)) return detail;
            }
            if (root.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in errorsProp.EnumerateObject())
                {
                    if (entry.Value.ValueKind == JsonValueKind.Array && entry.Value.GetArrayLength() > 0)
                    {
                        var first = entry.Value[0].GetString();
                        if (!string.IsNullOrWhiteSpace(first)) return first;
                    }
                }
            }
            if (root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                return titleProp.GetString();
        }
        catch (JsonException)
        {
        }
        return null;
    }

    internal static JsonSerializerOptions SerializerOptions => JsonOptions;
}

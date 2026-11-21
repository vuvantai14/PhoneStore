namespace PhoneStore.Business.AdminApi;

public enum AdminDataAccessMode
{
    DirectSql = 0,
    RestApi = 1
}

public sealed class AdminDataAccessOptions
{
    public const string SectionName = "AdminDataAccess";

    public AdminDataAccessMode Mode { get; init; } = AdminDataAccessMode.DirectSql;
    public string ApiBaseUrl { get; init; } = "http://localhost:5025";
    public int TimeoutSeconds { get; init; } = 15;

    public static AdminDataAccessOptions Parse(string? modeValue, string? apiBaseUrl, string? timeoutSeconds)
    {
        AdminDataAccessMode mode;
        if (string.IsNullOrWhiteSpace(modeValue))
        {
            mode = AdminDataAccessMode.DirectSql;
        }
        else if (!Enum.TryParse(modeValue.Trim(), ignoreCase: true, out mode) || !Enum.IsDefined(mode))
        {
            throw new InvalidOperationException(
                $"Invalid AdminDataAccess:Mode value '{modeValue}'. Supported values are DirectSql and RestApi.");
        }

        var timeout = 15;
        if (!string.IsNullOrWhiteSpace(timeoutSeconds))
        {
            if (!int.TryParse(timeoutSeconds.Trim(), out timeout))
                throw new InvalidOperationException("AdminDataAccess:TimeoutSeconds must be an integer.");
        }

        var options = new AdminDataAccessOptions
        {
            Mode = mode,
            ApiBaseUrl = string.IsNullOrWhiteSpace(apiBaseUrl) ? "http://localhost:5025" : apiBaseUrl.Trim(),
            TimeoutSeconds = timeout
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Mode))
            throw new InvalidOperationException("AdminDataAccess:Mode is invalid.");

        if (TimeoutSeconds is < 1 or > 120)
            throw new InvalidOperationException("AdminDataAccess:TimeoutSeconds must be between 1 and 120.");

        if (Mode == AdminDataAccessMode.RestApi)
        {
            if (string.IsNullOrWhiteSpace(ApiBaseUrl) ||
                !Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidOperationException(
                    "AdminDataAccess:ApiBaseUrl must be a valid http/https base URL without credentials, query, or fragment.");
            }
        }
    }

    public Uri GetNormalizedBaseUri()
    {
        Validate();
        var trimmed = ApiBaseUrl.TrimEnd('/') + "/";
        return new Uri(trimmed, UriKind.Absolute);
    }
}

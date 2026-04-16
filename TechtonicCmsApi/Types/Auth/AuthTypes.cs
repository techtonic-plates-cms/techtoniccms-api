using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Types.Auth;

public class Token
{
    public string TokenValue { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}



public class LoginPayload
{
    public required Token AccessToken { get; set; }
    public required Token RefreshToken { get; set; }
    public User? User { get; set; }
}

public class RefreshPayload
{
    public string AccessToken { get; set; } = "";
}

public class LogoutPayload
{
    public string Message { get; set; } = "";
}

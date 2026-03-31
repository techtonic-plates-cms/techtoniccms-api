using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Types.Auth;

public class LoginPayload
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
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

namespace DokTrino.SuperAdmin;

/// <summary>
/// Version legible de la aplicacion. INCREMENTAR ESTE VALOR a mano en cada release
/// (es la unica fuente de verdad de la version que se muestra en login, menu y /version).
/// El SHA/fecha de build los inyecta el CI como variables de entorno.
/// </summary>
public static class AppInfo
{
    public const string Version = "v0.0.1";

    public static string BuildSha => Environment.GetEnvironmentVariable("APP_BUILD_SHA") ?? "dev";
    public static string BuildTime => Environment.GetEnvironmentVariable("APP_BUILD_TIME") ?? "-";
    public static string BuildShaShort
    {
        get { var s = BuildSha; return s.Length > 7 ? s[..7] : s; }
    }
}

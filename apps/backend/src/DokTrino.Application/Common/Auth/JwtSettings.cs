namespace DokTrino.Application.Common.Auth;

/// <summary>Configuracion del JWT propio de DOKTRINO.travels (seccion "Jwt").</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "DokTrino";
    public string Audience { get; set; } = "DokTrino";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}

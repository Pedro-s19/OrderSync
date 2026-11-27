
namespace OrderSync.Infrastructure.Security;

public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public string SmtpServer { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

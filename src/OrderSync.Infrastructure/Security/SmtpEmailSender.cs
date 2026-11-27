
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using OrderSync.Application.Seguridad;
using System.Diagnostics;

namespace OrderSync.Infrastructure.Security;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly MailOptions _options;

    public SmtpEmailSender(IOptions<MailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(
        string destination,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        var etapa = "Validación de configuración";

        try
        {
            if (string.IsNullOrWhiteSpace(_options.SmtpServer) ||
                string.IsNullOrWhiteSpace(_options.Username) ||
                string.IsNullOrWhiteSpace(_options.Password) ||
                string.IsNullOrWhiteSpace(_options.From))
            {
                throw new InvalidOperationException(
                    "La configuración SMTP está incompleta.");
            }

            var message = new MimeMessage();

            message.From.Add(
                MailboxAddress.Parse(_options.From));

            message.To.Add(
                MailboxAddress.Parse(destination));

            message.Subject = subject;

            message.Body = new TextPart("plain")
            {
                Text = body
            };

            using var client = new SmtpClient();

            var socketOptions = _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            etapa = "Conexión con Gmail";

            await client.ConnectAsync(
                _options.SmtpServer,
                _options.Port,
                socketOptions,
                ct);

            etapa = "Autenticación con Gmail";

            var mecanismoLogin = new SaslMechanismLogin(
                 _options.Username,
                 _options.Password);

            await client.AuthenticateAsync(
                mecanismoLogin,
                ct);

            etapa = "Envío del correo";

            await client.SendAsync(message, ct);

            etapa = "Desconexión";

            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("========== ERROR SMTP ==========");
            Debug.WriteLine($"ETAPA: {etapa}");
            Debug.WriteLine(ex.ToString());
            Debug.WriteLine("================================");

            throw;
        }
    }
}

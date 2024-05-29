using System.Net.Mail;

using Bss.Platform.Notifications.Audit.Models;
using Bss.Platform.Notifications.Interfaces;

using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bss.Platform.Notifications.Audit.Services;

public class AuditService(ILogger<AuditService> logger, IOptions<NotificationAuditOptions> settings) : IAuditService
{
    private const string Sql = """
                                   insert into [{0}].[{1}]
                                       ([id], [from], [to], [copy], [replyTo], [subject], [message], [timestamp])
                                   values
                                       (newid(), @from, @to, @copy, @replyTo, @subject, @message, getdate())
                               """;

    public async Task LogAsync(MailMessage message, CancellationToken token)
    {
        try
        {
            await using var db = new SqlConnection(settings.Value.ConnectionString);
            await db.OpenAsync(token);

            await db.ExecuteAsync(
                string.Format(Sql, settings.Value.Schema, settings.Value.Table),
                new
                {
                    from = message.From!.Address,
                    to = string.Join(";", message.To.Select(x => x.Address)),
                    copy = string.Join(";", message.CC.Select(x => x.Address)),
                    replyTo = string.Join(";", message.ReplyToList.Select(x => x.Address)),
                    subject = message.Subject,
                    message = message.Body
                });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to log sent message");
        }
    }
}

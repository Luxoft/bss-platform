using Bss.Platform.Notifications.Audit.Models;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace Bss.Platform.Notifications.Audit.Services;

public class AuditSchemaMigrationService(ILogger<AuditSchemaMigrationService> logger, IOptions<NotificationAuditOptions> settings)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Schema migration: start");

            await using var connection = new SqlConnection(settings.Value.ConnectionString);
            await connection.OpenAsync(stoppingToken);

            var server = new Server(new ServerConnection(connection));
            var catalog = server.ConnectionContext.CurrentDatabase;

            if (string.IsNullOrWhiteSpace(catalog) || !server.Databases.Contains(catalog))
            {
                throw new ArgumentException("Initial catalog not provided or does not exist");
            }

            var database = server.Databases[catalog];

            if (!database.Tables.Contains(NotificationAuditOptions.TableName, settings.Value.Schema))
            {
                if (!database.Schemas.Contains(settings.Value.Schema))
                {
                    server.ConnectionContext.ExecuteNonQuery($"CREATE SCHEMA [{settings.Value.Schema}]");
                }

                server.ConnectionContext.ExecuteNonQuery(
                    $"""
                     CREATE TABLE [{settings.Value.Schema}].[{NotificationAuditOptions.TableName}] (
                                     [id] [uniqueidentifier] NOT NULL PRIMARY KEY,
                                     [from] [nvarchar](255) NOT NULL,
                                     [to] [nvarchar](max) NULL,
                                     [copy] [nvarchar](max) NULL,
                                     [replyTo] [nvarchar](max) NULL,
                                     [subject] [nvarchar](max) NULL,
                                     [message] [nvarchar](max) NULL,
                                     [date] [datetime2](7) NOT NULL)
                     """);
            }

            await connection.CloseAsync();

            logger.LogInformation("Schema migration: end");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Schema migration: failed");
            throw;
        }
    }
}

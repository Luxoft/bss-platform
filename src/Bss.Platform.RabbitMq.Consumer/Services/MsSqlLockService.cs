using System.Data;

using Bss.Platform.RabbitMq.Consumer.Interfaces;
using Bss.Platform.RabbitMq.Consumer.Settings;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Bss.Platform.RabbitMq.Consumer.Services;

internal record MsSqlLockService(IOptions<RabbitMqConsumerSettings> ConsumerSettings) : IRabbitMqConsumerLockService
{
    public bool TryObtainLock(SqlConnection connection)
    {
        try
        {
            var cmd = new SqlCommand("sp_getapplock", connection);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@Resource", this.GetLockName()));
            cmd.Parameters.Add(new SqlParameter("@LockMode", "Exclusive"));
            cmd.Parameters.Add(new SqlParameter("@LockOwner", "Session"));
            cmd.Parameters.Add(new SqlParameter("@LockTimeout", "0"));

            var returnValue = new SqlParameter { Direction = ParameterDirection.ReturnValue };
            cmd.Parameters.Add(returnValue);

            cmd.ExecuteNonQuery();

            return (int)returnValue.Value >= 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void TryReleaseLock(SqlConnection connection)
    {
        try
        {
            var cmd = new SqlCommand("sp_releaseapplock", connection);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@Resource", this.GetLockName()));
            cmd.Parameters.Add(new SqlParameter("@LockOwner", "Session"));

            cmd.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private string GetLockName() => $"{this.ConsumerSettings.Value.Queue}_Consumer_Lock";
}

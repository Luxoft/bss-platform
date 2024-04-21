using Microsoft.Data.SqlClient;

namespace Bss.Platform.RabbitMq.Consumer.Interfaces;

public interface IRabbitMqConsumerLockService
{
    bool TryObtainLock(SqlConnection connection);

    void TryReleaseLock(SqlConnection connection);
}

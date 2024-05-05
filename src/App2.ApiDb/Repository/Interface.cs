using App2.ApiDb.Events;

namespace App2.ApiDb.Repository;

public interface IRabbitRepository
{
    void Publish(IEvent evt);
}
public interface ISqlRepository
{
    Task Persist(string message);
}
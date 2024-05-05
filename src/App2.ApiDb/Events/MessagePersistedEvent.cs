namespace App2.ApiDb.Events;

public class MessagePersistedEvent : IEvent
{
    public string Message { get; set; }
}

public interface IEvent
{

}
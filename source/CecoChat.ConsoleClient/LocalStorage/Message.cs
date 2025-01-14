namespace CecoChat.ConsoleClient.LocalStorage;

public sealed class Message
{
    public long MessageId { get; init; }
    public long SenderId { get; init; }
    public long ReceiverId { get; init; }
    public DataType DataType { get; set; }
    public string Data { get; set; } = string.Empty;
    public IDictionary<long, string> Reactions { get; }

    public Message()
    {
        Reactions = new Dictionary<long, string>(capacity: 2);
    }
}

public enum DataType
{
    PlainText
}

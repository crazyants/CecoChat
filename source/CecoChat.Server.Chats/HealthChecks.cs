using CecoChat.Health;

namespace CecoChat.Server.Chats;

public class ConfigDbInitHealthCheck : StatusHealthCheck
{ }

public class ChatsDbInitHealthCheck : StatusHealthCheck
{ }

public class HistoryConsumerHealthCheck : StatusHealthCheck
{ }

public class ReceiversConsumerHealthCheck : StatusHealthCheck
{ }

public class SendersConsumerHealthCheck : StatusHealthCheck
{ }

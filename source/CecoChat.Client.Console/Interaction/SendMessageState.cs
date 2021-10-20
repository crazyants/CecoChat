using System.Threading.Tasks;

namespace CecoChat.Client.Console.Interaction
{
    public sealed class SendMessageState : State
    {
        public SendMessageState(StateContainer states) : base(states)
        {}

        public override async Task<State> Execute()
        {
            System.Console.Write("Message to ID={0}: ", Context.UserID);
            string plainText = System.Console.ReadLine();
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return States.Chat;
            }

            long messageID = await Client.SendPlainTextMessage(Context.UserID, plainText);
            Message message = new()
            {
                MessageID = messageID,
                SenderID = Client.UserID,
                ReceiverID = Context.UserID,
                DataType = DataType.PlainText,
                Data = plainText,
                Status = DeliveryStatus.Unprocessed
            };
            Storage.AddMessage(message);

            Context.ReloadData = true;
            return States.Chat;
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CecoChat.Client.Shared;
using CecoChat.Client.Shared.Storage;
using CecoChat.Contracts.Client;
using Microsoft.Toolkit.Mvvm.Input;
using PropertyChanged;

namespace CecoChat.Client.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public sealed class AllChatsViewModel : BaseViewModel
    {
        private readonly Dictionary<long, AllChatsItemViewModel> _chatsMap;

        public AllChatsViewModel(
            MessagingClient messagingClient,
            MessageStorage messageStorage,
            IDispatcher uiThreadDispatcher,
            IErrorService errorService,
            SingleChatViewModel singleChatVM)
            : base(messagingClient, messageStorage, uiThreadDispatcher, errorService)
        {
            _chatsMap = new Dictionary<long, AllChatsItemViewModel>();

            MessagingClient.MessageReceived += MessagingClientOnMessageReceived;

            CanOperate = true;
            Chats = new ObservableCollection<AllChatsItemViewModel>();
            SelectionChanged = new AsyncRelayCommand(SelectionChangedOnExecute);
            SingleChatVM = singleChatVM;

            SingleChatVM.MessageSent += SingleChatVMOnMessageSent;
        }

        public bool CanOperate { get; set; }

        public ObservableCollection<AllChatsItemViewModel> Chats { get; }

        public AllChatsItemViewModel SelectedChat { get; set; }

        public ICommand SelectionChanged { get; }

        public SingleChatViewModel SingleChatVM { get; }

        private void MessagingClientOnMessageReceived(object sender, ClientMessage message)
        {
            if (!TryGetOtherUserID(message, out long otherUserID))
            {
                return;
            }
            MessageStorage.AddMessage(otherUserID, message);
            ShowLastMessageFromUser(message, otherUserID);
        }

        private Task SelectionChangedOnExecute()
        {
            return SingleChatVM.SetOtherUser(SelectedChat.UserID);
        }

        public void Start()
        {
            Task.Run(async () => await DoStart());
        }

        private async Task DoStart()
        {
            try
            {
                IList<ClientMessage> messageHistory = await MessagingClient.GetUserHistory(DateTime.UtcNow);
                foreach (ClientMessage message in messageHistory)
                {
                    if (!TryGetOtherUserID(message, out long otherUserID))
                    {
                        break;
                    }
                    MessageStorage.AddMessage(otherUserID, message);
                    ShowLastMessageFromUser(message, otherUserID);
                }
            }
            catch (Exception exception)
            {
                ErrorService.ShowError(exception);
            }
        }

        private void SingleChatVMOnMessageSent(object sender, ClientMessage message)
        {
            if (!TryGetOtherUserID(message, out long otherUserID))
            {
                return;
            }
            ShowLastMessageFromUser(message, otherUserID);
        }

        private bool TryGetOtherUserID(ClientMessage message, out long otherUserID)
        {
            if (message.SenderId != MessagingClient.UserID)
            {
                otherUserID = message.SenderId;
                return true;
            }
            else if (message.ReceiverId != MessagingClient.UserID)
            {
                otherUserID = message.ReceiverId;
                return true;
            }
            else
            {
                ErrorService.ShowError($"Message '{message}' is from current user {MessagingClient.UserID} to himself.");
                otherUserID = -1;
                return false;
            }
        }

        private void ShowLastMessageFromUser(ClientMessage message, long otherUsedID)
        {
            DateTime messageTimestamp = message.Timestamp.ToDateTime();
            if (_chatsMap.TryGetValue(otherUsedID, out AllChatsItemViewModel chatVM))
            {
                if (chatVM.Timestamp < messageTimestamp)
                {
                    UIThreadDispatcher.Invoke(() =>
                    {
                        chatVM.LastMessage = message.PlainTextData.Text;
                        chatVM.Timestamp = messageTimestamp;
                    });
                }
            }
            else
            {
                chatVM = new AllChatsItemViewModel
                {
                    UserID = otherUsedID,
                    LastMessage = message.PlainTextData.Text,
                    Timestamp = messageTimestamp
                };

                _chatsMap.Add(otherUsedID, chatVM);
                UIThreadDispatcher.Invoke(() => Chats.Add(chatVM));
            }
        }
    }
}
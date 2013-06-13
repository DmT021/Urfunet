using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using NLog;
using YnetFS.Messages;

namespace YnetFS.InteractionEnvironment
{
    public class MemoryIE : BaseInteractionEnvironment
    {

        public static ObservableCollection<Client> Clients = new ObservableCollection<Client>();

        public MemoryIE(Client parent)
            : base(parent)
        {
            lock (Clients)
            {
                Clients.CollectionChanged += Clients_CollectionChanged;
            }
        }

        public override void Start()
        {
            lock (Clients)
            {
                Clients.Add(ParentClient);
            }
        }

        void Clients_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Client c in e.NewItems)
                {
                    if (c.Id != ParentClient.Id)
                    {
                        RemoteClients.Add(new RemoteClient(c.Id, this)
                        {
                            IsOnline = c.State != ClientStates.Offline,
                            Synchronized = c.Synchronized
                        });
                    }
                }
            }
        }

        public override void BootStrap()
        {
            ///look for connected clients and add them to the remotes
            foreach (var c in Clients)
            {
                if (c.Id != ParentClient.Id)
                {
                    RemoteClients.Add(new RemoteClient(c.Id, this)
                    {
                        IsOnline = c.State != ClientStates.Offline,
                        Synchronized = c.Synchronized
                    });
                }
            }
        }

        public override void Send(RemoteClient RemoteClient, Messages.Message message)
        {
            ///Remote Client must be offline
            ///this ckeck must be in remoteclient.send

            Tasks.Enqueue(new Task(() =>
            {
                lock (Clients)
                {
                    message.Environment = this;
                    if (!Clients.Any(x => x.Id == RemoteClient.Id)) { ParentClient.Log(LogLevel.Error, "Не могу послать сообщение {0} узлу {1}. Похоже что он оффлайн", message, RemoteClient); return; }
                    var B = Clients.FirstOrDefault(x => x.Id == RemoteClient.Id).Environment;
                    if (B == null) throw new Exception("client not ready or offline");
                    var m = Message.Decode(message.Code(), B);
                    var remote = B.RemoteClients.First(x => x.Id == m.FromId);
                    B.EmitMessageRecived(remote, m);
                }
            }, string.Format("Message {0} to {1} sended", message.Type, RemoteClient.Id)
        ));
            m_signal.Set();
        }

        public override bool CheckRemoteClientState(RemoteClient rc)
        {
            Client c = null;
            // lock (Clients)
            {
                for (int i = 0; i < Clients.Count; i++)
                    if (Clients[i].Id == rc.Id)
                    {
                        c = Clients[i];
                        break;
                    }
            }
            if (c == null) return false;
            var res = (c.State != ClientStates.Offline);
            return res;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            Clients.CollectionChanged -= Clients_CollectionChanged;
            Clients.Remove(ParentClient);
        }

        public override bool CheckClientLastOne(List<string> clientRemainingClients)
        {
            var online = RemoteClients.GetOnline().Select(x => x.Id);
            //foreach (var item in clientRemainingClients)
            //{
            //    if (!online.Any(x => x.Id == item))
            //        return false;
            //}
            //return true;

            return clientRemainingClients.Count(x => online.Contains(x)) == clientRemainingClients.Count;
        }
    }

}
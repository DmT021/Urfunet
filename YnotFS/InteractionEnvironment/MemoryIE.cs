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
    public class MemIE : BaseInteractionEnvironment
    {
        public static ObservableCollection<Client> Clients = new ObservableCollection<Client>();

        public MemIE(Client ParentClient)
            : base(ParentClient)
        {
            lock (Clients)
            {
                Clients.Add(ParentClient);
                Clients.CollectionChanged += Clients_CollectionChanged;
            }
        }

        void Clients_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            lock (RemoteClients)
            {
                if (e.NewItems != null)
                    foreach (Client c in e.NewItems)
                        if (c.Id!=ParentClient.Id)
                            RemoteClients.Add(new RemoteClient(c.Id, this));
            }
        }

        public override void BootStrap()
        {
            ///просмотр подулюченных клиентов
            //foreach (var it in Clients)
            //    if (OnRemoteClientStateChanged != null)
            //        OnRemoteClientStateChanged(new RemoteClient(it.Id, this), RemoteClientState.Unknown, RemoteClientState.Connected);

            lock (RemoteClients)
            {
                foreach (var c in Clients)
                {
                    RemoteClients.Add(new RemoteClient(c.Id, this));
                } 
            }
            ie_ready = true;
        }

        public override void Shutdown()
        {
            Clients.CollectionChanged -= Clients_CollectionChanged;
            ie_ready = false;
            Clients.Remove(this.ParentClient);
            base.Shutdown();
        }

        public override void Send(RemoteClient RemoteClient, Message message)
        {
            ///Remote Client must be offline
            ///this ckeck must be in remoteclient.send

            Tasks.Enqueue(new Task(() =>
                {
                    lock (Clients)
                    {
                        message.Environment = this;
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

        public override RemoteClientState CheckRemoteClientState(RemoteClient rc)
        {
            Client c;  
            //lock (Clients)
            {
                c = Clients.FirstOrDefault(x => x.Id == rc.Id); 
            }
            if (c == null) return RemoteClientState.Unknown;
            if (c.On) return RemoteClientState.Connected;
            return RemoteClientState.Disconnected;
        }
 

    }
}
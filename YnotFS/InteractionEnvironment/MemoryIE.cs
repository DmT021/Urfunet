﻿using System;
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
                Clients.Add(ParentClient);
                Clients.CollectionChanged += Clients_CollectionChanged;
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
                            LastOne = c.Settings.LastOne
                        });
                    }
                }
            }
        }

        public override void BootStrap()
        {
            ///look for connected clients and add them to the remotes
            foreach (var c in Clients)
                RemoteClients.Add(new RemoteClient(c.Id, this)
                {
                    LastOne = c.Settings.LastOne
                });
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
            var res = (c.State != ClientStates.offline);
            return res;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            Clients.Remove(ParentClient);
        }
    }

}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YnetFS.old
{
  /*    public class old_MemIE : BaseInteractionEnvironment
    {
        public static ObservableCollection<old_Client> Clients = new ObservableCollection<old_Client>();

        public old_MemIE(old_Client ParentClient)
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
                    foreach (old_Client c in e.NewItems)
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
            ///Remote old_Client must be offline
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
            old_Client c=null;  
           // lock (Clients)
            {
                for (int i=0;i<Clients.Count;i++)
                    if (Clients[i].Id == rc.Id)
                    {
                        c = Clients[i];
                        break;
                    }
            }
            if (c == null) return false;
            if (c.On) return true;
            return false;
        }
 

    }
*/

}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using NLog;
using YnetFS.Messages;

namespace YnetFS.InteractionEnvironment
{
    public enum RemoteClientState
    {
        Connected,
        Disconnected,
        Unknown
    }

    public delegate void dRemoteClientStateChanged(RemoteClient Remoteclient, RemoteClientState oldState, RemoteClientState newState);
    public delegate void dMessageRecived(RemoteClient fromClient, Message Message);

 

    public abstract class BaseInteractionEnvironment
    {
        const int HeartBeatInterval = 5*1000;

        public Client ParentClient { get; set; }

        protected volatile bool m_stop = false;
        protected readonly EventWaitHandle m_signal = new EventWaitHandle(false, EventResetMode.AutoReset);

        internal ObservableCollection<RemoteClient> RemoteClients { get; set; }
        

        bool _ie_ready = false;
        protected bool ie_ready
        {
            get { return _ie_ready; }
            set
            {
                _ie_ready = value;
                if (value) m_signal.Set();
            }
        }

        public event EventHandler OnReady;

        public BaseInteractionEnvironment(Client ParentClient)
        {
            RemoteClients = new ObservableCollection<RemoteClient>();
            RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;
            this.ParentClient = ParentClient;
            MessageRecived += OnMessageRecived;
            new Thread(LoopTask) { Name=ParentClient.Id.ToString().Substring(0,5)+": LoolpTask"}.Start();
            new Thread(HeartBeat) { Name = ParentClient.Id.ToString().Substring(0, 5) + ": HeartBit" }.Start();
            BootStrap();
            if (OnReady != null) OnReady(this, new EventArgs());
        }

        protected virtual void RemoteClients_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems!=null)
                foreach (RemoteClient r in e.NewItems)
                {
                    ParentClient.Log(LogLevel.Info, "Client {0} connected",r);
                }
            if (e.OldItems != null)
                foreach (RemoteClient r in e.OldItems)
                {
                    ParentClient.Log(LogLevel.Info, "Client {0} disconnected",r);
                }
            if (e.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                ParentClient.Log(LogLevel.Info, "all clients disconnected");

        }

        private void HeartBeat(object obj)
        {
            while (true)
            {
                // Если пришёл сигнал о прекращении работы - выходим
                if (m_stop)
                    break;

                lock (RemoteClients) //pign each remote clients and remove disconnected
                {
                    var disconnected = new List<RemoteClient>();
                    foreach (var r in RemoteClients)
                    {
                        var hbres = CheckRemoteClientState(r);
                        if (hbres != RemoteClientState.Connected)
                            disconnected.Add(r);
                    }
                    foreach (var r in disconnected)
                        RemoteClients.Remove(r);
                        //EmitRemoteClientStateChanged(r, RemoteClientState.Connected, RemoteClientState.Disconnected);
                }

                Thread.Sleep(HeartBeatInterval);
            }
        }

        /// <summary>
        /// method to start up interation environment. find other instances e.t.c
        /// </summary>
        public abstract void BootStrap();


        /// <summary>
        /// Send message to remote client
        /// </summary>
        /// <param name="RemoteClient"></param>
        /// <param name="message"></param>
        public abstract void Send(RemoteClient RemoteClient, Message message);

        #region Events
       
        //=============================================================================
        public event dMessageRecived MessageRecived;
        public void EmitMessageRecived(RemoteClient fromClient, Message Message)
        {
            if (MessageRecived != null)
                MessageRecived(fromClient, Message);
        }

        //=============================================================================

        #endregion

        protected ConcurrentQueue<Task> Tasks = new ConcurrentQueue<Task>();
        private void LoopTask(object obj)
        {
            while (m_signal.WaitOne(Timeout.Infinite))
            {
                // Если пришёл сигнал о прекращении работы - выходим
                if (m_stop)
                    break;

                if (!ie_ready) continue;

                lock (this)
                {
                    try
                    {
                        while (Tasks.Count > 0)
                        {
                            var t = new Task();
                            if (Tasks.TryDequeue(out t))
                            {
                                t.Method();
                                //ParentClient.Log(NLog.LogLevel.Info, t.Name + " complete" );  every message must log his own operating  status
                            }
                            else
                            {
                                ParentClient.Log(NLog.LogLevel.Error, "deque fail");

                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ParentClient.Log(LogLevel.Fatal, ex.Message, null);
                    }
                }
            }
            // Освобождаем ресурсы
            m_signal.Close();
        }



        public void OnMessageRecived(RemoteClient fromClient, Message Message) {
            Tasks.Enqueue(new Task(() =>
            {
                Message.OnRecived(fromClient, ParentClient);
            }, string.Format("message {0} from {1} recived", Message.Type, fromClient.Id)));
            m_signal.Set();
        }


   

        public abstract RemoteClientState CheckRemoteClientState(RemoteClient rc);

        public virtual void Shutdown()
        {
            m_stop = true;
            m_signal.Set();

            lock (RemoteClients)
            {
                RemoteClients.Clear();
                RemoteClients.CollectionChanged -= RemoteClients_CollectionChanged; 
            }
            RemoteClients = null;
            MessageRecived -= OnMessageRecived;
            ParentClient.Log(LogLevel.Info, "Env shutdown complete");
        }

        internal void AddRemoteClient (RemoteClient client)
        {
            lock (RemoteClients)
            {
                RemoteClients.Add(client);
            }
        }
        internal void RemoveRemoteClient(RemoteClient client)
        {
            lock (RemoteClients)
            {
                if (RemoteClients.Contains(client))
                    RemoteClients.Remove(client);
            }
        }
        public bool HasRemoteClient(Guid id)
        {
            lock (RemoteClients) return RemoteClients.Any(x=>x.Id==id);
        }
    }


    public class Task
    {

        public string Name { get; set; }
        public Action Method { get; set; }

        public Task(Action f, string name)
        {
            Method = f;
            this.Name = name;
        }
        public Task() { }
    }
}

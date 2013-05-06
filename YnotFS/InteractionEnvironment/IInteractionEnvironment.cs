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
        None,
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

        public ObservableCollection<RemoteClient> RemoteClients { get; set; }

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

        public BaseInteractionEnvironment(Client ParentClient)
        {
            RemoteClients = new ObservableCollection<RemoteClient>();
            this.ParentClient = ParentClient;
            MessageRecived += OnMessageRecived;
            RemoteClientStateChanged += OnRemoteClientStateChanged;
            new Thread(LoopTask).Start();
            new Thread(HeartBeat).Start();

            BootStrap();
        }

        private void HeartBeat(object obj)
        {
            while (true)
            {
                // Если пришёл сигнал о прекращении работы - выходим
                if (m_stop)
                    break;

                lock (RemoteClients)
                {

                    var checklist = new List<RemoteClient>();
                    foreach (var it in RemoteClients) checklist.Add(it);
                    foreach (var r in checklist)
                    {
                        var hbres = CheckRemoteClientState(r);
                        if (hbres == RemoteClientState.Disconnected)
                            EmitRemoteClientStateChanged(r, RemoteClientState.Connected, RemoteClientState.Disconnected);
                    }
                }

                Thread.Sleep(HeartBeatInterval);
            }
        }


        public abstract void BootStrap();


        /// <summary>
        /// Send message to remote client
        /// </summary>
        /// <param name="RemoteClient"></param>
        /// <param name="message"></param>
        public abstract void Send(RemoteClient RemoteClient, Message message);

        #region Events
        //=============================================================================
        /// <summary>
        /// remote client connected\disconnected
        /// </summary>
        public event dRemoteClientStateChanged RemoteClientStateChanged;
        /// <summary>
        /// Emmit RemoteClientStateChanged event manually (for external classes)
        /// </summary>
        /// <param name="Remoteclient"></param>
        /// <param name="oldState"></param>
        /// <param name="newState"></param>
        public void EmitRemoteClientStateChanged(RemoteClient Remoteclient, RemoteClientState oldState, RemoteClientState newState)
        {
            if (RemoteClientStateChanged != null)
                RemoteClientStateChanged(Remoteclient, oldState, newState);
        }
        /// <summary>
        /// Method, wich called on event invoke (for inherited classes)
        /// </summary>
        /// <param name="Remoteclient"></param>
        /// <param name="oldState"></param>
        /// <param name="newState"></param>
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


        public virtual void OnRemoteClientStateChanged(RemoteClient Remoteclient, RemoteClientState oldState, RemoteClientState newState) {

            lock (RemoteClients)
            {
                if (Remoteclient.Id == ParentClient.Id) return;

                if (newState == RemoteClientState.Connected)
                {
                    RemoteClients.Add(Remoteclient);
                }

                if (newState == RemoteClientState.Disconnected)
                {
                    RemoteClients.Remove(Remoteclient);
                }
            }


            ParentClient.Log(LogLevel.Info, "Client {0} {1}", Remoteclient, newState);

        }

        public abstract RemoteClientState CheckRemoteClientState(RemoteClient rc);
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

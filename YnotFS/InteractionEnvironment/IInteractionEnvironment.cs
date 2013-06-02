using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using NLog;
using YnetFS.FileSystem;
using YnetFS.Messages;

namespace YnetFS.InteractionEnvironment
{
    public delegate void dRemoteClientStateChanged(old_RemoteClient Remoteclient, bool NewState);
    public delegate void dMessageRecived(old_RemoteClient fromClient, Message Message);

 

    public abstract class BaseInteractionEnvironment
    {
        const int HeartBeatInterval = 5*1000;

        public old_Client ParentClient { get; set; }

        protected volatile bool m_stop = false;
        protected readonly EventWaitHandle m_signal = new EventWaitHandle(false, EventResetMode.AutoReset);

        internal ObservableCollection<old_RemoteClient> RemoteClients { get; set; }
        

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

        public BaseInteractionEnvironment(old_Client ParentClient)
        {
            RemoteClients = new ObservableCollection<old_RemoteClient>();
            RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;
            this.ParentClient = ParentClient;
            MessageRecived += OnMessageRecived;
            new Thread(LoopTask) { Name=ParentClient.Id+": LoolpTask"}.Start();
            new Thread(HeartBeat) { Name = ParentClient.Id + ": HeartBit" }.Start();

            BootStrap();
            if (OnReady != null) OnReady(this, new EventArgs());
        }

        protected virtual void RemoteClients_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {




        }

        /// <summary>
        /// return true if <paramref name="from_id"/>  is nearest to <paramref name="to_id"/> from <paramref name="sourcelist"/>
        /// nearest count by guid.gethashcode() function
        /// </summary>
        /// <param name="from_id"></param>
        /// <param name="to_id"></param>
        /// <param name="sourcelist"></param>
        /// <returns></returns>
        public bool IsNearest(INode from_id, INode to_id, List<old_RemoteClient> sourcelist)
        {
            //throw new Exception("сломалось!!");    
            ///имея список всех узлов, исключая конечную точку, проверяем, является ли текущий
            ///следующим по величине узлом относительно конечного
            ///для определения какой из guid`ов больше - используем его хеш=\
            ///

          
            var list_to_Find = new List<INode>();
            lock (sourcelist)
            {
                foreach (var r in sourcelist.Where(x => x.IsOnline))
                    if (r != from_id)
                        list_to_Find.Add(r);
            }
            if (list_to_Find.Count == 0) return true;
            list_to_Find = list_to_Find.OrderBy(x => x.hash).ToList();

            var rch = to_id.hash;//remote client hash
            var mydist = from_id.hash - rch;

            var best = list_to_Find.FirstOrDefault(x => rch < x.hash);
            if (mydist < 0)
            {
                if (best != null) return false;
                var minr = list_to_Find.First();
                return mydist < minr.hash - rch;
            }
            else
            {
                if (best == null) return true;
                return mydist <  best.hash - rch;
            }
        }

        /// <summary>
        /// looking for disconnected 
        /// </summary>
        /// <param name="obj"></param>
        private void HeartBeat(object obj)
        {
                    IEeventType lastevent = IEeventType.ready;
            while (true)
            {
                // Если пришёл сигнал о прекращении работы - выходим
                if (m_stop)
                    break;

                lock (RemoteClients) //pign each remote clients and remove disconnected
                {
                    var disconnected = new List<old_RemoteClient>();
                    foreach (var r in RemoteClients)
                    {
                        var hbres = CheckRemoteClientState(r);
                        if (!hbres)
                            disconnected.Add(r);
                    }
                    foreach (var r in disconnected)
                    { RemoteClients.Remove(r); throw new Exception("не убирать из списка"); }

                    //================
                    if (RemoteClients.Count(x => x.last_online_state == true) == 0)
                    {
                        if (lastevent != IEeventType.forever_alone_mode)
                        {
                            lastevent = IEeventType.forever_alone_mode;
                            if (OnIeStateChanged != null) OnIeStateChanged(this, IEeventType.forever_alone_mode);
                            continue;
                        }
                    }
                    else
                    {
                        if (lastevent == IEeventType.forever_alone_mode)
                        {
                            lastevent = IEeventType.ready;
                            if (OnIeStateChanged != null) OnIeStateChanged(this, IEeventType.ready);
                            continue;
                        }
                    }

                    ParentClient.Settings.LastAliveTime = DateTime.Now;
                    ParentClient.Settings.Save();

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
        /// <param name="old_RemoteClient"></param>
        /// <param name="message"></param>
        public abstract void Send(old_RemoteClient RemoteClient, Message message);

        #region Events
       
        //=============================================================================
        public event dMessageRecived MessageRecived;
        public void EmitMessageRecived(old_RemoteClient fromClient, Message Message)
        {
            if (MessageRecived != null)
                MessageRecived(fromClient, Message);
        }

        //=============================================================================

        #endregion

        protected ConcurrentQueue<Task> Tasks = new ConcurrentQueue<Task>();
        public void Addtask(Task t) { Tasks.Enqueue(t); m_signal.Set(); }
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
                    //try
                    //{
                        while (Tasks.Count > 0)
                        {
                            var t = new Task();
                            if (Tasks.TryDequeue(out t))
                            {
                                t.Method();
                            }
                            else
                            {
                                //ParentClient.Log(NLog.LogLevel.Error, "deque fail");
                                Thread.Sleep(100);
                            }
                        }
                   // }
                    //catch (Exception ex)
                  //  {
                    //    ParentClient.Log(LogLevel.Fatal, ex.Message, null);
                    //}
                }
            }
            // Освобождаем ресурсы
            m_signal.Close();
        }

 

        public void OnMessageRecived(old_RemoteClient fromClient, Message Message) {
            Tasks.Enqueue(new Task(() =>
            {
                Message.OnRecived(fromClient, ParentClient);
            },""));
            m_signal.Set();
        }



        public abstract bool CheckRemoteClientState(old_RemoteClient rc);

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
            if (OnIeStateChanged != null) OnIeStateChanged(this, IEeventType.shutdown);
        }



        public enum IEeventType
        {
            ready, shutdown, forever_alone_mode
        }



        public delegate void dIeEventHandler(BaseInteractionEnvironment b, IEeventType et);
        public event dIeEventHandler OnIeStateChanged;

        internal void AddRemoteClient (old_RemoteClient client)
        {
            lock (RemoteClients)
            {
                if (!RemoteClients.Contains(client))
                RemoteClients.Add(client);
            }
        }
        internal void RemoveRemoteClient(old_RemoteClient client)
        {
            lock (RemoteClients)
            {
                if (RemoteClients.Contains(client))
                    RemoteClients.Remove(client);
            }
        }

        public bool HasRemoteClient(string id)
        {
            lock (RemoteClients) return RemoteClients.Any(x=>x.Id==id);
        }

        internal void SendToAll(Message message)
        {
            Tasks.Enqueue(new Task(() =>
            {
                lock (RemoteClients)
                {
                    foreach (var it in RemoteClients)
                        it.Send(message);
                }
            }, ""));
            m_signal.Set();

        }



        public bool Alive { get; set; }
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

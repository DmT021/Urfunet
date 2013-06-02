using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NLog;
using YnetFS.FileSystem;
using YnetFS.InteractionEnvironment;
using YnetFS.Messages;

namespace YnetFS
{

    public enum ClientStates { offline, wait_lastone, idle, online }
    public class Client : INode
    {
        const int MinClients = 5;
        /// <summary>
        /// ready if remote clients >= 5
        /// </summary>
        public event StateHandler StateChanged;
        public delegate void StateHandler(object sender, ClientStates NewState);
        bool INode.IsOnline { get { return true; } }
        public string Id { get; set; }
        public bool IsRemote { get { return false; } }
        public int hash { get { return Id.GetHashCode(); } }
        public DirectoryInfo MyDir
        {
            get
            {
                var dir = Path.Combine(System.Environment.CurrentDirectory, Id);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return new DirectoryInfo(dir);
            }
        }
        [JsonIgnore]
        public BaseFileSystem FileSystem
        {
            get;
            private set;
        }

        public RemoteClientsManager RemoteClients { get { return Environment.RemoteClients; } }

        public Client(string id)
        {
            Id = id;
            Logs = new ObservableCollection<string>();

            Settings = new ClientSettings(this);

            Up();
        }

        public ClientSettings Settings { get; set; }


        #region logging
        Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public ObservableCollection<string> Logs { get; set; }
        public void Log(LogLevel lvl, string msg, params object[] objs)
        {
            if (objs == null) objs = new List<object>().ToArray();
            var tmp = objs.ToList();
            tmp.Add(this);
            Logger.Log(lvl, msg, tmp.ToArray());
        }
        #endregion

        new_BaseInteractionEnvironment _Environment = null;
        public new_BaseInteractionEnvironment Environment
        {
            get
            {
                return _Environment;
            }
        }

        public void ShutDown() { if (State != ClientStates.offline) State = ClientStates.offline; UpdateState(); }
        public void Up() { if (State == ClientStates.offline) State = ClientStates.wait_lastone; UpdateState(); }


        ClientStates _State = ClientStates.offline;
        public ClientStates State
        {
            get { return _State; }
            private set
            {
                var oldstaste = _State;
                _State = value;
                if (_State == oldstaste) return;
                switch (State)
                {
                    case ClientStates.offline:
                        {
                            _Environment.OnIeStateChanged -= _Environment_OnIeStateChanged;
                            Environment.OnReady -= Environment_OnReady;
                            RemoteClients.CollectionChanged -= RemoteClients_CollectionChanged;
                            FileSystem = null;
                            Environment.Shutdown();
                            _Environment = null;
                            break;
                        }
                    case ClientStates.wait_lastone:
                        {
                            if (FileSystem == null) FileSystem = new YnetFS.FileSystem.Mock.newMockFS(this, MyDir.FullName);

                            if (_Environment == null) _Environment = new MemoryIE(this);
                            Environment.OnIeStateChanged += _Environment_OnIeStateChanged; ;
                            Environment.OnReady += Environment_OnReady;
                            Environment.RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;
                            break;
                        }
                    case ClientStates.idle:
                        {




                            break;
                        }
                    case ClientStates.online:
                        if (oldstaste == ClientStates.offline)
                        { State = ClientStates.wait_lastone; break; }
                        wasonline = true;

                        break;
                    default:
                        break;
                }
                if (StateChanged != null) StateChanged(this, State);
            }
        }

        void RemoteClients_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (RemoteClient it in e.NewItems)
                    if (it.LastOne)
                        Log(LogLevel.Info, "last one comed  - {0}", sender);
            UpdateState();
        }

        bool wasonline = false;
        private void UpdateState()
        {
            ///lastone не должен сам ждать другого lastone
            ///Сразу надо перйти в odle
            if (State == ClientStates.wait_lastone && Settings.LastOne)
            {
                State = ClientStates.idle;
                UpdateState(); return;
            }

            ///если дождались lastone переходим в idle
            if (State == ClientStates.wait_lastone && RemoteClients.Count(x => x.LastOne) >= 1)
            {
                State = ClientStates.idle;
            }

            ///если группа набрана - переходим в online и снимаем пометку lastone
            if (State == ClientStates.idle && RemoteClients.OnlineCount > MinClients)
            {
                State = ClientStates.online;
                Settings.LastOne = false;

            }
            
            ///если группа рассыпается - переходим в ожидание и помечаем себя как lastone
            if (State == ClientStates.online && RemoteClients.OnlineCount <= MinClients)
            {
                State = ClientStates.idle;
                Settings.LastOne = true;
            }



        }

        void Environment_OnReady(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        void _Environment_OnIeStateChanged(new_BaseInteractionEnvironment b, new_BaseInteractionEnvironment.IEeventType et)
        {
            throw new NotImplementedException();
        }
    }

    public class ClientSettings
    {
        public ClientSettings() { }
        public ClientSettings(Client c)
        {
            this.c = c;
            var filename = Path.Combine(c.MyDir.FullName, "settings.dat");
            if (File.Exists(filename))
            {
                var tmp = (ClientSettings)JsonConvert.DeserializeObject(File.ReadAllText(filename), typeof(ClientSettings));
                LastOne = tmp.LastOne;
            }
            else FirstStart = true;

            Save();
        }
        public void Save()
        {

            if (c == null) return;
            var filename = Path.Combine(c.MyDir.FullName, "settings.dat");
            // if (File.Exists(filename))
            //     File.Delete(filename);
            File.WriteAllText(filename, JSonPresentationFormatter.Format(JsonConvert.SerializeObject(this)));
        }
        bool _LastOne = false;
        public bool LastOne
        {
            get { return _LastOne; }
            set
            {
                if (_LastOne == value) return;
                _LastOne = value;
                Save();
            }
        }
        public bool FirstStart = false;
        public string Id { get; set; }
        public DateTime LastAliveTime { get; set; }

        [JsonIgnore]
        public Client c { get; set; }
    }

    public class MemoryIE : new_BaseInteractionEnvironment
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
                foreach (Client c in e.NewItems)
                    if (c.Id != ParentClient.Id)
                        RemoteClients.Add(new RemoteClient(c.Id, this)
                        {
                            LastOne =
                                c.Settings.LastOne
                        });
        }
        public override void BootStrap()
        {
            ///look for connected clients and add them to the remotes
            foreach (var c in Clients)
                RemoteClients.Add(new RemoteClient(c.Id, this));
        }

        public override void Send(RemoteClient RemoteClient, Messages.Message message)
        {
            throw new NotImplementedException();
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
    }

    public class RemoteClientsManager : ICollection<RemoteClient>, INotifyCollectionChanged
    {
        ObservableCollection<RemoteClient> Items { get; set; }
        public new_BaseInteractionEnvironment Env { get; set; }

        public RemoteClientsManager(new_BaseInteractionEnvironment env)
        {
            this.Env = env;
            Items = new ObservableCollection<RemoteClient>();
            Items.CollectionChanged += items_CollectionChanged;

            Load();
        }

        void items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged != null) CollectionChanged(sender, e);
        }





        public RemoteClient this[string index]
        {
            get { lock (this)return Items.FirstOrDefault(x => x.Id == index); }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public void Add(RemoteClient item)
        {
            lock (this)
            {
                if (Items.Contains(item))
                {
                    var oldit = Items.FirstOrDefault(x => x.Id == item.Id);
                    var oldind = Items.IndexOf(oldit);
                    oldit.PropertyChanged -= item_PropertyChanged;
                    Items.Remove(oldit);
                    Items.Insert(oldind, item);
                }
                else
                    Items.Add(item);

                item.PropertyChanged += item_PropertyChanged;
                item_PropertyChanged(Items, null);
            }
        }
        void Load()
        {
            var filename = Path.Combine(Env.ParentClient.MyDir.FullName, "contacts.dat");
            if (!File.Exists(filename)) return;
            Items = (ObservableCollection<RemoteClient>)JsonConvert.DeserializeObject(File.ReadAllText(filename, Encoding.UTF8), typeof(ObservableCollection<RemoteClient>));

        }
        void Save()
        {
            var filename = Path.Combine(Env.ParentClient.MyDir.FullName, "contacts.dat");

            //   if (File.Exists(filename))
            //      File.Delete(filename);
            File.WriteAllText(filename, JSonPresentationFormatter.Format(JsonConvert.SerializeObject(Items)), Encoding.UTF8);
        }
        void item_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Save();


            if (CollectionChanged != null)
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Clear()
        {
            lock (this) Items.Clear();
        }

        public bool Contains(RemoteClient item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(RemoteClient[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { lock (Items)return Items.Count; }
        }

        public bool IsReadOnly
        {
            get { throw new NotImplementedException(); }
        }

        public bool Remove(RemoteClient item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<RemoteClient> GetEnumerator()
        {
            lock (this) return Items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            lock (this) return Items.GetEnumerator();
        }

        public int OnlineCount { get { lock (this)return Items.Count(x => x.IsOnline); } }
    }

    public abstract class new_BaseInteractionEnvironment
    {
        const int HeartBeatInterval = 5 * 1000;

        public Client ParentClient { get; set; }

        protected volatile bool m_stop = false;
        protected readonly EventWaitHandle m_signal = new EventWaitHandle(false, EventResetMode.AutoReset);

        internal RemoteClientsManager RemoteClients { get; set; }


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

        public new_BaseInteractionEnvironment(Client ParentClient)
        {
            this.ParentClient = ParentClient;
            RemoteClients = new RemoteClientsManager(this);
            RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;
            MessageRecived += OnMessageRecived;
            new Thread(LoopTask) { Name = ParentClient.Id + ": LoolpTask" }.Start();
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
                return mydist < best.hash - rch;
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
                    foreach (var r in RemoteClients)
                        r.IsOnline = CheckRemoteClientState(r);

                    //================
                    //if (RemoteClients.Count(x => x.last_online_state == true) == 0)
                    //{
                    //    if (lastevent != IEeventType.forever_alone_mode)
                    //    {
                    //        lastevent = IEeventType.forever_alone_mode;
                    //        if (OnIeStateChanged != null) OnIeStateChanged(this, IEeventType.forever_alone_mode);
                    //        continue;
                    //    }
                    //}
                    //else
                    //{
                    //    if (lastevent == IEeventType.forever_alone_mode)
                    //    {
                    //        lastevent = IEeventType.ready;
                    //        if (OnIeStateChanged != null) OnIeStateChanged(this, IEeventType.ready);
                    //        continue;
                    //    }
                    //}

                    //ParentClient.Settings.LastAliveTime = DateTime.Now;
                    //ParentClient.Settings.Save();

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
        public abstract void Send(RemoteClient RemoteClient, Message message);

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



        public void OnMessageRecived(old_RemoteClient fromClient, Message Message)
        {
            Tasks.Enqueue(new Task(() =>
            {
                // Message.OnRecived(fromClient, ParentClient);
            }, ""));
            m_signal.Set();
        }



        public abstract bool CheckRemoteClientState(RemoteClient rc);

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



        public delegate void dIeEventHandler(new_BaseInteractionEnvironment b, IEeventType et);
        public event dIeEventHandler OnIeStateChanged;


        public bool HasRemoteClient(string id)
        {
            lock (RemoteClients) return RemoteClients.Any(x => x.Id == id);
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

}

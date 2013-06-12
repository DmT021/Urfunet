using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NLog;
using NLog.Targets;
using YnetFS.FileSystem;
using YnetFS.FileSystem.Mock;
using YnetFS.InteractionEnvironment;
using YnetFS.Messages;

namespace YnetFS
{
    public enum ClientStates
    {
        Offline,
        //Starting,
        //WaitSynchronizedClient,
        Synchronization,
        Idle,
        Online
    }

    public class Client : INode, IDisposable
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

            Up();
        }

        public ClientSettings Settings { get; set; }
        public ClientSettings OpeningSettings { get; set; }


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

        public BaseInteractionEnvironment Environment
        {
            get;
            private set;
        }

        public void ShutDown()
        {
            if (State != ClientStates.Offline) Stop();
        }

        public void Up()
        {
            Log(LogLevel.Info, "Включение...", null);

            if (State == ClientStates.Offline)
            {
                //State = ClientStates.Starting;
                Start();
            }
        }

        private void Start()
        {
            OpeningSettings = new ClientSettings(this);
            Settings = OpeningSettings; // .Clone();

            if (FileSystem == null) 
                FileSystem = new YnetFS.FileSystem.Mock.MockFS(this, MyDir.FullName);

            if (Environment == null) 
                Environment = new MemoryIE(this);
            Environment.OnIeStateChanged += _Environment_OnIeStateChanged; ;
            Environment.OnReady += Environment_OnReady;
            RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;

            Environment.Start();

            var losync = CheckLastOneSynchronized();

            if (losync)
            {
                Log(LogLevel.Info, "Мы сами LastOne", null);
                Environment.SendToAll(new SyncMessage());

                if (Environment.HasEnoughNodes(GetAllClients()))
                {
                    State = ClientStates.Online;
                }
                else
                {
                    State = ClientStates.Idle;
                }
            }
            else
            {
                State = ClientStates.Synchronization;
            }
            SaveSettings();
        }

        private bool CheckLastOneSynchronized()
        {
            if (!OpeningSettings.WasInSynchronizedGroup)
                return false;

            return Environment.CheckClientLastOne(OpeningSettings.RemainingClients.ToList());
        }

        private void Stop()
        {
            SaveSettings();

            Environment.OnIeStateChanged -= _Environment_OnIeStateChanged;
            Environment.OnReady -= Environment_OnReady;
            RemoteClients.CollectionChanged -= RemoteClients_CollectionChanged;
            FileSystem = null;
            Environment.Shutdown();
            Environment = null;
            
            State = ClientStates.Offline;
        }

        ClientStates state = ClientStates.Offline;
        public ClientStates State
        {
            get { return state; }
            private set
            {
                var oldstaste = state;
                state = value;
                if (state == oldstaste) return;

                if (StateChanged != null)
                    StateChanged(this, State);

                switch (State)
                {
                    case ClientStates.Offline:
                        {
                            Log(LogLevel.Info, "Выключение...", null);
                            break;
                        }
                    //case ClientStates.Starting:
                    //    {
                    //        Log(LogLevel.Info, "Включение...", null);
                    //        break;
                    //    }
                    //case ClientStates.WaitSynchronizedClient:
                    //    {
                    //        Log(LogLevel.Info, "Ожидание синхронизованного...", null);
                    //        break;
                    //    }
                    case ClientStates.Synchronization:
                        {
                            Log(LogLevel.Info, "Синхронизация", null);
                            break;
                        }
                    case ClientStates.Idle:
                        {
                            Log(LogLevel.Info, "Монтирование файловой системы в режим ro...", null);
                            break;
                        }
                    case ClientStates.Online:
                        {
                            Log(LogLevel.Info, "Монтирование файловой системы в режим rw...", null);
                            Log(LogLevel.Info, "Переход в online...", null);

                            break;
                        }
                    default:
                        break;
                }
            }
        }

        void RemoteClients_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) // если узлы добавлены
            {
                foreach (RemoteClient it in e.NewItems)
                {
                    Log(LogLevel.Info, "Узел \"{0}\": {1}", it.Id, it.IsOnline ? "Connected" : "Disconnected");
                    if (it.IsOnline && 
                        Synchronized && 
                        Environment.IsNearest(it, this, RemoteClients.Where(x => x.Id != it.Id && x.IsOnline).ToList()))
                        it.Send(new SyncMessage());
                }
                if (State == ClientStates.Idle || State == ClientStates.Online)
                {
                    if (Environment.HasEnoughNodes(GetAllClients()))
                    {
                        State = ClientStates.Online;
                    }
                    else
                    {
                        State = ClientStates.Idle;
                    }
                }
                SaveSettings();
            }
            //if (e.OldItems != null) // если узлы удалены
            //{
            //    if (State == ClientStates.Online)
            //    {
            //        if (Environment.HasEnoughNodes(GetAllClients()))
            //        {
            //            State = ClientStates.Online;
            //        }
            //        else
            //        {
            //            State = ClientStates.Idle;
            //        }
            //    }
            //}
        }

        //private void UpdateState()
        //{
        //    SaveSettings();
        //    CheckHasEnoughNodes();
        //}

        private void SaveSettings()
        {
            if (State != ClientStates.Offline)
            {
                Settings.WasInSynchronizedGroup = Synchronized;
                Settings.RemainingClients.Clear();
                foreach (RemoteClient item in RemoteClients.GetOnline())
                {
                    Settings.RemainingClients.Add(item.Id);
                }
            }
        }

        //private void CheckHasEnoughNodes()
        //{
        //    var allClients = GetAllClients();
        //    if (State == ClientStates.Idle)
        //    {
        //        if (Environment.HasEnoughNodes(allClients))
        //        {
        //            // TODO: проверить, если множество компьютеров в сети образуют целостное хранилище (т.е. содержащее все файлы) - перейти в онлайн
        //            State = ClientStates.Online;
        //            Log(LogLevel.Info, "Есть достаточно нод, переходим в онлайн", null);
        //        }
        //        else
        //        {
        //            Log(LogLevel.Info, "Нужно больше нод", null);
        //        }
        //    }
        //}

        private List<INode> GetAllClients()
        {
            var allClients = RemoteClients.GetOnline().Select(x => x as INode).ToList();
            allClients.Add(this);
            return allClients;
        }

        //// workaround only for memoryie
        //public bool LastOneSynchronized
        //{
        //    get;
        //    private set;
        //}

        void Environment_OnReady(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        void _Environment_OnIeStateChanged(BaseInteractionEnvironment b, BaseInteractionEnvironment.IEeventType et)
        {
        }

        internal INode GetFileOwner(BaseFile baseFile)
        {
            lock (RemoteClients)
            {
                var own = RemoteClients.FirstOrDefault(x => x.Id.ToString() == baseFile.meta.Owner);
                return own;
            }
        }

        /// <summary>
        /// return remote replica-nodes
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public List<INode> GetFileReplics(BaseFile file)
        {
            var res = new List<INode>();
            lock (RemoteClients)
            {
                foreach (var it in file.meta.Replics)
                {
                    var n = GetNodeById(it);
                    if (n != null)
                        res.Add(n);
                }
            }
            return res;
        }

        public INode GetNodeById(string id)
        {
            lock (RemoteClients)
                if (RemoteClients.Any(x => x.Id.ToString() == id))
                    return RemoteClients.Single(x => x.Id.ToString() == id);
            if (Id.ToString() == id) return this;
            return null;
        }

        /// <summary>
        /// Choose @count random clients sourcelist souce list
        /// </summary>
        /// <param name="count"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public List<INode> GetRandomClients(int count, IEnumerable<INode> source)
        {
            var tmp = new List<INode>();
            foreach (var r in source) tmp.Add(r);
            if (count >= tmp.Count) return tmp;

            var rnd = new Random(DateTime.Now.Millisecond);

            while (tmp.Count != count)
                tmp.RemoveAt(rnd.Next(tmp.Count - 1));
            return tmp;
        }


        public void Dispose()
        {
            Environment.Dispose();
        }

        public bool Synchronized
        {
            get
            {
                return State == ClientStates.Idle || State == ClientStates.Online;
            }
        }

        internal void SyncComplited()
        {
            if (Environment.HasEnoughNodes(GetAllClients()))
            {
                State = ClientStates.Online;
            }
            else
            {
                State = ClientStates.Idle;
            }
            SaveSettings();
        }
    }


    [Target("CustomLogTarget")]
    public class CustomLogTarget : TargetWithLayout
    {

        protected override void Write(LogEventInfo logEvent)
        {
            var str = string.Format(logEvent.Message, logEvent.Parameters);
            if (logEvent.Parameters.Last() is Client)
            {

                (logEvent.Parameters.Last() as Client).Logs.Insert(0, string.Format("{0} [{1}] {2}", DateTime.Now.TimeOfDay, logEvent.Level.Name.ToUpper(), str));
            }
        }

    }
}

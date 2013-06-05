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

        BaseInteractionEnvironment _Environment = null;
        public BaseInteractionEnvironment Environment
        {
            get
            {
                return _Environment;
            }
        }

        public void ShutDown() { if (State != ClientStates.offline) State = ClientStates.offline; UpdateState(); }
        public void Up()
        {
            Log(LogLevel.Info, "Включение...", null);

            if (State == ClientStates.offline) State = ClientStates.wait_lastone; UpdateState();
        }


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
                            Log(LogLevel.Info, "Выключение...", null);
                            break;
                        }
                    case ClientStates.wait_lastone:
                        {
                            if (FileSystem == null) FileSystem = new YnetFS.FileSystem.Mock.MockFS(this, MyDir.FullName);

                            if (_Environment == null) _Environment = new MemoryIE(this);
                            Environment.OnIeStateChanged += _Environment_OnIeStateChanged; ;
                            Environment.OnReady += Environment_OnReady;
                            Environment.RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;
                            Log(LogLevel.Info, "Ожидание последнего...", null);
                            break;
                        }
                    case ClientStates.idle:
                        {
                            Log(LogLevel.Info, "Монтирование файловой системы в режим ro...", null);
                            break;
                        }
                    case ClientStates.online:
                        if (oldstaste == ClientStates.offline)
                        { State = ClientStates.wait_lastone; break; }
                        wasonline = true;
                        Log(LogLevel.Info, "Монтирование файловой системы в режим rw...", null);
                        Log(LogLevel.Info, "Переход в online...", null);

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
                {
                    Log(LogLevel.Info, "Узел \"{0}\" подключен...", it.Id);
                }
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
                UpdateState();
            }

            ///если дождались lastone переходим в idle
            else if (State == ClientStates.wait_lastone && RemoteClients.Count(x => x.IsOnline && x.LastOne) >= 1)
            {
                State = ClientStates.idle;
                var lo = RemoteClients.First(x => x.IsOnline && x.LastOne);
                lo.Send(new RequestSynch());
                UpdateState();
            }

            ///если группа набрана - переходим в online и снимаем пометку lastone
            else if (State == ClientStates.idle && RemoteClients.OnlineCount >= MinClients)
            {
                Log(LogLevel.Info, "Минимальная группа набрана", null);
                State = ClientStates.online;
                Settings.LastOne = false;

            }

            ///если группа рассыпается - переходим в ожидание и помечаем себя как lastone
            else if (State == ClientStates.online && RemoteClients.OnlineCount < MinClients)
            {
                State = ClientStates.idle;
            }
            if (((State == ClientStates.online) || (State == ClientStates.idle)) && RemoteClients.OnlineCount < MinClients)
            {
                Settings.LastOne = true;
            }



        }

        void Environment_OnReady(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        void _Environment_OnIeStateChanged(BaseInteractionEnvironment b, BaseInteractionEnvironment.IEeventType et)
        {
            throw new NotImplementedException();
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

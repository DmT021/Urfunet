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
        public override string ToString()
        {
            return "<> " + Id + " [" + (State != ClientStates.Offline ? "+" : "-") + "]" + (Synchronized ? "syncd" : "");
        }

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
            State = ClientStates.Synchronization;
            OpeningSettings = new ClientSettings(this);
            Settings = OpeningSettings; // .Clone();

            if (FileSystem == null)
            {
                FileSystem = new YnetFS.FileSystem.Mock.MockFS(this, MyDir.FullName);
                FileSystem.OnFolderEvent += FileSystem_OnFolderEvent;
                FileSystem.OnFileEvent += FileSystem_OnFileEvent;
            }

            if (Environment == null) 
                Environment = new MemoryIE(this);
            Environment.OnIeStateChanged += _Environment_OnIeStateChanged; ;
            Environment.OnReady += Environment_OnReady;
            RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;

            Environment.Start();

            bool groupSynced = CheckGroupSynchronized();

            var losync = !groupSynced && CheckLastOneSynchronized();

            if (losync)
            {
                Log(LogLevel.Info, "Мы сами LastOne", null);
                Environment.SendToAll(new SyncMessage());

                if (CheckReadyForOnline())
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

        private bool CheckGroupSynchronized()
        {
            if (RemoteClients.OnlineCount > 0)
            {
                var online = RemoteClients.GetOnline();
                var anyclient = online[0];
                if (anyclient.Synchronized)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CheckReadyForOnline()
        {
            return RemoteClients.OnlineCount >= 2 && Environment.HasEnoughNodes(GetAllClients());
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

            FileSystem.OnFolderEvent -= FileSystem_OnFolderEvent;
            FileSystem.OnFileEvent -= FileSystem_OnFileEvent;

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
                            Log(LogLevel.Info, "Выключено...", null);
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
                            FileSystem.ReadOnly = true;
                            break;
                        }
                    case ClientStates.Online:
                        {
                            Log(LogLevel.Info, "Монтирование файловой системы в режим rw...", null);
                            FileSystem.ReadOnly = false;
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
                    if (it.IsOnline &&
                        Synchronized &&
                        Environment.IsNearest(this, it, RemoteClients.Where(x => x.Id != it.Id && x.IsOnline).ToList()))
                        it.Send(new SyncMessage());
                }
            }
            
            var allItems = new List<RemoteClient>();
            if (e.NewItems != null)
                allItems.AddRange(e.NewItems.Cast<RemoteClient>());
            if (e.OldItems != null)
                allItems.AddRange(e.OldItems.Cast<RemoteClient>());

            foreach (RemoteClient it in allItems)
            {
                Log(LogLevel.Info, "Узел \"{0}\": {1}", it.Id, it.IsOnline ? "Connected" : "Disconnected");
            }

            if (State == ClientStates.Idle || State == ClientStates.Online)
            {
                if (CheckReadyForOnline())
                {
                    State = ClientStates.Online;
                }
                else
                {
                    State = ClientStates.Idle;
                }
            }

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && State==ClientStates.Online)
            {

                ///Вновь подключившемуся должен ответить ближайший узел 
                foreach (RemoteClient it in e.NewItems)
                {
                    lock (RemoteClients)
                    {
                        if (Environment.IsNearest(this, it, RemoteClients.ToList()))
                        {
                            Log(LogLevel.Info, "Отправляю {0} синхронизацию", it);
                            it.Send(new SyncMessage());
                        }
                    }
                }

            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (RemoteClient r in e.OldItems)
                {


                    var rid = r.Id.ToString();
                    var allfiles = FileSystem.GetFileList();
                    ///когда узел ушел мы должны найти файлы, для которых он был мастером и для которых мы являемся репликой.
                    ///если мы являемся ближайшей по расстоянию репликой, то делаем себя мастером и рассылаем метаинфу
                    ///



                    List<BaseFile> files = allfiles.Where(x => x.meta.Owner == rid).Where(x => x.meta.Replics.Contains(this.Id.ToString())).ToList(); //FileSystem.GetFilesByOwnerID(r.Id.ToString()).Where(x => x.meta.Replics.Contains(Id.ToString())).ToList();

                    foreach (var f in files)
                    {
                        var reps = new List<RemoteClient>();
                        foreach (var it in f.meta.Replics)//реплики файла надо профильровать. необходимо вычислить ближайшую из доступных реплик
                            if (RemoteClients.Any(x => x.Id.ToString() == it))
                            {
                                var tmprepl = RemoteClients.First(x => x.Id.ToString() == it);
                                if (!tmprepl.IsOnline) continue;
                                reps.Add(tmprepl);
                            }
                        if (Environment.IsNearest(this, r, reps))
                        {
                            Log(LogLevel.Info, "set me as owner of {0}", f.Name);
                            f.SetOwner(Id.ToString());
                            Log(LogLevel.Info, "remove {1} from replicas of {0}", f.Name, rid);
                            f.RemoveReplica(rid);
                            if (f.meta.Replics.Count < 3)
                            {
                                Log(LogLevel.Info, "not enouch replicas for {0}", f.Name);
                                var newrepl = RemoteClients.FirstOrDefault(x => x.IsOnline && !f.meta.Replics.Contains(x.Id.ToString()));
                                if (newrepl != null)
                                {
                                    Log(LogLevel.Info, "set {1} as replica for {0}", f.Name, newrepl.Id.ToString());
                                    f.AddReplica(newrepl.Id.ToString());
                                }
                            }

                            Environment.SendToAll(new UpdateMetaInfoMessage(f));
                        }
                    }
                    ///если пропала релика а я мастер, а реплик осталось маловато - создать новую репликкку
                    ///

                    files = allfiles.Where(x => GetFileOwner(x) == this).ToList();// FileSystem.GetFilesByOwnerID(Id.ToString());

                    foreach (var f in files)
                    {
                        if (!GetFileReplics(f).Any(x => x.Id == rid)) continue;// !f.InReplics(rid)) continue;
                        f.RemoveReplica(rid);

                        var ralive = new List<RemoteClient>();
                        foreach (var it in f.meta.Replics)//реплики файла надо профильровать. необходимо вычислить ближайшую из доступных реплик
                            if (RemoteClients.Any(x => x.Id.ToString() == it))
                            {
                                var tmprepl = RemoteClients.First(x => x.Id.ToString() == it);
                                if (!tmprepl.IsOnline) continue;
                                ralive.Add(tmprepl);
                            }

                        if (ralive.Count < 2)
                        {
                            Log(LogLevel.Info, "not enouch replicas for {0}", f.Name);
                            var newrepl = RemoteClients.FirstOrDefault(x => x.IsOnline && !ralive.Contains(x));
                            if (newrepl != null)
                            {
                                Log(LogLevel.Info, "set {1} as replica for {0}", f.Name, newrepl.Id.ToString());
                                f.AddReplica(newrepl.Id.ToString());
                            }
                        }
                        Environment.SendToAll(new UpdateMetaInfoMessage(f));
                    }


                }
            }

            SaveSettings();

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
            if (CheckReadyForOnline())
            {
                State = ClientStates.Online;
            }
            else
            {
                State = ClientStates.Idle;
            }
            SaveSettings();
        }

        public INode GetRandomReplica(BaseFile file)
        {
            lock (RemoteClients)
            {
                var reps = GetFileReplics(file);
                var rnd = new Random(DateTime.Now.Millisecond);
                while (true)
                {
                    if (reps.Count == 0) return null;
                    int ind = rnd.Next(reps.Count - 1);
                    var res = reps[ind];
                    if (res.IsOnline) return res;

                    reps.RemoveAt(ind);
                    ///if no one alive - vary bad =\
                }
            }
        }
        void FileSystem_OnFileEvent(BaseFile srcFile, FSObjectEvents eventtype)
        {
            lock (RemoteClients)
            {
                if (yNotRule.RulePool.ContainsKey(eventtype))
                    foreach (var r in yNotRule.RulePool[eventtype])
                        r.Eval(this, srcFile);
            }

            ///блокировки файлов и т д

            if (eventtype == FSObjectEvents.local_changed)
            {
                Log(LogLevel.Info, "Отправка обновленной метаинформации");
                Environment.SendToAll(new UpdateMetaInfoMessage(srcFile));
            }

            if (eventtype == FSObjectEvents.local_opend)
            {

                Environment.SendToAll(new LockFileMessage(srcFile));
                if (!srcFile.data.Downloaded)
                {
                    ///файл не загружен - выбираем слуйчайную реплику
                    ///загружаем файл
                    ///добавляем себя в реплики
                    ///сообщаем об изменении метаинформации

                    var r = GetRandomReplica(srcFile);
                    if (r == null) throw new Exception("Не найдены релики");
                    var m = new EventWaitHandle(false, EventResetMode.AutoReset);
                    (r as RemoteClient).Send(new DownloadFileMessage(srcFile, m));
                    m.WaitOne(-1);
                    srcFile.AddReplica(Id);
                    Log(NLog.LogLevel.Info, "Получена реплика файла {0}", this);
                    FileSystem_OnFileEvent(srcFile, FSObjectEvents.local_changed);
                }


            }

            if (eventtype == FSObjectEvents.local_changed || eventtype == FSObjectEvents.remote_changed)
            {

                ///если хэши не совпадают (и реплика загружена), обновить реплику
                ///OR
                ///если я в репликах - я дожен загузить этот файл
                ///
                // if ()
                if ((srcFile.data.Downloaded && srcFile.data.ComputeHash() != srcFile.meta.Hash) || (GetFileReplics(srcFile).Contains(this) && !srcFile.data.Downloaded))
                {
                    Log(LogLevel.Info, "Файл изменен. Инициирую обновление {0}", srcFile);
                    var m = new EventWaitHandle(false, EventResetMode.AutoReset);
                    Log(LogLevel.Info, "Загружаю реплику {0}", srcFile.Name);
                    (GetFileOwner(srcFile) as RemoteClient).Send(new DownloadFileMessage(srcFile, m));
                    //m.WaitOne(-1);
                }

                Log(LogLevel.Info, "Обновлена метаинформация для файла {0}", srcFile.Name);
            }
            if (eventtype == FSObjectEvents.local_closed)
            {
                ///сравнить хэш
                ///если совпадает - файл не изменен. просто разлокируем его
                ///если нет - ставим себя влядельцем файла и рассылаем всем новую метаинфу. 
                ///те должны сравнить хэш и загрузить файл.
                ///после этого файл можно разблокировать
                ///

                var oldhash = srcFile.meta.Hash;
                var newhash = srcFile.data.ComputeHash();
                if (oldhash != newhash)
                {
                    srcFile.SetHash(newhash);
                    srcFile.SetOwner(Id);

                    FileSystem_OnFileEvent(srcFile, FSObjectEvents.local_changed);
                    // ParentFolder.FS.ParentClient.Environment.SendToAll(new UpdateMetaInfoMessage(this));
                }
                Environment.SendToAll(new UnLockFileMessage(srcFile));

            }

        }

        void FileSystem_OnFolderEvent(BaseFolder srcFolder, FSObjectEvents eventtype)
        {
            if (eventtype == FSObjectEvents.local_created)
            {
                Log(LogLevel.Info, "Шлем команду \"создать папку {0}\"", srcFolder.Name);
                Environment.SendToAll(new NewFolderMessage(srcFolder));
            }
            if (eventtype == FSObjectEvents.local_delete)
            {
                Log(LogLevel.Info, "Шлем команду \"удалить папку {0}\"", srcFolder.Name);
                Environment.SendToAll(new DeleteFSObjMessage(srcFolder));
            }
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

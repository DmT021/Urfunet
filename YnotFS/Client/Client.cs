using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public class old_Client : INode
    {

        /// <summary>
        /// ready if remote clients >= 5
        /// </summary>
        public event StateHandler StateChanged;
        public delegate void StateHandler(object sender);
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

        bool _ready = false;
        public bool Ready
        {
            get { return _ready; }
            set
            {
                _ready = value;

                if (value)
                {
 
                    //start fs
                    FileSystem.OnFolderEvent += FileSystem_OnFolderEvent;
                    FileSystem.OnFileEvent += FileSystem_OnFileEvent;

                }
                else
                { 
                    //start fs
                    FileSystem.OnFolderEvent -= FileSystem_OnFolderEvent;
                    FileSystem.OnFileEvent += FileSystem_OnFileEvent;
                }

                if (StateChanged != null) StateChanged(this);

            }

        }


        const int MinClients = 5;

        BaseInteractionEnvironment _Environment = null;
        public BaseInteractionEnvironment Environment
        {
            get
            {
                 return _Environment; 
            }
        }

        bool stop_sd_flag = false;
        void SelfDiagnostic()
        {
            while (!stop_sd_flag)
            {

                Settings.LastAliveTime = DateTime.Now;
                Settings.Save();

                Thread.Sleep(2000);
            }
        }

        public old_Client(string guid = null)
        {
            if (!string.IsNullOrEmpty(guid))
                Id = guid;
            Logs = new ObservableCollection<string>();

            LoadSetting();

            On = true;
            
        }
        bool _On = false;
        public bool On
        {
            get
            {
                return _On;
            }
            set
            {
                _On = value;

                if (value)
                {
                    _Environment = new old_MemIE(this);

                    FileSystem = new MockFS(this, MyDir.FullName);
                    _Environment.OnIeStateChanged += _Environment_OnIeStateChanged;
                    Environment.OnReady += Environment_OnReady;
                    _Environment.RemoteClients.CollectionChanged += Environment_OnRemoteClientStateChanged;
                    UpdateReady();
                }
                else
                {
                    Ready = false;
                    _Environment.OnIeStateChanged -= _Environment_OnIeStateChanged;
                    Environment.OnReady -= Environment_OnReady;
                    _Environment.RemoteClients.CollectionChanged -= Environment_OnRemoteClientStateChanged;
                    FileSystem = null;
                    Environment.Shutdown();
                    _Environment = null;
                }
            }

        }

        void _Environment_OnIeStateChanged(BaseInteractionEnvironment b, BaseInteractionEnvironment.IEeventType et)
        {
            Log(LogLevel.Info, "IE {0}", et);
        }

        private void Environment_OnReady(object sender, EventArgs e)
        {

        }
        void UpdateReady()
        {
            if (!Ready && RemoteClients.Count >= MinClients)
                Ready = true;
            if (Ready && RemoteClients.Count < MinClients)
                Ready = false;
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
                Log(LogLevel.Info, "sending updated metainfo");
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
                    (r as old_RemoteClient).Send(new DownloadFileMessage(srcFile, m));
                    m.WaitOne(-1);
                    srcFile.AddReplica(Id);
                    Log(NLog.LogLevel.Info, "Get replice for {0}", this);
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
                    Log(LogLevel.Info, "Hash not equal. updating file {0}", this);
                    var m = new EventWaitHandle(false, EventResetMode.AutoReset);
                    Log(LogLevel.Info, "Load replica {0}", srcFile.Name);
                    (GetFileOwner(srcFile) as old_RemoteClient).Send(new DownloadFileMessage(srcFile, m));
                    //m.WaitOne(-1);
                }

                Log(LogLevel.Info, "remote MetaInfoUpdatet for file {0}", this);
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
                Log(LogLevel.Info, "send create folder {0} to all", srcFolder.Name);
                Environment.SendToAll(new NewFolderMessage(srcFolder));
            }
            if (eventtype == FSObjectEvents.local_delete)
            {
                Log(LogLevel.Info, "send delete folder {0} to all", srcFolder.Name);
                Environment.SendToAll(new DeleteFSObjMessage(srcFolder));
            }
        }

        internal ClientSettings Settings { get; set; }
        private void LoadSetting()
        {
           // Settings = ClientSettings.Load(MyDir.FullName);
           // if (string.IsNullOrEmpty(Settings.Id))
           //     Settings.Id = Id;
           // Settings.LastAliveTime = DateTime.Now;
           // Settings.Save();
        }

        void Environment_OnRemoteClientStateChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateReady();
            if (!Ready) return;

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {

                ///Вновь подключившемуся должен ответить ближайший узел 
                foreach (old_RemoteClient it in e.NewItems)
                {
                    lock (RemoteClients)
                    {
                        if (Environment.IsNearest(this, it, RemoteClients.ToList()))
                            it.Send(new SynchMessage());
                    }
                    Log(LogLevel.Info, "old_Client {0} connected", it);
                }

            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (old_RemoteClient r in e.OldItems)
                {
                    Log(LogLevel.Info, "old_Client {0} disconnected", r);


                    var rid = r.Id.ToString();
                    var allfiles = FileSystem.GetFileList();
                    ///когда узел ушел мы должны найти файлы, для которых он был мастером и для которых мы являемся репликой.
                    ///если мы являемся ближайшей по расстоянию репликой, то делаем себя мастером и рассылаем метаинфу
                    ///



                    List<BaseFile> files = allfiles.Where(x => x.meta.Owner == rid).Where(x => x.meta.Replics.Contains(this.Id.ToString())).ToList(); //FileSystem.GetFilesByOwnerID(r.Id.ToString()).Where(x => x.meta.Replics.Contains(Id.ToString())).ToList();

                    foreach (var f in files)
                    {
                        var reps = new List<old_RemoteClient>();
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

                        var ralive = new List<old_RemoteClient>();
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
        /// return random alive replica-node by file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
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

        internal INode GetFileOwner(BaseFile baseFile)
        {
            lock (RemoteClients)
            {
                var own = RemoteClients.FirstOrDefault(x => x.Id.ToString() == baseFile.meta.Owner);
                return own;
            }
        }

       
        [JsonIgnore]
        public BaseFileSystem FileSystem
        {
            get;
            private set;
        }

    
        public ObservableCollection<old_RemoteClient> RemoteClients
        {
            get
            {
                return Environment.RemoteClients;
            }
        }

        public override bool Equals(object obj)
        {
            return Id.Equals((obj as old_Client).Id);
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();

        }

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
    }

    [Target("CustomLogTarget")]
    public class CustomLogTarget : TargetWithLayout
    {

        protected override void Write(LogEventInfo logEvent)
        {
            var str = string.Format(logEvent.Message, logEvent.Parameters);
            if (logEvent.Parameters.Last() is old_Client)
            {

                (logEvent.Parameters.Last() as old_Client).Logs.Insert(0, string.Format("{0} [{1}] {2}", DateTime.Now.TimeOfDay, logEvent.Level.Name.ToUpper(), str));
            }
        }

    }

 

}

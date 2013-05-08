using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using NLog;
using NLog.Targets;
using YnetFS.FileSystem;
using YnetFS.FileSystem.Mock;
using YnetFS.InteractionEnvironment;
using YnetFS.Messages;

namespace YnetFS
{
    public class Client 
    {
        BaseInteractionEnvironment _Environment = null;
        public BaseInteractionEnvironment Environment
        {
            get
            {
                lock (this) { return _Environment; }
            }
        }

        Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public Client(string guid=null)
        {
            if (!string.IsNullOrEmpty(guid))
                _id = Guid.Parse(guid);
            Logs = new ObservableCollection<string>();

            init();
        }

        public void init()
        {
            lock (this)
            {
                LoadSetting();
                On = true;


                //start fs
                FileSystem = new MockFS(this, Id.ToString());

                //start ie
                _Environment = new MemIE(this);
                _Environment.RemoteClients.CollectionChanged += Environment_OnRemoteClientStateChanged;

                FileSystem.OnFolderEvent += FileSystem_OnFolderEvent;
                FileSystem.OnFileEvent += FileSystem_OnFileEvent;
            }

        }

        private void Environment_OnReady(object sender, EventArgs e)
        {

        }

        void FileSystem_OnFileEvent(IFile srcFile, IFSObjectEvents eventtype)
        {
            lock (RemoteClients)
            {
                if (eventtype == IFSObjectEvents.local_created)
                {
                    foreach (var r in RemoteClients)
                    {
                        r.Send(new NewFileMessage(srcFile));
                    }
                }
                if (eventtype == IFSObjectEvents.local_delete)
                {
                    foreach (var r in RemoteClients)
                    {
                        r.Send(new DeleteFSObjMessage(srcFile));
                    }
                } 
            }
        }

        void FileSystem_OnFolderEvent(IFolder srcFolder, IFSObjectEvents eventtype)
        {
            lock (RemoteClients)
            {
                if (eventtype == IFSObjectEvents.local_created)
                {
                    foreach (var r in RemoteClients)
                    {
                        r.Send(new NewFolderMessage(srcFolder));
                    }
                }
                if (eventtype == IFSObjectEvents.local_delete)
                {
                    foreach (var r in RemoteClients)
                    {
                        r.Send(new DeleteFSObjMessage(srcFolder));
                    }
                }
            }
        }

        private void LoadSetting()
        {
            //TODO: Load local settings
        }

        void Environment_OnRemoteClientStateChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                ///Подключившемуся клиенту должен ответить тот, у кого guid ближайший к текущему сверху 
                ///если такого нет - клиент с минимальным guid
                ///имея список всех узлов, исключая подключившийся, проверяем, является ли текущий
                ///следующим по величине узлом относительно добавившегося
                ///для определения какой из guid`ов больше - используем его хеш=\


                foreach (RemoteClient it in e.NewItems)
                {
                    if (IsMeNearestFor(it))
                        it.Send(new SynchMessage_mock());
                }

            }
        }


        /// <summary>
        /// Determines if this is nearest client for @RemoteClient
        /// </summary>
        /// <param name="Remoteclient"></param>
        /// <returns></returns>
        private bool IsMeNearestFor(RemoteClient Remoteclient)
        {
            var list_to_Find = new List<RemoteClient>();
            lock (RemoteClients)
            {
                foreach (var r in RemoteClients.Where(x => x.IsOnline()))
                    if (r != Remoteclient)
                        list_to_Find.Add(r);
            }
            if (list_to_Find.Count == 0) return true;
            list_to_Find = list_to_Find.OrderBy(x => x.Id.GetHashCode()).ToList();

            var rch = Remoteclient.GetHashCode();//remote client hash
            var mydist = Id.GetHashCode() - rch;

            var best = list_to_Find.FirstOrDefault(x => rch < x.Id.GetHashCode());
            if (mydist < 0)
            {
                if (best != null) return false;
                var minr = list_to_Find.First();
                return mydist < minr.Id.GetHashCode() - rch;
            }
            else
            {
                if (best == null) return true;
                return mydist < best.Id.GetHashCode() - rch;
            }
        }

        public FileSystem.IFileSystem FileSystem
        {
            get;
            private set;
        }

        Guid _id = Guid.Empty;
        public Guid Id
        {
            get { if (_id == Guid.Empty)_id = Guid.NewGuid(); return _id; }//TODO: guid must by constant for one instance after their restart
        }

        public ObservableCollection<RemoteClient> RemoteClients
        {
            get
            {
                return Environment.RemoteClients;
            }
        }

        public override bool Equals(object obj)
        {
            return Id.Equals((obj as Client).Id);
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        
        }
        public ObservableCollection<string> Logs { get; set; }
        public void Log(LogLevel lvl, string msg, params object[] objs)
        {
            if (objs == null) objs = new List<object>().ToArray();
            var tmp = objs.ToList();
            tmp.Add(this);
            Logger.Log(lvl, msg, tmp.ToArray());
        }

        public bool On = true;
        public void BeeOff()
        {
            FileSystem.OnFolderEvent -= FileSystem_OnFolderEvent;
            FileSystem.OnFileEvent -= FileSystem_OnFileEvent;
            Environment.OnReady -= Environment_OnReady;
            FileSystem = null;
            Environment.Shutdown();
            _Environment = null;
            On = false;
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

                (logEvent.Parameters.Last() as Client).Logs.Insert(0,string.Format("{0} [{1}] {2}", DateTime.Now.TimeOfDay, logEvent.Level.Name.ToUpper(), str));
            }
        }
        
    }

 

}

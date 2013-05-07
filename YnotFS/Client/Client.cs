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


                //start fs
                FileSystem = new MockFS(this, Id.ToString());

                //start ie
                _Environment = new MemIE(this);


                FileSystem.OnFolderEvent += FileSystem_OnFolderEvent;
                FileSystem.OnFileEvent += FileSystem_OnFileEvent;
                On = true;
            }
            SynckFS();

        }

        private void Environment_OnReady(object sender, EventArgs e)
        {

        }

        private void SynckFS()
        {
            foreach (var r in RemoteClients)
            {
                r.Send(new SynchMessage_mock());
            }
        }

        void FileSystem_OnFileEvent(IFile srcFile, IFSObjectEvents eventtype)
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

        void FileSystem_OnFolderEvent(IFolder srcFolder, IFSObjectEvents eventtype)
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

        private void LoadSetting()
        {
            //TODO: Load local settings
        }

        void Environment_OnRemoteClientStateChanged(RemoteClient Remoteclient, RemoteClientState oldState, RemoteClientState newState)
        {
            if (newState == RemoteClientState.Connected)
            {
                Remoteclient.Send(new SynchMessage_mock());   
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

using System;
using Newtonsoft.Json;
using NLog;
using YnetFS.FileSystem;
using YnetFS.FileSystem.Mock;


namespace YnetFS.Messages
{
    public class NewFileMessage : Message
    {
        [JsonProperty]
        public FileMetaInfo Meta { get; set; }

        protected BaseFile srcFile;
        public NewFileMessage() { }
        public NewFileMessage(BaseFile srcFile)
        {
            this.srcFile = srcFile;
        }
        [JsonProperty]
        public string RelativePath { get; set; }
        public override void BeforeSend()
        {
            RelativePath = srcFile.ParentFolder.RelativePath;
            Meta = srcFile.meta;
        }

        public override void OnRecived(RemoteClient from, Client to)
        {
            //check folder exists

            Environment.ParentClient.Log(LogLevel.Info, "REMOTE: create new file {0}", RelativePath);
            var fs = Environment.ParentClient.FileSystem as MockFS;
            var fsobj = fs.Find(RelativePath) as BaseFolder;

            if (fsobj == null)
                fsobj = Environment.ParentClient.FileSystem.CreateFolder(Environment.ParentClient.FileSystem.RootDir, RelativePath, FSObjectEvents.remote_create);

            Environment.ParentClient.FileSystem.AddFile(fsobj, Meta, FSObjectEvents.remote_create);
        }
    }
    public class UpdateMetaInfoMessage : FsObjectOperationMessage
    {
        [JsonProperty]
        public FileMetaInfo Meta { get; set; }

        public UpdateMetaInfoMessage() { }
        public UpdateMetaInfoMessage(BaseFile srcFile)
            : base(srcFile) { }

        public override void BeforeSend()
        {
            base.BeforeSend();
            Meta = srcFile.meta;
        }
        public override void OnRecived(RemoteClient from, Client to)
        {
            Environment.ParentClient.Log(LogLevel.Info, "REMOTE: updated meta for {0} from {1}", RelativePath,from.Id);
            base.OnRecived(from, to);
            if (srcFile!=null) 
            srcFile.UpdateMeta(Meta,true);
        }
    }

    public class FsObjectOperationMessage : Message
    {
        protected FsObjectOperationMessage() { }
        protected FsObjectOperationMessage(BaseFile srcFile)
        {
            this.srcFile = srcFile;
        }
        [JsonProperty]
        public string RelativePath { get; set; }

        [JsonIgnore]
        protected BaseFile srcFile;

        public override void BeforeSend()
        {
            if (srcFile!=null)
                RelativePath = srcFile.RelativePath;
            base.BeforeSend();
        }
        public override void OnRecived(RemoteClient from, Client to)
        {
            base.OnRecived(from, to);
            if (!string.IsNullOrEmpty(RelativePath))
            srcFile = Environment.ParentClient.FileSystem.Find(RelativePath) as BaseFile;
            if (srcFile == null)
            {
                throw new Exception(string.Format("File {0} not fount (request from {1})", RelativePath, from.Id));
                Environment.ParentClient.Log(LogLevel.Error, "File {0} not fount (request from {1})", RelativePath, from.Id);
            }
        }
    }

    public class LockFileMessage : FsObjectOperationMessage
    {
        public LockFileMessage(BaseFile srcFile) : base(srcFile) { }
        public override void OnRecived(RemoteClient from, Client to)
        {
            Environment.ParentClient.Log(LogLevel.Info, "REMOTE: Lock {0}", RelativePath);
            base.OnRecived(from, to);
            srcFile.Lock();
        }
    }
    public class UnLockFileMessage : FsObjectOperationMessage
    {
        public UnLockFileMessage(BaseFile srcFile) : base(srcFile) { }
        public override void OnRecived(RemoteClient from, Client to)
        {
            Environment.ParentClient.Log(LogLevel.Info, "REMOTE: Unlock {0}", RelativePath);
            base.OnRecived(from, to);
            srcFile.UnLock();
        }
    }

}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using YnetFS.Messages;

namespace YnetFS.FileSystem
{

    public class BaseFile : IFSObject
    {

        [JsonIgnore]
        public string MetaPath { get; private set; }

        [JsonIgnore]
        public string RealPath { get; private set; }

        public string RelativePath { get; private set; }

        [JsonIgnore]
        public BaseFolder ParentFolder { get; private set; }
        public string Name { get; set; }

        FileMetaInfo _meta = null;
        public FileMetaInfo meta
        {
            get
            {
                if (_meta == null)
                    _meta = new FileMetaInfo(this);
                return _meta;
            }
        }

        FileData _data = null;
        public FileData data
        {
            get
            {
                if (_data == null) _data = new FileData(this);
                return _data;
            }
        }
        public BaseFile() {
            
        }
        public BaseFile(BaseFolder Parentdir, string Name)
        {
            if (Parentdir == null) throw new ArgumentNullException();
            ParentFolder = Parentdir;
            this.Name = Name;

            RelativePath = Path.Combine(Parentdir.RelativePath, Name);


            MetaPath = Path.Combine(Parentdir.MetaPath, Name);
            RealPath = Path.Combine(Parentdir.RealPath, Name);

        }

        public BaseFile(BaseFolder ParentFolder, FileMetaInfo MetaFile)
        {
            this.ParentFolder = ParentFolder;
            this.Name = MetaFile.Name;

            RelativePath = Path.Combine(ParentFolder.RelativePath, Name);
            MetaPath = Path.Combine(ParentFolder.MetaPath, Name);
            RealPath = Path.Combine(ParentFolder.RealPath, Name);

            _meta = MetaFile;
            _meta.ParenFile = this;

            MetaFile.Save();
        }

        public override string ToString()
        {
            return Name;
        }

        public void Delete()
        {
            if (File.Exists(RealPath))
                File.Delete(RealPath);
            if (File.Exists(MetaPath))
                File.Delete(MetaPath);
        }

        internal void SetOwner(string id)
        {
            meta.SetOwner(id);
            meta.AddReplica(id);
        }
        internal void SetHash(string hash)
        {
            meta.SetHash(hash);
        }

        internal void AddReplica(string id)
        {
            if (meta.Replics.Contains(id)) return;
            meta.AddReplica(id);   
        }

        internal void RemoveReplica(string id)
        {
            if (!meta.Replics.Contains(id)) return;
            meta.RemoveReplica(id);
        }
 
        internal void PushData(string tmpfile)
        {
            if (File.Exists(RealPath))
                File.Delete(RealPath);
            File.Copy(tmpfile, RealPath);
        }

        public bool Open()
        {
            if (Locked) return false;

            Locked = true;
            if (OnFileEvent != null)
                OnFileEvent(this, FSObjectEvents.local_opend);

            return true;
        }

        public void Close()
        {
            if (!Locked) return ;



            Locked = false;
            if (OnFileEvent != null)
                OnFileEvent(this, FSObjectEvents.local_closed);

        }
        public void Lock()
        {
            Locked = true;
            if (OnFileEvent != null)
                OnFileEvent(this, FSObjectEvents.remote_opend);
        }
        public void UnLock()
        {
            Locked = false;
            if (OnFileEvent != null)
                OnFileEvent(this, FSObjectEvents.remote_closed);
        }
        bool Locked = false;

        public event FileEventHandler OnFileEvent;

        internal void UpdateMeta(FileMetaInfo Meta,bool remote)
        {
            _meta = Meta;
            _meta.ParenFile = this;
            _meta.Save();

            if (OnFileEvent != null)
                OnFileEvent(this, remote?FSObjectEvents.remote_changed:FSObjectEvents.local_changed);
        }

    }



    public class FileData
    {
        public bool Downloaded { get { return File.Exists(ParentFile.RealPath); } }
        BaseFile ParentFile { get; set; }
        public FileData(BaseFile f)
        {
            this.ParentFile = f;

        }
        public string ComputeHash()
        {
            if (!Downloaded) return null;

            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var stream = File.OpenRead(ParentFile.RealPath))
                return System.BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
        }

    }
}

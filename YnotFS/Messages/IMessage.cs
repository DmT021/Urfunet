using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using YnetFS.FileSystem;
using YnetFS.FileSystem.Mock;
using YnetFS.InteractionEnvironment;

namespace YnetFS.Messages
{

    public abstract class Message
    {
        [JsonIgnore]
        public BaseInteractionEnvironment Environment { get; set; }

        #region Code-decode
        public static Message Decode(string s, BaseInteractionEnvironment Env)
        {
            var o = JObject.Parse(s).GetValue("Type").ToString();
            var output = FindAllDerivedTypes<Message>(Assembly.GetAssembly(typeof(Message)));

            foreach (var it in output)
                if (it.Name == o)
                {
                    var res = (Message)JsonConvert.DeserializeObject(s, it);
                    res.Environment = Env;
                    return res;
                }
            throw new Exception("MessageType not found");
        }
        public string Code()
        {
            BeforeSend();
            return JsonConvert.SerializeObject(this);
        }
        static List<Type> FindAllDerivedTypes<T>(Assembly assembly)
        {
            var derivedType = typeof(T);
            return assembly
                .GetTypes()
                .Where(t =>
                    t != derivedType &&
                    derivedType.IsAssignableFrom(t)
                    ).ToList();

        }
        #endregion
        #region Properties

        [JsonProperty(PropertyName = "Type")]
        public virtual string Type { get { return this.GetType().Name; } }

        [JsonProperty(PropertyName = "Data")]
        public string Data { get; set; }

        [JsonProperty(PropertyName = "_fromId")]
        public string _fromId { get; set; }
        [JsonIgnore]
        public string FromId { get { return _fromId; } set { _fromId = value; } }
        #endregion

        /// <summary>
        /// действие, которое вызывается у клиета по приходу этого сообщения
        /// </summary>
        /// <param name="c"></param>
        public virtual void OnRecived(RemoteClient from, Client to) { }

        public virtual void BeforeSend() { }

        public override string ToString()
        {
            return GetType().Name;
        }

    }

    public class SyncMessage : Message
    {
        public SyncMessage()
        {
        }

        [JsonProperty(PropertyName = "RootDir")]
        public BaseFolder RootDir { get; set; }

        public override void BeforeSend()
        {
            RootDir = this.Environment.ParentClient.FileSystem.RootDir;
        }

        public override void OnRecived(RemoteClient from, Client to)
        {
            Environment.ParentClient.Log(LogLevel.Info, "Синхронизироваться {0} -> {1}", from.Id, to.Id);
            base.OnRecived(from, to);
            var myroot = Environment.ParentClient.FileSystem.RootDir;
            MergeFolder(RootDir, myroot);
            to.SyncComplited();
        }

        void MergeFolder(BaseFolder remote, BaseFolder local)
        {
            ///процесс сливания (local) папки с (remote) папкой
            ///1. Добавляем в local файлы из remote
            ///2. Удаляем из local файлы, отсутствующие в remote
            ///3. Добавляем в local папки из remote
            ///4. Удаляем из local папки, отсутствующие в remote
            ///5. Сверяем хэши файлов

            foreach (var it in remote.Files)
            {
                //if (!local.Files.Any(x => x.meta.Id == it.meta.Id))
                if (!local.Files.Any(x => x.RelativePath == it.RelativePath))
                {
                    //craete metafile
                    Environment.ParentClient.FileSystem.AddFile(local, it.meta, FSObjectEvents.remote_create);
                }
            }
            var tmpfiles = new List<BaseFile>();
            foreach (var it in local.Files) tmpfiles.Add(it);

            foreach (var f in tmpfiles)
            {
                //if (!remote.Files.Any(x => x.meta.Id == f.meta.Id))
                if (!remote.Files.Any(x => x.RelativePath == f.RelativePath))
                    Environment.ParentClient.FileSystem.Delete(f, FSObjectEvents.remote_delete);
            }

            foreach (var it in remote.Folders)
            {
                if (!local.Folders.Any(x => x.Name == it.Name))
                {
                    Environment.ParentClient.FileSystem.CreateFolder(local, it.Name, FSObjectEvents.remote_create);
                }
            }
            var tmpfolders = new List<BaseFolder>();
            foreach (var it in local.Folders) tmpfolders.Add(it);
            foreach (var f in tmpfolders)
            {
                var rf = remote.Folders.FirstOrDefault(x => x.Name == f.Name);
                if (rf != null)
                    MergeFolder(rf, f);
                else
                    Environment.ParentClient.FileSystem.Delete(f, FSObjectEvents.remote_delete);
            }
            foreach (var it in local.Files)
            {
                var rf = remote.Files.FirstOrDefault(x => x.Name == it.Name);

                if (rf.meta.Hash != it.meta.Hash)
                {
                    var m = new EventWaitHandle(false, EventResetMode.AutoReset);
                    (Environment.ParentClient.GetFileOwner(rf) as RemoteClient).Send(new DownloadFileMessage(it, m));
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public Guid FromId { get { return string.IsNullOrEmpty(_fromId) ? Guid.Empty : Guid.Parse(_fromId); } set { _fromId = value.ToString(); } } 
        #endregion

        /// <summary>
        /// действие, которое вызывается у клиета по приходу этого сообщения
        /// </summary>
        /// <param name="c"></param>
        public abstract void OnRecived(RemoteClient from, Client to);

        public virtual void BeforeSend() { }

    }

    public class SynchMessage_mock : Message
    {
        
        [JsonProperty(PropertyName = "RootDir")]
        public MockFolder RootDir { get; set; }

        public override void BeforeSend()
        {
            RootDir = this.Environment.ParentClient.FileSystem.RootDir as MockFolder;
        }


        public override void OnRecived(RemoteClient from, Client to)
        {
            var myroot = Environment.ParentClient.FileSystem.RootDir;
            MergeFolder(RootDir, myroot);
        }
        
        void MergeFolder(IFolder remote, IFolder local)
        {
            foreach (var it in remote.Files)
            {
                if (!local.Files.Any(x => x.meta.Id == it.meta.Id))
                {
                    //craete metafile
                    Environment.ParentClient.FileSystem.PushFile(local, it.meta,IFSObjectEvents.remote_create);
                }
            }
            foreach (var it in remote.Folders)
            {
                if (!local.Folders.Any(x => x.Name == it.Name))
                {
                    Environment.ParentClient.FileSystem.CreateFolder(local, it.Name,IFSObjectEvents.remote_create);
                }
            }
            foreach (var f in local.Folders)
            {
                var rf = remote.Folders.FirstOrDefault(x => x.Name == f.Name);
                if (rf!=null)
                    MergeFolder(rf, f);
            }
        }

    }

  
}

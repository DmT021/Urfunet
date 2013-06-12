using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NLog;
using YnetFS.FileSystem;
using YnetFS.FileSystem.Mock;

namespace YnetFS.Messages
{
    class DeleteFSObjMessage : Message
    {
        [JsonProperty]
        public string RelativePath { get; set; }

        public DeleteFSObjMessage() { }
        public DeleteFSObjMessage(IFSObject obj)
        {
            RelativePath = obj.RelativePath;
        }

        public override void BeforeSend()
        {
            base.BeforeSend();
        }

        public override void OnRecived(RemoteClient from, Client to)
        {
            Environment.ParentClient.Log(LogLevel.Info, "{1}: удалить файл {0}",RelativePath,from);
            base.OnRecived(from, to);
            var fs = Environment.ParentClient.FileSystem as MockFS;
            var fsobj = fs.Find(RelativePath);
            if (fsobj == null) return;
            Environment.ParentClient.FileSystem.Delete(fsobj,FSObjectEvents.remote_delete);
        }
    }
}

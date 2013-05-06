using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
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
            var fs = Environment.ParentClient.FileSystem as MockFS;
            var fsobj = fs.FindFSObjByRelativePath(RelativePath);

            Environment.ParentClient.FileSystem.Delete(fsobj,IFSObjectEvents.remote_delete);
        }
    }
}

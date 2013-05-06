using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using YnetFS.FileSystem;
using YnetFS.FileSystem.Mock;

namespace YnetFS.Messages
{
    public class NewFolderMessage : Message
    {
        [JsonProperty]
        public string RelativePath { get; set; }
        [JsonProperty]
        public string Name { get; set; }

        private FileSystem.IFolder srcFolder;

        public NewFolderMessage(FileSystem.IFolder srcFolder)
        {
            // TODO: Complete member initialization
            this.srcFolder = srcFolder;
        }
        public override void BeforeSend()
        {
            RelativePath = (srcFolder as MockFolder).ParentFolder.RelativePath;
            this.Name = srcFolder.Name;
        }

        public override void OnRecived(RemoteClient from, Client to)
        {
            ////check folder exists

           var fs = Environment.ParentClient.FileSystem as MockFS;
           var fsobj = fs.FindFSObjByRelativePath(RelativePath) as IFolder;

           if (fsobj == null)
               fsobj = Environment.ParentClient.FileSystem.CreateFolder(Environment.ParentClient.FileSystem.RootDir, RelativePath, IFSObjectEvents.remote_create);

           Environment.ParentClient.FileSystem.CreateFolder(fsobj, Name, IFSObjectEvents.remote_create);
        }
    }
}

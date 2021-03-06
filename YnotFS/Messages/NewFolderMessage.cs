﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NLog;
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

        private BaseFolder srcFolder;

        public NewFolderMessage(BaseFolder srcFolder)
        {
            // TODO: Complete member initialization
            this.srcFolder = srcFolder;
        }
        public override void BeforeSend()
        {
            RelativePath = (srcFolder as BaseFolder).ParentFolder.RelativePath;
            this.Name = srcFolder.Name;
        }

        public override void OnRecived(RemoteClient from, Client to)
        {
            ////check folder exists

            Environment.ParentClient.Log(LogLevel.Info, "{1}: Создать папку {0}", Name,from);

            base.OnRecived(from, to);
            var fs = Environment.ParentClient.FileSystem;
           var fsobj = fs.Find(RelativePath) as BaseFolder;

           if (fsobj == null)
               fsobj = Environment.ParentClient.FileSystem.CreateFolder(Environment.ParentClient.FileSystem.RootDir, RelativePath, FSObjectEvents.remote_create);

           Environment.ParentClient.FileSystem.CreateFolder(fsobj, Name, FSObjectEvents.remote_create);
        }
    }
}

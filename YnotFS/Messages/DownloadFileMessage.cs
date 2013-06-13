using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NLog;
using NLog.Targets;
using YnetFS.FileSystem;
using YnetFS.FileSystem.Mock;
using YnetFS.InteractionEnvironment;

namespace YnetFS.Messages
{

    //Запрос на загрузку файла
    public class DownloadFileMessage : FsObjectOperationMessage
    {

        public static DirectoryInfo ExchageFolder = null;
        public static int count = 0;


        public static Dictionary<string, EventWaitHandle> WaitHandles = new Dictionary<string, EventWaitHandle>();

        public string specdir { get; set; }

        public DownloadFileMessage() { }

        public DownloadFileMessage(BaseFile baseFile, EventWaitHandle m)
            : base(baseFile)
        {
            if (m != null && !WaitHandles.ContainsKey(srcFile.RelativePath))
                WaitHandles.Add(srcFile.RelativePath, m);
        }

        public override void BeforeSend()
        {
            base.BeforeSend();
            specdir = Guid.NewGuid().ToString();
            var path = new DirectoryInfo(Environment.ParentClient.FileSystem.RealPath).Parent.Parent.FullName;

            var gpath = Path.Combine(path, "exchange", specdir);
            if (!Directory.Exists(gpath))
                Directory.CreateDirectory(gpath);

            path = Path.Combine(path, "exchange");

            ExchageFolder = new DirectoryInfo(path);
            count++;
            
        }

        public override void OnRecived(RemoteClient from, Client to)
        {
            Environment.ParentClient.Log(LogLevel.Info, "{1}: загрузить файл {0}", RelativePath,from);
            Environment.ParentClient.Log(LogLevel.Info, "Отправка файла {0} на узел {1}", RelativePath, from);
            base.OnRecived(from, to);
 
            var newfilename = Path.Combine(ExchageFolder.FullName,specdir, srcFile.Name);
            if (File.Exists(newfilename))
                File.Delete(newfilename);

                if (File.Exists(srcFile.RealPath))
                    File.Copy(srcFile.RealPath, newfilename);
               // else
           //         from.Send(new SendFileMessage());
            from.Send(new SendFileMessage(srcFile,specdir));
        }
    }
    //передача файла
    public class SendFileMessage : FsObjectOperationMessage
    {

        public string specdir { get; set; }


        public SendFileMessage() { }
        public SendFileMessage(BaseFile baseFile,string specdir) : base(baseFile) {
            this.specdir = specdir;
        }


        public override void OnRecived(RemoteClient from, Client to)
        {
            base.OnRecived(from, to);
            if (srcFile == null) throw new Exception("Удаленный клиент сообщает что такого файла нет ({0})");
            Environment.ParentClient.Log(LogLevel.Info, "Загружен файл {0} с узла {1}", RelativePath, from);
            var tmpfile = Path.Combine(DownloadFileMessage.ExchageFolder.FullName,specdir, srcFile.Name);
            srcFile.PushData(tmpfile);
            Directory.Delete(Path.Combine(DownloadFileMessage.ExchageFolder.FullName, specdir), true);

            DownloadFileMessage.count--;
            if (DownloadFileMessage.count == 0)

                if (DownloadFileMessage.WaitHandles.ContainsKey(srcFile.RelativePath))
                {
                    var m = DownloadFileMessage.WaitHandles[srcFile.RelativePath];
                    if (m != null)
                    {

                        m.Set();
                        DownloadFileMessage.WaitHandles.Remove(srcFile.RelativePath);
                    }
                }
        }
    }
}
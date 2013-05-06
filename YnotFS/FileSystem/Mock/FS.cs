using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using YnetFS.InteractionEnvironment;

namespace YnetFS.FileSystem.Mock
{

    public class MockFS : IFileSystem
    {
        public Client ParentClient { get; set; }
        
        string rootpath { get; set; }

        public string MetaPath
        {
            get
            {
                var p = Path.Combine(rootpath, ".meta");
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                return p;
            }
        }
        public string RealPath
        {
            get
            {
                var p = Path.Combine(rootpath, "files");
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                return p;
            }
        }
        public string RelativePath { get { return "\\"; } }

        public MockFS(Client client, string rootpath)
        {
            this.ParentClient = client;
            this.rootpath = new DirectoryInfo(rootpath).FullName;
            LoadFS(RootDir);

            OnFolderEvent += OnFolderEventHandled;
            OnFileEvent += OnFileEventHandled;
        }

        public IFSObject FindFSObjByRelativePath(string RelPath)
        {
            if (RootDir.RelativePath == RelPath) return RootDir;

            return FindFSObjByRelativePath(RelPath,RootDir);
        }

        private IFSObject FindFSObjByRelativePath(string RelPath, IFolder folder)
        {
            foreach (var it in folder.Files)
                if (it.RelativePath == RelPath) return it;
            foreach (var it in folder.Folders)
                if (it.RelativePath == RelPath) return it;

            foreach (var it in folder.Folders)
            {
                var res = FindFSObjByRelativePath(RelPath, it as IFolder);
                if (res != null) return res;
            }
            return null;
        }

        private void LoadFS(IFolder RootDir)
        {
            foreach (var f in Directory.GetDirectories(RootDir.MetaPath))
            {
                var d = new MockFolder(new DirectoryInfo(f).Name, RootDir);
                RootDir.Folders.Add(d);
            }

            foreach (var f in Directory.GetFiles(RootDir.MetaPath))
            {
                var newf = new MockFile(new FileInfo(f).Name, RootDir);
                RootDir.Files.Add(newf);
            }

            foreach (var f in RootDir.Folders)
                LoadFS(f);
        }


        MockFolder rdir = null;
        public IFolder RootDir
        {
            get
            {
                if (rdir == null)
                {
                    rdir = new MockFolder(RelativePath, this);
                }
                return rdir;
            }
        }


        public string Name { get; set; }

        public BaseInteractionEnvironment Environment { get { return ParentClient.Environment; } }



        public IFolder CreateFolder(IFolder ParentFolder, string FolderName, IFSObjectEvents CreationType)
        {
            if (!string.IsNullOrEmpty(FolderName) && FolderName.Length > 0 && FolderName != "\\" && FolderName[0] == '\\') FolderName = FolderName.Substring(1);

            if (FolderName.Contains("\\") || FolderName.Contains("/")) throw new NotImplementedException("Add subfolder creation");

            var realdir = Path.Combine(ParentFolder.RealPath, FolderName);
            if (Directory.Exists(realdir))
            {
                (ParentClient as Client).Log(LogLevel.Error, "Folder {0} exists!", FolderName);
                return FindFSObjByRelativePath(FolderName) as IFolder;
            }
            //create real
            var fir = Directory.CreateDirectory(realdir);
            //create meta
            var fim = Directory.CreateDirectory(Path.Combine(ParentFolder.MetaPath, FolderName));
            //send info to others
            var f = new MockFolder(FolderName, ParentFolder);
            ParentFolder.Folders.Add(f);

            if (OnFolderEvent != null)
                OnFolderEvent(f, CreationType);

            return f;
        }





        public event FolderEventHandelr OnFolderEvent;

        public event FileEventHandler OnFileEvent;

        public void OnFolderEventHandled(IFolder srcFolder, IFSObjectEvents eventtype)
        {
            var c = ParentClient as Client;
            c.Log(NLog.LogLevel.Info, "Folder \"{0}\" {1}", srcFolder, eventtype);
        }

        public void OnFileEventHandled(IFile srcFile, IFSObjectEvents eventtype)
        {
            var c = ParentClient as Client;
            c.Log(NLog.LogLevel.Info, "File \"{0}\" {1}", srcFile, eventtype);
        }


        public IFile PushFile(IFolder ParentFolder, FileMetaInfo MetaFile, IFSObjectEvents FileEventType)
        {
            //check exists
            //save meta
            //add to files
            //throw event

            var metapath = Path.Combine(ParentFolder.MetaPath, MetaFile.Name);
            if (File.Exists(metapath))
            { (ParentClient as Client).Log(NLog.LogLevel.Error, "File \"{0}\" already exists!", metapath); return null; }


            var f = new MockFile(MetaFile.Name,ParentFolder);
            ParentFolder.Files.Add(f);

            f.SetMetaFile(MetaFile);

            MetaFile.Save();

            //add to files

            if (OnFileEvent != null)
                OnFileEvent(f, FileEventType);

            return f;
        }

        public IFile PushFile(IFolder ParentFolder, string pathToExistingfile)
        {
            //check exists
            //copy to local
            //add to files

            //check exists
            if (!File.Exists(pathToExistingfile))
            { (ParentClient as Client).Log(NLog.LogLevel.Error, "File \"{0}\" not exists!", pathToExistingfile); return null; }


            //copy to local
            var newfile = new FileInfo(pathToExistingfile);
            var newFileRealPath = Path.Combine(ParentFolder.RealPath, newfile.Name);
                Debug.Assert(!File.Exists(newFileRealPath),"надо бы както обработать замену файла");
            File.Copy(pathToExistingfile, newFileRealPath);
            var f = new MockFile(newfile.Name, ParentFolder);
            f.meta.SetOwner(ParentClient);

            //add to files
            ParentFolder.Files.Add(f);

            if (OnFileEvent != null)
                OnFileEvent(f, IFSObjectEvents.local_created);

            return f;
        }


        public void Delete(IFSObject iFSObject, IFSObjectEvents eventtype)
        {
            ///check exists
            ///
            if (FindFSObjByRelativePath(iFSObject.RelativePath) == null) throw new Exception("Not found");

            //remove real
            if (iFSObject is IFile)
                File.Delete(iFSObject.RealPath);
            else if (iFSObject is IFolder)
                Directory.Delete(iFSObject.RealPath);

            //remove meta
            if (iFSObject is IFile)
                File.Delete(iFSObject.MetaPath);
            else if (iFSObject is IFolder)
                Directory.Delete(iFSObject.MetaPath);

            //remove from fs
            if (iFSObject is IFile)
            {
                var f = iFSObject as IFile;
                f.ParentFolder.Files.Remove(f);
            }
            else if (iFSObject is IFolder)
            {
                var f = iFSObject as IFolder;
                (f.ParentFolder as IFolder).Folders.Remove(f);
            }

            if (iFSObject is IFile)
                if (OnFileEvent != null)
                    OnFileEvent(iFSObject as IFile, eventtype);
            if (iFSObject is IFolder)
                if (OnFolderEvent != null)
                    OnFolderEvent(iFSObject as IFolder, eventtype);
        }
    }

}

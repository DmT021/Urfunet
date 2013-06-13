using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NLog;
using YnetFS.FileSystem.Mock;
using YnetFS.InteractionEnvironment;
using YnetFS.Messages;

namespace YnetFS.FileSystem
{
    public abstract class BaseFileSystem
    {
        public const string metafolder = ".meta";

        string rootpath { get; set; }
        public string MetaPath
        {
            get
            {
                var p = Path.Combine(rootpath, metafolder);
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                return p;
            }
        }
        public const string datafolder = "data";

        public string RealPath
        {
            get
            {
                var p = Path.Combine(rootpath, datafolder);
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                return p;
            }
        }

        BaseFolder rdir = null;
        public BaseFolder RootDir
        {
            get
            {
                if (rdir == null)
                {
                    rdir = new BaseFolder(this);
                }
                return rdir;
            }
        }
        public string RelativePath { get { return "\\"; } }

        bool _ReadOnly = true;
        public bool ReadOnly
        {
            get { return _ReadOnly; }
            set
            {

                _ReadOnly = value;
                if (ReadOnly)
                {
                    _OnFolderEvent -= OnFolderEventHandled;
                    _OnFileEvent -= OnFileEventHandled;
                }
                else
                {
                    _OnFolderEvent += OnFolderEventHandled;
                    _OnFileEvent += OnFileEventHandled;
                }
            }

        }

        public BaseFileSystem(string rootpath)
        {
            this.rootpath = rootpath;
            LoadFS(RootDir);
        }

        private void LoadFS(BaseFolder RootDir)
        {
            lock (RootDir.FS)
            {
                foreach (var f in Directory.GetDirectories(RootDir.MetaPath))
                {
                    var d = new BaseFolder(RootDir, new DirectoryInfo(f).Name);
                    RootDir.Folders.Add(d);
                }

                foreach (var f in Directory.GetFiles(RootDir.MetaPath))
                {
                    var newf = new BaseFile(RootDir, new FileInfo(f).Name);
                    RootDir.Files.Add(newf);
                    newf.OnFileEvent += OnFileEventHandled;
                }

                foreach (var f in RootDir.Folders)
                    LoadFS(f);
            }
        }

        public IFSObject Find(string RelPath)
        {
            if (RootDir.RelativePath == RelPath) return RootDir;
            return FindFSObjByRelativePath(RelPath, RootDir);
        }

        private IFSObject FindFSObjByRelativePath(string RelPath, BaseFolder folder)
        {
            lock (this)
            {
                foreach (var it in folder.Files)
                    if (it.RelativePath == RelPath) return it;
                foreach (var it in folder.Folders)
                    if (it.RelativePath == RelPath) return it;

                foreach (var it in folder.Folders)
                {
                    var res = FindFSObjByRelativePath(RelPath, it as BaseFolder);
                    if (res != null) return res;
                }
                return null;
            }
        }

        public List<BaseFile> GetFileList(BaseFolder rootdir = null)
        {
            if (rootdir == null) rootdir = RootDir;
            var ret = new List<BaseFile>();
            foreach (var it in rootdir.Files)
                ret.Add(it);
            foreach (var it in rootdir.Folders)
                ret.AddRange(GetFileList(it));
            return ret;
        }

        #region Events
        /// <summary>
        /// внутренние события
        /// </summary>
        event FolderEventHandelr _OnFolderEvent;
        event FileEventHandler _OnFileEvent;


        /// <summary>
        /// внешние события
        /// разделение необходимо для аггрегации событий файлов (динамическое создание\уделение)
        /// </summary>
        public event FolderEventHandelr OnFolderEvent;
        public event FileEventHandler OnFileEvent;

        public virtual void OnFolderEventHandled(BaseFolder srcFolder, FSObjectEvents eventtype)
        {
            ///передача агрегированных событий
            if (OnFolderEvent != null)
                OnFolderEvent(srcFolder, eventtype);
        }

        public virtual void OnFileEventHandled(BaseFile srcFile, FSObjectEvents eventtype)
        {
            //if (ReadOnly && eventtype == FSObjectEvents.remote_create)
            //    throw new Exception("Can not create file on readonly mode");


            ///подписываемся на события каждого нового файла
            if (eventtype == FSObjectEvents.local_created || eventtype == FSObjectEvents.remote_create)
                srcFile.OnFileEvent += OnFileEventHandled;

            ///передача агрегированных событий
            if (OnFileEvent != null)
                OnFileEvent(srcFile, eventtype);
        }
        #endregion

        #region BaseOperations
        /// <summary>
        /// CreateFolder file+meta sourcelist existing file in local file system
        /// it meant that file where local_created
        /// </summary>
        /// <param name="ParentFolder">Direactory to move new file</param>
        /// <param name="pathToExistingfile">existing file</param>
        /// <returns></returns>
        public BaseFile AddFile(BaseFolder ParentFolder, string pathToExistingfile)
        {
    //        if (ReadOnly) return null;
            try
            {
                var f = ParentFolder.CreateFile(pathToExistingfile);
                lock (ParentFolder.FS)
                {
                    ParentFolder.Files.Add(f);
                }

                if (_OnFileEvent != null)
                    _OnFileEvent(f, FSObjectEvents.local_created);
                return f;

            }
            catch (Exception)
            {
                //ParentClient.Log(NLog.LogLevel.Error, rc.Message);
                return null;
            }
        }

        /// <summary>
        /// Add file by metafileinfo
        /// </summary>
        /// <param name="ParentFolder">Direactory to move new file</param>
        /// <param name="MetaFile">new file meta information</param>
        /// <returns></returns>
        public BaseFile AddFile(BaseFolder ParentFolder, FileMetaInfo MetaFile, FSObjectEvents FileEventType)
        {
         //   if (ReadOnly) return null;

            lock (ParentFolder.FS)
            {
                var metapath = Path.Combine(ParentFolder.MetaPath, MetaFile.Name);

                if (File.Exists(metapath))
                    throw new Exception(string.Format("Файл \"{0}\" уже существует!", metapath));// return null; 


                var f = new BaseFile(ParentFolder, MetaFile);
                ParentFolder.Files.Add(f);


                //add to files
                if (_OnFileEvent != null)
                    _OnFileEvent(f, FileEventType);

                return f;
            }
        }

        /// <summary>
        /// CreateFolder folders without subfolders
        /// </summary>
        /// <param name="ParentFolder">parent folder</param>
        /// <param name="FolderName">folder name only (or '\\' for root)</param>
        /// <param name="CreationType">type of folder creatinon (by local or by remote)</param>
        /// <returns></returns>
        public BaseFolder CreateFolder(BaseFolder ParentFolder, string FolderName, FSObjectEvents CreationType)
        {
         //   if (ReadOnly) return null;

            if (!string.IsNullOrEmpty(FolderName) && FolderName.Length > 0 && FolderName != "\\" && FolderName[0] == '\\')
                FolderName = FolderName.Substring(1);
            if (FolderName.Contains("\\") || FolderName.Contains("/"))
            {
                var g = FolderName.Split('\\').ToList();
                while (g.Count > 0)
                {
                    var pfold = Path.Combine(ParentFolder.RelativePath, g[0]);
                    var tmpfold = FindFSObjByRelativePath(pfold, ParentFolder) as BaseFolder;
                    if (tmpfold == null)
                        ParentFolder = CreateFolder(ParentFolder, g[0], CreationType);
                    else ParentFolder = tmpfold;
                    g.RemoveAt(0);
                }
            }

            var realdir = Path.Combine(ParentFolder.RealPath, FolderName);
            if (Directory.Exists(realdir))
            {
                //Delete(dirtodel, FSObjectEvents.local_delete);
                throw new Exception(string.Format("Folder {0} exists!", FolderName));
                return Find(FolderName) as BaseFolder;
            }
            //create real
            //create meta
            try
            {
                lock (ParentFolder.FS)
                {
                    var f = ParentFolder.CreateFolder(FolderName);

                    ParentFolder.Folders.Add(f);

                    if (_OnFolderEvent != null)
                        _OnFolderEvent(f, CreationType);

                    return f;
                }
            }
            catch (Exception)
            {
                //ParentClient.Log(NLog.LogLevel.Error, ex.Message);
                return null;
            }
        }

        public IFSObject Delete(IFSObject iFSObject, FSObjectEvents eventtype)
        {
       //     if (ReadOnly) return null;

            lock (RootDir.FS)
            {
                ///check exists
                ///
                if (Find(iFSObject.RelativePath) == null)
                {
                    throw new Exception(string.Format("Cant found {0} ", iFSObject.Name));
                    return null;
                }

                if (iFSObject is BaseFile) (iFSObject as BaseFile).Delete();
                if (iFSObject is BaseFolder)
                {
                    var its = (iFSObject as BaseFolder).Items;
                    while (its.Count > 0)
                        Delete(its[0], eventtype);
                    (iFSObject as BaseFolder).Delete(eventtype);
                }

                //remove sourcelist fs
                if (iFSObject is BaseFile)
                {
                    var f = iFSObject as BaseFile;
                    f.ParentFolder.Files.Remove(f);
                }
                else if (iFSObject is BaseFolder)
                {
                    var f = iFSObject as BaseFolder;
                    (f.ParentFolder as BaseFolder).Folders.Remove(f);
                }

                if (iFSObject is BaseFile)
                    if (_OnFileEvent != null)
                        _OnFileEvent(iFSObject as BaseFile, eventtype);
                if (iFSObject is BaseFolder)
                    if (_OnFolderEvent != null)
                        _OnFolderEvent(iFSObject as BaseFolder, eventtype);


                return iFSObject;
            }
        }
        #endregion
    }

    public enum FSObjectEvents
    {
        local_created,
        remote_create,

        remote_opend,
        local_opend,
        
        remote_closed,
        local_closed,
        
        local_delete,
        remote_delete,
        
        local_renamed,
        remote_renamed,
        
        local_changed,
        remote_changed,

        //Local_metachange,
        //remote_metachcnge

    }



    public delegate void FileEventHandler(BaseFile srcFile, FSObjectEvents eventtype);
    public delegate void FolderEventHandelr(BaseFolder srcFolder, FSObjectEvents eventtype);

    public interface IFSObject
    {
        string Name { get; }

        /// <summary>
        /// Full path to meta info
        /// </summary>
        string MetaPath { get; }

        /// <summary>
        /// full path to real file
        /// </summary>
        string RealPath { get; }

        /// <summary>
        /// realtive path in ynotfs
        /// </summary>
        [JsonProperty]
        string RelativePath { get; }



    }


}

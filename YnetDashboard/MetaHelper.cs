using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Ynet
{

    public class FileMetaInfo
    {
        [JsonProperty(PropertyName = "Id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "Owners")]
        public List<string> Owners { get; set; }
        [JsonProperty(PropertyName = "LastModifiedDate")]
        public DateTime LastModifiedDate { get; set; }
    }

    public class FsSync
    {

        string fileshomedir;
        string metahomedir;

        public FsSync(string homedir)
        {
            fileshomedir = Path.Combine(homedir, "files\\");
            metahomedir = Path.Combine(homedir, ".meta\\");
            if (!Directory.Exists(fileshomedir))
                Directory.CreateDirectory(fileshomedir);
            if (!Directory.Exists(metahomedir))
                Directory.CreateDirectory(metahomedir);
        }

        public FileMetaInfo GetFileMetaInfo(string path)
        {
            var fullpath = GetMetaFilePath(path);
            if (File.Exists(fullpath))
            {

                return JsonConvert.DeserializeObject<FileMetaInfo>(File.ReadAllText(fullpath)) ;
            }
            else
            {
                throw new FileNotFoundException("File not found", path);
            }
        }

        private string GetMetaFilePath(string relativePath)
        {
            return Path.Combine(metahomedir, relativePath);
        }

        private string GetFileRealPath(string relativePath)
        {
            return Path.Combine(fileshomedir, relativePath);

        }

        public void SetFileMetaInfo(string path, FileMetaInfo meta)
        {
            var fullpath = Path.Combine(metahomedir, path);
            var data = JsonConvert.SerializeObject(meta);
            File.WriteAllText(fullpath,data);
        }

        public IEnumerable<string> SyncLocal()
        {
            List<string> updates = new List<string>();

            Queue<string> folders = new Queue<string>();
            folders.Enqueue("");
            while (folders.Count > 0)
            {
                var currentFolder = folders.Dequeue();

                var fullFileDirectory = Path.Combine(fileshomedir, currentFolder);
                var fullMetaDirectory = Path.Combine(metahomedir, currentFolder);

                if (!Directory.Exists(fullMetaDirectory))
                {
                    Directory.CreateDirectory(fullMetaDirectory);
                }

                var subdirs = Directory.GetDirectories(fullFileDirectory);
                foreach (var item in subdirs)
                {
                    var relativePath = item.Replace(fileshomedir, "");
                    folders.Enqueue(relativePath);
                }

                var upd = SyncLocalFolder(currentFolder);
                updates.AddRange(upd);
            }

            return updates;
        }

        private IEnumerable<string> SyncLocalFolder(string folder)
        {
            List<string> updates = new List<string>();

            var fullFileDirectory = Path.Combine(fileshomedir, folder);
            var fullMetaDirectory = Path.Combine(metahomedir, folder);

            var files = Directory.GetFiles(fullFileDirectory);
            foreach (var item in files)
            {
                var relativePath = item.Replace(fileshomedir, "");
                var lastModified = File.GetLastWriteTimeUtc(item);
                if (!IsMetaFileExists(relativePath))
                {
                    var meta = new FileMetaInfo()
                    {
                        LastModifiedDate = lastModified
                    };
                    SetFileMetaInfo(relativePath, meta);
                    updates.Add(relativePath);
                }
                else
                {
                    var meta = GetFileMetaInfo(relativePath);
                    if (meta.LastModifiedDate < lastModified)
                    {
                        meta.LastModifiedDate = lastModified;
                        SetFileMetaInfo(relativePath, meta); // save changes
                        updates.Add(relativePath);
                    }
                }
            }

            return updates;
        }

        public bool IsMetaFileExists(string path)
        {
            var fullpath = GetMetaFilePath(path);
            return File.Exists(fullpath);
        }

        public void AddMetaFile(string path, string content)
        {
            var fullpath = GetMetaFilePath(path);
            if (IsMetaFileExists(path))
                throw new IOException("Metafile already exists.");

            var dirPath = Path.GetDirectoryName(fullpath);
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            File.WriteAllText(fullpath, content);
        }

        public string GetMetaFileContent(string path)
        {
            var fullpath = GetMetaFilePath(path);
            return File.ReadAllText(fullpath);
        }

        public IEnumerable<string> GetAllMetaFilesList()
        {
            List<string> list = new List<string>();

            Queue<string> folders = new Queue<string>();
            folders.Enqueue("");
            while (folders.Count > 0)
            {
                var currentFolder = folders.Dequeue();

                var fullMetaDirectory = Path.Combine(metahomedir, currentFolder);

                if (!Directory.Exists(fullMetaDirectory))
                {
                    Directory.CreateDirectory(fullMetaDirectory);
                }

                var subdirs = Directory.GetDirectories(fullMetaDirectory);
                foreach (var item in subdirs)
                {
                    var relativePath = item.Replace(metahomedir, "");
                    folders.Enqueue(relativePath);
                }

                var files = Directory.GetFiles(fullMetaDirectory);
                foreach (var item in files)
                {
                    var relativePath = item.Replace(metahomedir, "");
                    list.Add(relativePath);
                }
            }

            return list;
        }

        public byte[] GetFileData(string path)
        {
            var fullpath = GetFileRealPath(path);
            if (File.Exists(fullpath))
            {
                return File.ReadAllBytes(fullpath);
            }
            else
            {
                throw new IOException("File not found.");
            }
        }

        public void SaveFileData(string path, byte[] data)
        {
            var fullpath = GetFileRealPath(path);

            var dirPath = Path.GetDirectoryName(fullpath);
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            File.WriteAllBytes(fullpath, data);
        }
    }
}

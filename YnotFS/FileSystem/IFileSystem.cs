using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using YnetFS.InteractionEnvironment;

namespace YnetFS.FileSystem
{

    public enum IFSObjectEvents
    {
        local_created,
        remote_create,
        opend,
        closed,
        local_delete,
        remote_delete,
        local_renamed,
        remote_renamed,
        local_changed,
        remote_changed
    }

    public delegate void FileEventHandler(IFile srcFile, IFSObjectEvents eventtype);
    public delegate void FolderEventHandelr(IFolder srcFolder, IFSObjectEvents eventtype);

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

    public interface IFileSystem:IFSObject
    {
        IFolder RootDir {get; }
        BaseInteractionEnvironment Environment { get; }


        event FolderEventHandelr OnFolderEvent;
        event FileEventHandler OnFileEvent;

        /// <summary>
        /// Create folders without subfolders
        /// </summary>
        /// <param name="ParentFolder">parent folder</param>
        /// <param name="FolderName">folder name only (or '\\' for root)</param>
        /// <param name="CreationType">type of folder creatinon (by local or by remote)</param>
        /// <returns></returns>
        IFolder CreateFolder(IFolder ParentFolder, string FolderName, IFSObjectEvents CreationType);

        /// <summary>
        /// Create file+meta from existing file in local file system
        /// it meant that file where local_created
        /// </summary>
        /// <param name="ParentFolder">Direactory to move new file</param>
        /// <param name="pathToExistingfile">existing file</param>
        /// <returns></returns>
        IFile PushFile(IFolder ParentFolder, string pathToExistingfile);

        /// <summary>
        /// Create meta from other meta info
        /// </summary>
        /// <param name="ParentFolder">Direactory to move new file</param>
        /// <param name="MetaFile">new file meta information</param>
        /// <returns></returns>
        IFile PushFile(IFolder ParentFolder, FileMetaInfo MetaFile, IFSObjectEvents FileEventType);

        void OnFolderEventHandled(IFolder srcFolder, IFSObjectEvents eventtype);
        void OnFileEventHandled(IFile srcFile, IFSObjectEvents eventtype);

        void Delete(IFSObject iFSObject, IFSObjectEvents eventtype);

    }

    public interface IFile : IFSObject
    {
        [JsonIgnore]
        IFolder ParentFolder { get; }

        [JsonProperty(PropertyName = "meta")]

        FileMetaInfo meta { get; }



    }

    public interface IFolder : IFSObject
    {
        [JsonIgnore]
        IFSObject ParentFolder { get; }

        [JsonProperty(PropertyName = "Files", ItemTypeNameHandling = TypeNameHandling.Auto)]
        ObservableCollection<IFile> Files { get; }

        [JsonProperty(PropertyName = "Folders", ItemTypeNameHandling = TypeNameHandling.Auto)]
        ObservableCollection<IFolder> Folders { get; }



    }


}

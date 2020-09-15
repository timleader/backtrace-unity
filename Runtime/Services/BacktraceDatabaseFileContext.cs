using Backtrace.Unity.Interfaces;
using Backtrace.Unity.Model.Database;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Backtrace.Unity.Services
{
    /// <summary>
    /// BacktraceDatabase class for file collection operations
    /// </summary>
    internal class BacktraceDatabaseFileContext : IBacktraceDatabaseFileContext
    {
        private string[] _possibleDatabaseExtension = new string[] { ".dmp", ".json", ".jpg", ".log" };


        private readonly BacktraceDatabaseSettings _settings;

        /// <summary>
        /// Database directory info
        /// </summary>
        private readonly DirectoryInfo _databaseDirectoryInfo;

        /// <summary>
        /// Regex for filter physical database records
        /// </summary>
        private const string RecordFilterRegex = "*-record.json";

        /// <summary>
        /// Initialize new BacktraceDatabaseFileContext instance
        /// </summary>

        public BacktraceDatabaseFileContext(BacktraceDatabaseSettings settings)
        {
            _settings = settings;
            _databaseDirectoryInfo = new DirectoryInfo(_settings.DatabasePath);
        }

        /// <summary>
        /// Get all physical files stored in database directory
        /// </summary>
        /// <returns>All existing physical files</returns>
        public IEnumerable<FileInfo> GetAll()
        {
            return _databaseDirectoryInfo.GetFiles();
        }

        /// <summary>
        /// Get all valid physical records stored in database directory
        /// </summary>
        /// <returns>All existing physical records</returns>
        public IEnumerable<FileInfo> GetRecords()
        {
            return _databaseDirectoryInfo
                .GetFiles(RecordFilterRegex, SearchOption.TopDirectoryOnly)
                .OrderBy(n => n.CreationTime);
        }

        /// <summary>
        /// Remove orphaned files existing in database directory
        /// </summary>
        public void RemoveOrphaned(IEnumerable<BacktraceDatabaseRecord> existingRecords)
        {
            var recordStringIds = existingRecords.Select(n => n.Id.ToString());
            var files = GetAll();
            for (int fileIndex = 0; fileIndex < files.Count(); fileIndex++)
            {
                var file = files.ElementAt(fileIndex);
                //check if file should be stored in database
                //database only store data in json and files in dmp extension
                try
                {
                    if (!_possibleDatabaseExtension.Any(n => n == file.Extension))
                    {
                        file.Delete();
                        continue;
                    }
                    //get id from file name
                    //substring from position 0 to position from character '-' contains id
                    var name = file.Name.LastIndexOf('-');
                    // file can store invalid record because our regex don't match
                    // in this case we remove invalid file
                    if (name == -1)
                    {
                        file.Delete();
                        continue;
                    }
                    var stringGuid = file.Name.Substring(0, name);
                    if (!recordStringIds.Contains(stringGuid))
                    {
                        file.Delete();
                    }
                }
#pragma warning disable CS0168
                catch (Exception e)
                {
#if DEBUG
                    Debug.Log(e.ToString());
#endif
                    Debug.LogWarning(string.Format("Cannot remove file in path: {0}", file.FullName));
                }
#pragma warning restore CS0168
            }
        }

        /// <summary>
        /// Valid all files consistencies
        /// </summary>
        public bool ValidFileConsistency()
        {
            // Get array of all files
            FileInfo[] files = _databaseDirectoryInfo.GetFiles();

            // Calculate total bytes of all files in a loop.
            long size = 0;
            long totalRecordFiles = 0;
            foreach (var file in files)
            {
                if (Regex.Match(file.FullName, RecordFilterRegex).Success)
                {
                    totalRecordFiles++;

                    if (_settings.MaxRecordCount > totalRecordFiles)
                    {
                        return false;
                    }
                }
                size += file.Length;
                if (size > _settings.MaxDatabaseSize)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Remove all files from database directory
        /// </summary>
        public void Clear()
        {
            // Get array of all files
            FileInfo[] files = _databaseDirectoryInfo.GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                file.Delete();
            }
        }

        public IEnumerator Commit(BacktraceDatabaseRecord record)
        {
            if (record == null)
            {
                throw new ArgumentException("Record is null");
            }
            try
            {
                Save(record.BacktraceDataJson(), string.Format("{0}-attachment", record.Id));
                Save(record.ToJson(), string.Format("{0}-record", record.Id));
            }
            catch (IOException io)
            {
                Debug.Log(string.Format("Received {0} while saving data to database.",
                    "IOException"));
                Debug.Log(string.Format("Message {0}", io.Message));
            }
            catch (Exception ex)
            {
                Debug.Log(string.Format("Received {0} while saving data to database.", ex.GetType().Name));
                Debug.Log(string.Format("Message {0}", ex.Message));
            }
            yield return null;
        }

        private void Save(string json, string prefix)
        {
            byte[] file = Encoding.UTF8.GetBytes(json);
            Write(file, prefix);
        }

        private void Write(byte[] data, string prefix)
        {
            string filename = string.Format("{0}.json", prefix);
            string tempFilePath = Path.Combine(_settings.DatabasePath, string.Format("temp_{0}", filename));
            SaveTemporaryFile(tempFilePath, data);
            string destFilePath = Path.Combine(_settings.DatabasePath, filename);
            SaveValidRecord(tempFilePath, destFilePath);
        }

        /// <summary>
        /// Save valid diagnostic data from temporary file
        /// </summary>
        /// <param name="sourcePath">Temporary file path</param>
        /// <param name="destinationPath">destination path</param>
        private void SaveValidRecord(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        /// <summary>
        /// Save temporary file to hard drive.
        /// </summary>
        /// <param name="path">Path to temporary file</param>
        /// <param name="file">Current file</param>
        private void SaveTemporaryFile(string path, byte[] file)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                fs.Write(file, 0, file.Length);
            }
        }
    }
}

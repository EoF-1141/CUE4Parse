﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;

namespace CUE4Parse.FileProvider
{
    public class DefaultFileProvider : AbstractVfsFileProvider
    {
        public DefaultFileProvider(DirectoryInfo dir, bool isCaseInsensitive = false, bool autoLoadMappings = true, EGame game = EGame.GAME_UE4_LATEST, UE4Version ver = UE4Version.VER_UE4_DETERMINE_BY_GAME)
            : base(isCaseInsensitive, game, ver)
        {
            if (!dir.Exists)
                throw new ArgumentException("Given directory must exist", nameof(dir));
            ScanGameDirectory(dir, true);
            
            if (autoLoadMappings)
                MappingsContainer = new BenBotMappingsProvider("fortnitegame");
        }

        private void ScanGameDirectory(DirectoryInfo dir, bool recurse)
        {
            // Container for files located in the os file system
            var osFiles = new Dictionary<string, GameFile>();
            
            // Iterate over all directories recursively
            foreach (var file in dir.EnumerateFiles("*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                var ext = file.Extension.SubstringAfter('.');
                if (string.IsNullOrEmpty(ext) || !file.Exists) return;
                
                // If we got a pak file, add it to the list
                if (ext.Equals("pak", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var reader = new PakFileReader(file, Game, Ver) {IsConcurrent = true};
                        _unloadedVfs[reader] = null;
                        if (reader.IsEncrypted && !_requiredKeys.ContainsKey(reader.Info.EncryptionKeyGuid))
                            _requiredKeys[reader.Info.EncryptionKeyGuid] = null;
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e.ToString());
                    }
                }
                else if (ext.Equals("utoc", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var reader = new IoStoreReader(file, EIoStoreTocReadOptions.ReadDirectoryIndex, Game, Ver)
                            {IsConcurrent = true};
                        _unloadedVfs[reader] = null; 
                        if (reader.IsEncrypted && !_requiredKeys.ContainsKey(reader.Info.EncryptionKeyGuid))
                            _requiredKeys[reader.Info.EncryptionKeyGuid] = null;
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e.ToString());
                    }
                }
                else
                {
                    // Register local file only if it has a known extension, we don't need every file
                    if (GameFile.Ue4KnownExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    {
                        var osFile = new OsGameFile(dir, file, Game, Ver);
                        var path = osFile.Path;
                        if (IsCaseInsensitive)
                            osFiles[path.ToLowerInvariant()] = osFile;
                        else
                            osFiles[path] = osFile;
                    }
                }
            }

            if (osFiles.Count > 0)
            {
                _files.AddFiles(osFiles);
            }
        }
    }
}
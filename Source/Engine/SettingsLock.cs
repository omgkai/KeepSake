sealed partial class EditorSession
{
    // Separate window engines share preferences, but never their save/Undo state.
    // Merge each field edit against the latest disk settings while holding a lock.
    FileStream? AcquireSettingsLock()
    {
        if(settingsPath is null)return null;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!);
        for(int attempt=0;;attempt++) {
            try{return new FileStream(settingsPath+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException) when(attempt<80){Thread.Sleep(25);}
        }
    }
}

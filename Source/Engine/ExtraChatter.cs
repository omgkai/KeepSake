using System.Buffers.Binary;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    IChatter Chatter()=>RequireSave() switch {SAV4 s=>s.Chatter,SAV5 s=>s.Chatter,_=>throw new Exception("This game has no Chatter recording.")};
    ExtraPage ReadChatter(ExtraTool tool)
    {
        var c=Chatter();
        var row=ER("recording","Chatot’s voice",[
            EV("Initialized","Use recorded voice",c.Initialized),
            EV("Chance","Chatter confusion chance",c.ConfusionChance) with {kind="readonly",value=c.ConfusionChance+"%"}
        ],"Listen to the saved voice, import a 1,000-byte PCM recording, or export PCM/WAV audio. Confusion chance is calculated by the game’s original rules.",actions:[new("clear","Clear Recording")]) with {fileExtension="pcm"};
        return new("chatter",revision,tool,[row],[],true);
    }
    void EditChatter(JsonElement r,string id,string mode,Dictionary<string,string> edits)
    {
        if(id!="recording")throw new Exception("Choose the Chatter recording.");var c=Chatter();
        if(mode=="import"){ReadExtraBytes(S(r,"path"),IChatter.SIZE_PCM).CopyTo(c.Recording);c.Initialized=true;}
        else if(mode=="clear"){c.Recording.Clear();c.Initialized=false;}
        else if(edits.TryGetValue("Initialized",out var initialized))c.Initialized=bool.Parse(initialized);
    }
    byte[] ChatterWave()
    {
        // Nintendo DS recording: 1,000 bytes, low nibble first, 2 kHz mono.
        var pcm=Chatter().Recording;var wave=new byte[44+pcm.Length*2];
        "RIFF"u8.CopyTo(wave);BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(4),wave.Length-8);
        "WAVEfmt "u8.CopyTo(wave.AsSpan(8));BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(16),16);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(20),1);BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(22),1);
        BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(24),2000);BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(28),2000);
        BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(32),1);BinaryPrimitives.WriteInt16LittleEndian(wave.AsSpan(34),8);
        "data"u8.CopyTo(wave.AsSpan(36));BinaryPrimitives.WriteInt32LittleEndian(wave.AsSpan(40),pcm.Length*2);
        for(int i=0;i<pcm.Length;i++){wave[44+i*2]=(byte)((pcm[i]&15)<<4);wave[45+i*2]=(byte)(pcm[i]&0xF0);}
        return wave;
    }
    byte[] ExportChatter(JsonElement r)
    {
        if(S(r,"id")!="recording")throw new Exception("Choose the Chatter recording.");
        return S(r,"format","pcm") switch {"pcm"=>Chatter().Recording.ToArray(),"wav"=>ChatterWave(),_=>throw new Exception("Choose PCM or WAV audio.")};
    }
}

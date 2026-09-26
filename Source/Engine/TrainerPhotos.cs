using PKHeX.Core;

sealed partial class EditorSession
{
    object TrainerPhotos()
    {
        var sav=RequireSave();
        var images=new List<object>();
        SCBlockAccessor? blocks=sav switch { SAV9SV s=>s.Blocks,SAV9ZA s=>s.Blocks,_=>null };
        if(blocks is null)return images;
        void Add(string id,string title,uint dataKey,uint widthKey,uint heightKey)
        {
            if(!blocks.TryGetBlock(widthKey,out var w) || !blocks.TryGetBlock(heightKey,out var h) || !blocks.TryGetBlock(dataKey,out var data))return;
            if(w.Type!=SCTypeCode.UInt32 || h.Type!=SCTypeCode.UInt32)return;
            uint width=(uint)w.GetValue(),height=(uint)h.GetValue();
            if(width==0 || height==0)return;
            if(width>2048 || height>2048 || width%4!=0 || height%4!=0 || data.Data.Length<(long)width*height/2)return;
            var pixels=DXT1.Decompress(data.Data,(int)width,(int)height);
            images.Add(new{id,title,width,height,bgra=Convert.ToBase64String(pixels)});
        }
        if(sav is SAV9SV)
        {
            Add("profile","Profile photo",SaveBlockAccessor9SV.KPictureProfileCurrent,SaveBlockAccessor9SV.KPictureProfileCurrentWidth,SaveBlockAccessor9SV.KPictureProfileCurrentHeight);
            Add("current","Current icon",SaveBlockAccessor9SV.KPictureIconCurrent,SaveBlockAccessor9SV.KPictureIconCurrentWidth,SaveBlockAccessor9SV.KPictureIconCurrentHeight);
            Add("initial","Initial icon",SaveBlockAccessor9SV.KPictureIconInitial,SaveBlockAccessor9SV.KPictureIconInitialWidth,SaveBlockAccessor9SV.KPictureIconInitialHeight);
        }
        else
        {
            Add("current","Current photo",SaveBlockAccessor9ZA.KPictureCurrentData,SaveBlockAccessor9ZA.KPictureCurrentWidth,SaveBlockAccessor9ZA.KPictureCurrentHeight);
            Add("sbc","SBC photo",SaveBlockAccessor9ZA.KPictureSBCData,SaveBlockAccessor9ZA.KPictureSBCWidth,SaveBlockAccessor9ZA.KPictureSBCHeight);
            Add("initial","Initial photo",SaveBlockAccessor9ZA.KPictureInitialData,SaveBlockAccessor9ZA.KPictureInitialWidth,SaveBlockAccessor9ZA.KPictureInitialHeight);
        }
        return images;
    }
}

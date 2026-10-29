using PBZCraft.Trial;

InputCheck.Run();

string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"file-handoff-check-"+Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
string avatar=Path.Combine(root,"avatar.bin");
try
{
    File.WriteAllBytes(avatar,[1,2,3]);
    var capture=new RenderCapture(root);
    using(var locked=File.Open(avatar,FileMode.Open,FileAccess.Read,FileShare.Read))
    {
        capture.Receive(5,[4,5,6]);capture.Flush(100);
        if(capture.PendingWrites!=1 || capture.SharingRetries!=1 || !File.ReadAllBytes(avatar).SequenceEqual(new byte[]{1,2,3}))
            throw new InvalidOperationException("A live Lua reader was not handled safely.");
        capture.Receive(5,[7,8,9]);capture.Flush(200);
    }
    capture.Flush(300);
    if(capture.PendingWrites!=0 || !File.ReadAllBytes(avatar).SequenceEqual(new byte[]{7,8,9}))
        throw new InvalidOperationException("Latest avatar did not replace the old file after reader release.");
    Console.WriteLine("PASS: Windows reader lock defers replacement; latest avatar is committed after release.");
    var pixels=new byte[8+320*64*4];BitConverter.GetBytes(320).CopyTo(pixels,0);BitConverter.GetBytes(64).CopyTo(pixels,4);
    for(int n=8;n<pixels.Length;n+=4){pixels[n]=255;pixels[n+1]=255;pixels[n+2]=255;pixels[n+3]=255;}
    pixels[8]=10;pixels[9]=20;pixels[10]=30;
    int bottom=8+48*320*4;pixels[bottom+4]=0;pixels[bottom+5]=0;pixels[bottom+6]=0;
    capture.Receive(1,pixels);capture.Flush(400);
    using(var image=new System.Drawing.Bitmap(Path.Combine(root,"atlas.png")))
    {
        if(image.GetPixel(0,48).A!=0 || image.GetPixel(1,48).A!=255 || image.GetPixel(1,48).R!=0
            || image.GetPixel(160,48).R!=255 || image.GetPixel(160,48).A!=255
            || image.GetPixel(0,0).R!=10 || image.GetPixel(0,0).G!=20 || image.GetPixel(0,0).B!=30)
            throw new InvalidOperationException("Destroy texture hid the original block or changed unrelated atlas pixels.");
    }
    Console.WriteLine("PASS: Destroy texture white background becomes transparent ink; block atlas pixels stay intact.");
}
finally
{
    File.Delete(avatar);File.Delete(avatar+".tmp");File.Delete(Path.Combine(root,"atlas.png"));Directory.Delete(root);
}

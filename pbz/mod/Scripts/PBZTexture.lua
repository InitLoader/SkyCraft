local Texture={}
local importing=false
NotifyOnNewObject("/Script/Engine.Texture2D",function(value)
    if importing and value:GetFullName():match("^Texture2D /Engine/Transient%.Texture2D_") then
        value.Filter=0
        print("[PBZProbe] Owned texture nearest filter before resource creation: "..value:GetFullName().."\n")
    end
end)
function Texture.import(pawn,path)
    local rendering=StaticFindObject("/Script/Engine.Default__KismetRenderingLibrary")
    importing=true
    local ok,value=pcall(rendering.ImportFileAsTexture2D,rendering,pawn,path)
    importing=false
    assert(ok,tostring(value))
    return value
end
return Texture

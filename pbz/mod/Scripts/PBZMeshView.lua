local MeshView={}
local Origin=require("PBZOrigin")
local Texture=require("PBZTexture")
local root=os.getenv("PBZCRAFT_EXCHANGE")
local owner=nil
local origin=nil
local meshes={}
local textures={}
local materials={}
local sections={}
local previousAvatar=nil
local previousEntities=nil
local previousBatches=-1
local inspected={}
local lastPoll=0
local spriteParent=nil
local brightness=math.max(0.01,math.min(1,tonumber(os.getenv("PBZCRAFT_TEXTURE_BRIGHTNESS")) or 0.15))
local identity={Rotation={X=0,Y=0,Z=0,W=1},Translation={X=0,Y=0,Z=0},Scale3D={X=1,Y=1,Z=1}}

local function read(name)
    local file=io.open(root.."/"..name,"rb")
    if not file then return nil end
    local data=file:read("*a");file:close();return data
end

local function material(pawn,mesh,id)
    if materials[id] and materials[id]:IsValid() then return materials[id] end
    local name=id==0 and "atlas.png" or "texture-"..id..".png"
    if not read(name) then return nil end
    local texture=textures[id]
    if not texture or not texture:IsValid() then
        texture=Texture.import(pawn,root.."/"..name)
        assert(texture and texture:IsValid(),"Cannot import MC texture "..name)
        texture.Filter=0
        textures[id]=texture
        return nil
    end
    if not spriteParent then
        LoadAsset("/Paper2D/MaskedUnlitSpriteMaterial.MaskedUnlitSpriteMaterial")
        spriteParent=StaticFindObject("/Paper2D/MaskedUnlitSpriteMaterial.MaskedUnlitSpriteMaterial")
    end
    local parent=spriteParent
    assert(parent and parent:IsValid(),"Cooked masked sprite material unavailable")
    local instance=mesh:CreateDynamicMaterialInstance(0,parent,FName("PBZCraftTexture"..id))
    assert(instance and instance:IsValid(),"Cannot create MC texture material")
    instance:SetTextureParameterValue(FName("SpriteTexture"),texture)
    local assigned=instance:K2_GetTextureParameterValue(FName("SpriteTexture"))
    print(string.format("[PBZProbe] Material id=%d texture=%s size=%d,%d assigned=%s\n",id,texture:GetFullName(),texture:Blueprint_GetSizeX(),texture:Blueprint_GetSizeY(),assigned:IsValid() and assigned:GetFullName() or "none"))
    textures[id]=texture;materials[id]=instance
    return instance
end

local function component(pawn,key)
    if meshes[key] and meshes[key]:IsValid() then return meshes[key] end
    local value=owner:AddComponentByClass(StaticFindObject("/Script/ProceduralMeshComponent.ProceduralMeshComponent"),false,identity,false)
    assert(value and value:IsValid(),"Cannot create MC procedural mesh")
    value:SetCollisionEnabled(0)
    owner:K2_SetActorLocation(origin,false,{},true)
    meshes[key]=value
    local position=owner:K2_GetActorLocation()
    print(string.format("[PBZProbe] Mesh component created key=%s actor=%.3f,%.3f,%.3f\n",key,position.X,position.Y,position.Z))
    return value
end

local function build(pawn,key,data,start,first,count,id,offset)
    if count<0 or count>50000 or start+(first+count)*32-1>#data then error("Invalid MC mesh bounds") end
    local mesh=component(pawn,key)
    if count==0 then mesh:ClearAllMeshSections();return true end
    local surface=material(pawn,mesh,id)
    if not surface then return false end
    local vertices,indices,uv,colors,normals={},{},{},{},{}
    for index=0,count-1 do
        local x,y,z,u,v,color=string.unpack("<fffffI4",data,start+(first+index)*32)
        vertices[#vertices+1]={X=z*100+offset.X,Y=-x*100+offset.Y,Z=y*100+offset.Z}
        uv[#uv+1]={X=u,Y=v}
        colors[#colors+1]={R=math.floor((color&255)*brightness),G=math.floor(((color>>8)&255)*brightness),B=math.floor(((color>>16)&255)*brightness),A=(color>>24)&255}
    end
    for index=0,count-3,3 do
        indices[#indices+1]=index;indices[#indices+1]=index+2;indices[#indices+1]=index+1
        local a,b,c=vertices[index+1],vertices[index+3],vertices[index+2]
        local ux,uy,uz=b.X-a.X,b.Y-a.Y,b.Z-a.Z
        local vx,vy,vz=c.X-a.X,c.Y-a.Y,c.Z-a.Z
        local x,y,z=uy*vz-uz*vy,uz*vx-ux*vz,ux*vy-uy*vx
        local length=math.sqrt(x*x+y*y+z*z)
        local normal=length>0.00001 and {X=x/length,Y=y/length,Z=z/length} or {X=0,Y=0,Z=1}
        normals[index+1]=normal;normals[index+2]=normal;normals[index+3]=normal
    end
    mesh:SetMaterial(0,surface)
    mesh:CreateMeshSection(0,vertices,indices,normals,uv,colors,{},false)
    if not inspected[key] then
        inspected[key]=true
        local applied=mesh:GetMaterial(0)
        print("[PBZProbe] Component material key="..key.." applied="..applied:GetFullName().." expected="..surface:GetFullName().."\n")
        local uvMinX,uvMaxX,uvMinY,uvMaxY=10,-10,10,-10
        mesh.ProcMeshSections:ForEach(function(_,section)
            section:get().ProcVertexBuffer:ForEach(function(_,vertex)
                local value=vertex:get()
                uvMinX=math.min(uvMinX,value.UV0.X);uvMaxX=math.max(uvMaxX,value.UV0.X)
                uvMinY=math.min(uvMinY,value.UV0.Y);uvMaxY=math.max(uvMaxY,value.UV0.Y)
            end)
            return true
        end)
        print(string.format("[PBZProbe] Native mesh UV bounds key=%s uv=%.7f,%.7f / %.7f,%.7f\n",key,uvMinX,uvMaxX,uvMinY,uvMaxY))
    end
    return true
end

function MeshView.step(pawn,visible,intent)
    if not root then return end
    if owner and owner:IsValid() then owner:SetActorHiddenInGame(not visible) end
    if not visible then return end
    if not owner or not owner:IsValid() then
        origin=Origin.get(pawn)
        owner=pawn:GetWorld():SpawnActor(StaticFindObject("/Script/Engine.Actor"),origin,{Pitch=0,Yaw=0,Roll=0})
        meshes={};sections={};materials={};textures={};previousAvatar=nil;previousEntities=nil
        print("[PBZProbe] Owned MC world mesh actor created.\n")
    end
    if os.clock()-lastPoll<0.1 then return end
    lastPoll=os.clock()
    local manifest=read("sections.txt") or ""
    local current={}
    for name in manifest:gmatch("[^\r\n]+") do
        if name:match("^section%-[%d%-]+%.bin$") then
            current[name]=true
            local data=read(name)
            if data and data~=sections[name] then
                local sx,sy,sz,count=string.unpack("<i4i4i4i4",data)
                if build(pawn,name,data,17,0,count,0,{X=sz*1600,Y=-sx*1600,Z=(sy*16-128)*100}) then
                    sections[name]=data
                    print(string.format("[PBZProbe] Native MC section %s vertices=%d\n",name,count))
                end
            end
        end
    end
    for name in pairs(sections) do if not current[name] then if meshes[name] and meshes[name]:IsValid() then meshes[name]:ClearAllMeshSections() end;sections[name]=nil end end
    local data=read("avatar.bin")
    if data and data~=previousAvatar and #data>=8 then
        local batches,count=string.unpack("<I4I4",data)
        assert(batches<=64 and count<=50000 and #data==8+batches*16+count*32,"Invalid MC avatar")
        if batches~=previousBatches then print(string.format("[PBZProbe] MC avatar batches=%d vertices=%d\n",batches,count));previousBatches=batches end
        local complete=true
        for index=0,batches-1 do
            local id,first,n=string.unpack("<I4I4I4",data,9+index*16)
            local feet=intent and intent.FeetWorld or origin
            if not build(pawn,"avatar"..index,data,9+batches*16,first,n,id,{X=feet.X-origin.X,Y=feet.Y-origin.Y,Z=feet.Z-origin.Z}) then complete=false end
        end
        for index=batches,63 do local mesh=meshes["avatar"..index];if mesh and mesh:IsValid() then mesh:ClearAllMeshSections() end end
        if complete then previousAvatar=data end
    end
    local entities=read("entities.bin")
    if entities and entities~=previousEntities and #entities>=8 then
        local batches,count=string.unpack("<I4I4",entities)
        assert(batches<=1 and count<=50000 and #entities==8+batches*16+count*32,"Invalid MC world entities")
        if batches>0 then
            if not build(pawn,"worldentities",entities,25,0,count,0,{X=0,Y=0,Z=-12800}) then return end
        elseif meshes.worldentities and meshes.worldentities:IsValid() then meshes.worldentities:ClearAllMeshSections() end
        previousEntities=entities
    end
end
return MeshView

local modRoot=assert(os.getenv("PBZCRAFT_MOD_ROOT"),"Launch with the PBZCraft launcher")
local loader, err = package.loadlib(modRoot.."/PBZNative.dll", "luaopen_pbznative")
assert(loader, err)
loader()
print("[PBZProbe] " .. PBZNative_Ping() .. "\n")
local Ipc = {}
local Ground = require("PBZGroundProbe")
local Ray=require("PBZRayProbe")
local Origin=require("PBZOrigin")
local Input=require("PBZInput")
local Obstacle=require("PBZObstacleProbe")
function Ipc.publish(controller, pawn, intent, active)
    Ground.step(pawn)
    local pos = pawn:K2_GetActorLocation()
    local look = controller:GetControlRotation()
    local half = pawn.CapsuleComponent:GetScaledCapsuleHalfHeight()
    local camera = controller.PlayerCameraManager
    local eye = camera:GetCameraLocation()
    local samples = Ground.values()
    local statics = StaticFindObject("/Script/Engine.Default__GameplayStatics")
    local values = {pos.X, pos.Y, pos.Z, half, look.Yaw, look.Pitch, eye.X, eye.Y, eye.Z, camera:GetFOVAngle(), statics:IsGamePaused(pawn) and 1 or 0, #samples/4}
    for _,value in ipairs(samples) do values[#values+1]=value end
    if #samples>0 then for _,value in ipairs(Ray.values(controller,pawn,intent)) do values[#values+1]=value end end
    if #samples>0 then local origin=Origin.get(pawn);values[#values+1]=origin.X;values[#values+1]=origin.Y;values[#values+1]=origin.Z end
    if #samples>0 then for _,value in ipairs(Input.values(controller,active)) do values[#values+1]=value end end
    if #samples>0 then for _,value in ipairs(Obstacle.values(pawn)) do values[#values+1]=value end end
    assert(PBZNative_Send(table.unpack(values)), "IPC publish failed")
end
return Ipc

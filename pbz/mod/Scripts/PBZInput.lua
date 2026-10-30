local Input={}
local names={"W","A","S","D","SpaceBar","LeftShift","LeftControl","E","Q","R","T","Escape","Tab","F5","One","Two","Three","Four","Five","Six","Seven","Eight","Nine","LeftMouseButton","RightMouseButton","MiddleMouseButton","F9"}
local keys={}
local configured=nil
local fullTick=false
local frame=0
for index,name in ipairs(names) do keys[index]={KeyName=FName(name)} end

function Input.restore()
    if configured and configured:IsValid() then
        configured.bShouldPerformFullTickWhenPaused=fullTick
        configured:SetIgnoreMoveInput(false)
    end
    configured=nil
end

function Input.values(controller,active)
    frame=frame+1
    if not active then Input.restore();return {0,0,0,0,0,0,frame,0,0} end
    if not configured then
        configured=controller
        fullTick=controller.bShouldPerformFullTickWhenPaused
        controller.bShouldPerformFullTickWhenPaused=true
        controller:SetIgnoreMoveInput(true)
    end
    local mask=0
    for index,key in ipairs(keys) do if controller:IsInputKeyDown(key) then mask=mask | (1 << (index-1)) end end
    local dx,dy,x,y={},{},{},{}
    controller:GetInputMouseDelta(dx,dy)
    local cursor=controller:GetMousePosition(x,y)
    local w,h={},{}
    controller:GetViewportSize(w,h)
    local wheel=controller:GetInputAnalogKeyState({KeyName=FName("MouseWheelAxis")})
    if frame%240==0 then print(string.format("[PBZProbe] Native input API frame=%d mask=%d mouse=%.2f,%.2f cursor=%s\n",frame,mask,dx.DeltaX or 0,dy.DeltaY or 0,tostring(cursor))) end
    return {mask,dx.DeltaX or 0,dy.DeltaY or 0,cursor and x.LocationX or 0,cursor and y.LocationY or 0,wheel or 0,frame,w.SizeX or 0,h.SizeY or 0}
end
return Input

local Resume = {}
local requested = false
local paused = false

function Resume.step(controller)
    local pawn = controller.Pawn
    if pawn and pawn:IsValid() and pawn:IsA("/Script/Engine.Character") then
        if not paused then
            local statics = StaticFindObject("/Script/Engine.Default__GameplayStatics")
            assert(statics:IsValid(), "GameplayStatics is not available")
            statics:SetGamePaused(pawn, true)
            paused = true
            print("[PBZProbe] Loaded player detected; native gameplay paused for background inspection.\n")
        end
        return
    end
    if requested then return end
    local menu = FindFirstOf("WB_StartMenu_V2_C")
    if not menu or not menu:IsValid() then return end
    print("[PBZProbe] Native menu found: "..menu:GetFullName().."\n")
    local button = menu.ContinueGame
    if not button or not button:IsValid() then return end
    if not button:GetFullName():find(".ContinueGame", 1, true) then return end
    requested = true
    print("[PBZProbe] Invoking existing Continue Game handler: " .. button:GetFullName() .. "\n")
    button:HandleButtonClicked()
end

return Resume

local Effects={}
function Effects.apply(camera)
    local component=camera.CameraComponent
    local settings=component.PostProcessSettings
    settings.bOverride_DepthOfFieldScale=true
    settings.DepthOfFieldScale=0
    settings.bOverride_DepthOfFieldFocalDistance=true
    settings.DepthOfFieldFocalDistance=0
    settings.bOverride_DepthOfFieldNearBlurSize=true
    settings.DepthOfFieldNearBlurSize=0
    settings.bOverride_DepthOfFieldFarBlurSize=true
    settings.DepthOfFieldFarBlurSize=0
    settings.bOverride_DepthOfFieldDepthBlurRadius=true
    settings.DepthOfFieldDepthBlurRadius=0
    settings.bOverride_MotionBlurAmount=true
    settings.MotionBlurAmount=0
    component:SetPostProcessBlendWeight(1)
    print(string.format("[PBZProbe] Owned camera effects: dofScale=%.2f motionBlur=%.2f\n",settings.DepthOfFieldScale,settings.MotionBlurAmount))
end
return Effects

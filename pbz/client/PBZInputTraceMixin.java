package dev.pbzcraft.mixin;

import dev.pbzcraft.PBZDiagnostics;
import dev.skycraft.client.InputBridge;
import dev.skycraft.link.SkyLink;
import net.minecraft.client.Minecraft;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(InputBridge.class)
public abstract class PBZInputTraceMixin {
    @Inject(method="dispatch",at=@At("HEAD"),remap=false)
    private static void pbzcraft$inputAge(Minecraft mc,int type,int code,int a,int b,int c,CallbackInfo callback) {
        if(type<1 || type>4 || c==0)return;
        long age=((SkyLink.tickCount()&0xffffffffL)-Integer.toUnsignedLong(c))&0xffffffffL;
        if(age>60000)return;
        PBZDiagnostics.inputSamples++;
        PBZDiagnostics.inputTotalMs+=age;
        PBZDiagnostics.inputMaxMs=Math.max(PBZDiagnostics.inputMaxMs,age);
        PBZDiagnostics.inputLastMs=age;
    }
}

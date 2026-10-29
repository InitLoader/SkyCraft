package dev.pbzcraft.mixin;

import dev.skycraft.client.SkyClient;
import dev.pbzcraft.NativeCollision;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(Entity.class)
public abstract class PBZEntityCollideMixin {
    @Inject(method="collide",at=@At("RETURN"),cancellable=true)
    private void pbzcraft$nativeCollision(Vec3 movement,CallbackInfoReturnable<Vec3> result) {
        if((Object)this instanceof LocalPlayer player && SkyClient.linked() && !player.noPhysics) {
            result.setReturnValue(NativeCollision.collide(player,result.getReturnValue()));
        }
    }
}

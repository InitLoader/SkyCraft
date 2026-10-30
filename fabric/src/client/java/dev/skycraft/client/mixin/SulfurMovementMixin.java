package dev.skycraft.client.mixin;

import dev.skycraft.HostProfile;
import dev.skycraft.client.SkyClient;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.world.entity.MoverType;
import net.minecraft.world.phys.Vec3;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfo;

@Mixin(LocalPlayer.class)
public abstract class SulfurMovementMixin {
	@Inject(method = "move", at = @At("HEAD"))
	private void skycraft$sulfurMovement(MoverType type, Vec3 movement, CallbackInfo ci) {
		if (HostProfile.SULFUR && type == MoverType.SELF) {
			LocalPlayer player = (LocalPlayer) (Object) this;
			SkyClient.sulfurMovement(movement, player.getBbWidth(), player.getBbHeight());
		}
	}
}

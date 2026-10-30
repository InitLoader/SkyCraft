package dev.skycraft.mixin;

import dev.skycraft.world.SulfurLadders;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.player.Player;
import org.spongepowered.asm.mixin.Mixin;
import org.spongepowered.asm.mixin.injection.At;
import org.spongepowered.asm.mixin.injection.Inject;
import org.spongepowered.asm.mixin.injection.callback.CallbackInfoReturnable;

@Mixin(LivingEntity.class)
public abstract class SulfurLadderMixin {
	@Inject(method = "onClimbable", at = @At("HEAD"), cancellable = true)
	private void skycraft$sulfurLadder(CallbackInfoReturnable<Boolean> ci) {
		LivingEntity entity = (LivingEntity) (Object) this;
		if (entity instanceof Player && SulfurLadders.contains(entity.getBoundingBox())) {
			ci.setReturnValue(true);
		}
	}
}

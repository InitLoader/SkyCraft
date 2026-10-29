package dev.pbzcraft;

import net.fabricmc.api.ClientModInitializer;
import net.minecraft.client.Minecraft;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;

public final class PBZDiagnostics implements ClientModInitializer {
    private String previous = "";
    @Override public void onInitializeClient() {
        var executor = Executors.newSingleThreadScheduledExecutor(r -> { var t = new Thread(r, "PBZCraft diagnostics"); t.setDaemon(true); return t; });
        executor.scheduleWithFixedDelay(() -> Minecraft.getInstance().execute(() -> {
            var mc = Minecraft.getInstance();
            String state = "screen=" + (mc.gui.screen() == null ? "none" : mc.gui.screen().getClass().getName())
                + ", player=" + (mc.player != null) + ", level=" + (mc.level != null)
                + ", held=" + (mc.player == null ? "none" : mc.player.getMainHandItem().toString())
                + ", grabbed=" + mc.mouseHandler.isMouseGrabbed()
                + ", pitch=" + (mc.player == null ? 0 : Math.round(mc.player.getXRot()))
                + ", hit=" + (mc.hitResult == null ? "none" : mc.hitResult.getType()+":"+mc.hitResult.getLocation())
                + ", triangles=" + dev.skycraft.world.SkyCollision.triangleCount()
                + ", nativeQueries=" + NativeCollision.queries + ", nativeTimeouts=" + NativeCollision.timeouts + ", nativeRejected=" + NativeCollision.rejected
                + ", nativeAvgMs=" + (NativeCollision.queries==0?0:NativeCollision.totalNanos/1e6/NativeCollision.queries)
                + ", nativeMaxMs=" + NativeCollision.maxNanos/1e6
                + ", directRay=" + (mc.player == null ? "none" : dev.skycraft.world.SkyClip.cast(mc.player.getEyePosition(),mc.player.getEyePosition().add(mc.player.getViewVector(1).scale(4.5))));
            if (state.equals(previous)) return;
            previous = state;
            try { Files.writeString(Path.of(mc.gameDirectory.toString(), "pbz-diagnostics.log"), System.currentTimeMillis() + " " + state + "\n", StandardOpenOption.CREATE, StandardOpenOption.APPEND); }
            catch (Exception e) { System.err.println("PBZCraft diagnostics: " + e); }
        }), 1, 1, TimeUnit.SECONDS);
    }
}

package dev.skycraft.world;

import dev.skycraft.HostProfile;
import java.lang.foreign.Arena;
import java.lang.foreign.MemorySegment;
import net.minecraft.world.phys.AABB;
import org.junit.jupiter.api.Test;
import static java.lang.foreign.ValueLayout.*;
import static org.junit.jupiter.api.Assertions.*;
import static org.junit.jupiter.api.Assumptions.assumeTrue;

class SulfurLaddersTest {
	@Test
	void nativeVolumeClimbsOnlyInsideAndRejectsStaleWorlds() {
		assumeTrue(HostProfile.SULFUR);
		SulfurLadders.clear();
		try (Arena arena = Arena.ofConfined()) {
			MemorySegment payload = arena.allocate(32, 4);
			payload.set(JAVA_INT, 0, 7);
			payload.set(JAVA_INT, 4, 1);
			float[] bounds = { 8192, 128, 0, 8193, 138, 1 };
			for (int i = 0; i < bounds.length; i++) payload.set(JAVA_FLOAT, 8 + i * 4L, bounds[i]);
			AABB player = new AABB(8192.2, 130, .2, 8192.8, 131.8, .8);
			SulfurLadders.read(payload, 0, 32, 8);
			assertFalse(SulfurLadders.contains(player));
			SulfurLadders.read(payload, 0, 32, 7);
			assertTrue(SulfurLadders.contains(player));
			assertFalse(SulfurLadders.contains(new AABB(8194, 130, .2, 8194.6, 131.8, .8)));
			assertThrows(IllegalArgumentException.class, () -> SulfurLadders.read(payload, 0, 31, 7));
			SulfurLadders.clear();
			assertFalse(SulfurLadders.contains(player));
		}
	}
}

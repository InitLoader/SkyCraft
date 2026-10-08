package dev.skycraft.world;

import dev.skycraft.HostProfile;
import java.lang.foreign.MemorySegment;
import net.minecraft.world.phys.AABB;
import static java.lang.foreign.ValueLayout.*;

public final class SulfurLadders {
	private static volatile AABB[] volumes = new AABB[0];
	private SulfurLadders() {
	}
	public static void clear() {
		volumes = new AABB[0];
	}
	public static void read(MemorySegment memory, long offset, int bytes, int epoch) {
		if (!HostProfile.SULFUR || bytes < 8 || memory.get(JAVA_INT, offset) != epoch) {
			return;
		}
		int count = memory.get(JAVA_INT, offset + 4);
		if (count < 0 || count > 256 || bytes != 8 + count * 24) {
			throw new IllegalArgumentException("Invalid SULFUR ladder payload");
		}
		AABB[] next = new AABB[count];
		for (int i = 0; i < count; i++) {
			long at = offset + 8 + i * 24L;
			next[i] = new AABB(memory.get(JAVA_FLOAT, at), memory.get(JAVA_FLOAT, at + 4), memory.get(JAVA_FLOAT, at + 8),
				memory.get(JAVA_FLOAT, at + 12), memory.get(JAVA_FLOAT, at + 16), memory.get(JAVA_FLOAT, at + 20));
		}
		volumes = next;
	}
	public static boolean contains(AABB player) {
		if (!HostProfile.SULFUR) {
			return false;
		}
		for (AABB volume : volumes) {
			if (volume.intersects(player)) {
				return true;
			}
		}
		return false;
	}
}

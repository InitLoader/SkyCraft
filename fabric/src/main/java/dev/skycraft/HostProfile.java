package dev.skycraft;

import java.io.IOException;
import java.util.Properties;

/** Selects the host packaged with the mod without changing the shared-memory layout. */
public final class HostProfile {
	public static final boolean SULFUR = "sulfur".equals(System.getProperty("skycraft.host", packagedHost()));

	private HostProfile() {
	}

	private static String packagedHost() {
		Properties properties = new Properties();
		try (var input = HostProfile.class.getResourceAsStream("/skycraft-host.properties")) {
			if (input != null) {
				properties.load(input);
			}
		} catch (IOException e) {
			throw new IllegalStateException("Cannot read the packaged bridge host", e);
		}
		return properties.getProperty("host", "skyrim");
	}
}

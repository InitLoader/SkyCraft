package dev.duckovcraft;

import net.fabricmc.loader.api.entrypoint.PreLaunchEntrypoint;

public final class DuckovCraftProfile implements PreLaunchEntrypoint {
	@Override
	public void onPreLaunch() {
		setDefault("host", "sulfur");
		setDefault("link", "Local\\DuckovCraft_v1");
		setDefault("startHidden", "false");
		setDefault("quitWithSkyrim", "true");
		setDefault("discordAppId", "0");
	}

	private static void setDefault(String name, String fallback) {
		String value = System.getProperty("duckovcraft." + name);
		if (value != null || System.getProperty("skycraft." + name) == null) {
			System.setProperty("skycraft." + name, value != null ? value : fallback);
		}
	}
}

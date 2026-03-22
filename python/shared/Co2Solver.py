import numpy as np


class Co2Solver:
    def __init__(self, G, V, G_ext_air_mix):
        self.G = G
        self.V = V
        self.G_ext_air_mix = G_ext_air_mix

        self.co2 = np.full(len(V), 750.0)

    def step(self, dt, outside_co2, room_noise_sigma=0.0):
        co2_mixed = np.dot(self.G, self.co2) - (np.sum(self.G, axis=1) * self.co2)

        co2_infiltrated = self.G_ext_air_mix * (outside_co2 - self.co2)

        # --- MIEJSCE NA PRZYSZŁOŚĆ ---
        # Tutaj za chwilę wpadnie generacja z ludzi:
        # co2_generated = generation_rate * is_enabled_mask
        # Oraz wentylacja z MPC:
        # co2_vented = active_vent_m3_s * (outside_co2 - self.co2)
        # -----------------------------

        total_co2_flow = co2_mixed + co2_infiltrated

        self.co2 += (total_co2_flow / self.V) * dt

        if room_noise_sigma > 0:
            time_scale = np.sqrt(dt / 3600.0)

            state_drift = np.random.normal(0.0, room_noise_sigma * time_scale, size=len(self.V))

            self.co2 += state_drift

        return np.round(self.co2).astype(int)

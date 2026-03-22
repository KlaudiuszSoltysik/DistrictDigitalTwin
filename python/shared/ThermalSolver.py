import numpy as np


class ThermalSolver:
    def __init__(self, G_temp, C, G_ext_air, G_ext_ground, T_ground):
        self.G_temp = G_temp
        self.C = C
        self.G_ext_air = G_ext_air
        self.G_ext_ground = G_ext_ground
        self.T_ground = T_ground

        self.T = np.full(len(C), 21.0)

    def step(self, dt, T_outside, Q_extra, room_noise_sigma=0.0):
        Q_inter = np.dot(self.G_temp, self.T) - (np.sum(self.G_temp, axis=1) * self.T)

        Q_air = self.G_ext_air * (T_outside - self.T)

        Q_ground = self.G_ext_ground * (self.T_ground - self.T)

        total_Q = Q_inter + Q_air + Q_ground + Q_extra

        self.T += (total_Q / self.C) * dt

        if room_noise_sigma > 0:
            time_scale = np.sqrt(dt / 3600.0)

            state_drift = np.random.normal(0.0, room_noise_sigma * time_scale, size=len(self.T))

            self.T += state_drift

        return self.T

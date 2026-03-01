import numpy as np


class ThermalSolver:
    def __init__(self, G, C, G_ext_air, G_ext_ground, T_ground):
        self.G = G
        self.C = C
        self.G_ext_air = G_ext_air
        self.G_ext_ground = G_ext_ground
        self.T_ground = T_ground

        # TODO: change that
        self.T = np.full(len(C), 21.0)

    def step(self, dt, T_outside, Q_extra, drift_sigma_per_hour=0.0):
        Q_inter = np.dot(self.G, self.T) - (np.sum(self.G, axis=1) * self.T)

        Q_air = self.G_ext_air * (T_outside - self.T)

        Q_ground = self.G_ext_ground * (self.T_ground - self.T)

        total_Q = Q_inter + Q_air + Q_ground + Q_extra

        self.T += (total_Q / self.C) * dt

        if drift_sigma_per_hour > 0:
            time_scale = np.sqrt(dt / 3600.0)

            state_drift = np.random.normal(loc=0.0, scale=drift_sigma_per_hour * time_scale, size=len(self.T))

            self.T += state_drift

        return self.T

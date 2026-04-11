import numpy as np


class PVFarm:
    def __init__(self):
        self.max_power = 100
        self.efficiency = 0.95

    def get_power_prognosis(self, weather, noise_sigma):
        base_yield = self.max_power * self.efficiency * (weather["sun_radiation"] / 1000.0)

        if base_yield <= 0:
            return 0.0

        if noise_sigma > 0:
            noise_factor = np.random.normal(1.0, scale=noise_sigma * base_yield / 75)
            noisy_yield = base_yield * noise_factor
        else:
            noisy_yield = base_yield

        final_yield = np.clip(noisy_yield, 0.0, self.max_power)

        return final_yield

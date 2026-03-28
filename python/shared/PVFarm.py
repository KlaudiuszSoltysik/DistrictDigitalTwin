class PVFarm:
    def __init__(self):
        self.max_power = 100
        self.efficiency = 0.95

    def get_power_prognosis(self, weather):
        return self.max_power * self.efficiency * weather["sun_radiation"] / 1000

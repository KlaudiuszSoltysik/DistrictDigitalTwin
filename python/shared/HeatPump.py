class HeatPump:
    def __init__(self):
        self.base_cop = 3.0
        self.temp_modifier = 0.1
        self.min_cop = 1.0
        self.eer = 3.0

    def get_cop(self, weather):
        t_out = weather["temperature"]

        cop_heating = max(self.min_cop, self.base_cop + (self.temp_modifier * t_out))

        cop_cooling = self.eer

        return cop_heating, cop_cooling